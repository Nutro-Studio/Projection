using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using NUnit.Framework;
using Presentation.Prototype;

namespace Tests.Editor
{
    [TestFixture]
    public class GuildRulesetTests
    {
        private const int GuildPlayer = 1;
        private const int MilitantPlayer = 2;
        private const int ZealotPlayer = 3;

        private class DummyRules : IGameRules
        {
            private readonly IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets =
                new Dictionary<FactionType, IFactionRuleset>
                {
                    { FactionType.Militants, new MilitantRuleset() },
                    { FactionType.Zealots, new ZealotRuleset() },
                    { FactionType.Guerrillas, new GuerrillaRuleset() },
                    { FactionType.Guilds, new GuildRuleset() }
                };

            public CombatResult ResolveSkirmish(IReadOnlyList<UnitInstance> a, IReadOnlyList<UnitInstance> d,
                FactionType f, bool fort) => throw new System.NotSupportedException();

            public PlayerProfile EvaluatePlayerTurnState(PlayerProfile p, int c) => p;
            public int CalculateRevenue(FactionType f, int c) => _rulesets[f].CalculateUpkeepRevenue(c);
            public int RollContest() => 6;
            public bool CanTriggerAscension(PlayerProfile p, int c) => false;
            public bool CanTriggerVoluntaryShift(PlayerProfile p, FactionType t, int c) => false;

            public CrisisShiftResult ExecuteAscension(PlayerProfile p, FactionType t) =>
                throw new System.NotSupportedException();

            public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile p, FactionType t) =>
                throw new System.NotSupportedException();
        }

        private static PrototypeGameSession CreateGuildSession(int startingGold = 15, bool targetFortified = false)
        {
            var players = new[]
            {
                new PlayerProfile(GuildPlayer, FactionType.Guilds, startingGold, 0, new List<UnitInstance>()),
                new PlayerProfile(MilitantPlayer, FactionType.Militants, 10, 0, new List<UnitInstance>()),
                new PlayerProfile(ZealotPlayer, FactionType.Zealots, 10, 0, new List<UnitInstance>())
            };

            // Linear chain: 0 (Neutral, Guild Trader + Trading Post) - 1 (Militant) - 2 (Zealot) - 3 (Neutral, Trading Post)
            var provinces = new[]
            {
                new ProvinceState(0, "Freeport", false, new[] { 1 }, ProvinceState.NeutralOwner,
                    new List<UnitInstance> { new(1, GuildPlayer, FactionType.Guilds, UnitType.Trader, 1, 1) },
                    tradingPostOwnerId: GuildPlayer),
                new ProvinceState(1, "Ironmarch", targetFortified, new[] { 0, 2 }, MilitantPlayer,
                    new List<UnitInstance>
                        { new(2, MilitantPlayer, FactionType.Militants, UnitType.RegularMilitary, 1, 1) }),
                new ProvinceState(2, "Sanctum", false, new[] { 1, 3 }, ZealotPlayer,
                    new List<UnitInstance>(), churchOwnerId: ZealotPlayer),
                new ProvinceState(3, "Outpost", false, new[] { 2 }, ProvinceState.NeutralOwner,
                    new List<UnitInstance>(), tradingPostOwnerId: GuildPlayer)
            };

            return new PrototypeGameSession(new DummyRules(), players, provinces, crisisEnabled: false);
        }

        [Test]
        public void Guilds_AreNonLanded_AndCannotRecruitMilitaryUnits()
        {
            Assert.IsFalse(new GuildRuleset().IsLanded);
            var sessionOwn = new PrototypeGameSession(
                new DummyRules(),
                new[] { new PlayerProfile(GuildPlayer, FactionType.Guilds, 10, 0, new List<UnitInstance>()) },
                new[]
                {
                    new ProvinceState(0, "Cap", false, System.Array.Empty<int>(), GuildPlayer, new List<UnitInstance>())
                },
                false);

            Assert.AreEqual(CommandResult.WrongFaction, sessionOwn.BuyUnit(0));
        }

        [Test]
        public void BuildTradingPost_GrantsFreeTrader_AndEnforcesTraderCap()
        {
            var session = CreateGuildSession(startingGold: 15);
            // Move Trader from 0 to 1, then build Trading Post at 1 (costs 4G, spawns +1 Trader)
            session.MoveArmy(new MoveArmyCommand(0, 1, new[] { 1 }));
            Assert.AreEqual(CommandResult.Ok, session.BuildTradingPost(1));

            Assert.AreEqual(11, session.CurrentPlayer.Gold);
            Assert.AreEqual(GuildPlayer, session.Provinces[1].TradingPostOwnerId);
            Assert.AreEqual(2, session.CountPlayerUnitsOfType(GuildPlayer, UnitType.Trader));
            Assert.AreEqual(3, session.CountOwnedTradingPosts(GuildPlayer));

            // Can recruit 1 more Trader (2G) to reach cap of 3 Traders for 3 Trading Posts
            Assert.AreEqual(CommandResult.Ok, session.BuyTrader(1));
            Assert.AreEqual(9, session.CurrentPlayer.Gold);
            Assert.AreEqual(CommandResult.TraderCapReached, session.BuyTrader(1));
        }

