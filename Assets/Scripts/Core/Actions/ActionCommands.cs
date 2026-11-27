using System.Collections.Generic;

namespace Core.Actions
{
    public sealed class MoveUnitsCommand
    {
        public int OriginProvinceId { get; }
        public int TargetProvinceId { get; }
        public IReadOnlyList<int> SelectedUnitIds { get; }

        public MoveUnitsCommand(int originProvinceId, int targetProvinceId, IReadOnlyList<int> selectedUnitIds)
        {
            OriginProvinceId = originProvinceId;
            TargetProvinceId = targetProvinceId;
            SelectedUnitIds = selectedUnitIds;
        }
    }
}