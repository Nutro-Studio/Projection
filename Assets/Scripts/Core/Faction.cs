using Core.Combat;
using Core.Crisis;
using Core.Economy;

namespace Core
{
    /// <summary>A player's faction: mutable runtime state (treasury, VP) plus the ruleset it currently plays by.</summary>
    public class Faction
    {
        public int Id { get; }
        public string Name { get; }

        // Not readonly: Ruleset Shift swaps the ruleset at runtime.
        public IFactionRuleset Ruleset { get; set; }

        public Treasury Treasury { get; }
        public int VictoryPoints { get; set; }

        public FactionType Archetype => Ruleset.Archetype;
        public bool IsLanded => Ruleset.IsLanded;

        public Faction(int id, string name, IFactionRuleset ruleset, int startingGold)
        {
            Id = id;
            Name = name;
            Ruleset = ruleset;
            Treasury = new Treasury(startingGold);
        }

        public override string ToString() => Name;
    }
}
