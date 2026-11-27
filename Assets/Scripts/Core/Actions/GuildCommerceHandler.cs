using System;
using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Simulation;

namespace Core.Actions
{
    internal sealed class GuildCommerceHandler
    {
        private readonly GameSessionState _state;
        private readonly MovementAndCombatHandler _movement;

        public GuildCommerceHandler(GameSessionState state, MovementAndCombatHandler movement)
        {
            _state = state;
            _movement = movement;
        }

        public ActionResult BuyTrader(int provinceId)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;
            if (_state.CurrentPlayer.Faction != FactionType.Guilds) return ActionResult.WrongFaction;

            var province = _state.Provinces[provinceId];
            bool canRecruitHere = province.OwnerId == _state.CurrentPlayer.PlayerId
                                  || province.TradingPostOwnerId == _state.CurrentPlayer.PlayerId;
            if (!canRecruitHere) return ActionResult.NotYourProvince;

            if (CountPlayerUnitsOfType(_state.CurrentPlayer.PlayerId, UnitType.Trader) >=
                CountOwnedTradingPosts(_state.CurrentPlayer.PlayerId))
            {
                return ActionResult.TraderCapReached;
            }

            if (_state.CurrentPlayer.Gold < _state.Config.TraderCost) return ActionResult.NotEnoughGold;

            var units = new List<UnitInstance>(province.Units)
            {
                new(_state.NextUnitId++, _state.CurrentPlayer.PlayerId, FactionType.Guilds, UnitType.Trader, 1, 1)
            };

            _state.SetProvince(province.WithUnits(units));
            _state.AddGold(_state.CurrentPlayerIndex, -_state.Config.TraderCost);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public int GetTradingPostBuildCost(int playerId) =>
            CountOwnedTradingPosts(playerId) < 2 ? 0 : _state.Config.TradingPostCost;

        public ActionResult BuildTradingPost(int provinceId)
        {
            var check = ValidateGuildAction();
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            var player = _state.CurrentPlayer;

            if (province.HasTradingPost) return ActionResult.AlreadyBuilt;

            int cost = GetTradingPostBuildCost(player.PlayerId);
            if (player.Gold < cost) return ActionResult.NotEnoughGold;

            var updatedUnits = new List<UnitInstance>(province.Units);
            if (IndexOfPlayerUnitType(province.Units, player.PlayerId, UnitType.Trader) < 0)
            {
                updatedUnits.Add(new UnitInstance(
                    _state.NextUnitId++, player.PlayerId, FactionType.Guilds, UnitType.Trader, 1, 1));
            }

            _state.SetProvince(province.WithTradingPost(player.PlayerId).WithUnits(updatedUnits));
            if (cost > 0)
            {
                _state.AddGold(_state.CurrentPlayerIndex, -cost);
            }

            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public bool HasAssignedCaravan(int provinceId, int playerId)
        {
            var units = _state.Provinces[provinceId].Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.OwnerPlayerId == playerId && u.Type == UnitType.Trader && _state.ActiveCaravans.ContainsKey(u.Id))
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<int> GetAssignedCaravanPath(int provinceId, int playerId)
        {
            var units = _state.Provinces[provinceId].Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.OwnerPlayerId == playerId && u.Type == UnitType.Trader
                                                && _state.ActiveCaravans.TryGetValue(u.Id, out var route))
                {
                    return route.Path;
                }
            }

            return Array.Empty<int>();
        }

