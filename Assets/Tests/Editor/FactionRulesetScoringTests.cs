using Core.Crisis;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
    public class FactionRulesetScoringTests
    {
        [Test]
        public void Militants_ScoreOnePerThreeProvinces_AndForFortsCapturesAndCrushedRebellions()
        {
            var militants = new MilitantRuleset();

            Assert.AreEqual(2, militants.CalculateUpkeepVictoryPoints(new UpkeepState(8, 0, 30)));
            Assert.AreEqual(1, militants.ScoreEvent(ScoringEvent.FortificationBuilt));
            Assert.AreEqual(2, militants.ScoreEvent(ScoringEvent.ProvinceCaptured));
            Assert.AreEqual(2, militants.ScoreEvent(ScoringEvent.RebellionCrushed));
            Assert.AreEqual(0, militants.ScoreEvent(ScoringEvent.ChurchBuilt));
        }

        [Test]
        public void Zealots_ScoreReligiousPresenceAtUpkeep_AndTwoPerChurch()
        {
            var zealots = new ZealotRuleset();

            Assert.AreEqual(3, zealots.CalculateUpkeepVictoryPoints(new UpkeepState(8, 0, 30, PresenceTokens: 3)));
            Assert.AreEqual(2, zealots.ScoreEvent(ScoringEvent.ChurchBuilt));
            Assert.AreEqual(0, zealots.ScoreEvent(ScoringEvent.ProvinceCaptured));
        }

        [Test]
        public void Guerrillas_ScoreOpenRebellionsAtUpkeep_AndTwoPerDefendedRebellion()
        {
            var guerrillas = new GuerrillaRuleset();

            Assert.AreEqual(2, guerrillas.CalculateUpkeepVictoryPoints(new UpkeepState(5, 0, 30, OpenRebellions: 2)));
            Assert.AreEqual(2, guerrillas.ScoreEvent(ScoringEvent.RebellionDefended));
            Assert.AreEqual(0, guerrillas.ScoreEvent(ScoringEvent.ProvinceCaptured));
        }

        [Test]
        public void Guilds_ScoreTradingPostsAndBankedGold_AndTwoPerTradeRoute()
        {
            var guilds = new GuildRuleset();

            Assert.AreEqual(2 + 3, guilds.CalculateUpkeepVictoryPoints(new UpkeepState(0, 2, 35)));
            Assert.AreEqual(2, guilds.ScoreEvent(ScoringEvent.TradeRouteCompleted));
            Assert.AreEqual(0, guilds.ScoreEvent(ScoringEvent.ProvinceCaptured));
        }
    }
}
