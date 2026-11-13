using System;
using System.Collections.Generic;
using Core.Combat;
using Presentation.Prototype;

namespace Presentation.Hud
{
    /// <summary>
    /// Selection and targeting state shared by the map input and the HUD buttons.
    /// </summary>
    public sealed class HudInteraction
    {
        public const int NoSelection = -1;

        private static readonly int UnitTypeCount = Enum.GetValues(typeof(UnitType)).Length;

        private enum TargetingMode
        {
            None,
            Move,
            Conversion
        }

        private readonly PrototypeGameSession _session;
        private readonly int[] _unitsToMove = new int[UnitTypeCount];
        private readonly int[] _unitsLeftToTake = new int[UnitTypeCount];
        private TargetingMode _targeting;

        public event Action Changed;

        public int SelectedProvinceId { get; private set; } = NoSelection;
        public bool IsTargetingMove => _targeting == TargetingMode.Move;
        public bool IsTargetingConversion => _targeting == TargetingMode.Conversion;
        public CommandResult LastResult { get; private set; } = CommandResult.Ok;

        /// <summary>Ruleset shift armed by a first click and waiting for the confirming second click.</summary>
        public FactionType? PendingShift { get; private set; }

        public HudInteraction(PrototypeGameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            ResetUnitsToMove();
        }

        public int MovableUnitCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < UnitTypeCount; i++)
                {
                    total += GetMovableUnitCount((UnitType)i);
                }

