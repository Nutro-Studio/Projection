using System.Collections.Generic;
using Projection.Core.Map;
using Projection.Core.Units;

namespace Projection.Core.Combat
{
    /// <summary>What the movement system hands over when an army enters a province held by enemy troops.</summary>
    public class CombatContext
    {
        public Province Province { get; set; }
        public Faction Attacker { get; set; }
        public Faction Defender { get; set; }
        public IReadOnlyList<Unit> AttackingUnits { get; set; }
        public IReadOnlyList<Unit> DefendingUnits { get; set; }
        public bool DefenderFortified { get; set; }
    }

    /// <summary>Casualties decided by the combat system. Movement applies them and handles retreat/capture.</summary>
    public class CombatOutcome
    {
        public int AttackerLosses { get; set; }
        public int DefenderLosses { get; set; }

        /// <summary>Free-form description of the dice, shown in the log.</summary>
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Combat contract between movement (economy/systems) and the Root 3d4 combat subsystem.
    /// The real implementation belongs to the combat module; see PlaceholderCombatResolver for a stand-in.
    /// </summary>
    public interface ICombatResolver
    {
        CombatOutcome Resolve(CombatContext context);
    }
}
