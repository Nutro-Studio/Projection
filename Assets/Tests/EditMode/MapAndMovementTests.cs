using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Projection.Core;
using Projection.Core.Combat;
using Projection.Core.Turns;
using Projection.Core.Units;

namespace Projection.Tests
{
    public class MapTests
    {
        [Test]
        public void StandardMap_Has21Provinces_8_8_5()
        {
            var match = new Match();
            var map = match.State.Map;
            Assert.AreEqual(21, map.Count);
            Assert.AreEqual(8, map.CountControlledBy(match.Militants));
            Assert.AreEqual(8, map.CountControlledBy(match.Zealots));
            Assert.AreEqual(5, map.Provinces.Count(p => p.Owner == null));
        }

        [Test]
        public void StandardMap_IsConnected_AndStartingTerritoriesAreContiguous()
        {
            var match = new Match();
            Assert.IsTrue(match.State.Map.IsConnected());
            Assert.IsTrue(match.State.Map.IsTerritoryContiguous(match.Militants));
            Assert.IsTrue(match.State.Map.IsTerritoryContiguous(match.Zealots));
        }

        [Test]
        public void Adjacency_IsSymmetricAndExcludesDiagonals()
        {
            var match = new Match();
            var map = match.State.Map;
            Assert.IsTrue(map.AreAdjacent(match.Province(1, 1), match.Province(2, 1)));
            Assert.IsTrue(map.AreAdjacent(match.Province(2, 1), match.Province(1, 1)));
            Assert.IsFalse(map.AreAdjacent(match.Province(1, 1), match.Province(2, 2)));
            Assert.IsFalse(map.AreAdjacent(match.Province(0, 0), match.Province(2, 0)));
        }

        [Test]
        public void ShortestPath_AcrossTheContinent()
        {
            var match = new Match();
            var path = match.State.Map.ShortestPath(match.Province(1, 1), match.Province(5, 1));
            Assert.AreEqual(5, path.Count);
            Assert.AreEqual(match.Province(1, 1), path.First());
            Assert.AreEqual(match.Province(5, 1), path.Last());
        }
    }

    public class MovementTests
    {
        /// <summary>Returns the given die faces in order, then repeats the last one.</summary>
        private static PlaceholderCombatResolver Dice(params int[] faces)
        {
            var queue = new Queue<int>(faces);
            int last = faces.Last();
            return new PlaceholderCombatResolver(_ => queue.Count > 0 ? queue.Dequeue() : last);
        }

        [Test]
        public void Move_ToNonAdjacentProvince_IsRejected()
        {
            var match = new Match();
            var result = match.Movement.Move(match.Militants, match.Province(1, 1), match.Province(3, 1), 1);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(2, match.State.UnitsOf(match.Militants, match.Province(1, 1)).Count());
        }

        [Test]
        public void Move_UnitCanMoveOnlyOncePerTurn()
        {
            var match = new Match();
            var bastion = match.Province(1, 1);
            var ironhold = match.Province(1, 0);
            Assert.IsTrue(match.Movement.Move(match.Militants, bastion, ironhold, 2).Success);
            Assert.IsFalse(match.Movement.Move(match.Militants, ironhold, bastion, 1).Success);
        }

        [Test]
        public void Move_MoreUnitsThanAvailable_IsRejected()
        {
            var match = new Match();
            Assert.IsFalse(match.Movement.Move(match.Militants, match.Province(1, 1), match.Province(1, 0), 3).Success);
        }

        [Test]
        public void Move_IntoEmptyNeutralProvince_CapturesIt()
        {
            var match = new Match();
            var rustford = match.Province(2, 1);
            Assert.IsNull(rustford.Owner);

            Assert.IsTrue(match.Movement.Move(match.Militants, match.Province(1, 1), rustford, 1).Success);
            Assert.AreEqual(match.Militants, rustford.Owner);
            Assert.AreEqual(9, match.State.Map.CountControlledBy(match.Militants));
        }

        [Test]
        public void Missionary_DoesNotCaptureProvinces()
        {
            var match = new Match();
            var oldMill = match.Province(4, 1);
            Assert.IsTrue(match.Movement.Move(match.Zealots, match.Province(5, 1), oldMill, 1, UnitType.Missionary).Success);
            Assert.IsNull(oldMill.Owner);
        }

        [Test]
        public void Attack_Won_CapturesProvince()
        {
            // Attacker 12 vs defender 3. 3 attackers deal min(12, 3)=3 hits -> both defenders die;
            // 2 defenders deal min(3, 2)=2 hits -> 1 attacker survives and takes the province.
            var match = new Match(Dice(4, 4, 4, 1, 1, 1));
            var lumenVale = match.Province(4, 0);
            match.State.SpawnUnit(UnitType.Military, match.Zealots, lumenVale);
            var ashenPass = MilitantStaging(match, 3);

            var result = match.Movement.Move(match.Militants, ashenPass, lumenVale, 3);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(match.Militants, lumenVale.Owner);
            Assert.AreEqual(0, match.State.UnitsOf(match.Zealots, lumenVale).Count());
            Assert.AreEqual(1, match.State.UnitsOf(match.Militants, lumenVale).Count());
        }