        public ActionResult RunCaravan(IReadOnlyList<int> path)
        {
            var check = ValidateGuildAction();
            if (check != ActionResult.Ok) return check;
            if (path == null || path.Count < 2) return ActionResult.InvalidCaravanPath;

            int playerId = _state.CurrentPlayer.PlayerId;
            int originId = path[0];
            int destId = path[path.Count - 1];
            if (originId < 0 || originId >= _state.Provinces.Length || destId < 0 || destId >= _state.Provinces.Length)
            {
                return ActionResult.InvalidCaravanPath;
            }

            if (!_state.Provinces[originId].HasTradingPost || !_state.Provinces[destId].HasTradingPost)
            {
                return ActionResult.InvalidCaravanPath;
            }

            var origin = _state.Provinces[originId];
            int traderIndex = -1;
            for (int i = 0; i < origin.Units.Count; i++)
            {
                var u = origin.Units[i];
                if (u.OwnerPlayerId == playerId && u.Type == UnitType.Trader && !u.HasActed)
                {
                    if (_state.ActiveCaravans.ContainsKey(u.Id)) return ActionResult.CaravanAlreadyAssigned;
                    traderIndex = i;
                    break;
                }
            }

            if (traderIndex < 0) return ActionResult.NoTrader;

            var visited = new HashSet<int> { originId };
            for (int step = 1; step < path.Count; step++)
            {
                int prevId = path[step - 1];
                int nextId = path[step];
                if (nextId < 0 || nextId >= _state.Provinces.Length) return ActionResult.InvalidCaravanPath;
                if (!visited.Add(nextId)) return ActionResult.InvalidCaravanPath;
                if (!_state.Provinces[prevId].IsAdjacentTo(nextId)) return ActionResult.NotAdjacent;
            }

            var trader = origin.Units[traderIndex];
            var route = new GameSessionState.ActiveCaravanRoute(trader.Id, new List<int>(path), 0);
            return StepCaravanRoute(route, isInitialAssignment: true);
        }

