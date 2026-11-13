using System;
using System.Collections.Generic;

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

            // 1. Roll 3d4 for both sides (Range: 3-12)
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

            // 4. Resolve Fortification Armor (+1 HP)
            bool fortArmorActive = context.DefenderHasFort && context.DefenderReceivesFortBonus;
            var (survivingDefenders, defenderCasualties) = ApplyHitsToArmy(
                context.Defenders,
                attackerEffectiveHits,
                hasFortArmor: fortArmorActive
            );

            var (survivingAttackers, attackerCasualties) = ApplyHitsToArmy(
                context.Attackers,
                defenderEffectiveHits,
                hasFortArmor: false
            );

            // 5. Casualties & Retreat: higher net losses retreats; defender wins ties
            bool attackerRetreats = attackerCasualties >= defenderCasualties;
            bool defenderRetreats = defenderCasualties > attackerCasualties;

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
                DefenderMustRetreat: defenderRetreats,
                AttackerRolls: (a1, a2, a3),
                DefenderRolls: (d1, d2, d3)
            );
        }

        private static (List<CombatUnit> SurvivingUnits, int Casualties) ApplyHitsToArmy(
            IReadOnlyList<CombatUnit> units,
            int incomingHits,
            bool hasFortArmor)
        {
            int count = units.Count;
            int hitsLeft = incomingHits;
            int casualties = 0;

            var surviving = new List<CombatUnit>(count);

            for (int i = 0; i < count; i++)
            {
                var unit = units[i];
                int maxHp = hasFortArmor ? 2 : unit.MaxHp;
                int currentHp = hasFortArmor ? 2 : unit.CurrentHp;

                if (hitsLeft > 0)
                {
                    int damageApplied = Math.Min(currentHp, hitsLeft);
                    currentHp -= damageApplied;
                    hitsLeft -= damageApplied;
                }

                if (currentHp > 0)
                {
                    surviving.Add(unit with { MaxHp = maxHp, CurrentHp = currentHp });
                }
                else
                {
                    casualties++;
                }
            }

            return (surviving, casualties);
        }
    }
}