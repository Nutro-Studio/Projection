using Projection.Core.Economy;

namespace Projection.Core
{
    public enum FactionArchetype
    {
        Militants,
        Zealots,
        Guilds,
        Guerrillas
    }

    public class Faction
    {
        public int Id { get; }
        public string Name { get; }

        // Not readonly: Ruleset Shift changes the archetype at runtime.
        public FactionArchetype Archetype { get; set; }

        public Treasury Treasury { get; }
        public int VictoryPoints { get; set; }

        public bool IsLanded => Archetype == FactionArchetype.Militants || Archetype == FactionArchetype.Zealots;

        public Faction(int id, string name, FactionArchetype archetype, int startingGold)
        {
            Id = id;
            Name = name;
            Archetype = archetype;
            Treasury = new Treasury(startingGold);
        }

        public override string ToString() => Name;
    }
}
