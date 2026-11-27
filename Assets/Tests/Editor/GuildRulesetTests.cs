using System.Collections.Generic;
using Core.Actions;
using Core.Board;
using Core.Combat;
using Core.Crisis;
using Core.Simulation;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
    public class GuildRulesetTests
    {
        private const int GuildPlayer = 1;
        private const int MilitantPlayer = 2;
        private const int ZealotPlayer = 3;

        private static GameSession CreateGuildSession(int startingGold = 15, bool targetFortified = false)
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

            return TestSessionFactory.Create(players, provinces, crisisEnabled: false);
        }

        [Test]
        public void Guilds_AreNonLanded_AndCannotRecruitMilitaryUnits()
        {
            Assert.IsFalse(new GuildRuleset().IsLanded);
            var sessionOwn = TestSessionFactory.Create(
                new[] { new PlayerProfile(GuildPlayer, FactionType.Guilds, 10, 0, new List<UnitInstance>()) },
                new[]
                {
                    new ProvinceState(0, "Cap", false, System.Array.Empty<int>(), GuildPlayer, new List<UnitInstance>())
                },
                crisisEnabled: false);

            Assert.AreEqual(ActionResult.WrongFaction, sessionOwn.BuyUnit(0));
        }

        [Test]
        public void BuildTradingPost_GrantsFreeTrader_AndEnforcesTraderCap()
        {
            var session = CreateGuildSession(startingGold: 15);
            session.MoveArmy(new MoveUnitsCommand(0, 1, new[] { 1 }));
            Assert.AreEqual(ActionResult.Ok, session.BuildTradingPost(1));

            Assert.AreEqual(11, session.CurrentPlayer.Gold);
            Assert.AreEqual(GuildPlayer, session.Provinces[1].TradingPostOwnerId);
            Assert.AreEqual(2, session.CountPlayerUnitsOfType(GuildPlayer, UnitType.Trader));
            Assert.AreEqual(3, session.CountOwnedTradingPosts(GuildPlayer));

            Assert.AreEqual(ActionResult.Ok, session.BuyTrader(1));
            Assert.AreEqual(9, session.CurrentPlayer.Gold);
            Assert.AreEqual(ActionResult.TraderCapReached, session.BuyTrader(1));
        }

        [Test]
        public void RunCaravan_BetweenTradingPosts_PaysTransitTolls_GainsScalingGold_AndMovesTrader()
        {
            var session = CreateGuildSession(startingGold: 10);

            var result = session.RunCaravan(new[] { 0, 1, 2, 3 });

            Assert.AreEqual(ActionResult.Ok, result);
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
            Assert.AreEqual(ActionResult.InvalidCaravanPath, session.RunCaravan(new[] { 0, 1, 2 }));
        }

        [Test]
        public void HostileBuyout_PurchasesUnfortifiedProvince_PlacesTradingPost_AndRetreatsGarrison()
        {
            var session = CreateGuildSession(startingGold: 20, targetFortified: false);
            session.MoveArmy(new MoveUnitsCommand(0, 1, new[] { 1 }));

            int expectedCost = session.GetHostileBuyoutCost(1);
            Assert.AreEqual(8, expectedCost);

            Assert.AreEqual(ActionResult.Ok, session.HostileBuyout(1));
            Assert.AreEqual(GuildPlayer, session.Provinces[1].OwnerId);
            Assert.AreEqual(GuildPlayer, session.Provinces[1].TradingPostOwnerId);
            Assert.AreEqual(2, session.GetUnitsForPlayer(1, GuildPlayer, UnitType.Trader).Count);
            Assert.AreEqual(12, session.CurrentPlayer.Gold);
            Assert.AreEqual(18, session.GetPlayer(MilitantPlayer).Gold);
            Assert.AreEqual(0, session.GetUnitsForPlayer(1, MilitantPlayer).Count);
        }

        [Test]
        public void Embargo_BlocksBuyout_AndAllowsRaidingGuildCaravans()
        {
            var session = CreateGuildSession(startingGold: 20);
            session.MoveArmy(new MoveUnitsCommand(0, 1, new[] { 1 }));
            session.EndTurn();

            Assert.AreEqual(ActionResult.Ok, session.DeclareEmbargo(GuildPlayer));
            Assert.AreEqual(ActionResult.Ok, session.RaidCaravan(1, GuildPlayer));
            Assert.AreEqual(12, session.CurrentPlayer.Gold);
            Assert.AreEqual(0, session.GetUnitsForPlayer(1, GuildPlayer, UnitType.Trader).Count);
        }

        [Test]
        public void SellMercenaryContract_TransfersGold_AndSpawnsMercenaryUnitForBuyer()
        {
            var session = CreateGuildSession(startingGold: 10);
            session.MoveArmy(new MoveUnitsCommand(0, 1, new[] { 1 }));

            Assert.AreEqual(ActionResult.Ok, session.SellMercenaryContract(1, MilitantPlayer, 4));
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
            Assert.AreEqual(ActionResult.Ok, session.RunCaravan(new[] { 0, 1, 2, 3 }));

            session.EndTurn();

            Assert.AreEqual(6, session.GetPlayer(GuildPlayer).VictoryPoints);
        }
    }
}