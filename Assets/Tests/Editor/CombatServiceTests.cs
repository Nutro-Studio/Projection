using System.Collections.Generic;
using Core.Combat;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
    public class CombatServiceTests
    {
        private class MockDiceRoller : IDiceRoller
        {
            public (int, int, int) AttackerRoll { get; set; }
            public (int, int, int) DefenderRoll { get; set; }

            private bool _isAttackerNext = true;

            public int RollD4() => 1;

            public (int, int, int) Roll3D4()
            {
                if (_isAttackerNext)
                {
                    _isAttackerNext = false;
                    return AttackerRoll;
                }

                _isAttackerNext = true;
                return DefenderRoll;
            }
        }

        private static List<CombatUnit> CreateArmy(FactionType faction, int count)
        {
            var list = new List<CombatUnit>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new CombatUnit(i, faction, 1, 1));
            }

            return list;
        }

        [Test]
        public void ResolveBattle_AppliesRootCap_WhenRollExceedsUnitCount()
        {
            // 2 units rolling a sum of 10 must only deal 2 hits
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (4, 3, 3), // Sum = 10
                DefenderRoll = (1, 1, 1) // Sum = 3
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Militants, 2);
            var defenders = CreateArmy(FactionType.Zealots, 5);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: false,
                DefenderFaction: FactionType.Zealots);
            var result = combatService.ResolveBattle(context);

            Assert.AreEqual(10, result.AttackerAssignedRoll);
            Assert.AreEqual(2, result.AttackerEffectiveHits, "Hits dealt must be capped by surviving attackers count.");
            Assert.AreEqual(3, result.SurvivingDefenders.Count);
        }

        [Test]
        public void ResolveBattle_MilitantFortGivesPlusOneHp_RequiresTwoHitsToKill()
        {
            // Defender has 1 Militant unit in a Fort (Effective 2 HP)
            // Attacker deals 1 hit -> Unit should take damage but survive
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (3, 3, 3), // Sum = 9 -> Attacker takes higher
                DefenderRoll = (1, 1, 1) // Sum = 3
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Zealots, 1);
            var defenders = CreateArmy(FactionType.Militants, 1);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: true,
                DefenderFaction: FactionType.Militants);
            var result = combatService.ResolveBattle(context);

            Assert.AreEqual(1, result.AttackerEffectiveHits);
            Assert.AreEqual(1, result.SurvivingDefenders.Count,
                "Fortified Militant should survive 1 hit with 1 HP left.");
            Assert.AreEqual(1, result.SurvivingDefenders[0].CurrentHp);
        }

        [Test]
        public void ResolveBattle_TiesAwardedToDefender_AttackerMustRetreat()
        {
            // Both sides suffer 1 casualty -> Tie goes to defender, attacker retreats
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (2, 2, 2), // Sum = 6
                DefenderRoll = (2, 2, 2) // Sum = 6
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Militants, 2);
            var defenders = CreateArmy(FactionType.Zealots, 2);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: false,
                DefenderFaction: FactionType.Zealots);
            var result = combatService.ResolveBattle(context);

            Assert.IsTrue(result.AttackerMustRetreat, "Defender wins tiebreakers; attacker must retreat.");
            Assert.IsFalse(result.DefenderMustRetreat);
        }
    }
}