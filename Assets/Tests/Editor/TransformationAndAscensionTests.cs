using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using NUnit.Framework;
using Presentation.Prototype;

namespace Tests.Editor
{
    [TestFixture]
    public class TransformationAndAscensionTests
    {
        private class RulesAdapter : IGameRules
        {
            private readonly CrisisDirector _director;
            private readonly IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets;

            public RulesAdapter()
            {
                _rulesets = new Dictionary<FactionType, IFactionRuleset>
                {
                    { FactionType.Militants, new MilitantRuleset() },
                    { FactionType.Zealots, new ZealotRuleset() },
                    { FactionType.Guerrillas, new GuerrillaRuleset() },
                    { FactionType.Guilds, new GuildRuleset() }
                };
                _director = new CrisisDirector(_rulesets);
            }

            public CombatResult ResolveSkirmish(IReadOnlyList<UnitInstance> a, IReadOnlyList<UnitInstance> d,
                FactionType f, bool fort) => throw new System.NotSupportedException();

            public PlayerProfile EvaluatePlayerTurnState(PlayerProfile p, int c) =>
                _director.CanTriggerCrisis(p, c) ? _director.ExecuteForcedCrisis(p).UpdatedProfile : p;

            public int CalculateRevenue(FactionType f, int c) => _rulesets[f].CalculateUpkeepRevenue(c);
            public int RollContest() => 6;
            public bool CanTriggerAscension(PlayerProfile p, int c) => _director.CanTriggerAscension(p, c);
            public bool CanCapitulate(PlayerProfile p) => _director.CanCapitulate(p);

            public bool CanTriggerVoluntaryShift(PlayerProfile p, FactionType t, int c) =>
                _director.CanTriggerVoluntaryShift(p, t, c);

            public CrisisShiftResult ExecuteAscension(PlayerProfile p, FactionType t) =>
                _director.ExecuteAscension(p, t);

            public CrisisShiftResult ExecuteCapitulation(PlayerProfile p) =>
                _director.ExecuteForcedCrisis(p, isCapitulation: true);

            public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile p, FactionType t) =>
                _director.ExecuteVoluntaryShift(p, t);
        }

        [Test]
        public void Capitulate_CollapsesLandedFactionIntoGuerrillas_ZeroesGold_AndPenalizesVp()
        {
            var players = new[]
            {
                new PlayerProfile(1, FactionType.Militants, 14, 20, new List<UnitInstance>()),
                new PlayerProfile(2, FactionType.Zealots, 10, 0, new List<UnitInstance>())
            };
            var provinces = new[]
            {
                new ProvinceState(0, "P0", true, new[] { 1 }, 1,
                    new List<UnitInstance> { new(1, 1, FactionType.Militants, UnitType.RegularMilitary, 1, 1) }),
                new ProvinceState(1, "P1", false, new[] { 0 }, 2,
                    new List<UnitInstance> { new(2, 2, FactionType.Zealots, UnitType.RegularMilitary, 1, 1) })
            };
            var session = new PrototypeGameSession(new RulesAdapter(), players, provinces, crisisEnabled: true);

            Assert.AreEqual(CommandResult.Ok, session.Capitulate());

            var p1 = session.GetPlayer(1);
            Assert.AreEqual(FactionType.Guerrillas, p1.Faction);
            Assert.AreEqual(0, p1.Gold);
            Assert.AreEqual(16, p1.VictoryPoints);
            Assert.IsNotNull(session.Provinces[0].RebellionCell);
            Assert.AreEqual(1, session.Provinces[0].RebellionCell.OwnerId);
        }

