using System;
using System.Collections.Generic;
using System.Linq;
using Core.Combat;

namespace Core.Combat
{
    public class RootCombatService : ICombatService
    {
        private readonly IDiceRoller _diceRoller;

        public RootCombatService(IDiceRoller diceRoller)
        {
            _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
        }

        public CombatResult ResolveBattle(CombatContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Attackers.Count == 0 || context.Defenders.Count == 0)
            {
                throw new InvalidOperationException("Combat requires at least one unit on both sides.");
            }

            // 1. Roll 3d4 for both sides (Range 3-12)
            var (a1, a2, a3) = _diceRoller.Roll3D4();
            var (d1, d2, d3) = _diceRoller.Roll3D4();
            int rollSumA = a1 + a2 + a3;
            int rollSumB = d1 + d2 + d3;

            // 2. Hit Allocation: Higher sum -> Attacker, Lower sum -> Defender
            int attackerAssigned = Math.Max(rollSumA, rollSumB);
            int defenderAssigned = Math.Min(rollSumA, rollSumB);

            // 3. Root Cap Rule: Hits cannot exceed surviving unit count
            int attackerEffectiveHits = Math.Min(attackerAssigned, context.Attackers.Count);
            int defenderEffectiveHits = Math.Min(defenderAssigned, context.Defenders.Count);

            // 4. Apply damage to defenders
            // Fortification Armor (+1 HP): Militants defending in Forts take 2 hits per unit
            bool militantFortArmor = context.DefenderHasFort && context.DefenderFaction == FactionType.Militants;
            var (survivingDefenders, defenderCasualties) =
                ApplyHitsToArmy(context.Defenders, attackerEffectiveHits, militantFortArmor);

            // Apply damage to attackers (no fort benefit on offense)
            var (survivingAttackers, attackerCasualties) =
                ApplyHitsToArmy(context.Attackers, defenderEffectiveHits, hasFortArmor: false);

            // 5. Casualties & Retreat Rule
            // Higher net unit losses retreats. Defender wins ties.
            bool attackerRetreats = false;
            bool defenderRetreats = false;

            if (attackerCasualties > defenderCasualties)
            {
                attackerRetreats = true;
            }
            else if (defenderCasualties > attackerCasualties)
            {
                defenderRetreats = true;
            }
            else
            {
                // Tie goes to defender -> Attacker must retreat if no progress made
                attackerRetreats = true;
            }

            return new CombatResult(
                AttackerRawSum: rollSumA,
                DefenderRawSum: rollSumB,
                AttackerAssignedRoll: attackerAssigned,
                DefenderAssignedRoll: defenderAssigned,
                AttackerEffectiveHits: attackerEffectiveHits,
                DefenderEffectiveHits: defenderEffectiveHits,
                SurvivingAttackers: survivingAttackers,
                SurvivingDefenders: survivingDefenders,
                AttackerMustRetreat: attackerRetreats,
                DefenderMustRetreat: defenderRetreats
            );
        }

        private static (List<CombatUnit> SurvivingUnits, int Casualties) ApplyHitsToArmy(
            IReadOnlyList<CombatUnit> units,
            int incomingHits,
            bool hasFortArmor)
        {
            var armyState = units.Select(u => new CombatUnit(
                u.Id,
                u.Faction,
                hasFortArmor ? 2 : u.MaxHp,
                hasFortArmor ? 2 : u.CurrentHp
            )).ToList();

            int hitsLeft = incomingHits;
            int initialCount = armyState.Count;

            for (int i = 0; i < armyState.Count && hitsLeft > 0; i++)
            {
                var unit = armyState[i];
                int damageApplied = Math.Min(unit.CurrentHp, hitsLeft);
                int remainingHp = unit.CurrentHp - damageApplied;
                hitsLeft -= damageApplied;

                armyState[i] = unit with { CurrentHp = remainingHp };
            }

            var surviving = armyState.Where(u => u.CurrentHp > 0).ToList();
            int casualties = initialCount - surviving.Count;
            return (surviving, casualties);
        }
    }
}