                return total;
            }
        }

        /// <summary>
        /// How many of the selected province's movable units the next move takes, across all unit types.
        /// </summary>
        public int UnitsToMove
        {
            get
            {
                int total = 0;
                for (int i = 0; i < UnitTypeCount; i++)
                {
                    total += GetUnitsToMove((UnitType)i);
                }

                return total;
            }
        }

        public int GetMovableUnitCount(UnitType type)
        {
            if (SelectedProvinceId == NoSelection) return 0;

            var units = _session.Provinces[SelectedProvinceId].Units;
            int playerId = _session.CurrentPlayer.PlayerId;
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == playerId && units[i].Type == type && !units[i].HasActed)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Units of this type the next move takes. Defaults to all movable ones.
        /// </summary>
        public int GetUnitsToMove(UnitType type) =>
            Math.Clamp(_unitsToMove[(int)type], 0, GetMovableUnitCount(type));

        public void IncreaseUnitsToMove(UnitType type)
        {
            _unitsToMove[(int)type] = Math.Min(GetMovableUnitCount(type), GetUnitsToMove(type) + 1);
            Changed?.Invoke();
        }

        public void DecreaseUnitsToMove(UnitType type)
        {
            _unitsToMove[(int)type] = Math.Max(0, GetUnitsToMove(type) - 1);
            Changed?.Invoke();
        }

        public void ClickProvince(int provinceId)
        {
            LastResult = CommandResult.Ok;
            PendingShift = null;

            if (_targeting == TargetingMode.None)
            {
                Select(provinceId);
                Changed?.Invoke();
                return;
            }

            int origin = SelectedProvinceId;
            var mode = _targeting;
            _targeting = TargetingMode.None;
            if (provinceId == origin)
            {
                Changed?.Invoke();
                return;
            }

            if (mode == TargetingMode.Move)
            {
                LastResult = _session.MoveArmy(new MoveArmyCommand(origin, provinceId, TakeUnitsToMove(origin)));
                if (LastResult == CommandResult.Ok)
                {
                    Select(provinceId);
                }
            }
            else
            {
                LastResult = _session.DivineConversion(origin, provinceId);
            }

            Changed?.Invoke();
        }

        public void ClickEmptySpace()
        {
            LastResult = CommandResult.Ok;
            _targeting = TargetingMode.None;
            PendingShift = null;
            Select(NoSelection);
            Changed?.Invoke();
        }

        public void CancelTargeting()
        {
            if (_targeting == TargetingMode.None && PendingShift == null) return;

            _targeting = TargetingMode.None;
            PendingShift = null;
            Changed?.Invoke();
        }

        public void BeginMove()
        {
            if (SelectedProvinceId == NoSelection) return;

            PendingShift = null;
            LastResult = _session.ValidateMoveOrigin(SelectedProvinceId);
            if (LastResult == CommandResult.Ok && UnitsToMove == 0)
            {
                LastResult = CommandResult.NoUnitsSelected;
            }

            _targeting = LastResult == CommandResult.Ok ? TargetingMode.Move : TargetingMode.None;
            Changed?.Invoke();
        }

        /// <summary>
        /// Fills <paramref name="actions"/> with the commands that make sense for the current player in the selected
        /// province. The session still makes the final checks (gold, AP, presence) and reports them through
        /// <see cref="LastResult"/>.
        /// </summary>
        public void GetAvailableActions(List<HudAction> actions)
        {
            actions.Clear();
            if (SelectedProvinceId == NoSelection || _session.IsGameOver) return;

            var province = _session.Provinces[SelectedProvinceId];
            var player = _session.CurrentPlayer;
            int playerId = player.PlayerId;
            bool ownsProvince = province.OwnerId == playerId;
            bool hasMilitary = HasUnit(province, playerId, UnitType.RegularMilitary);

            switch (player.Faction)
            {
                case FactionType.Zealots:
                    if (!ownsProvince) break;

                    actions.Add(new HudAction(HudActionKind.BuyMissionary));
                    if (!province.HasChurch) actions.Add(new HudAction(HudActionKind.BuildChurch));
                    if (HasUnit(province, playerId, UnitType.Missionary))
                    {
                        if (province.PresenceToken == null) actions.Add(new HudAction(HudActionKind.Consecrate));
                        actions.Add(new HudAction(HudActionKind.DivineConversion));
                    }

                    break;

                case FactionType.Guerrillas:
                    if (province.RebellionCell == null)
                    {
                        actions.Add(new HudAction(HudActionKind.PlaceRebellionCell));
                    }
                    else if (province.RebellionCell.OwnerId == playerId && !province.RebellionCell.IsOpenRebellion)
                    {
                        actions.Add(new HudAction(HudActionKind.InciteRebellion));
                    }

                    if (ownsProvince) actions.Add(new HudAction(HudActionKind.BuyUnit));
                    break;

                default:
                    if (ownsProvince) actions.Add(new HudAction(HudActionKind.BuyUnit));
                    if (player.Faction == FactionType.Militants && hasMilitary)
                    {
                        if (province.RebellionCell != null && province.RebellionCell.IsOpenRebellion)
                        {
                            actions.Add(new HudAction(HudActionKind.MartialLaw));
                        }

                        if (province.PresenceToken != null) actions.Add(new HudAction(HudActionKind.Inquisition));
                    }

                    break;
            }

            bool isLanded = player.Faction == FactionType.Militants || player.Faction == FactionType.Zealots;
            if (isLanded && hasMilitary && HasSomethingToCleanse(province, playerId))
            {
                actions.Add(new HudAction(HudActionKind.CleanseProvince));
            }

            if (!hasMilitary) return;

            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId == playerId || unit.Type != UnitType.RegularMilitary) continue;
                if (ContainsAttack(actions, unit.OwnerPlayerId)) continue;

                actions.Add(new HudAction(HudActionKind.Attack, unit.OwnerPlayerId));
            }
        }

        public void Execute(HudAction action)
        {
            if (SelectedProvinceId == NoSelection) return;

            int province = SelectedProvinceId;
            PendingShift = null;
            _targeting = TargetingMode.None;
            LastResult = action.Kind switch
            {
                HudActionKind.BuyUnit => _session.BuyUnit(province),
                HudActionKind.BuyMissionary => _session.BuyMissionary(province),
                HudActionKind.BuildChurch => _session.BuildChurch(province),
                HudActionKind.Consecrate => _session.Consecrate(province),
                HudActionKind.DivineConversion => BeginConversion(),
                HudActionKind.PlaceRebellionCell => _session.PlaceRebellionCell(province),
                HudActionKind.InciteRebellion => _session.InciteRebellion(province),
                HudActionKind.MartialLaw => _session.MartialLaw(province),
                HudActionKind.Inquisition => _session.Inquisition(province),
                HudActionKind.CleanseProvince => _session.CleanseProvince(province),
                HudActionKind.Attack => _session.InitiateBattle(province, action.TargetPlayerId),
                _ => CommandResult.Ok
            };

            Changed?.Invoke();
        }

        /// <summary>
        /// Ruleset shifts cost VP, so the first click only arms the shift and a second click on it confirms.
        /// </summary>
        public void RequestShift(FactionType targetFaction)
        {
            _targeting = TargetingMode.None;
            if (PendingShift != targetFaction)
            {
                PendingShift = targetFaction;
                LastResult = CommandResult.Ok;
                Changed?.Invoke();
                return;
            }

            PendingShift = null;
            LastResult = _session.CurrentPlayer.Faction == FactionType.Guerrillas
                ? _session.DeclareAscension(targetFaction)
                : _session.ShiftRuleset(targetFaction);
            Changed?.Invoke();
        }

        public void EndTurn()
        {
            LastResult = _session.EndTurn();
            _targeting = TargetingMode.None;
            PendingShift = null;
            Select(NoSelection);
            Changed?.Invoke();
        }

        public bool IsValidMoveTarget(int provinceId) =>
            IsTargetingMove
            && provinceId != SelectedProvinceId
            && _session.Provinces[SelectedProvinceId].IsAdjacentTo(provinceId);

        /// <summary>
        /// Provinces to highlight while a move or a Divine Conversion waits for its target.
        /// </summary>
        public bool IsValidTarget(int provinceId)
        {
            if (IsTargetingMove) return IsValidMoveTarget(provinceId);
            if (!IsTargetingConversion || provinceId == SelectedProvinceId) return false;

            return _session.Provinces[SelectedProvinceId].IsAdjacentTo(provinceId)
                   && HasEnemyUnit(_session.Provinces[provinceId], _session.CurrentPlayer.PlayerId);
        }

        private CommandResult BeginConversion()
        {
            _targeting = TargetingMode.Conversion;
            return CommandResult.Ok;
        }

        private void Select(int provinceId)
        {
            SelectedProvinceId = provinceId;
            ResetUnitsToMove();
        }

        private void ResetUnitsToMove()
        {
            for (int i = 0; i < _unitsToMove.Length; i++)
            {
                _unitsToMove[i] = int.MaxValue;
            }
        }

        private List<int> TakeUnitsToMove(int provinceId)
        {
            for (int i = 0; i < UnitTypeCount; i++)
            {
                _unitsLeftToTake[i] = GetUnitsToMove((UnitType)i);
            }

            var units = _session.Provinces[provinceId].Units;
            int playerId = _session.CurrentPlayer.PlayerId;
            var ids = new List<int>(UnitsToMove);
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                int type = (int)unit.Type;
                if (unit.OwnerPlayerId != playerId || unit.HasActed || _unitsLeftToTake[type] == 0) continue;

                _unitsLeftToTake[type]--;
                ids.Add(unit.Id);
            }

            return ids;
        }

        private static bool HasUnit(ProvinceState province, int playerId, UnitType type)
        {
            for (int i = 0; i < province.Units.Count; i++)
            {
                if (province.Units[i].OwnerPlayerId == playerId && province.Units[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEnemyUnit(ProvinceState province, int playerId)
        {
            for (int i = 0; i < province.Units.Count; i++)
            {
                if (province.Units[i].OwnerPlayerId != playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSomethingToCleanse(ProvinceState province, int playerId)
        {
            bool enemyChurch = province.HasChurch && province.ChurchOwnerId != playerId && !province.HasFort;
            bool enemyToken = province.PresenceToken != null && province.PresenceToken.OwnerId != playerId;
            return enemyChurch || enemyToken;
        }

        private static bool ContainsAttack(List<HudAction> actions, int targetPlayerId)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Kind == HudActionKind.Attack && actions[i].TargetPlayerId == targetPlayerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