        [Test]
        public void Attack_Won_SurvivingDefendersRetreatToFriendlyNeighbour()
        {
            // 5 attackers vs 6 defenders, attacker 12 vs defender 3: attacker deals 5, defender deals 3
            // -> defenders keep 1 and retreat (higher losses), attackers keep 2.
            var match = new Match(Dice(4, 4, 4, 1, 1, 1));
            var lumenVale = match.Province(4, 0);
            for (int i = 0; i < 5; i++) match.State.SpawnUnit(UnitType.Military, match.Zealots, lumenVale);
            var ashenPass = MilitantStaging(match, 5);

            match.Movement.Move(match.Militants, ashenPass, lumenVale, 5);

            Assert.AreEqual(match.Militants, lumenVale.Owner);
            Assert.AreEqual(2, match.State.UnitsOf(match.Militants, lumenVale).Count());
            Assert.AreEqual(0, match.State.UnitsOf(match.Zealots, lumenVale).Count());
            Assert.AreEqual(1, match.State.UnitsOf(match.Zealots, match.Province(5, 0)).Count());
        }

        [Test]
        public void Attack_Tie_DefenderHolds_AttackersStayHome()
        {
            // Both roll 3 -> each deals 3 hits: 3 losses each, the tie goes to the defender.
            var match = new Match(Dice(1));
            var lumenVale = match.Province(4, 0);
            for (int i = 0; i < 3; i++) match.State.SpawnUnit(UnitType.Military, match.Zealots, lumenVale);
            var ashenPass = MilitantStaging(match, 4);

            match.Movement.Move(match.Militants, ashenPass, lumenVale, 4);

            Assert.AreEqual(match.Zealots, lumenVale.Owner);
            Assert.AreEqual(1, match.State.UnitsOf(match.Zealots, lumenVale).Count());
            Assert.AreEqual(1, match.State.UnitsOf(match.Militants, ashenPass).Count());
        }

        private static Projection.Core.Map.Province MilitantStaging(Match match, int units)
        {
            var ashenPass = match.Province(3, 0);
            ashenPass.Owner = match.Militants;
            for (int i = 0; i < units; i++) match.State.SpawnUnit(UnitType.Military, match.Militants, ashenPass);
            return ashenPass;
        }

        [Test]
        public void Fortification_GivesMilitantDefenders2Hp()
        {
            var resolver = Dice(4, 4, 4, 1, 1, 1);
            var outcome = resolver.Resolve(new CombatContext
            {
                AttackingUnits = new Unit[3],
                DefendingUnits = new Unit[2],
                DefenderFortified = true
            });
            Assert.AreEqual(1, outcome.DefenderLosses); // 3 hits vs 2 HP units
        }
    }

    public class TurnTests
    {
        [Test]
        public void TurnFlow_IncomeOnStart_RecruitThenMove_ThenNextPlayer()
        {
            var match = new Match();
            var turns = match.Turns;
            turns.StartGame();

            Assert.AreEqual(match.Militants, turns.Current);
            Assert.AreEqual(TurnPhase.Actions, turns.Phase);
            Assert.AreEqual(18, match.Militants.Treasury.Gold);

            var bastion = match.Province(1, 1);
            Assert.IsFalse(turns.Move(bastion, match.Province(2, 1), 1).Success, "Moving is not allowed in Actions");
            Assert.IsTrue(turns.Recruit(bastion, UnitType.Military).Success);

            turns.AdvancePhase();
            Assert.AreEqual(TurnPhase.Movement, turns.Phase);
            Assert.IsFalse(turns.Recruit(bastion, UnitType.Military).Success, "Recruiting is not allowed in Movement");
            Assert.IsTrue(turns.Move(bastion, match.Province(2, 1), 3).Success);

            turns.AdvancePhase();
            Assert.AreEqual(match.Zealots, turns.Current);
            Assert.AreEqual(TurnPhase.Actions, turns.Phase);
            Assert.AreEqual(18, match.Zealots.Treasury.Gold);
        }

        [Test]
        public void UnitsCanMoveAgainNextTurn()
        {
            var match = new Match();
            var turns = match.Turns;
            turns.StartGame();
            turns.AdvancePhase();
            Assert.IsTrue(turns.Move(match.Province(1, 1), match.Province(1, 0), 1).Success);
            turns.AdvancePhase(); // Zealots
            turns.AdvancePhase();
            turns.AdvancePhase(); // Militants again
            turns.AdvancePhase();
            Assert.IsTrue(turns.Move(match.Province(1, 0), match.Province(1, 1), 1).Success);
            Assert.AreEqual(2, turns.Round);
        }
    }
}
