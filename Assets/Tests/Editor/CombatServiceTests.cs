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
            var list = new List<CombatUnit>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new CombatUnit(i, faction, 1, 1));
            }

            return list;
        }

        [Test]
        public void ResolveBattle_AppliesRootCap_WhenRollExceedsUnitCount()
        {
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (4, 3, 3), // Sum = 10
                DefenderRoll = (1, 1, 1) // Sum = 3
            };
            var combatService = new RootCombatService(mockDice);
            var attackers = CreateArmy(FactionType.Militants, 2);
            var defenders = CreateArmy(FactionType.Zealots, 5);

            var context = new CombatContext(attackers, defenders, DefenderHasFort: false,
                DefenderReceivesFortBonus: false);
            var result = combatService.ResolveBattle(context);

            Assert.AreEqual(10, result.AttackerAssignedRoll);
            Assert.AreEqual(2, result.AttackerEffectiveHits);
            Assert.AreEqual(3, result.SurvivingDefenders.Count);
        }

        [Test]
        public void ResolveBattle_DefenderReceivingFortBonus_TakesTwoHitsToKill()
        {
            var mockDice = new MockDiceRoller
            {
                AttackerRoll = (3, 3, 3), // Sum = 9
                DefenderRoll = (1, 1, 1) // Sum = 3
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
                AttackerRoll = (2, 2, 2),
                DefenderRoll = (2, 2, 2)
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