        public ActionResult AdvanceCaravan(int provinceId)
        {
            var check = ValidateGuildAction();
            if (check != ActionResult.Ok) return check;

            int playerId = _state.CurrentPlayer.PlayerId;
            var units = _state.Provinces[provinceId].Units;
            GameSessionState.ActiveCaravanRoute route = null;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.OwnerPlayerId == playerId && u.Type == UnitType.Trader && !u.HasActed
                    && _state.ActiveCaravans.TryGetValue(u.Id, out var existing))
                {
                    route = existing;
                    break;
                }
            }

            if (route == null) return ActionResult.NoTrader;
            return StepCaravanRoute(route, isInitialAssignment: false);
        }

        private ActionResult StepCaravanRoute(GameSessionState.ActiveCaravanRoute route, bool isInitialAssignment)
        {
            int playerId = _state.CurrentPlayer.PlayerId;
            int currentProvinceId = route.Path[route.CurrentStepIndex];
            int targetStepIndex = Math.Min(route.Path.Count - 1, route.CurrentStepIndex + 2);

            int legToll = 0;
            for (int step = route.CurrentStepIndex + 1; step <= targetStepIndex; step++)
            {
                int ownerId = _state.Provinces[route.Path[step]].OwnerId;
                if (ownerId != ProvinceState.NeutralOwner && ownerId != playerId)
                {
                    legToll += _state.Config.CaravanTransitToll;
                }
            }

            if (_state.CurrentPlayer.Gold < legToll) return ActionResult.NotEnoughGold;

            var currentProvince = _state.Provinces[currentProvinceId];
            int traderIndex = -1;
            for (int i = 0; i < currentProvince.Units.Count; i++)
            {
                if (currentProvince.Units[i].Id == route.TraderUnitId)
                {
                    traderIndex = i;
                    break;
                }
            }

            if (traderIndex < 0)
            {
                _state.ActiveCaravans.Remove(route.TraderUnitId);
                return ActionResult.NoTrader;
            }

            for (int step = route.CurrentStepIndex + 1; step <= targetStepIndex; step++)
            {
                int ownerId = _state.Provinces[route.Path[step]].OwnerId;
                if (ownerId != ProvinceState.NeutralOwner && ownerId != playerId)
                {
                    int ownerIdx = _state.IndexOfPlayer(ownerId);
                    if (ownerIdx >= 0) _state.AddGold(ownerIdx, _state.Config.CaravanTransitToll);
                }
            }

            if (legToll > 0)
            {
                _state.AddGold(_state.CurrentPlayerIndex, -legToll);
            }

            int nextProvinceId = route.Path[targetStepIndex];
            var movedTrader = currentProvince.Units[traderIndex] with { HasActed = true };

            var originUnits = new List<UnitInstance>(currentProvince.Units.Count - 1);
            for (int i = 0; i < currentProvince.Units.Count; i++)
            {
                if (i != traderIndex) originUnits.Add(currentProvince.Units[i]);
            }

            var nextProvince = _state.Provinces[nextProvinceId];
            var nextUnits = new List<UnitInstance>(nextProvince.Units) { movedTrader };

            _state.SetProvince(currentProvince.WithUnits(originUnits));
            _state.SetProvince(nextProvince.WithUnits(nextUnits));

            route.CurrentStepIndex = targetStepIndex;

            if (isInitialAssignment)
            {
                _state.ActiveCaravans[route.TraderUnitId] = route;
            }

            if (route.CurrentStepIndex >= route.Path.Count - 1)
            {
                _state.ActiveCaravans.Remove(route.TraderUnitId);
                int traversedCount = route.Path.Count - 1;
                int extraPosts = Math.Max(0, CountOwnedTradingPosts(playerId) - _state.Config.StartingTradingPostCount);
                int gainPerProvince = _state.Config.CaravanBaseGainPerProvince + (extraPosts / 2);
                _state.AddGold(_state.CurrentPlayerIndex, traversedCount * gainPerProvince);

                if (traversedCount >= _state.Config.CaravanMinTraversedForVp)
                {
                    _state.QualifyingCaravanRoutesThisTurn++;
                }
            }

            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public int GetHostileBuyoutCost(int provinceId)
        {
            var province = _state.Provinces[provinceId];
            int cost = _state.Config.HostileBuyoutBaseCost;
            for (int i = 0; i < province.Units.Count; i++)
            {
                var u = province.Units[i];
                if (u.OwnerPlayerId == province.OwnerId && u.Type == UnitType.RegularMilitary)
                {
                    cost += _state.Config.HostileBuyoutUnitPremium;
                }
            }

            if (province.HasChurch) cost += _state.Config.HostileBuyoutBuildingPremium;
            if (province.HasTradingPost) cost += _state.Config.HostileBuyoutBuildingPremium;
            return cost;
        }

        public ActionResult DeclareEmbargo(int guildPlayerId)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;

            var guild = _state.GetPlayer(guildPlayerId);
            if (guild == null || guild.Faction != FactionType.Guilds || guildPlayerId == _state.CurrentPlayer.PlayerId)
            {
                return ActionResult.InvalidBattleTarget;
            }

            if (!_state.ActiveEmbargoes.Add((_state.CurrentPlayer.PlayerId, guildPlayerId)))
            {
                return ActionResult.AlreadyBuilt;
            }

            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult RaidCaravan(int provinceId, int guildPlayerId)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;
            if (!_state.ActiveEmbargoes.Contains((_state.CurrentPlayer.PlayerId, guildPlayerId)))
            {
                return ActionResult.NotEmbargoed;
            }

            if (IndexOfPlayerUnitType(_state.Provinces[provinceId].Units, _state.CurrentPlayer.PlayerId,
                    UnitType.RegularMilitary) < 0)
            {
                return ActionResult.NoMilitaryUnits;
            }

            var province = _state.Provinces[provinceId];
            int traderIdx = IndexOfPlayerUnitType(province.Units, guildPlayerId, UnitType.Trader);
            if (traderIdx < 0) return ActionResult.NoTrader;

            var updatedUnits = new List<UnitInstance>(province.Units.Count - 1);
            for (int i = 0; i < province.Units.Count; i++)
            {
                if (i != traderIdx) updatedUnits.Add(province.Units[i]);
            }

            _state.SetProvince(province.WithUnits(updatedUnits));
            _state.AddGold(_state.CurrentPlayerIndex, _state.Config.TraderCost);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult HostileBuyout(int provinceId)
        {
            var check = ValidateGuildAction();
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            var player = _state.CurrentPlayer;

            if (province.OwnerId == ProvinceState.NeutralOwner || province.OwnerId == player.PlayerId)
            {
                return ActionResult.InvalidBattleTarget;
            }

            if (_state.ActiveEmbargoes.Contains((province.OwnerId, player.PlayerId)))
            {
                return ActionResult.TargetEmbargoed;
            }

            if (province.HasFort) return ActionResult.ProvinceFortified;
            if (IndexOfPlayerUnitType(province.Units, player.PlayerId, UnitType.Trader) < 0)
            {
                return ActionResult.NoTrader;
            }

            int cost = GetHostileBuyoutCost(provinceId);
            if (player.Gold < cost) return ActionResult.NotEnoughGold;

            int previousOwnerId = province.OwnerId;
            int previousOwnerIdx = _state.IndexOfPlayer(previousOwnerId);

            var remainingUnits = new List<UnitInstance>(province.Units.Count);
            var expelledMilitary = new List<UnitInstance>();
            for (int i = 0; i < province.Units.Count; i++)
            {
                var u = province.Units[i];
                if (u.OwnerPlayerId == previousOwnerId && u.Type == UnitType.RegularMilitary)
                {
                    expelledMilitary.Add(u);
                }
                else
                {
                    remainingUnits.Add(u);
                }
            }

            if (!province.HasTradingPost)
            {
                remainingUnits.Add(new UnitInstance(
                    _state.NextUnitId++, player.PlayerId, FactionType.Guilds, UnitType.Trader, 1, 1));
            }

            _state.AddGold(_state.CurrentPlayerIndex, -cost);
            if (previousOwnerIdx >= 0) _state.AddGold(previousOwnerIdx, cost);

            var updatedProvince = province
                .WithOccupation(player.PlayerId, remainingUnits)
                .WithTradingPost(player.PlayerId);
            _state.SetProvinceDirect(updatedProvince);

            if (expelledMilitary.Count > 0)
            {
                _movement.RetreatUnits(updatedProvince, previousOwnerId, expelledMilitary);
            }

            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult SellMercenaryContract(int provinceId, int buyerPlayerId, int goldPrice)
        {
            var check = ValidateGuildAction();
            if (check != ActionResult.Ok) return check;

            if (goldPrice < 0 || buyerPlayerId == _state.CurrentPlayer.PlayerId)
                return ActionResult.InvalidBattleTarget;
            int buyerIdx = _state.IndexOfPlayer(buyerPlayerId);
            if (buyerIdx < 0) return ActionResult.InvalidBattleTarget;
            if (_state.Players[buyerIdx].Gold < goldPrice) return ActionResult.NotEnoughGold;

            var province = _state.Provinces[provinceId];
            if (IndexOfPlayerUnitType(province.Units, _state.CurrentPlayer.PlayerId, UnitType.Trader) < 0
                && province.TradingPostOwnerId != _state.CurrentPlayer.PlayerId)
            {
                return ActionResult.NoTrader;
            }

            var units = new List<UnitInstance>(province.Units)
            {
                new(_state.NextUnitId++, buyerPlayerId, FactionType.Guilds, UnitType.RegularMilitary, 1, 1)
            };

            _state.AddGold(buyerIdx, -goldPrice);
            _state.AddGold(_state.CurrentPlayerIndex, goldPrice);
            _state.SetProvince(province.WithUnits(units));
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public int CountOwnedTradingPosts(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                if (_state.Provinces[i].TradingPostOwnerId == playerId) count++;
            }

            return count;
        }

        public int CountPlayerUnitsOfType(int playerId, UnitType type)
        {
            int count = 0;
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var units = _state.Provinces[i].Units;
                for (int u = 0; u < units.Count; u++)
                {
                    if (units[u].OwnerPlayerId == playerId && units[u].Type == type) count++;
                }
            }

            return count;
        }

        private ActionResult ValidateGuildAction()
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;
            if (_state.CurrentPlayer.Faction != FactionType.Guilds) return ActionResult.WrongFaction;
            return ActionResult.Ok;
        }

        private static int IndexOfPlayerUnitType(IReadOnlyList<UnitInstance> units, int playerId, UnitType type)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == playerId && units[i].Type == type) return i;
            }

            return -1;
        }
    }
}