        [Test]
        public void VoluntaryShift_MilitantToZealot_RemovesHalfUnitsAndThirtyPercentForts()
        {
            var players = new[] { new PlayerProfile(1, FactionType.Militants, 10, 25, new List<UnitInstance>()) };
            var provinces = new[]
            {
                new ProvinceState(0, "F0", true, new[] { 1 }, 1,
                    new List<UnitInstance>
                    {
                        new(1, 1, FactionType.Militants, UnitType.RegularMilitary, 1, 1),
                        new(2, 1, FactionType.Militants, UnitType.RegularMilitary, 1, 1)
                    }),
                new ProvinceState(1, "F1", true, new[] { 0, 2 }, 1,
                    new List<UnitInstance>
                    {
                        new(3, 1, FactionType.Militants, UnitType.RegularMilitary, 1, 1),
                        new(4, 1, FactionType.Militants, UnitType.RegularMilitary, 1, 1)
                    }),
                new ProvinceState(2, "F2", true, new[] { 1 }, 1, new List<UnitInstance>())
            };
            var session = new PrototypeGameSession(new RulesAdapter(), players, provinces, crisisEnabled: false);

            Assert.AreEqual(CommandResult.Ok, session.VoluntaryShift(FactionType.Zealots));

            var p1 = session.GetPlayer(1);
            Assert.AreEqual(FactionType.Zealots, p1.Faction);
            Assert.AreEqual(20, p1.VictoryPoints); // 25 * 0.8
            Assert.AreEqual(2, session.CountPlayerUnitsOfType(1, UnitType.RegularMilitary)); // 4 -> 2
            int remainingForts = 0;
            for (int i = 0; i < session.Provinces.Count; i++)
            {
                if (session.Provinces[i].HasFort) remainingForts++;
            }

            Assert.AreEqual(2, remainingForts); // 3 forts - 30% (1) = 2 forts
        }

        [Test]
        public void VoluntaryShift_ZealotToGuild_RemovesAllMissionariesAndHalfChurches()
        {
            var players = new[] { new PlayerProfile(1, FactionType.Zealots, 10, 30, new List<UnitInstance>()) };
            var provinces = new[]
            {
                new ProvinceState(0, "C0", false, new[] { 1 }, 1,
                    new List<UnitInstance> { new(1, 1, FactionType.Zealots, UnitType.Missionary, 1, 1) },
                    churchOwnerId: 1),
                new ProvinceState(1, "C1", false, new[] { 0 }, 1,
                    new List<UnitInstance> { new(2, 1, FactionType.Zealots, UnitType.Missionary, 1, 1) },
                    churchOwnerId: 1)
            };
            var session = new PrototypeGameSession(new RulesAdapter(), players, provinces, crisisEnabled: false);

            Assert.AreEqual(CommandResult.Ok, session.VoluntaryShift(FactionType.Guilds));

            Assert.AreEqual(FactionType.Guilds, session.GetPlayer(1).Faction);
            Assert.AreEqual(24, session.GetPlayer(1).VictoryPoints);
            Assert.AreEqual(0, session.CountPlayerUnitsOfType(1, UnitType.Missionary));
            int churches = 0;
            for (int i = 0; i < session.Provinces.Count; i++)
            {
                if (session.Provinces[i].HasChurch) churches++;
            }

            Assert.AreEqual(1, churches);
        }

        [Test]
        public void GuerrillaAscension_AtFiveRebelProvinces_GrantsTenPercentVpAndConvertsToFormalState()
        {
            var players = new[]
            {
                new PlayerProfile(1, FactionType.Guerrillas, 10, 30, new List<UnitInstance>()),
                new PlayerProfile(2, FactionType.Militants, 10, 0, new List<UnitInstance>())
            };

            var provinces = new ProvinceState[5];
            for (int i = 0; i < 5; i++)
            {
                provinces[i] = new ProvinceState(
                    i,
                    $"Rebel_{i}",
                    false,
                    i == 0 ? new[] { 1 } : new[] { i - 1 },
                    ProvinceState.NeutralOwner,
                    new List<UnitInstance>(),
                    rebellionCell: new RebellionCell(1, isOpenRebellion: true)
                );
            }

            var session = new PrototypeGameSession(new RulesAdapter(), players, provinces, crisisEnabled: true);

            Assert.IsTrue(session.CanAscend());
            Assert.AreEqual(CommandResult.Ok, session.Ascend(FactionType.Militants));

            var p1 = session.GetPlayer(1);
            Assert.AreEqual(FactionType.Militants, p1.Faction);
            Assert.AreEqual(33, p1.VictoryPoints); // 30 * 1.10
            for (int i = 0; i < 5; i++)
            {
                Assert.IsNull(session.Provinces[i].RebellionCell);
                Assert.AreEqual(1, session.Provinces[i].OwnerId);
                Assert.AreEqual(1, session.GetUnitsForPlayer(i, 1, UnitType.RegularMilitary).Count);
            }
        }
    }
}