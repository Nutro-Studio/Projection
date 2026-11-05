using Core;
using Core.Combat;
using Core.Map;
using Core.Turns;
using Core.Units;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
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
            Assert.AreEqual(5, map.CountControlledBy(null));
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
            Assert.AreEqual(match.Province(1, 1), path[0]);
            Assert.AreEqual(match.Province(5, 1), path[path.Count - 1]);
        }
    }

    [TestFixture]
    public class MovementTests
    {
        private static Match MatchWithDice(params (int, int, int)[] rolls) =>
            new Match(new RootCombatService(new FixedDiceRoller(rolls)));

        private static Province Staging(Match match, Faction owner, int x, int y, int units)
        {
            var province = match.Province(x, y);
            province.Owner = owner;
            for (int i = 0; i < units; i++) match.State.SpawnUnit(UnitType.Military, owner, province);
            return province;
        }

        private static void AddDefenders(Match match, Faction owner, Province province, int units)
        {
            for (int i = 0; i < units; i++) match.State.SpawnUnit(UnitType.Military, owner, province);
        }

        [Test]
        public void Move_ToNonAdjacentProvince_IsRejected()
        {
            var match = new Match();
            var result = match.Movement.Move(match.Militants, match.Province(1, 1), match.Province(3, 1), 1);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(2, match.State.CountUnits(match.Militants, match.Province(1, 1)));
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
        public void Attack_WipesOutGarrison_CapturesProvince()
        {
            // 12 vs 3: 3 attackers deal 3 hits (2 defenders die), 2 defenders deal 2 hits -> 1 attacker takes the province.
            var match = MatchWithDice((4, 4, 4), (1, 1, 1));
            var lumenVale = match.Province(4, 0);
            AddDefenders(match, match.Zealots, lumenVale, 1);
            var ashenPass = Staging(match, match.Militants, 3, 0, 3);

            var result = match.Movement.Move(match.Militants, ashenPass, lumenVale, 3);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(match.Militants, lumenVale.Owner);
            Assert.AreEqual(0, match.State.CountUnits(match.Zealots, lumenVale));
            Assert.AreEqual(1, match.State.CountUnits(match.Militants, lumenVale));
        }

        [Test]
        public void Attack_Won_SurvivingDefendersRetreatToFriendlyNeighbour()
        {
            // 5 attackers vs 6 defenders, 12 vs 3: defenders lose 5, attackers lose 3 -> defenders retreat.
            var match = MatchWithDice((4, 4, 4), (1, 1, 1));
            var lumenVale = match.Province(4, 0);
            AddDefenders(match, match.Zealots, lumenVale, 5);
            var ashenPass = Staging(match, match.Militants, 3, 0, 5);

            match.Movement.Move(match.Militants, ashenPass, lumenVale, 5);

            Assert.AreEqual(match.Militants, lumenVale.Owner);
            Assert.AreEqual(2, match.State.CountUnits(match.Militants, lumenVale));
            Assert.AreEqual(0, match.State.CountUnits(match.Zealots, lumenVale));
            Assert.AreEqual(1, match.State.CountUnits(match.Zealots, match.Province(5, 0)));
        }

        [Test]
        public void Attack_Tie_DefenderHolds_AttackersStayHome()
        {
            // Both roll 3 -> 3 losses each; the tie goes to the defender.
            var match = MatchWithDice((1, 1, 1));
            var lumenVale = match.Province(4, 0);
            AddDefenders(match, match.Zealots, lumenVale, 3);
            var ashenPass = Staging(match, match.Militants, 3, 0, 4);

            match.Movement.Move(match.Militants, ashenPass, lumenVale, 4);

            Assert.AreEqual(match.Zealots, lumenVale.Owner);
            Assert.AreEqual(1, match.State.CountUnits(match.Zealots, lumenVale));
            Assert.AreEqual(1, match.State.CountUnits(match.Militants, ashenPass));
        }

        [Test]
        public void Attack_OnFortifiedMilitants_UsesFortBonus()
        {
            // 3 Zealots hit Bastion (fort, 2 Militants at 2 HP) for 3: only 1 defender dies; Zealots lose 2 and retreat.
            var match = MatchWithDice((4, 4, 4), (1, 1, 1));
            var bastion = match.Province(1, 1);
            var rustford = Staging(match, match.Zealots, 2, 1, 3);

            match.Movement.Move(match.Zealots, rustford, bastion, 3);

            Assert.AreEqual(match.Militants, bastion.Owner);
            Assert.AreEqual(1, match.State.CountUnits(match.Militants, bastion));
            Assert.AreEqual(1, match.State.CountUnits(match.Zealots, rustford));
        }
    }

    [TestFixture]
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

            turns.AdvancePhase(); // Zealots: Actions
            turns.AdvancePhase(); // Zealots: Movement
            turns.AdvancePhase(); // Militants: Actions
            turns.AdvancePhase(); // Militants: Movement

            Assert.IsTrue(turns.Move(match.Province(1, 0), match.Province(1, 1), 1).Success);
            Assert.AreEqual(2, turns.Round);
        }
    }
}