        [Test]
        public void RunCaravan_BetweenTradingPosts_PaysTransitTolls_GainsScalingGold_AndMovesTrader()
        {
            var session = CreateGuildSession(startingGold: 10);

            // Path: 0 (Trading Post) -> 1 (Militant: 1G toll) -> 2 (Zealot: 1G toll) -> 3 (Trading Post, Neutral: 0G toll)
            // Traversed = 3 provinces -> +6G revenue, -2G tolls = +4G net (10 -> 14G)
            var result = session.RunCaravan(new[] { 0, 1, 2, 3 });

            Assert.AreEqual(CommandResult.Ok, result);
            Assert.AreEqual(14, session.CurrentPlayer.Gold);
            Assert.AreEqual(11, session.GetPlayer(MilitantPlayer).Gold);
            Assert.AreEqual(11, session.GetPlayer(ZealotPlayer).Gold);
            Assert.AreEqual(0, session.Provinces[0].Units.Count);
            Assert.AreEqual(1, session.Provinces[3].Units.Count);
            Assert.IsTrue(session.Provinces[3].Units[0].HasActed);
        }

        [Test]
        public void RunCaravan_Rejected_WhenEndpointsLackTradingPosts()
        {
            var session = CreateGuildSession(startingGold: 10);
            Assert.AreEqual(CommandResult.InvalidCaravanPath, session.RunCaravan(new[] { 0, 1, 2 }));
        }

        [Test]
        public void HostileBuyout_PurchasesUnfortifiedProvince_PlacesTradingPost_AndRetreatsGarrison()
        {
            var session = CreateGuildSession(startingGold: 20, targetFortified: false);
            session.MoveArmy(new MoveArmyCommand(0, 1, new[] { 1 }));

            int expectedCost = session.GetHostileBuyoutCost(1); // 6 base + 2 for 1 military unit = 8G
            Assert.AreEqual(8, expectedCost);

            Assert.AreEqual(CommandResult.Ok, session.HostileBuyout(1));
            Assert.AreEqual(GuildPlayer, session.Provinces[1].OwnerId);
            Assert.AreEqual(GuildPlayer, session.Provinces[1].TradingPostOwnerId);
            Assert.AreEqual(2, session.GetUnitsForPlayer(1, GuildPlayer, UnitType.Trader).Count,
                "Instant Trading Post grants +1 Trader.");
            Assert.AreEqual(12, session.CurrentPlayer.Gold);
            Assert.AreEqual(18, session.GetPlayer(MilitantPlayer).Gold);
            Assert.AreEqual(0, session.GetUnitsForPlayer(1, MilitantPlayer).Count,
                "Garrison retreated with nowhere to go.");
        }

        [Test]
        public void Embargo_BlocksBuyout_AndAllowsRaidingGuildCaravans()
        {
            var session = CreateGuildSession(startingGold: 20);
            session.MoveArmy(new MoveArmyCommand(0, 1, new[] { 1 })); // Move Trader into Militant province 1
            session.EndTurn(); // Militant turn

            Assert.AreEqual(CommandResult.Ok, session.DeclareEmbargo(GuildPlayer));
            Assert.AreEqual(CommandResult.Ok, session.RaidCaravan(1, GuildPlayer));
            Assert.AreEqual(12, session.CurrentPlayer.Gold, "Militant looted 2G from Guild Trader.");
            Assert.AreEqual(0, session.GetUnitsForPlayer(1, GuildPlayer, UnitType.Trader).Count);
        }

        [Test]
        public void SellMercenaryContract_TransfersGold_AndSpawnsMercenaryUnitForBuyer()
        {
            var session = CreateGuildSession(startingGold: 10);
            session.MoveArmy(new MoveArmyCommand(0, 1, new[] { 1 }));

            Assert.AreEqual(CommandResult.Ok, session.SellMercenaryContract(1, MilitantPlayer, 4));
            Assert.AreEqual(14, session.CurrentPlayer.Gold);
            Assert.AreEqual(6, session.GetPlayer(MilitantPlayer).Gold);
            var militantUnits = session.GetUnitsForPlayer(1, MilitantPlayer, UnitType.RegularMilitary);
            Assert.AreEqual(2, militantUnits.Count);
            Assert.AreEqual(FactionType.Guilds, militantUnits[1].OriginalFaction);
        }

        [Test]
        public void GuildScoring_AwardsTradingPosts_BankedGoldPerTen_AndThreePlusCaravanRoutes()
        {
            var session = CreateGuildSession(startingGold: 16);
            // 2 existing Trading Posts on provinces 0 and 3 (+2 VP)
            // Run 3-traversed caravan 0 -> 1 -> 2 -> 3 (+4G net -> 20G banked = +2 VP; >=3 traversed route = +2 VP)
            Assert.AreEqual(CommandResult.Ok, session.RunCaravan(new[] { 0, 1, 2, 3 }));

            session.EndTurn();

            // Total VP: 2 (Trading Posts) + 2 (20G / 10) + 2 (3+ traversed route) = 6 VP
            Assert.AreEqual(6, session.GetPlayer(GuildPlayer).VictoryPoints);
        }
    }
}