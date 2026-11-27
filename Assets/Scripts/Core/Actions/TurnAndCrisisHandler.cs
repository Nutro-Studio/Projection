using System;
using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Crisis;
using Core.Simulation;

namespace Core.Actions
{
    internal sealed class TurnAndCrisisHandler
    {
        private readonly GameSessionState _state;
        private readonly ICrisisDirector _crisisDirector;
        private readonly IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets;
        private readonly GuildCommerceHandler _guildCommerce;

        public TurnAndCrisisHandler(
            GameSessionState state,
            ICrisisDirector crisisDirector,
            IReadOnlyDictionary<FactionType, IFactionRuleset> rulesets,
            GuildCommerceHandler guildCommerce)
        {
            _state = state;
            _crisisDirector = crisisDirector;
            _rulesets = rulesets;
            _guildCommerce = guildCommerce;
        }

        public ActionResult EndTurn()
        {
            if (_state.IsGameOver) return ActionResult.GameOver;

            CollectChurchTithes();

            var player = _state.CurrentPlayer;
            int controlled = _state.CountProvinces(player.PlayerId);
            int revenue = _rulesets.TryGetValue(player.Faction, out var ruleset)
                ? ruleset.CalculateUpkeepRevenue(controlled)
                : 0;
            int gold = player.Gold + revenue;
            int vpGain = CalculateTurnEndVictoryPoints(player, controlled, gold);
            int victoryPoints = player.VictoryPoints + vpGain;

            var beforeRules = new PlayerProfile(
                player.PlayerId, player.Faction, gold, victoryPoints,
                CollectUnits(player.PlayerId), player.ActionPoints);

            int provincesForRules = _state.CrisisEnabled
                ? controlled
                : Math.Max(controlled, _state.Config.CrisisProvinceThreshold);

            var updated = EvaluatePlayerTurnState(beforeRules, provincesForRules);
            _state.Players[_state.CurrentPlayerIndex] = updated;
            _state.QualifyingCaravanRoutesThisTurn = 0;

            if (updated.Faction != beforeRules.Faction)
            {
                SynchronizeBoardAfterShift(
                    new CrisisShiftResult(updated, beforeRules.Faction, updated.Faction, true, false, false));
            }

            _state.IsGameOver = updated.VictoryPoints >= _state.Config.VictoryPointsThreshold;
            if (!_state.IsGameOver)
            {
                ResetUnitActionFlags();
                _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Length;
                if (_state.CurrentPlayerIndex == 0)
                {
                    _state.TurnNumber++;
                }

                _state.Players[_state.CurrentPlayerIndex] =
                    _state.CurrentPlayer with { ActionPoints = PlayerProfile.BaseActionPoints };
            }

            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult Capitulate()
        {
            if (_state.IsGameOver) return ActionResult.GameOver;

            var player = _state.CurrentPlayer;
            if (!_crisisDirector.CanCapitulate(player)) return ActionResult.InvalidRulesetShift;

            var withUnits = player with { Units = CollectUnits(player.PlayerId) };
            var shift = _crisisDirector.ExecuteForcedCrisis(withUnits, isCapitulation: true);
            ApplyShiftResult(shift, spendActionPoint: false);
            return ActionResult.Ok;
        }

        public ActionResult VoluntaryShift(FactionType targetFaction)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;

            var player = _state.CurrentPlayer;
            int controlled = _state.CountProvinces(player.PlayerId);
            if (!_crisisDirector.CanTriggerVoluntaryShift(player, targetFaction, controlled))
            {
                return ActionResult.InvalidRulesetShift;
            }

            var withUnits = player with { Units = CollectUnits(player.PlayerId) };
            var shift = _crisisDirector.ExecuteVoluntaryShift(withUnits, targetFaction);
            ApplyShiftResult(shift, spendActionPoint: true);
            return ActionResult.Ok;
        }

        public ActionResult Ascend(FactionType targetLandedFaction)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;

            var player = _state.CurrentPlayer;
            int rebelCount = CountRebelProvinces(player.PlayerId);
            if (!_crisisDirector.CanTriggerAscension(player, rebelCount))
            {
                return ActionResult.InvalidRulesetShift;
            }

            var withUnits = player with { Units = CollectUnits(player.PlayerId) };
            var shift = _crisisDirector.ExecuteAscension(withUnits, targetLandedFaction);
            ApplyShiftResult(shift, spendActionPoint: true);
            return ActionResult.Ok;
        }

