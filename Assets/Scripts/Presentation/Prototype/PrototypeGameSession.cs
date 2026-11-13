using System;
using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    /// <summary>
    /// Stand-in for the economy and movement systems so the HUD can run the prototype scenario.
    /// Economy and map services should replace this class once they land in Core.
    /// </summary>
    public sealed class PrototypeGameSession
    {
        public const int StartingGold = 10;
        public const int UnitCost = 3;
        public const int VictoryPointsPerProvince = 1;

        private readonly IGameRules _rules;
        private readonly bool _crisisEnabled;
        private readonly PlayerProfile[] _players;
        private readonly ProvinceState[] _provinces;
        private readonly HashSet<int> _movedUnitIds = new();

        private int _currentPlayerIndex;
        private int _nextUnitId;

        public event Action StateChanged;
        public event Action<BattleReport> BattleResolved;
        public event Action<ProvinceState> OnProvinceStateChanged;

        public IReadOnlyList<ProvinceState> Provinces => _provinces;
        public PlayerProfile CurrentPlayer => _players[_currentPlayerIndex];
        public int TurnNumber { get; private set; } = 1;
        public bool IsGameOver { get; private set; }

        /// <param name="crisisEnabled">
        /// The three-province test map can never hold the crisis threshold of three, so that scene turns it off.
        /// </param>
        public PrototypeGameSession(
            IGameRules rules,
            IReadOnlyList<PlayerProfile> players,
            IReadOnlyList<ProvinceState> provinces,
            bool crisisEnabled)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _crisisEnabled = crisisEnabled;

            _players = new PlayerProfile[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                _players[i] = players[i];
            }

            _provinces = new ProvinceState[provinces.Count];
            for (int i = 0; i < provinces.Count; i++)
            {
                _provinces[i] = provinces[i];
                for (int u = 0; u < provinces[i].Units.Count; u++)
                {
                    _nextUnitId = Math.Max(_nextUnitId, provinces[i].Units[u].Id + 1);
                }
            }
        }

        public PlayerProfile GetPlayer(int playerId)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i].PlayerId == playerId)
                {
                    return _players[i];
                }
            }

            return null;
        }

        public int CountMovableUnits(int provinceId)
        {
            var units = _provinces[provinceId].Units;
            int movable = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (!_movedUnitIds.Contains(units[i].Id))
                {
                    movable++;
                }
            }

            return movable;
        }

        public CommandResult ValidateMoveOrigin(int provinceId)
        {
            if (IsGameOver) return CommandResult.GameOver;

            var province = _provinces[provinceId];
            if (province.OwnerId != CurrentPlayer.PlayerId) return CommandResult.NotYourProvince;
            if (province.Units.Count == 0) return CommandResult.NoUnits;
            if (CountMovableUnits(provinceId) == 0) return CommandResult.UnitsAlreadyMoved;

            return CommandResult.Ok;
        }

        public CommandResult BuyUnit(int provinceId)
        {
            if (IsGameOver) return CommandResult.GameOver;

            var province = _provinces[provinceId];
            var player = CurrentPlayer;
            if (province.OwnerId != player.PlayerId) return CommandResult.NotYourProvince;
            if (player.Gold < UnitCost) return CommandResult.NotEnoughGold;

            var units = new List<CombatUnit>(province.Units.Count + 1);
            for (int i = 0; i < province.Units.Count; i++)
            {
                units.Add(province.Units[i]);
            }

            units.Add(new CombatUnit(_nextUnitId++, player.Faction, 1, 1));
            SetProvince(province.WithOccupation(province.OwnerId, units));
            _players[_currentPlayerIndex] = CopyProfile(player, player.Faction, player.Gold - UnitCost,
                player.VictoryPoints, player.Units);

            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        public List<int> GetMovableUnitIds(int provinceId)
        {
            var units = _provinces[provinceId].Units;
            var ids = new List<int>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                if (!_movedUnitIds.Contains(units[i].Id))
                {
                    ids.Add(units[i].Id);
                }
            }

            return ids;
        }

        public CommandResult MoveArmy(MoveArmyCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            int fromId = command.OriginProvinceId;
            int toId = command.TargetProvinceId;
            var originCheck = ValidateMoveOrigin(fromId);
            if (originCheck != CommandResult.Ok) return originCheck;
            if (command.SelectedUnitIds == null || command.SelectedUnitIds.Count == 0)
            {
                return CommandResult.NoUnitsSelected;
            }

            var from = _provinces[fromId];
            var to = _provinces[toId];
            if (fromId == toId || !from.IsAdjacentTo(toId)) return CommandResult.NotAdjacent;

            var selection = SplitSelectedUnits(from, command.SelectedUnitIds, out var movers, out var stayers);
            if (selection != CommandResult.Ok) return selection;

            int playerId = CurrentPlayer.PlayerId;
            bool isFriendly = to.OwnerId == playerId;

            if (isFriendly || to.Units.Count == 0)
            {
                SetProvince(from.WithOccupation(from.OwnerId, stayers));
                SetProvince(to.WithOccupation(playerId, Concat(to.Units, movers)));
                MarkMoved(movers);
                StateChanged?.Invoke();
                return CommandResult.Ok;
            }

            ResolveBattle(from, to, movers, stayers);
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        public CommandResult EndTurn()
        {
            if (IsGameOver) return CommandResult.GameOver;

            var player = CurrentPlayer;
            int controlled = CountProvinces(player.PlayerId);
            int gold = player.Gold + _rules.CalculateRevenue(player.Faction, controlled);
            int victoryPoints = player.VictoryPoints + controlled * VictoryPointsPerProvince;
            var beforeRules = CopyProfile(player, player.Faction, gold, victoryPoints,
                CollectUnits(player.PlayerId));

            int provincesForRules = _crisisEnabled
                ? controlled
                : Math.Max(controlled, CrisisDirector.CrisisProvinceThreshold);
            var updated = _rules.EvaluatePlayerTurnState(beforeRules, provincesForRules);
            _players[_currentPlayerIndex] = updated;

            if (updated.Faction != player.Faction)
            {
                ConvertProvinceUnits(player.PlayerId, updated.Faction);
            }

            IsGameOver = updated.VictoryPoints >= CrisisDirector.VictoryPointsThreshold;
            if (!IsGameOver)
            {
                _movedUnitIds.Clear();
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Length;
                if (_currentPlayerIndex == 0)
                {
                    TurnNumber++;
                }
            }

            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        private void ResolveBattle(
            ProvinceState from,
            ProvinceState to,
            List<CombatUnit> movers,
            List<CombatUnit> stayers)
        {
            int attackerId = CurrentPlayer.PlayerId;
            int defenderId = to.OwnerId;
            var result = _rules.ResolveSkirmish(movers, to.Units, GetPlayer(defenderId).Faction, to.HasFort);

            bool captured = result.DefenderMustRetreat && result.SurvivingAttackers.Count > 0;
            if (captured)
            {
                SetProvince(from.WithOccupation(from.OwnerId, stayers));
                SetProvince(to.WithOccupation(attackerId, result.SurvivingAttackers));
                RetreatDefenders(to, defenderId, result.SurvivingDefenders);
            }
            else
            {
                SetProvince(from.WithOccupation(from.OwnerId, Concat(stayers, result.SurvivingAttackers)));
                SetProvince(to.WithOccupation(defenderId, result.SurvivingDefenders));
            }

            MarkMoved(result.SurvivingAttackers);
            BattleResolved?.Invoke(new BattleReport(to.Name, attackerId, defenderId, movers.Count, to.Units.Count,
                captured, result));
        }

        /// <summary>
        /// Survivors fall back to the first adjacent friendly province. With nowhere to go they are lost.
        /// </summary>
        private void RetreatDefenders(ProvinceState lostProvince, int defenderId, IReadOnlyList<CombatUnit> survivors)
        {
            for (int i = 0; i < lostProvince.Neighbours.Count; i++)
            {
                var neighbour = _provinces[lostProvince.Neighbours[i]];
                if (neighbour.OwnerId == defenderId)
                {
                    SetProvince(neighbour.WithOccupation(defenderId, Concat(neighbour.Units, survivors)));
                    return;
                }
            }
        }

        /// <summary>
        /// Selected units move, every other unit stays in the origin garrison. Fails without side effects when a
        /// selected unit has already moved, is not in the province, or is listed twice.
        /// </summary>
        private CommandResult SplitSelectedUnits(
            ProvinceState province,
            IReadOnlyList<int> selectedUnitIds,
            out List<CombatUnit> movers,
            out List<CombatUnit> stayers)
        {
            int count = province.Units.Count;
            movers = new List<CombatUnit>(selectedUnitIds.Count);
            stayers = new List<CombatUnit>(count);
            for (int i = 0; i < count; i++)
            {
                var unit = province.Units[i];
                if (!Contains(selectedUnitIds, unit.Id))
                {
                    stayers.Add(unit);
                    continue;
                }

                if (_movedUnitIds.Contains(unit.Id)) return CommandResult.UnitsAlreadyMoved;

                movers.Add(unit);
            }

            return movers.Count == selectedUnitIds.Count ? CommandResult.Ok : CommandResult.InvalidUnitSelection;
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkMoved(IReadOnlyList<CombatUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                _movedUnitIds.Add(units[i].Id);
            }
        }

        private int CountProvinces(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _provinces.Length; i++)
            {
                if (_provinces[i].OwnerId == playerId)
                {
                    count++;
                }
            }

            return count;
        }

        private List<CombatUnit> CollectUnits(int playerId)
        {
            var units = new List<CombatUnit>();
            for (int i = 0; i < _provinces.Length; i++)
            {
                if (_provinces[i].OwnerId == playerId)
                {
                    units.AddRange(_provinces[i].Units);
                }
            }

            return units;
        }

        private void ConvertProvinceUnits(int playerId, FactionType faction)
        {
            for (int i = 0; i < _provinces.Length; i++)
            {
                var province = _provinces[i];
                if (province.OwnerId != playerId) continue;

                var converted = new List<CombatUnit>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    converted.Add(new CombatUnit(unit.Id, faction, unit.MaxHp, unit.CurrentHp));
                }

                SetProvince(province.WithOccupation(playerId, converted));
            }
        }

        private void SetProvince(ProvinceState province)
        {
            _provinces[province.Id] = province;
            OnProvinceStateChanged?.Invoke(province);
        }

        private static List<CombatUnit> Concat(IReadOnlyList<CombatUnit> first, IReadOnlyList<CombatUnit> second)
        {
            var units = new List<CombatUnit>(first.Count + second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                units.Add(first[i]);
            }

            for (int i = 0; i < second.Count; i++)
            {
                units.Add(second[i]);
            }

            return units;
        }

        private static PlayerProfile CopyProfile(
            PlayerProfile profile,
            FactionType faction,
            int gold,
            int victoryPoints,
            IReadOnlyList<CombatUnit> units) =>
            new(profile.PlayerId, faction, gold, victoryPoints, units);
    }
}
