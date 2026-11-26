using System;
using System.Collections.Generic;
using Core.Combat;
using Core.Simulation;
using Core.Actions;

namespace Presentation.Hud
{
    public enum TargetingMode
    {
        None,
        Move,
        Caravan
    }

    /// <summary>
    /// Selection and targeting state shared by the map input and the HUD buttons.
    /// </summary>
    public sealed class HudInteraction
    {
        public const int NoSelection = -1;

        private readonly GameSession _session;
        private readonly List<int> _caravanPath = new();
        private int _unitsToMove = int.MaxValue;

        public event Action Changed;

        public int SelectedProvinceId { get; private set; } = NoSelection;
        public TargetingMode Targeting { get; private set; } = TargetingMode.None;
        public UnitType? MoveUnitTypeFilter { get; private set; }
        public ActionResult LastResult { get; private set; } = ActionResult.Ok;

        public bool IsTargetingMove => Targeting == TargetingMode.Move;
        public bool IsTargetingCaravan => Targeting == TargetingMode.Caravan;
        public IReadOnlyList<int> CaravanPath => _caravanPath;

        public int MovableUnitCount =>
            SelectedProvinceId == NoSelection ? 0 : GetFilteredMovableUnitIds(SelectedProvinceId).Count;

        /// <summary>
        /// How many of the selected province's filtered movable units the next move takes. Defaults to all of them.
        /// </summary>
        public int UnitsToMove => Math.Clamp(_unitsToMove, Math.Min(1, MovableUnitCount), MovableUnitCount);

        public HudInteraction(GameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void ClickProvince(int provinceId)
        {
            LastResult = ActionResult.Ok;

            if (Targeting == TargetingMode.None)
            {
                Select(provinceId);
                Changed?.Invoke();
                return;
            }

            if (Targeting == TargetingMode.Caravan)
            {
                HandleCaravanWaypointClick(provinceId);
                Changed?.Invoke();
                return;
            }

            int origin = SelectedProvinceId;
            Targeting = TargetingMode.None;

            if (provinceId == origin)
            {
                Changed?.Invoke();
                return;
            }

            LastResult = _session.MoveArmy(new MoveUnitsCommand(origin, provinceId, TakeUnitsToMove(origin)));
            if (LastResult == ActionResult.Ok)
            {
                Select(provinceId);
            }

            Changed?.Invoke();
        }

        private void HandleCaravanWaypointClick(int provinceId)
        {
            int lastNode = _caravanPath[_caravanPath.Count - 1];
            if (provinceId == lastNode && _caravanPath.Count >= 2)
            {
                ExecuteCaravan();
                return;
            }

            if (!_caravanPath.Contains(provinceId) && _session.Provinces[lastNode].IsAdjacentTo(provinceId))
            {
                _caravanPath.Add(provinceId);
                return;
            }

            LastResult = ActionResult.NotAdjacent;
        }

        public void BeginCaravan()
        {
            if (SelectedProvinceId == NoSelection) return;

            _caravanPath.Clear();
            _caravanPath.Add(SelectedProvinceId);
            Targeting = TargetingMode.Caravan;
            Changed?.Invoke();
        }

        public void ExecuteCaravan()
        {
            if (_caravanPath.Count < 2)
            {
                CancelTargeting();
                return;
            }

            int destination = _caravanPath[_caravanPath.Count - 1];
            LastResult = _session.RunCaravan(_caravanPath);
            _caravanPath.Clear();
            Targeting = TargetingMode.None;
            if (LastResult == ActionResult.Ok)
            {
                Select(destination);
            }

            Changed?.Invoke();
        }

        public void BuildTradingPost()
        {
            if (SelectedProvinceId == NoSelection) return;
            LastResult = _session.BuildTradingPost(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void HostileBuyout()
        {
            if (SelectedProvinceId == NoSelection) return;
            LastResult = _session.HostileBuyout(SelectedProvinceId);
            Changed?.Invoke();
        }

        public void DeclareEmbargo(int guildPlayerId)
        {
            if (SelectedProvinceId == NoSelection) return;
            LastResult = _session.DeclareEmbargo(guildPlayerId);
            Changed?.Invoke();
        }

        public void RaidCaravan(int guildPlayerId)
        {
            if (SelectedProvinceId == NoSelection) return;
            LastResult = _session.RaidCaravan(SelectedProvinceId, guildPlayerId);
            Changed?.Invoke();
        }

        public void SellMercenaryContract(int buyerPlayerId)
        {
            if (SelectedProvinceId == NoSelection) return;
            LastResult = _session.SellMercenaryContract(SelectedProvinceId, buyerPlayerId);
            Changed?.Invoke();
        }

        public void ClickEmptySpace()
        {
            LastResult = ActionResult.Ok;
            Targeting = TargetingMode.None;
            _caravanPath.Clear();
            Select(NoSelection);
            Changed?.Invoke();
        }

        public void CancelTargeting()
        {
            if (Targeting == TargetingMode.None) return;

            Targeting = TargetingMode.None;
            _caravanPath.Clear();
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
            if (LastResult == ActionResult.Ok && MovableUnitCount == 0)
            {
                LastResult = ActionResult.NoUnitsSelected;
            }

            Targeting = LastResult == ActionResult.Ok ? TargetingMode.Move : TargetingMode.None;
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

        public void BuildFort()
        {
            if (SelectedProvinceId == NoSelection) return;

            LastResult = _session.BuildFort(SelectedProvinceId);
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

        public void DivineConversion(int missionaryCount = 1)
        {
            if (SelectedProvinceId == NoSelection) return;

            Targeting = TargetingMode.None;
            LastResult = _session.DivineConversion(SelectedProvinceId, missionaryCount);
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
            _caravanPath.Clear();
            Select(NoSelection);
            Changed?.Invoke();
        }

        public bool IsValidMoveTarget(int provinceId)
        {
            if (Targeting == TargetingMode.Move)
            {
                return provinceId != SelectedProvinceId
                       && _session.Provinces[SelectedProvinceId].IsAdjacentTo(provinceId);
            }

            if (Targeting == TargetingMode.Caravan && _caravanPath.Count > 0)
            {
                int tail = _caravanPath[_caravanPath.Count - 1];
                return !_caravanPath.Contains(provinceId) && _session.Provinces[tail].IsAdjacentTo(provinceId);
            }

            return false;
        }

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

        public void Capitulate()
        {
            Targeting = TargetingMode.None;
            _caravanPath.Clear();
            LastResult = _session.Capitulate();
            Changed?.Invoke();
        }
    }
}