using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using NUnit.Framework;
using Presentation.Prototype;

namespace Tests.Editor
{
    [TestFixture]
    public class PrototypeSetupTests
    {
        private const int MilitantHome = 0;
        private const int Frontier = 1;
        private const int ZealotHome = 2;

        private class UnusedRules : IGameRules
        {
            public CombatResult ResolveSkirmish(
                IReadOnlyList<UnitInstance> attackers,
                IReadOnlyList<UnitInstance> defenders,
                FactionType defenderFaction,
                bool hasFortification) => throw new System.NotSupportedException();

            public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces) => player;

            public int CalculateRevenue(FactionType faction, int controlledProvinces) => 0;

            public int RollContest() => throw new System.NotSupportedException();
        }

        private static ProvinceSeed[] PrototypeMap() => new[]
        {
            new ProvinceSeed(MilitantHome, "Province 1", true, new[] { Frontier, ZealotHome },
                PrototypeSetup.MilitantsPlayerId, 2, rebellionCellOwnerId: PrototypeSetup.GuerrillasPlayerId),
            new ProvinceSeed(Frontier, "Province 2", false, new[] { MilitantHome, ZealotHome },
                ProvinceState.NeutralOwner, 0),
            new ProvinceSeed(ZealotHome, "Province 3", false, new[] { MilitantHome, Frontier },
                PrototypeSetup.ZealotsPlayerId, 0, missionaries: 1, hasChurch: true)
        };

        private static PrototypeGameSession CreateSession()
        {
            var players = PrototypeSetup.CreatePlayers();
            return new PrototypeGameSession(new UnusedRules(), players,
                PrototypeSetup.BuildProvinces(PrototypeMap(), players), false);
        }

        private static int CountUnits(ProvinceState province, UnitType type)
        {
            int count = 0;
            for (int i = 0; i < province.Units.Count; i++)
            {
                if (province.Units[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void CreatePlayers_SeedsMilitantsZealotsAndGuerrillas()
        {
            var players = PrototypeSetup.CreatePlayers();

            Assert.AreEqual(3, players.Length);
            Assert.AreEqual(FactionType.Militants, players[0].Faction);
            Assert.AreEqual(FactionType.Zealots, players[1].Faction);
            Assert.AreEqual(FactionType.Guerrillas, players[2].Faction);
            Assert.AreEqual(PrototypeSetup.GuerrillasPlayerId, players[2].PlayerId);
        }

        [Test]
        public void BuildProvinces_SeedsMilitantsWithRegularUnitsAndFort()
        {
            var province = CreateSession().Provinces[MilitantHome];

            Assert.AreEqual(PrototypeSetup.MilitantsPlayerId, province.OwnerId);
            Assert.IsTrue(province.HasFort);
            Assert.AreEqual(2, CountUnits(province, UnitType.RegularMilitary));
            Assert.AreEqual(PrototypeSetup.MilitantsPlayerId, province.Units[0].OwnerPlayerId);
        }

        [Test]
        public void BuildProvinces_SeedsZealotsWithChurchAndOneMissionary_AndChurchHoldsTheProvince()
        {
            var province = CreateSession().Provinces[ZealotHome];

            Assert.AreEqual(PrototypeSetup.ZealotsPlayerId, province.ChurchOwnerId);
            Assert.AreEqual(1, CountUnits(province, UnitType.Missionary));
            Assert.AreEqual(0, CountUnits(province, UnitType.RegularMilitary));
            Assert.AreEqual(PrototypeSetup.ZealotsPlayerId, province.OwnerId);
        }

        [Test]
        public void BuildProvinces_SeedsGuerrillasWithNoLandAndOneUndercoverCellInAnotherPlayersProvince()
        {
            var session = CreateSession();
            int landed = 0;
            int cells = 0;
            for (int i = 0; i < session.Provinces.Count; i++)
            {
                var province = session.Provinces[i];
                if (province.OwnerId == PrototypeSetup.GuerrillasPlayerId) landed++;
                if (province.RebellionCell == null) continue;

                cells++;
                Assert.AreEqual(PrototypeSetup.GuerrillasPlayerId, province.RebellionCell.OwnerId);
                Assert.IsFalse(province.RebellionCell.IsOpenRebellion);
                Assert.AreEqual(PrototypeSetup.MilitantsPlayerId, province.OwnerId);
            }

            Assert.AreEqual(0, landed);
            Assert.AreEqual(1, cells);
        }

        [Test]
        public void BuildProvinces_GivesEveryUnitAUniqueId()
        {
            var session = CreateSession();
            var ids = new HashSet<int>();
            for (int i = 0; i < session.Provinces.Count; i++)
            {
                var units = session.Provinces[i].Units;
                for (int u = 0; u < units.Count; u++)
                {
                    Assert.IsTrue(ids.Add(units[u].Id), $"Duplicate unit id {units[u].Id}");
                }
            }
        }

        [Test]
        public void TurnOrder_PassesThroughAllThreeFactions()
        {
            var session = CreateSession();

            session.EndTurn();
            Assert.AreEqual(PrototypeSetup.ZealotsPlayerId, session.CurrentPlayer.PlayerId);
            session.EndTurn();
            Assert.AreEqual(PrototypeSetup.GuerrillasPlayerId, session.CurrentPlayer.PlayerId);
            session.EndTurn();
            Assert.AreEqual(PrototypeSetup.MilitantsPlayerId, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(2, session.TurnNumber);
        }
    }
}
