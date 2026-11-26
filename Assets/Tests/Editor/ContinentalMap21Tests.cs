using System.Collections.Generic;
using Core.Combat;
using Core.Setup;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
    public class ContinentalMap21Tests
    {
        [Test]
        public void ContinentalMap21_Has21ConnectedNodes_SymmetricAdjacency_AndNoProvinceNames()
        {
            var seeds = ContinentalMapBuilder.CreateContinentalMap21();
            Assert.AreEqual(21, seeds.Length);

            var visited = new HashSet<int> { 0 };
            var queue = new Queue<int>();
            queue.Enqueue(0);

            for (int i = 0; i < seeds.Length; i++)
            {
                Assert.AreEqual(i, seeds[i].Id);
                Assert.IsEmpty(seeds[i].Name);
                Assert.IsNotEmpty(seeds[i].Neighbours);

                for (int n = 0; n < seeds[i].Neighbours.Count; n++)
                {
                    int neighbourId = seeds[i].Neighbours[n];
                    Assert.That(neighbourId, Is.InRange(0, 20));
                    Assert.AreNotEqual(i, neighbourId);

                    bool hasBackEdge = false;
                    var backList = seeds[neighbourId].Neighbours;
                    for (int b = 0; b < backList.Count; b++)
                    {
                        if (backList[b] == i) hasBackEdge = true;
                    }

                    Assert.IsTrue(hasBackEdge, $"Adjacency {i} -> {neighbourId} is missing reverse edge.");
                }
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in seeds[current].Neighbours)
                {
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }

            Assert.AreEqual(21, visited.Count, "All 21 provinces must form a single connected continent.");
        }

        [Test]
        public void ContinentalMap21_SeedsExactTokensAndStructuresPerUserSpec()
        {
            var players = ContinentalMapBuilder.CreatePlayers();
            var seeds = ContinentalMapBuilder.CreateContinentalMap21();
            var session = TestSessionFactory.Create(
                players, ContinentalMapBuilder.BuildProvinces(seeds, players), crisisEnabled: true);

            // 1. Militants: 1 fort in capital (0), 3 warriors in capital (0), no extra tokens elsewhere
            var milCap = session.Provinces[ContinentalMapBuilder.MilitantCapitalId];
            Assert.AreEqual(ContinentalMapBuilder.MilitantsPlayerId, milCap.OwnerId);
            Assert.IsTrue(milCap.HasFort);
            Assert.AreEqual(3,
                session.GetUnitsForPlayer(0, ContinentalMapBuilder.MilitantsPlayerId, UnitType.RegularMilitary).Count);
            Assert.AreEqual(3,
                session.CountPlayerUnitsOfType(ContinentalMapBuilder.MilitantsPlayerId, UnitType.RegularMilitary));

            // 2. Zealots: 1 church, 1 missionary, 1 religious presence token in capital (13)
            var zealCap = session.Provinces[ContinentalMapBuilder.ZealotCapitalId];
            Assert.AreEqual(ContinentalMapBuilder.ZealotsPlayerId, zealCap.OwnerId);
            Assert.IsTrue(zealCap.HasChurch);
            Assert.AreEqual(ContinentalMapBuilder.ZealotsPlayerId, zealCap.ChurchOwnerId);
            Assert.IsNotNull(zealCap.PresenceToken);
            Assert.AreEqual(ContinentalMapBuilder.ZealotsPlayerId, zealCap.PresenceToken.OwnerId);
            Assert.AreEqual(1,
                session.GetUnitsForPlayer(13, ContinentalMapBuilder.ZealotsPlayerId, UnitType.Missionary).Count);
            Assert.AreEqual(1,
                session.CountPlayerUnitsOfType(ContinentalMapBuilder.ZealotsPlayerId, UnitType.Missionary));

            // 3. Guerrillas: 1 rebellion token in non-neutral province (14) + 1 rebel unit in rebellion province
            var rebelProv = session.Provinces[ContinentalMapBuilder.GuerrillaCellProvinceId];
            Assert.IsNotNull(rebelProv.RebellionCell);
            Assert.AreEqual(ContinentalMapBuilder.GuerrillasPlayerId, rebelProv.RebellionCell.OwnerId);
            Assert.IsFalse(rebelProv.RebellionCell.IsOpenRebellion);
            Assert.AreEqual(1,
                session.GetUnitsForPlayer(14, ContinentalMapBuilder.GuerrillasPlayerId, UnitType.RegularMilitary)
                    .Count);
            Assert.AreEqual(1,
                session.CountPlayerUnitsOfType(ContinentalMapBuilder.GuerrillasPlayerId, UnitType.RegularMilitary));

            // 4. Guilds: 1 tradepost in Zealot capital (13), 1 tradepost in Militant capital (0), 1 trader unit
            Assert.AreEqual(ContinentalMapBuilder.GuildsPlayerId, milCap.TradingPostOwnerId);
            Assert.AreEqual(ContinentalMapBuilder.GuildsPlayerId, zealCap.TradingPostOwnerId);
            Assert.AreEqual(2, session.CountOwnedTradingPosts(ContinentalMapBuilder.GuildsPlayerId));
            Assert.AreEqual(1, session.CountPlayerUnitsOfType(ContinentalMapBuilder.GuildsPlayerId, UnitType.Trader));
            Assert.AreEqual(1,
                session.GetUnitsForPlayer(0, ContinentalMapBuilder.GuildsPlayerId, UnitType.Trader).Count);
        }
    }
}