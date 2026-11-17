using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using NUnit.Framework;
using Presentation;
using Presentation.Map;
using Presentation.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Tests.Editor
{
    /// <summary>
    /// Validates the 21-province continent authored in the prototype scene.
    /// </summary>
    [TestFixture]
    public class ContinentMapTests
    {
        private const string ScenePath = "Assets/Scenes/Prototype_CombatTest.unity";
        private const int ProvinceCount = 21;

        private Scene _scene;
        private ProvinceView[] _views;
        private ProvinceSeed[] _seeds;

        /// <summary>Counts controlled provinces as revenue so scoring can be checked against territory.</summary>
        private class TerritoryRules : IGameRules
        {
            public CombatResult ResolveSkirmish(
                IReadOnlyList<UnitInstance> attackers,
                IReadOnlyList<UnitInstance> defenders,
                FactionType defenderFaction,
                bool hasFortification) => throw new System.NotSupportedException();

            public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces) => player;

            public int CalculateRevenue(FactionType faction, int controlledProvinces) => controlledProvinces;

            public int RollContest() => throw new System.NotSupportedException();

            public bool CanTriggerAscension(PlayerProfile player, int controlledProvinces) => false;

            public bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction,
                int controlledProvinces) => false;

            public CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction) =>
                throw new System.NotSupportedException();

            public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction) =>
                throw new System.NotSupportedException();
        }

        [OneTimeSetUp]
        public void LoadScene()
        {
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            PrototypeGameBootstrap bootstrap = null;
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out PrototypeGameBootstrap found)) bootstrap = found;
            }

            Assert.IsNotNull(bootstrap, "The scene needs a PrototypeGameBootstrap.");
            var views = new SerializedObject(bootstrap).FindProperty("_provinceViews");
            _views = new ProvinceView[views.arraySize];
            _seeds = new ProvinceSeed[views.arraySize];
            for (int i = 0; i < views.arraySize; i++)
            {
                _views[i] = (ProvinceView)views.GetArrayElementAtIndex(i).objectReferenceValue;
                _seeds[i] = _views[i].ToSeed();
            }
        }

        [OneTimeTearDown]
        public void CloseScene() => EditorSceneManager.CloseScene(_scene, true);

        private int CountOwnedBy(int ownerId)
        {
            int count = 0;
            for (int i = 0; i < _seeds.Length; i++)
            {
                if (_seeds[i].OwnerId == ownerId) count++;
            }

            return count;
        }

        private PrototypeGameSession CreateSession()
        {
            var players = PrototypeSetup.CreatePlayers();
            return new PrototypeGameSession(new TerritoryRules(), players,
                PrototypeSetup.BuildProvinces(_seeds, players), true);
        }

        /// <summary>Breadth-first search that may only pass through provinces matching <paramref name="canEnter"/>.</summary>
        private bool CanReach(int from, int to, System.Predicate<ProvinceSeed> canEnter)
        {
            var visited = new bool[_seeds.Length];
            var queue = new Queue<int>();
            queue.Enqueue(from);
            visited[from] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to) return true;

                var neighbours = _seeds[current].Neighbours;
                for (int i = 0; i < neighbours.Count; i++)
                {
                    int next = neighbours[i];
                    if (visited[next] || (next != to && !canEnter(_seeds[next]))) continue;

                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        [Test]
        public void Map_Has21Provinces_8Militant_8Zealot_5NeutralFrontier()
        {
            Assert.AreEqual(ProvinceCount, _seeds.Length);
            Assert.AreEqual(8, CountOwnedBy(PrototypeSetup.MilitantsPlayerId));
            Assert.AreEqual(8, CountOwnedBy(PrototypeSetup.ZealotsPlayerId));
            Assert.AreEqual(5, CountOwnedBy(ProvinceState.NeutralOwner));
        }

        [Test]
        public void ProvinceIds_MatchTheirIndexAndNamesAreUnique()
        {
            var names = new HashSet<string>();
            for (int i = 0; i < _seeds.Length; i++)
            {
                Assert.AreEqual(i, _seeds[i].Id, $"{_views[i].name} must sit at index {_seeds[i].Id}.");
                Assert.IsTrue(names.Add(_seeds[i].Name), $"Duplicate province name {_seeds[i].Name}.");
            }
        }

        [Test]
        public void Adjacency_IsSymmetric_WithoutSelfLinksOrDuplicates()
        {
            for (int i = 0; i < _seeds.Length; i++)
            {
                var neighbours = _seeds[i].Neighbours;
                Assert.Greater(neighbours.Count, 0, $"{_seeds[i].Name} has no neighbours.");
                var seen = new HashSet<int>();
                for (int n = 0; n < neighbours.Count; n++)
                {
                    int other = neighbours[n];
                    Assert.That(other, Is.InRange(0, _seeds.Length - 1), $"{_seeds[i].Name} links to {other}.");
                    Assert.AreNotEqual(i, other, $"{_seeds[i].Name} links to itself.");
                    Assert.IsTrue(seen.Add(other), $"{_seeds[i].Name} lists {other} twice.");
                    CollectionAssert.Contains(_seeds[other].Neighbours, i,
                        $"{_seeds[i].Name} -> {_seeds[other].Name} has no way back.");
                }
            }
        }

        [Test]
        public void Map_IsConnected()
        {
            for (int i = 1; i < _seeds.Length; i++)
            {
                Assert.IsTrue(CanReach(0, i, _ => true), $"{_seeds[i].Name} cannot be reached.");
            }
        }

        [Test]
        public void NeutralFrontier_IsTheOnlyWayBetweenMilitantsAndZealots()
        {
            for (int m = 0; m < _seeds.Length; m++)
            {
                if (_seeds[m].OwnerId != PrototypeSetup.MilitantsPlayerId) continue;

                for (int z = 0; z < _seeds.Length; z++)
                {
                    if (_seeds[z].OwnerId != PrototypeSetup.ZealotsPlayerId) continue;

                    Assert.IsFalse(CanReach(m, z, seed => seed.OwnerId != ProvinceState.NeutralOwner),
                        $"{_seeds[m].Name} reaches {_seeds[z].Name} without crossing the frontier.");
                }
            }
        }

        [Test]
        public void StartingSetup_PlacesFortsWithMilitantsAndChurchesWithZealots()
        {
            int forts = 0;
            int churches = 0;
            int missionaries = 0;
            int cells = 0;
            for (int i = 0; i < _seeds.Length; i++)
            {
                var seed = _seeds[i];
                if (seed.HasFort)
                {
                    forts++;
                    Assert.AreEqual(PrototypeSetup.MilitantsPlayerId, seed.OwnerId, $"Fort in {seed.Name}.");
                }

                if (seed.HasChurch)
                {
                    churches++;
                    Assert.AreEqual(PrototypeSetup.ZealotsPlayerId, seed.OwnerId, $"Church in {seed.Name}.");
                }

                if (seed.OwnerId == PrototypeSetup.MilitantsPlayerId)
                {
                    Assert.Greater(seed.RegularUnits, 0, $"{seed.Name} needs a garrison to hold hegemony.");
                }

                if (seed.OwnerId == ProvinceState.NeutralOwner)
                {
                    Assert.AreEqual(0, seed.RegularUnits + seed.Missionaries, $"{seed.Name} should start empty.");
                }

                missionaries += seed.Missionaries;
                if (seed.RebellionCellOwnerId != ProvinceSeed.NoRebellionCell)
                {
                    cells++;
                    Assert.AreEqual(PrototypeSetup.GuerrillasPlayerId, seed.RebellionCellOwnerId);
                    Assert.AreNotEqual(ProvinceState.NeutralOwner, seed.OwnerId, "The cell starts in landed territory.");
                }
            }

            Assert.Greater(forts, 1);
            Assert.Greater(churches, 1);
            Assert.Greater(missionaries, 0);
            Assert.AreEqual(1, cells);
        }

        [Test]
        public void Session_KeepsEveryStartingOwner_AfterHegemonyIsEvaluated()
        {
            var session = CreateSession();

            Assert.AreEqual(ProvinceCount, session.Provinces.Count);
            for (int i = 0; i < _seeds.Length; i++)
            {
                Assert.AreEqual(_seeds[i].OwnerId, session.Provinces[i].OwnerId, _seeds[i].Name);
                CollectionAssert.AreEqual(_seeds[i].Neighbours, session.Provinces[i].Neighbours, _seeds[i].Name);
            }
        }

        [Test]
        public void TerritoryCounts_ScaleToEightProvincesInScoringAndRevenue()
        {
            var session = CreateSession();
            int startingGold = session.CurrentPlayer.Gold;

            session.EndTurn();

            var militants = session.GetPlayer(PrototypeSetup.MilitantsPlayerId);
            Assert.AreEqual(startingGold + 8, militants.Gold);
            Assert.AreEqual(8 * PrototypeGameSession.VictoryPointsPerProvince, militants.VictoryPoints);
            Assert.AreEqual(FactionType.Militants, militants.Faction, "Eight provinces is far above the crisis threshold.");
        }
    }
}
