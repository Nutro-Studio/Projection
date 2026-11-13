using System;
using System.Collections.Generic;
using Presentation.Prototype;

namespace Presentation.Hud
{
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
        public bool IsTargetingMove { get; private set; }
        public CommandResult LastResult { get; private set; } = CommandResult.Ok;

        public int MovableUnitCount =>
            SelectedProvinceId == NoSelection ? 0 : _session.CountMovableUnits(SelectedProvinceId);

        /// <summary>
        /// How many of the selected province's movable units the next move takes. Defaults to all of them.
        /// </summary>
        public int UnitsToMove => Math.Clamp(_unitsToMove, Math.Min(1, MovableUnitCount), MovableUnitCount);

        public HudInteraction(PrototypeGameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void ClickProvince(int provinceId)
        {
            LastResult = CommandResult.Ok;

            if (!IsTargetingMove)
            {
                Select(provinceId);
                Changed?.Invoke();
                return;
            }

            int origin = SelectedProvinceId;
            IsTargetingMove = false;
            if (provinceId == origin)
            {
                Changed?.Invoke();
                return;
            }

            LastResult = _session.MoveArmy(new MoveArmyCommand(origin, provinceId, TakeUnitsToMove(origin)));
            if (LastResult == CommandResult.Ok)
            {
                Select(provinceId);
            }

            Changed?.Invoke();
        }

        public void ClickEmptySpace()
        {
            LastResult = CommandResult.Ok;
            IsTargetingMove = false;
            Select(NoSelection);
            Changed?.Invoke();
        }

        public void CancelTargeting()
        {
            if (!IsTargetingMove) return;

            IsTargetingMove = false;
            Changed?.Invoke();
        }

        public void BuyUnit()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.BuyUnit(SelectedProvinceId);
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
            IsTargetingMove = LastResult == CommandResult.Ok;
            Changed?.Invoke();
        }

        public void EndTurn()
        {
            LastResult = _session.EndTurn();
            IsTargetingMove = false;
            Select(NoSelection);
            Changed?.Invoke();
        }

        public bool IsValidMoveTarget(int provinceId) =>
            IsTargetingMove
            && provinceId != SelectedProvinceId
            && _session.Provinces[SelectedProvinceId].IsAdjacentTo(provinceId);

        private void Select(int provinceId)
        {
            SelectedProvinceId = provinceId;
            _unitsToMove = int.MaxValue;
        }

        private List<int> TakeUnitsToMove(int provinceId)
        {
            int count = UnitsToMove;
            var movable = _session.GetMovableUnitIds(provinceId);
            if (movable.Count > count)
            {
                movable.RemoveRange(count, movable.Count - count);
            }

            return movable;
        }
    }
}
