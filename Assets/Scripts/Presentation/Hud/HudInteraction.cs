using System;
using System.Collections.Generic;
using Core.Combat;
using Presentation.Prototype;

namespace Presentation.Hud
{
    public enum TargetingMode
    {
        None,
        Move,
        DivineConversion
    }

    /// <summary>
    /// Selection and targeting state shared by the map input and the HUD buttons.
    /// </summary>
    public sealed class HudInteraction
    {
        public const int NoSelection = -1;

        private readonly PrototypeGameSession _session;
        private int _unitsToMove = int.MaxValue;

        public event Action Changed;

        public int SelectedProvinceId { get; private set; } = NoSelection;
        public TargetingMode Targeting { get; private set; } = TargetingMode.None;
        public UnitType? MoveUnitTypeFilter { get; private set; }
        public CommandResult LastResult { get; private set; } = CommandResult.Ok;

        public bool IsTargetingMove => Targeting == TargetingMode.Move;
        public bool IsTargetingConversion => Targeting == TargetingMode.DivineConversion;

        public int MovableUnitCount =>
            SelectedProvinceId == NoSelection ? 0 : GetFilteredMovableUnitIds(SelectedProvinceId).Count;

        /// <summary>
        /// How many of the selected province's filtered movable units the next move takes. Defaults to all of them.
        /// </summary>
        public int UnitsToMove => Math.Clamp(_unitsToMove, Math.Min(1, MovableUnitCount), MovableUnitCount);

        public HudInteraction(PrototypeGameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void ClickProvince(int provinceId)
        {
            LastResult = CommandResult.Ok;

            if (Targeting == TargetingMode.None)
            {
                Select(provinceId);
                Changed?.Invoke();
                return;
            }

            int origin = SelectedProvinceId;
            var activeMode = Targeting;
            Targeting = TargetingMode.None;

            if (provinceId == origin)
            {
                Changed?.Invoke();
                return;
            }

            if (activeMode == TargetingMode.Move)
            {
                LastResult = _session.MoveArmy(new MoveArmyCommand(origin, provinceId, TakeUnitsToMove(origin)));
                if (LastResult == CommandResult.Ok)
                {
                    Select(provinceId);
                }
            }
            else if (activeMode == TargetingMode.DivineConversion)
            {
                LastResult = _session.DivineConversion(origin, provinceId);
            }

            Changed?.Invoke();
        }

        public void ClickEmptySpace()
        {
            LastResult = CommandResult.Ok;
            Targeting = TargetingMode.None;
            Select(NoSelection);
            Changed?.Invoke();
        }

        public void CancelTargeting()
        {
            if (Targeting == TargetingMode.None) return;

            Targeting = TargetingMode.None;
            Changed?.Invoke();
        }

        public void CycleMoveUnitType()
        {
            MoveUnitTypeFilter = MoveUnitTypeFilter switch
            {
                null => UnitType.RegularMilitary,
                UnitType.RegularMilitary => UnitType.Missionary,
                UnitType.Missionary => UnitType.Trader,
                _ => null
            };
            _unitsToMove = int.MaxValue;
            Changed?.Invoke();
        }

        public void IncreaseUnitsToMove()
        {
            _unitsToMove = Math.Min(MovableUnitCount, UnitsToMove + 1);
            Changed?.Invoke();
        }

        public void DecreaseUnitsToMove()
        {
            _unitsToMove = Math.Max(1, UnitsToMove - 1);
            Changed?.Invoke();
        }

        public void BeginMove()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.ValidateMoveOrigin(SelectedProvinceId);
            if (LastResult == CommandResult.Ok && MovableUnitCount == 0)
            {
                LastResult = CommandResult.NoUnitsSelected;
            }

            Targeting = LastResult == CommandResult.Ok ? TargetingMode.Move : TargetingMode.None;
            Changed?.Invoke();
        }

        public void Attack(int defenderPlayerId)
        {
            if (SelectedProvinceId == NoSelection) return;

            Targeting = TargetingMode.None;
            LastResult = _session.InitiateBattle(SelectedProvinceId, defenderPlayerId);
            Changed?.Invoke();
        }

        public void BuyUnit()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.BuyUnit(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void BuyMissionary()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.BuyMissionary(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void BuyTrader()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.BuyTrader(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void BuildChurch()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.BuildChurch(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void Consecrate()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.Consecrate(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void BeginDivineConversion()
        {
            if (SelectedProvinceId == NoSelection) return;

            if (_session.CurrentPlayer.Faction != FactionType.Zealots)
            {
                LastResult = CommandResult.WrongFaction;
                Changed?.Invoke();
                return;
            }

            if (_session.GetUnitsForPlayer(SelectedProvinceId, _session.CurrentPlayer.PlayerId, UnitType.Missionary)
                    .Count == 0)
            {
                LastResult = CommandResult.NoMissionary;
                Changed?.Invoke();
                return;
            }

            LastResult = CommandResult.Ok;
            Targeting = TargetingMode.DivineConversion;
            Changed?.Invoke();
        }

        public void PlaceRebellionCell()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.PlaceRebellionCell(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void InciteRebellion()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.InciteRebellion(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void MartialLaw()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.MartialLaw(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void Inquisition()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.Inquisition(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void CleanseProvince()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.CleanseProvince(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void VoluntaryShift(FactionType targetFaction)
        {
            Targeting = TargetingMode.None;
            LastResult = _session.VoluntaryShift(targetFaction);
            Changed?.Invoke();
        }

        public void Ascend(FactionType targetLandedFaction)
        {
            Targeting = TargetingMode.None;
            LastResult = _session.Ascend(targetLandedFaction);
            Changed?.Invoke();
        }

        public void EndTurn()
        {
            LastResult = _session.EndTurn();
            Targeting = TargetingMode.None;
            Select(NoSelection);
            Changed?.Invoke();
        }

        public bool IsValidMoveTarget(int provinceId) =>
            Targeting != TargetingMode.None
            && provinceId != SelectedProvinceId
            && _session.Provinces[SelectedProvinceId].IsAdjacentTo(provinceId);

        private void Select(int provinceId)
        {
            SelectedProvinceId = provinceId;
            MoveUnitTypeFilter = null;
            _unitsToMove = int.MaxValue;
        }

        private List<int> GetFilteredMovableUnitIds(int provinceId)
        {
            var units = _session.Provinces[provinceId].Units;
            int currentPlayerId = _session.CurrentPlayer.PlayerId;
            var ids = new List<int>(units.Count);

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.OwnerPlayerId != currentPlayerId || unit.HasActed) continue;
                if (MoveUnitTypeFilter.HasValue && unit.Type != MoveUnitTypeFilter.Value) continue;
                ids.Add(unit.Id);
            }

            return ids;
        }

        private List<int> TakeUnitsToMove(int provinceId)
        {
            int count = UnitsToMove;
            var movable = GetFilteredMovableUnitIds(provinceId);
            if (movable.Count > count)
            {
                movable.RemoveRange(count, movable.Count - count);
            }

            return movable;
        }
    }
}