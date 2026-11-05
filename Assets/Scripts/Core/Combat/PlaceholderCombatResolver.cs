using System;

namespace Projection.Core.Combat
{
    /// <summary>
    /// TEMPORARY stand-in so movement can be played end to end before the real Root 3d4 module lands.
    /// Follows the rulebook in the simplest way: both roll 3d4, attacker deals the higher sum, defender the lower,
    /// each capped by own unit count; fortified defenders have 2 HP. Replace with the combat module's resolver.
    /// </summary>
    public class PlaceholderCombatResolver : ICombatResolver
    {
        private readonly Func<int, int> _rollDie;

        /// <param name="rollDie">Returns a value in [1, sides]. Defaults to System.Random.</param>
        public PlaceholderCombatResolver(Func<int, int> rollDie = null)
        {
            var random = new Random();
            _rollDie = rollDie ?? (sides => random.Next(1, sides + 1));
        }

        public CombatOutcome Resolve(CombatContext context)
        {
            int sumA = Roll3d4();
            int sumB = Roll3d4();

            int attackerHits = Math.Min(Math.Max(sumA, sumB), context.AttackingUnits.Count);
            int defenderHits = Math.Min(Math.Min(sumA, sumB), context.DefendingUnits.Count);

            int defenderHp = context.DefenderFortified ? 2 : 1;

            return new CombatOutcome
            {
                DefenderLosses = Math.Min(attackerHits / defenderHp, context.DefendingUnits.Count),
                AttackerLosses = Math.Min(defenderHits, context.AttackingUnits.Count),
                Description = $"3d4: attacker {sumA} vs defender {sumB}; hits {attackerHits}/{defenderHits}" +
                              (context.DefenderFortified ? " (fort: 2 HP)" : "")
            };
        }

        private int Roll3d4() => _rollDie(4) + _rollDie(4) + _rollDie(4);
    }
}
