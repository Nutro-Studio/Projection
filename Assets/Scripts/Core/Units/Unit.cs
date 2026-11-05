using Projection.Core.Map;

namespace Projection.Core.Units
{
    public enum UnitType
    {
        Military,
        Missionary,
        Trader,
        RebellionCell
    }

    public class Unit
    {
        public int Id { get; }
        public UnitType Type { get; }
        public Faction Owner { get; set; }
        public Province Location { get; set; }
        public bool HasMovedThisTurn { get; set; }

        public bool IsCombatant => Type == UnitType.Military;

        public Unit(int id, UnitType type, Faction owner, Province location)
        {
            Id = id;
            Type = type;
            Owner = owner;
            Location = location;
        }

        public override string ToString() => $"{Owner} {Type} #{Id} @ {Location}";
    }
}