        public int CountRebelProvinces(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var cell = _state.Provinces[i].RebellionCell;
                if (cell != null && cell.OwnerId == playerId) count++;
            }

            return count;
        }

        public int CountPresenceProvinces(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                if (_state.Provinces[i].PresenceToken != null && _state.Provinces[i].PresenceToken.OwnerId == playerId)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountActiveRebellionProvinces(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var cell = _state.Provinces[i].RebellionCell;
                if (cell != null && cell.OwnerId == playerId && cell.IsOpenRebellion)
                {
                    count++;
                }
            }

            return count;
        }

        private PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces)
        {
            if (_crisisDirector.CheckVictory(player)) return player;
            if (_crisisDirector.CanTriggerCrisis(player, controlledProvinces))
            {
                return _crisisDirector.ExecuteForcedCrisis(player).UpdatedProfile;
            }

            return player;
        }

        private void ApplyShiftResult(CrisisShiftResult shift, bool spendActionPoint)
        {
            int ap = spendActionPoint
                ? Math.Max(0, _state.CurrentPlayer.ActionPoints - 1)
                : _state.CurrentPlayer.ActionPoints;
            _state.Players[_state.CurrentPlayerIndex] = shift.UpdatedProfile with { ActionPoints = ap };

            SynchronizeBoardAfterShift(shift);

            if (_state.Players[_state.CurrentPlayerIndex].VictoryPoints >= _state.Config.VictoryPointsThreshold)
            {
                _state.IsGameOver = true;
            }

            _state.NotifyStateChanged();
        }

        private void SynchronizeBoardAfterShift(CrisisShiftResult shift)
        {
            int playerId = shift.UpdatedProfile.PlayerId;
            var retainedById = new Dictionary<int, UnitInstance>(shift.UpdatedProfile.Units.Count);
            for (int i = 0; i < shift.UpdatedProfile.Units.Count; i++)
            {
                var u = shift.UpdatedProfile.Units[i];
                retainedById[u.Id] = u;
            }

            for (int p = 0; p < _state.Provinces.Length; p++)
            {
                var province = _state.Provinces[p];
                bool unitsChanged = false;
                var updatedUnits = new List<UnitInstance>(province.Units.Count);

                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.OwnerPlayerId != playerId)
                    {
                        updatedUnits.Add(unit);
                        continue;
                    }

                    if (retainedById.TryGetValue(unit.Id, out var transformed))
                    {
                        updatedUnits.Add(transformed);
                        if (transformed != unit) unitsChanged = true;
                    }
                    else
                    {
                        _state.ActiveCaravans.Remove(unit.Id);
                        unitsChanged = true;
                    }
                }

                if (unitsChanged) _state.SetProvince(province.WithUnits(updatedUnits));
            }

            if (shift.IsCrisis)
            {
                for (int p = 0; p < _state.Provinces.Length; p++)
                {
                    var province = _state.Provinces[p];
                    if (province.OwnerId != playerId) continue;

                    bool hasEnemyMilitary = false;
                    for (int u = 0; u < province.Units.Count; u++)
                    {
                        var unit = province.Units[u];
                        if (unit.OwnerPlayerId != playerId && unit.Type == UnitType.RegularMilitary)
                        {
                            hasEnemyMilitary = true;
                            break;
                        }
                    }

                    if (!hasEnemyMilitary)
                    {
                        var collapsed = province.WithOccupation(ProvinceState.NeutralOwner, province.Units);
                        if (collapsed.RebellionCell == null)
                        {
                            collapsed = collapsed.WithRebellionCell(new RebellionCell(playerId, false));
                        }

                        _state.SetProvinceDirect(collapsed);
                    }
                    else if (province.RebellionCell == null)
                    {
                        _state.SetProvince(province.WithRebellionCell(new RebellionCell(playerId, false)));
                    }
                }
            }
            else if (shift.PreviousFaction == FactionType.Militants && shift.IsVoluntary)
            {
                var fortifiedProvinces = new List<int>();
                for (int p = 0; p < _state.Provinces.Length; p++)
                {
                    if (_state.Provinces[p].OwnerId == playerId && _state.Provinces[p].HasFort)
                    {
                        fortifiedProvinces.Add(p);
                    }
                }

                int fortsToRemove = (int)Math.Round(fortifiedProvinces.Count * 0.30, MidpointRounding.AwayFromZero);
                for (int i = 0; i < fortsToRemove && i < fortifiedProvinces.Count; i++)
                {
                    _state.SetProvince(_state.Provinces[fortifiedProvinces[i]].WithFort(false));
                }
            }
            else if (shift.PreviousFaction == FactionType.Zealots && shift.IsVoluntary)
            {
                var churchProvinces = new List<int>();
                for (int p = 0; p < _state.Provinces.Length; p++)
                {
                    if (_state.Provinces[p].ChurchOwnerId == playerId) churchProvinces.Add(p);
                }

                int churchesToRemove = (int)Math.Floor(churchProvinces.Count * 0.50);
                for (int i = 0; i < churchesToRemove && i < churchProvinces.Count; i++)
                {
                    _state.SetProvince(_state.Provinces[churchProvinces[i]].WithChurch(ProvinceState.NoChurch));
                }
            }
            else if (shift.PreviousFaction == FactionType.Guilds && shift.IsVoluntary)
            {
                for (int p = 0; p < _state.Provinces.Length; p++)
                {
                    if (_state.Provinces[p].TradingPostOwnerId == playerId)
                    {
                        _state.SetProvince(_state.Provinces[p].WithTradingPost(ProvinceState.NoTradingPost));
                    }
                }
            }

            if (shift.IsAscension)
            {
                int capitalId = FindCapitalProvinceId(playerId, preferRebelCells: true);
                for (int p = 0; p < _state.Provinces.Length; p++)
                {
                    var province = _state.Provinces[p];
                    bool isRebelProvince = province.RebellionCell != null && province.RebellionCell.OwnerId == playerId;
                    if (!isRebelProvince) continue;

                    var updated = province.WithRebellionCell(null);
                    int playerStrength = BoardHegemonyEvaluator.CalculateStrength(updated, playerId);
                    int maxEnemyStrength = GetMaxEnemyStrength(updated, playerId);

                    if (p == capitalId)
                    {
                        var cleared = new List<UnitInstance>(updated.Units.Count);
                        for (int u = 0; u < updated.Units.Count; u++)
                        {
                            var unit = updated.Units[u];
                            if (unit.OwnerPlayerId != playerId && unit.Type == UnitType.RegularMilitary) continue;
                            cleared.Add(unit);
                        }

                        updated = updated.WithUnits(cleared);
                        maxEnemyStrength = 0;
                    }

                    if (playerStrength >= maxEnemyStrength)
                    {
                        bool hasPlayerMilitary = false;
                        for (int u = 0; u < updated.Units.Count; u++)
                        {
                            if (updated.Units[u].OwnerPlayerId == playerId &&
                                updated.Units[u].Type == UnitType.RegularMilitary)
                            {
                                hasPlayerMilitary = true;
                                break;
                            }
                        }

                        if (!hasPlayerMilitary)
                        {
                            var garrison = new List<UnitInstance>(updated.Units)
                            {
                                new(_state.NextUnitId++, playerId, shift.NewFaction, UnitType.RegularMilitary, 1, 1)
                            };
                            updated = updated.WithUnits(garrison);
                        }
                    }

                    _state.SetProvince(updated);
                }
            }

            if (shift.NewFaction == FactionType.Guilds)
            {
                SetupGuildTransitionCapitals(playerId);
            }
        }

        private void SetupGuildTransitionCapitals(int playerId)
        {
            int ownCapitalId = FindCapitalProvinceId(playerId, preferRebelCells: false);
            if (ownCapitalId >= 0)
            {
                var cap = _state.Provinces[ownCapitalId];
                var units = new List<UnitInstance>(cap.Units);
                bool hasTrader = false;
                for (int u = 0; u < units.Count; u++)
                {
                    if (units[u].OwnerPlayerId == playerId && units[u].Type == UnitType.Trader)
                    {
                        hasTrader = true;
                        break;
                    }
                }

                if (!hasTrader)
                {
                    units.Add(
                        new UnitInstance(_state.NextUnitId++, playerId, FactionType.Guilds, UnitType.Trader, 1, 1));
                }

                _state.SetProvince(cap.WithTradingPost(playerId).WithUnits(units));
            }

            bool hasValidSecondTarget = false;
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var p = _state.Provinces[i];
                if (p.OwnerId != playerId && !p.HasTradingPost)
                {
                    hasValidSecondTarget = true;
                    break;
                }
            }

            _state.PendingGuildCapitalPlayerId = hasValidSecondTarget ? playerId : -1;
        }

        private int FindCapitalProvinceId(int playerId, bool preferRebelCells)
        {
            int bestId = -1;
            int bestScore = -1;

            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var p = _state.Provinces[i];
                bool candidate = preferRebelCells
                    ? (p.RebellionCell != null && p.RebellionCell.OwnerId == playerId) || p.OwnerId == playerId
                    : p.OwnerId == playerId;
                if (!candidate) continue;

                int score = BoardHegemonyEvaluator.CalculateStrength(p, playerId) * 10
                            + (p.OwnerId == playerId ? 5 : 0)
                            + (p.HasFort || p.HasChurch ? 2 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            if (bestId >= 0) return bestId;

            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                if (!_state.Provinces[i].HasTradingPost) return i;
            }

            return _state.Provinces.Length > 0 ? 0 : -1;
        }

        private int GetMaxEnemyStrength(ProvinceState province, int playerId)
        {
            int maxEnemy = 0;
            for (int i = 0; i < _state.Players.Length; i++)
            {
                int otherId = _state.Players[i].PlayerId;
                if (otherId == playerId) continue;
                int s = BoardHegemonyEvaluator.CalculateStrength(province, otherId);
                if (s > maxEnemy) maxEnemy = s;
            }

            return maxEnemy;
        }

        private int CalculateTurnEndVictoryPoints(PlayerProfile player, int controlledProvinces, int bankedGold) =>
            player.Faction switch
            {
                FactionType.Militants => controlledProvinces / 3,
                FactionType.Zealots => CountPresenceProvinces(player.PlayerId),
                FactionType.Guilds => _guildCommerce.CountOwnedTradingPosts(player.PlayerId)
                                      + (Math.Max(0, bankedGold) / 10)
                                      + (_state.QualifyingCaravanRoutesThisTurn * 2),
                FactionType.Guerrillas => CountActiveRebellionProvinces(player.PlayerId),
                _ => controlledProvinces * _state.Config.VictoryPointsPerProvince
            };

        private void CollectChurchTithes()
        {
            var payer = _state.CurrentPlayer;
            if (payer.Faction == FactionType.Zealots || payer.Faction == FactionType.Guerrillas) return;

            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var province = _state.Provinces[i];
                if (!province.HasChurch) continue;

                int churchOwnerIndex = _state.IndexOfPlayer(province.ChurchOwnerId);
                if (churchOwnerIndex < 0 || _state.Players[churchOwnerIndex].Faction != FactionType.Zealots) continue;

                var kept = new List<UnitInstance>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.OwnerPlayerId != payer.PlayerId || unit.Type != UnitType.RegularMilitary)
                    {
                        kept.Add(unit);
                        continue;
                    }

                    if (_state.CurrentPlayer.Gold < _state.Config.ChurchTithe) continue;

                    _state.AddGold(_state.CurrentPlayerIndex, -_state.Config.ChurchTithe);
                    _state.AddGold(churchOwnerIndex, _state.Config.ChurchTithe);
                    kept.Add(unit);
                }

                if (kept.Count != province.Units.Count)
                {
                    _state.SetProvince(province.WithUnits(kept));
                }
            }
        }

        private void ResetUnitActionFlags()
        {
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var province = _state.Provinces[i];
                bool changed = false;
                var resetUnits = new List<UnitInstance>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.HasActed)
                    {
                        resetUnits.Add(unit with { HasActed = false });
                        changed = true;
                    }
                    else
                    {
                        resetUnits.Add(unit);
                    }
                }

                if (changed) _state.Provinces[i] = province.WithUnits(resetUnits);
            }
        }

        private List<UnitInstance> CollectUnits(int playerId)
        {
            var units = new List<UnitInstance>();
            for (int i = 0; i < _state.Provinces.Length; i++)
            {
                var provinceUnits = _state.Provinces[i].Units;
                for (int u = 0; u < provinceUnits.Count; u++)
                {
                    if (provinceUnits[u].OwnerPlayerId == playerId) units.Add(provinceUnits[u]);
                }
            }

            return units;
        }
    }
}