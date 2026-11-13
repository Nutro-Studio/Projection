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

            public int RollD4() => 0;

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
            var list = new List<CombatUnit>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new CombatUnit(i, faction, 1, 1));
            }

            return list;
        }

        [Test]
        public void SystemRandomDiceRoller_ProducesValuesInZeroToThreeRange()
        {
            var roller = new SystemRandomDiceRoller();

            for (int i = 0; i < 500; i++)
            {
                int single = roller.RollD4();
                Assert.That(single, Is.InRange(0, 3));

                var (r1, r2, r3) = roller.Roll3D4();
                Assert.That(r1, Is.InRange(0, 3));
                Assert.That(r2, Is.InRange(0, 3));
                Assert.That(r3, Is.InRange(0, 3));
                Assert.That(r1 + r2 + r3, Is.InRange(0, 9));
            }
        }

        [Test]
        public void ResolveBattle_AppliesRootCap_WhenMaxRollExceedsUnitCount()
        {
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (3, 3, 3), // Max Sum = 9
                DefenderRoll = (0, 0, 0)  // Min Sum = 0
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Militants, 2);
            var defenders = CreateArmy(FactionType.Zealots, 10);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: false,
                DefenderReceivesFortBonus: false);
            var result = combatService.ResolveBattle(context);

            Assert.AreEqual(9, result.AttackerAssignedRoll);
            Assert.AreEqual(0, result.DefenderAssignedRoll);
            Assert.AreEqual(2, result.AttackerEffectiveHits);
            Assert.AreEqual(0, result.DefenderEffectiveHits);
            Assert.AreEqual(8, result.SurvivingDefenders.Count);
            Assert.AreEqual(2, result.SurvivingAttackers.Count);
        }

        [Test]
        public void ResolveBattle_NullRolls_DealZeroDamageAndDefenderHolds()
        {
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (0, 0, 0), // Sum = 0
                DefenderRoll = (0, 0, 0)  // Sum = 0
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Militants, 3);
            var defenders = CreateArmy(FactionType.Zealots, 3);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: false,
                DefenderReceivesFortBonus: false);
            var result = combatService.ResolveBattle(context);

            Assert.AreEqual(0, result.AttackerEffectiveHits);
            Assert.AreEqual(0, result.DefenderEffectiveHits);
            Assert.AreEqual(3, result.SurvivingAttackers.Count);
            Assert.AreEqual(3, result.SurvivingDefenders.Count);
            Assert.IsTrue(result.AttackerMustRetreat);
            Assert.IsFalse(result.DefenderMustRetreat);
        }

        [Test]
        public void ResolveBattle_DefenderReceivingFortBonus_TakesTwoHitsToKill()
        {
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (3, 3, 3), // Sum = 9
                DefenderRoll = (0, 0, 0)  // Sum = 0
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Zealots, 1);
            var defenders = CreateArmy(FactionType.Militants, 1);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: true,
                DefenderReceivesFortBonus: true);
            var result = combatService.ResolveBattle(context);

            Assert.AreEqual(1, result.AttackerEffectiveHits);
            Assert.AreEqual(1, result.SurvivingDefenders.Count);
            Assert.AreEqual(1, result.SurvivingDefenders[0].CurrentHp);
        }

        [Test]
        public void ResolveBattle_TiesAwardedToDefender_AttackerMustRetreat()
        {
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (2, 1, 0), // Sum = 3
                DefenderRoll = (1, 1, 1)  // Sum = 3
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Militants, 2);
            var defenders = CreateArmy(FactionType.Zealots, 2);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: false,
                DefenderReceivesFortBonus: false);
            var result = combatService.ResolveBattle(context);

            Assert.IsTrue(result.AttackerMustRetreat);
            Assert.IsFalse(result.DefenderMustRetreat);
        }
    }
}