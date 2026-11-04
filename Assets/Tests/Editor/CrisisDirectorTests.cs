using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
    public class CrisisDirectorTests
    {
        private CrisisDirector _director;

        [SetUp]
        public void Setup()
        {
            var rulesets = new Dictionary<FactionType, IFactionRuleset>
            {
                { FactionType.Militants, new MilitantRuleset() },
                { FactionType.Zealots, new ZealotRuleset() },
                { FactionType.Guerrillas, new GuerrillaRuleset() },
                { FactionType.Guilds, new GuildRuleset() }
            };
            _director = new CrisisDirector(rulesets);
        }

        [Test]
        public void CanTriggerCrisis_ReturnsTrue_WhenLandedFactionDropsBelowThreeProvinces()
        {
            var player = new PlayerProfile(1, FactionType.Militants, Gold: 10, VictoryPoints: 20,
                new List<CombatUnit>());

            Assert.IsTrue(_director.CanTriggerCrisis(player, controlledProvinces: 2));
            Assert.IsFalse(_director.CanTriggerCrisis(player, controlledProvinces: 3));
        }

        [Test]
        public void CanTriggerCrisis_ReturnsFalse_ForNonLandedFaction()
        {
            var player = new PlayerProfile(1, FactionType.Guerrillas, Gold: 0, VictoryPoints: 10,
                new List<CombatUnit>());

            Assert.IsFalse(_director.CanTriggerCrisis(player, controlledProvinces: 1));
        }

        [Test]
        public void ExecuteForcedCrisis_AppliesTwentyPercentVpPenaltyAndZeroesGold()
        {
            var startingUnits = new List<CombatUnit>
            {
                new(1, FactionType.Zealots, 1, 1),
                new(2, FactionType.Zealots, 1, 1)
            };
            var player = new PlayerProfile(1, FactionType.Zealots, Gold: 15, VictoryPoints: 25, startingUnits);

            var result = _director.ExecuteForcedCrisis(player);

            // 25 * 0.8 = 20 VP
            Assert.AreEqual(20, result.UpdatedProfile.VictoryPoints);
            Assert.AreEqual(0, result.UpdatedProfile.Gold);
            Assert.AreEqual(FactionType.Guerrillas, result.UpdatedProfile.Faction);
            Assert.AreEqual(FactionType.Guerrillas, result.UpdatedProfile.Units[0].Faction);
            Assert.IsTrue(result.IsCrisis);
        }

        [Test]
        public void ExecuteAscension_AppliesTenPercentBonusAndTransitionsToTargetFaction()
        {
            var insurgentUnits = new List<CombatUnit>
            {
                new(1, FactionType.Guerrillas, 1, 1)
            };
            var player = new PlayerProfile(1, FactionType.Guerrillas, Gold: 8, VictoryPoints: 30, insurgentUnits);

            Assert.IsTrue(_director.CanTriggerAscension(player, controlledProvinces: 5));

            var result = _director.ExecuteAscension(player, FactionType.Militants);

            // 30 * 1.1 = 33 VP
            Assert.AreEqual(33, result.UpdatedProfile.VictoryPoints);
            Assert.AreEqual(FactionType.Militants, result.UpdatedProfile.Faction);
            Assert.AreEqual(FactionType.Militants, result.UpdatedProfile.Units[0].Faction);
            Assert.IsTrue(result.IsAscension);
        }
    }
}