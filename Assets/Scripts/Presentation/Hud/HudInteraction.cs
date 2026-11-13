using System;
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

        public event Action Changed;

        public int SelectedProvinceId { get; private set; } = NoSelection;
        public bool IsTargetingMove { get; private set; }
        public CommandResult LastResult { get; private set; } = CommandResult.Ok;

        public HudInteraction(PrototypeGameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void ClickProvince(int provinceId)
        {
            LastResult = CommandResult.Ok;

            if (!IsTargetingMove)
            {
                SelectedProvinceId = provinceId;
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

            LastResult = _session.MoveArmy(
                new MoveArmyCommand(origin, provinceId, _session.GetMovableUnitIds(origin)));
            if (LastResult == CommandResult.Ok)
            {
                SelectedProvinceId = provinceId;
            }

            Changed?.Invoke();
        }

        public void ClickEmptySpace()
        {
            LastResult = CommandResult.Ok;
            IsTargetingMove = false;
            SelectedProvinceId = NoSelection;
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
            SelectedProvinceId = NoSelection;
            Changed?.Invoke();
        }

        public bool IsValidMoveTarget(int provinceId) =>
            IsTargetingMove
            && provinceId != SelectedProvinceId
            && _session.Provinces[SelectedProvinceId].IsAdjacentTo(provinceId);
    }
}
