using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using Core.Board;
using Core.Simulation;
using Core.Actions;
using NUnit.Framework;
using Presentation.Hud;

namespace Tests.Editor
{
    [TestFixture]
    public class GameSessionTests
    {
        private const int Attacker = 1;
        private const int Defender = 2;
        private const int AttackerHome = 0;
        private const int NeutralLand = 1;
        private const int DefenderHome = 2;

        private ScriptedDiceRoller _dice;

        [SetUp]
        public void Setup()
        {
            _dice = new ScriptedDiceRoller();
        }

        private GameSession CreateSession(
            bool crisisEnabled = false,
            int attackerVictoryPoints = 0,
            int defenderUnits = 1,
            bool defenderFort = false,
            FactionType defenderFaction = FactionType.Zealots,
            int middleOwner = ProvinceState.NeutralOwner,
            FactionType attackerFaction = FactionType.Militants,
            int defenderGold = GameSession.StartingGold,
            int defenderChurchOwner = ProvinceState.NoChurch,
            bool defenderPresence = false)
        {
            var players = new[]
            {
                new PlayerProfile(Attacker, attackerFaction, GameSession.StartingGold,
                    attackerVictoryPoints, new List<UnitInstance>()),
                new PlayerProfile(Defender, defenderFaction, defenderGold, 0,
                    new List<UnitInstance>())
            };

            var defenders = new List<UnitInstance>(defenderUnits);
            for (int i = 0; i < defenderUnits; i++)
            {
                defenders.Add(new UnitInstance(100 + i, Defender, defenderFaction, UnitType.RegularMilitary, 1, 1));
            }

            var provinces = new[]
            {
                new ProvinceState(AttackerHome, "A", false, new[] { NeutralLand, DefenderHome }, Attacker,
                    new List<UnitInstance> { new(1, Attacker, attackerFaction, UnitType.RegularMilitary, 1, 1) }),
                new ProvinceState(NeutralLand, "N", false, new[] { AttackerHome, DefenderHome },
                    middleOwner, new List<UnitInstance>()),
                new ProvinceState(DefenderHome, "D", defenderFort, new[] { AttackerHome, NeutralLand }, Defender,
                    defenders, defenderChurchOwner, defenderPresence ? new ReligiousPresenceToken(Defender) : null)
            };

            return TestSessionFactory.Create(players, provinces, crisisEnabled, _dice);
        }

        private static ActionResult MoveAllMovable(GameSession session, int from, int to) =>
            session.MoveArmy(new MoveUnitsCommand(from, to, session.GetMovableUnitIds(from)));

        private static ActionResult Move(GameSession session, int from, int to, params int[] unitIds) =>
            session.MoveArmy(new MoveUnitsCommand(from, to, unitIds));

        [Test]
        public void BuyUnit_SpendsThreeGold_AndAddsUnitToProvince()
        {
            var session = CreateSession();

            Assert.AreEqual(ActionResult.Ok, session.BuyUnit(AttackerHome));

            Assert.AreEqual(7, session.CurrentPlayer.Gold);
            Assert.AreEqual(2, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void BuyUnit_IsRejected_WhenTreasuryCannotPay()
        {
            var session = CreateSession();
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);

            Assert.AreEqual(ActionResult.NotEnoughGold, session.BuyUnit(AttackerHome));
            Assert.AreEqual(1, session.CurrentPlayer.Gold);
        }

        [Test]
        public void BuyUnit_IsRejected_OnForeignProvince()
        {
            var session = CreateSession();

            Assert.AreEqual(ActionResult.NotYourProvince, session.BuyUnit(DefenderHome));
            Assert.AreEqual(GameSession.StartingGold, session.CurrentPlayer.Gold);
        }

        [Test]
        public void MoveArmy_IntoEmptyProvince_TransfersOwnershipWithoutBattle()
        {
            var session = CreateSession();
            int battles = 0;
            session.BattleResolved += _ => battles++;

            Assert.AreEqual(ActionResult.Ok, MoveAllMovable(session, AttackerHome, NeutralLand));

            Assert.AreEqual(0, battles);
            Assert.AreEqual(Attacker, session.Provinces[NeutralLand].OwnerId);
            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(0, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void OnProvinceStateChanged_ReportsNewGarrison_WhenUnitIsBought()
        {
            var session = CreateSession();
            var changed = new List<ProvinceState>();
            session.OnProvinceStateChanged += changed.Add;

            session.BuyUnit(AttackerHome);

            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(AttackerHome, changed[0].Id);
            Assert.AreEqual(2, changed[0].Units.Count);
        }

        [Test]
        public void OnProvinceStateChanged_ReportsOriginAndTarget_WhenArmyMoves()
        {
            var session = CreateSession();
            var changed = new List<ProvinceState>();
            session.OnProvinceStateChanged += changed.Add;

            MoveAllMovable(session, AttackerHome, NeutralLand);

            Assert.AreEqual(2, changed.Count);
            Assert.AreEqual(AttackerHome, changed[0].Id);
            Assert.AreEqual(0, changed[0].Units.Count);
            Assert.AreEqual(NeutralLand, changed[1].Id);
            Assert.AreEqual(Attacker, changed[1].OwnerId);
        }

        [Test]
        public void OnProvinceStateChanged_IsNotRaised_WhenCommandIsRejected()
        {
            var session = CreateSession();
            int changes = 0;
            session.OnProvinceStateChanged += _ => changes++;

            session.BuyUnit(DefenderHome);
            Move(session, DefenderHome, NeutralLand, 100);

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void MoveArmy_WithSelectedUnits_LeavesUnselectedGarrisonBehind()
        {
            var session = CreateSession();
            session.BuyUnit(AttackerHome);
            int boughtUnitId = session.Provinces[AttackerHome].Units[1].Id;

            Assert.AreEqual(ActionResult.Ok, Move(session, AttackerHome, NeutralLand, boughtUnitId));

            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units[0].Id);
            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(boughtUnitId, session.Provinces[NeutralLand].Units[0].Id);
            Assert.IsTrue(session.Provinces[NeutralLand].Units[0].HasActed);
            Assert.AreEqual(1, session.CountMovableUnits(AttackerHome));
        }

        [Test]
        public void MoveArmy_IsRejected_WhenSelectedUnitIsNotInOriginProvince()
        {
            var session = CreateSession();

            Assert.AreEqual(ActionResult.InvalidUnitSelection, Move(session, AttackerHome, NeutralLand, 1, 100));

            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(0, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenSelectedUnitIsListedTwice()
        {
            var session = CreateSession();

            Assert.AreEqual(ActionResult.InvalidUnitSelection, Move(session, AttackerHome, NeutralLand, 1, 1));
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenUnitsBelongToAnotherPlayer()
        {
            var session = CreateSession();

            Assert.AreEqual(ActionResult.NoUnits, Move(session, DefenderHome, NeutralLand, 100));
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenSelectionIsEmpty()
        {
            var session = CreateSession();

            Assert.AreEqual(ActionResult.NoUnitsSelected, Move(session, AttackerHome, NeutralLand));
            Assert.AreEqual(ActionResult.NoUnitsSelected,
                session.MoveArmy(new MoveUnitsCommand(AttackerHome, NeutralLand, null)));
        }

        [Test]
        public void MoveArmy_IsRejected_WhenAnySelectedUnitHasAlreadyMoved()
        {
            var session = CreateSession();
            Move(session, AttackerHome, NeutralLand, 1);
            session.BuyUnit(NeutralLand);
            int freshUnitId = session.Provinces[NeutralLand].Units[1].Id;

            Assert.AreEqual(ActionResult.UnitsAlreadyMoved,
                Move(session, NeutralLand, DefenderHome, 1, freshUnitId));

            Assert.AreEqual(2, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void MoveArmy_UnitsCannotMoveTwiceInOneTurn()
        {
            var session = CreateSession();
            MoveAllMovable(session, AttackerHome, NeutralLand);

            Assert.AreEqual(ActionResult.UnitsAlreadyMoved, MoveAllMovable(session, NeutralLand, AttackerHome));
        }

        [Test]
        public void DemoScenario_BuyMoveRollDamage_WipingOutGarrisonCapturesProvince()
        {
            var session = CreateSession();
            CombatReport report = null;
            session.BattleResolved += battle => report = battle;
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);

            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);
            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));

            Assert.AreEqual((3, 2, 2), report.Result.AttackerRolls);
            Assert.AreEqual((1, 1, 1), report.Result.DefenderRolls);
            Assert.AreEqual(1, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties);
            Assert.IsTrue(report.ProvinceCaptured, "All defenders were eliminated while an attacker survived.");
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(0, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void InitiateBattle_WinningBattle_CapturesProvinceAndRetreatsSurvivors()
        {
            var session = CreateSession(defenderUnits: 5, middleOwner: Defender);
            _dice.AttackerRoll = (2, 1, 1);
            _dice.DefenderRoll = (1, 1, 1);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            CombatReport report = null;
            session.BattleResolved += battle => report = battle;

            // 3 units move in (leaving 1 behind at AttackerHome so 1 AP remains for InitiateBattle)
            var threeUnits = new[]
            {
                session.Provinces[AttackerHome].Units[0].Id,
                session.Provinces[AttackerHome].Units[1].Id,
                session.Provinces[AttackerHome].Units[2].Id
            };
            session.MoveArmy(new MoveUnitsCommand(AttackerHome, DefenderHome, threeUnits));
            session.EndTurn();
            session.EndTurn();
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));

            Assert.AreEqual(4, report.DefenderCasualties);
            Assert.AreEqual(3, report.AttackerCasualties);
            Assert.IsTrue(report.ProvinceCaptured);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void InitiateBattle_WinningBattle_DefendersWithNoRetreatAreLost()
        {
            var session = CreateSession(defenderUnits: 5);
            _dice.AttackerRoll = (2, 1, 1);
            _dice.DefenderRoll = (1, 1, 1);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);

            MoveAllMovable(session, AttackerHome, DefenderHome);
            session.EndTurn();
            session.EndTurn();

            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));

            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(0, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void InitiateBattle_FortifiedMilitantDefender_AbsorbsHit_AndHoldsProvince()
        {
            var session = CreateSession(defenderFort: true, defenderFaction: FactionType.Militants);
            _dice.AttackerRoll = (2, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);
            CombatReport report = null;
            session.BattleResolved += battle => report = battle;

            MoveAllMovable(session, AttackerHome, DefenderHome);
            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));

            Assert.AreEqual(0, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties);
            Assert.IsFalse(report.ProvinceCaptured);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units[0].CurrentHp);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void EndTurn_PaysRevenueAndVictoryPoints_ThenPassesTurn()
        {
            var session = CreateSession();

            session.EndTurn();

            Assert.AreEqual(Defender, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(GameSession.StartingGold + 1, session.GetPlayer(Attacker).Gold);
            Assert.AreEqual(0, session.GetPlayer(Attacker).VictoryPoints);
            Assert.AreEqual(1, session.TurnNumber);

            session.EndTurn();

            Assert.AreEqual(Attacker, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(2, session.TurnNumber);
        }

        [Test]
        public void MilitantScoring_AwardsPerThreeProvinces_FortBuild_Capture_AndCrushedRebellion()
        {
            var session = CreateSession(middleOwner: Attacker);
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);

            // 1. Build Fort (+1 VP, 1 AP)
            Assert.AreEqual(ActionResult.Ok, session.BuildFort(AttackerHome));
            Assert.AreEqual(1, session.GetPlayer(Attacker).VictoryPoints);
            Assert.AreEqual(GameSession.StartingGold - GameSession.FortCost,
                session.CurrentPlayer.Gold);

            // 2. Recruit 1 unit (2 total at AttackerHome) and move both into DefenderHome (2 AP)
            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            // 3. Capture DefenderHome -> 3 controlled provinces + 2 VP for capture (1 AP)
            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));
            Assert.AreEqual(3, session.GetPlayer(Attacker).VictoryPoints);

            // 4. EndTurn with 3 controlled provinces -> +1 VP (3 / 3 = 1)
            session.EndTurn();
            Assert.AreEqual(4, session.GetPlayer(Attacker).VictoryPoints);
        }

        [Test]
        public void MilitantScoring_CrushingOpenRebellion_AwardsTwoVp()
        {
            var session = CreateSession(defenderFaction: FactionType.Guerrillas);
            session.EndTurn();
            session.PlaceRebellionCell(AttackerHome);
            session.InciteRebellion(AttackerHome);
            session.EndTurn();

            Assert.AreEqual(ActionResult.Ok, session.MartialLaw(AttackerHome));
            Assert.AreEqual(2, session.GetPlayer(Attacker).VictoryPoints);
        }

        [Test]
        public void ZealotScoring_AwardsChurchConstruction_AndUpkeepPresenceTokens()
        {
            var session = CreateZealotSession();
            Assert.AreEqual(ActionResult.Ok, session.BuildChurch(AttackerHome));
            Assert.AreEqual(2, session.GetPlayer(Attacker).VictoryPoints);

            session.BuyMissionary(AttackerHome);
            session.Consecrate(AttackerHome);
            session.EndTurn();

            // 2 VP (Church) + 1 VP (1 Religious Presence token at upkeep) = 3 VP
            Assert.AreEqual(3, session.GetPlayer(Attacker).VictoryPoints);
        }

        [Test]
        public void GuerrillaScoring_AwardsActiveRebellionProvinces_AndDefendingRebellionZone()
        {
            var session = CreateSession(attackerFaction: FactionType.Militants,
                defenderFaction: FactionType.Guerrillas);
            session.EndTurn();
            session.PlaceRebellionCell(DefenderHome);
            session.InciteRebellion(DefenderHome);
            session.EndTurn();

            // Guerrillas earned +1 VP at the end of their turn for 1 active rebellion province
            Assert.AreEqual(1, session.GetPlayer(Defender).VictoryPoints);

            // Militants attack DefenderHome and lose -> Guerrillas earn +2 VP for defending rebellion zone
            _dice.AttackerRoll = (1, 1, 1);
            _dice.DefenderRoll = (3, 3, 3);
            MoveAllMovable(session, AttackerHome, DefenderHome);
            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));
            Assert.AreEqual(3, session.GetPlayer(Defender).VictoryPoints);
        }

        [Test]
        public void EndTurn_WithCrisisEnabled_ConvertsPlayerToGuerrillasWhilePreservingOriginalFaction()
        {
            var session = CreateSession(crisisEnabled: true, attackerVictoryPoints: 10);

            session.EndTurn();

            var player = session.GetPlayer(Attacker);
            Assert.AreEqual(FactionType.Guerrillas, player.Faction);
            Assert.AreEqual(0, player.Gold);
            Assert.AreEqual(8, player.VictoryPoints);
            Assert.AreEqual(Attacker, session.Provinces[AttackerHome].Units[0].OwnerPlayerId);
            Assert.AreEqual(FactionType.Militants, session.Provinces[AttackerHome].Units[0].OriginalFaction);
        }

        [Test]
        public void EndTurn_WithCrisisDisabled_KeepsFactionOnSmallMap()
        {
            var session = CreateSession(crisisEnabled: false);

            session.EndTurn();

            Assert.AreEqual(FactionType.Militants, session.GetPlayer(Attacker).Faction);
        }

        [Test]
        public void EndTurn_ReachingFiftyVictoryPoints_EndsGame()
        {
            var session = CreateSession(attackerVictoryPoints: 49, middleOwner: Attacker);
            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);
            session.InitiateBattle(DefenderHome, Defender);

            // 49 starting VP + 2 VP capture = 51 VP >= 50 threshold
            Assert.IsTrue(session.IsGameOver);
            Assert.AreEqual(ActionResult.GameOver, session.BuyUnit(AttackerHome));
            Assert.AreEqual(ActionResult.GameOver, session.EndTurn());
        }

        [Test]
        public void Interaction_SelectBeginMoveClickTarget_MovesArmyAndFollowsSelection()
        {
            var session = CreateSession();
            var interaction = new HudInteraction(session);

            interaction.ClickProvince(AttackerHome);
            interaction.BeginMove();

            Assert.IsTrue(interaction.IsTargetingMove);
            Assert.IsTrue(interaction.IsValidMoveTarget(NeutralLand));
            Assert.IsFalse(interaction.IsValidMoveTarget(AttackerHome));

            interaction.ClickProvince(NeutralLand);

            Assert.IsFalse(interaction.IsTargetingMove);
            Assert.AreEqual(NeutralLand, interaction.SelectedProvinceId);
            Assert.AreEqual(Attacker, session.Provinces[NeutralLand].OwnerId);
        }

        [Test]
        public void Interaction_UnitsToMove_DefaultsToAllMovableUnits()
        {
            var session = CreateSession();
            var interaction = new HudInteraction(session);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);

            interaction.ClickProvince(AttackerHome);

            Assert.AreEqual(3, interaction.MovableUnitCount);
            Assert.AreEqual(3, interaction.UnitsToMove);
        }

        [Test]
        public void Interaction_MovingFewerUnits_SplitsArmyAndLeavesGarrison()
        {
            var session = CreateSession();
            var interaction = new HudInteraction(session);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);

            interaction.ClickProvince(AttackerHome);
            interaction.DecreaseUnitsToMove();
            interaction.BeginMove();
            interaction.ClickProvince(NeutralLand);

            Assert.AreEqual(ActionResult.Ok, interaction.LastResult);
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
            Assert.AreEqual(2, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void Interaction_UnitsToMove_StaysBetweenOneAndMovableCount()
        {
            var session = CreateSession();
            var interaction = new HudInteraction(session);
            session.BuyUnit(AttackerHome);
            interaction.ClickProvince(AttackerHome);

            interaction.IncreaseUnitsToMove();
            Assert.AreEqual(2, interaction.UnitsToMove);

            interaction.DecreaseUnitsToMove();
            interaction.DecreaseUnitsToMove();
            Assert.AreEqual(1, interaction.UnitsToMove);
        }

        [Test]
        public void Interaction_SelectingAnotherProvince_ResetsUnitsToMove()
        {
            var session = CreateSession();
            var interaction = new HudInteraction(session);
            session.BuyUnit(AttackerHome);
            interaction.ClickProvince(AttackerHome);
            interaction.DecreaseUnitsToMove();

            interaction.ClickProvince(DefenderHome);
            interaction.ClickProvince(AttackerHome);

            Assert.AreEqual(2, interaction.UnitsToMove);
        }

        [Test]
        public void Interaction_BeginMoveOnForeignProvince_ReportsFailureWithoutTargeting()
        {
            var interaction = new HudInteraction(CreateSession());

            interaction.ClickProvince(DefenderHome);
            interaction.BeginMove();

            Assert.IsFalse(interaction.IsTargetingMove);
            Assert.AreEqual(ActionResult.NoUnits, interaction.LastResult);
        }

        [Test]
        public void Interaction_ClickingEmptySpace_ClearsSelectionAndTargeting()
        {
            var interaction = new HudInteraction(CreateSession());
            interaction.ClickProvince(AttackerHome);
            interaction.BeginMove();

            interaction.ClickEmptySpace();

            Assert.AreEqual(HudInteraction.NoSelection, interaction.SelectedProvinceId);
            Assert.IsFalse(interaction.IsTargetingMove);
        }

        [Test]
        public void HudText_BattleSummary_ReportsCapAndOutcome()
        {
            var session = CreateSession();
            CombatReport report = null;
            session.BattleResolved += battle => report = battle;
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);

            MoveAllMovable(session, AttackerHome, DefenderHome);
            session.InitiateBattle(DefenderHome, Defender);

            StringAssert.Contains("(7): 1 hit(s)", HudText.BattleSummary(report));
            StringAssert.Contains("Defender holds D.", HudText.BattleSummary(report));
        }

        /// <summary>
        /// Buys units until one AP is left, then spends it moving the starting unit. The bought units stay at home
        /// and can still move, so only the AP pool blocks further commands.
        /// </summary>
        private static void SpendAllActionPoints(GameSession session)
        {
            for (int i = 1; i < PlayerProfile.BaseActionPoints; i++)
            {
                Assert.AreEqual(ActionResult.Ok, session.BuyUnit(AttackerHome));
            }

            Assert.AreEqual(ActionResult.Ok, Move(session, AttackerHome, NeutralLand, 1));
            Assert.AreEqual(0, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void ActionPoints_StartAtBase_AndEachActionSpendsOne()
        {
            var session = CreateSession();
            Assert.AreEqual(PlayerProfile.BaseActionPoints, session.CurrentPlayer.ActionPoints);

            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, NeutralLand);

            Assert.AreEqual(PlayerProfile.BaseActionPoints - 2, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void ActionPoints_InitiatingBattle_SpendsOne()
        {
            var session = CreateSession();
            MoveAllMovable(session, AttackerHome, DefenderHome);

            session.InitiateBattle(DefenderHome, Defender);

            Assert.AreEqual(PlayerProfile.BaseActionPoints - 2, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void ActionPoints_RejectedCommandsDoNotSpendPoints()
        {
            var session = CreateSession();

            session.BuyUnit(DefenderHome);
            Move(session, AttackerHome, DefenderHome);

            Assert.AreEqual(PlayerProfile.BaseActionPoints, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void BuyUnit_IsRejected_WhenActionPointsAreDepleted()
        {
            var session = CreateSession();
            SpendAllActionPoints(session);
            int gold = session.CurrentPlayer.Gold;
            int units = session.Provinces[AttackerHome].Units.Count;

            Assert.AreEqual(ActionResult.NoActionPoints, session.BuyUnit(AttackerHome));

            Assert.AreEqual(gold, session.CurrentPlayer.Gold);
            Assert.AreEqual(units, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenActionPointsAreDepleted()
        {
            var session = CreateSession();
            SpendAllActionPoints(session);

            Assert.AreEqual(ActionResult.NoActionPoints, session.ValidateMoveOrigin(AttackerHome));
            Assert.AreEqual(ActionResult.NoActionPoints, MoveAllMovable(session, AttackerHome, NeutralLand));

            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 1, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void EndTurn_IsFree_AndRefillsActionPointsForNextTurn()
        {
            var session = CreateSession();
            SpendAllActionPoints(session);

            Assert.AreEqual(ActionResult.Ok, session.EndTurn());
            Assert.AreEqual(PlayerProfile.BaseActionPoints, session.CurrentPlayer.ActionPoints);
            session.EndTurn();

            Assert.AreEqual(Attacker, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(PlayerProfile.BaseActionPoints, session.CurrentPlayer.ActionPoints);
        }

        private GameSession CreateZealotSession(int defenderUnits = 1, int defenderGold = 10,
            int defenderChurchOwner = ProvinceState.NoChurch) =>
            CreateSession(defenderUnits: defenderUnits, defenderFaction: FactionType.Militants,
                attackerFaction: FactionType.Zealots, defenderGold: defenderGold,
                defenderChurchOwner: defenderChurchOwner);

        [Test]
        public void Zealots_CannotRecruitMilitaryUnits()
        {
            var session = CreateZealotSession();

            Assert.AreEqual(ActionResult.WrongFaction, session.BuyUnit(AttackerHome));

            Assert.AreEqual(GameSession.StartingGold, session.CurrentPlayer.Gold);
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void BuyMissionary_CostsThreeGold_AndIsZealotOnly()
        {
            var session = CreateZealotSession();

            Assert.AreEqual(ActionResult.Ok, session.BuyMissionary(AttackerHome));

            Assert.AreEqual(GameSession.StartingGold - 3, session.CurrentPlayer.Gold);
            Assert.AreEqual(UnitType.Missionary, session.Provinces[AttackerHome].Units[1].Type);
            Assert.AreEqual(FactionType.Zealots, session.Provinces[AttackerHome].Units[1].OriginalFaction);
            Assert.AreEqual(ActionResult.WrongFaction, CreateSession().BuyMissionary(AttackerHome));
        }

        [Test]
        public void BuyTrader_CostsTwoGold_AndIsGuildOnly()
        {
            var session = CreateSession(attackerFaction: FactionType.Guilds);
            var withPost = new ProvinceState(AttackerHome, "A", false, new[] { NeutralLand, DefenderHome }, Attacker,
                new List<UnitInstance>(), tradingPostOwnerId: Attacker);
            session = TestSessionFactory.Create(
                new[]
                {
                    new PlayerProfile(Attacker, FactionType.Guilds, GameSession.StartingGold, 0,
                        new List<UnitInstance>()),
                    new PlayerProfile(Defender, FactionType.Zealots, GameSession.StartingGold, 0,
                        new List<UnitInstance>())
                },
                new[] { withPost, session.Provinces[NeutralLand], session.Provinces[DefenderHome] },
                crisisEnabled: false,
                dice: _dice);

            Assert.AreEqual(ActionResult.Ok, session.BuyTrader(AttackerHome));

            Assert.AreEqual(GameSession.StartingGold - 2, session.CurrentPlayer.Gold);
            Assert.AreEqual(UnitType.Trader, session.Provinces[AttackerHome].Units[0].Type);
            Assert.AreEqual(FactionType.Guilds, session.Provinces[AttackerHome].Units[0].OriginalFaction);
            Assert.AreEqual(ActionResult.WrongFaction, CreateSession().BuyTrader(AttackerHome));
        }

        [Test]
        public void BuildChurch_CostsFourGold_OncePerProvince()
        {
            var session = CreateZealotSession();

            Assert.AreEqual(ActionResult.Ok, session.BuildChurch(AttackerHome));
            Assert.AreEqual(ActionResult.AlreadyBuilt, session.BuildChurch(AttackerHome));

            Assert.AreEqual(Attacker, session.Provinces[AttackerHome].ChurchOwnerId);
            Assert.AreEqual(GameSession.StartingGold - 4, session.CurrentPlayer.Gold);
            Assert.AreEqual(ActionResult.WrongFaction, CreateSession().BuildChurch(AttackerHome));
        }

        [Test]
        public void Consecrate_NeedsMissionary_AndPlacesOnePresenceToken()
        {
            var session = CreateZealotSession();
            Assert.AreEqual(ActionResult.NoMissionary, session.Consecrate(AttackerHome));
            session.BuyMissionary(AttackerHome);

            Assert.AreEqual(ActionResult.Ok, session.Consecrate(AttackerHome));
            Assert.AreEqual(ActionResult.AlreadyBuilt, session.Consecrate(AttackerHome));

            Assert.AreEqual(Attacker, session.Provinces[AttackerHome].PresenceToken.OwnerId);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 2, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void MoveArmy_MissionaryOnlyGarrison_CoexistsWithoutBattle()
        {
            var session = CreateSession();
            int battles = 0;
            session.BattleResolved += _ => battles++;
            session.EndTurn();
            Move(session, DefenderHome, NeutralLand, 100);
            session.BuyMissionary(DefenderHome);
            session.EndTurn();

            Assert.AreEqual(ActionResult.Ok, MoveAllMovable(session, AttackerHome, DefenderHome));

            Assert.AreEqual(0, battles);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId,
                "Attacker military (1 strength) takes hegemony over Missionary-only garrison (0 strength).");
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count,
                "Missionary remains co-existing in the province.");
        }

        [Test]
        public void InitiateBattle_DefendingMissionaries_DoNotFightOrRaiseHitCap()
        {
            var session = CreateSession(middleOwner: Defender);
            CombatReport report = null;
            session.BattleResolved += battle => report = battle;
            session.EndTurn();
            session.BuyMissionary(DefenderHome);
            session.BuyMissionary(DefenderHome);
            session.EndTurn();
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            _dice.AttackerRoll = (3, 3, 3);
            _dice.DefenderRoll = (1, 1, 1);

            MoveAllMovable(session, AttackerHome, DefenderHome);
            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));

            Assert.AreEqual(1, report.DefenderCount);
            Assert.AreEqual(1, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties, "Only the one soldier sets the defender's hit cap.");
            Assert.IsTrue(report.ProvinceCaptured);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(4, session.Provinces[DefenderHome].Units.Count,
                "2 surviving attackers + 2 non-combatant missionaries co-exist.");
        }

        [Test]
        public void MoveArmy_MissionariesCanEnterHostileAndNeutralProvincesFreely()
        {
            var session = CreateZealotSession();
            session.BuyMissionary(AttackerHome);
            int missionaryId = session.Provinces[AttackerHome].Units[1].Id;

            Assert.AreEqual(ActionResult.Ok, Move(session, AttackerHome, DefenderHome, missionaryId));
            Assert.AreEqual(Defender, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void DivineConversion_ConvertsInPlaceCappedAtMissionariesPlusOne_WithNoCasualties()
        {
            var session = CreateZealotSession(defenderUnits: 4);
            session.BuyMissionary(AttackerHome);
            session.BuyMissionary(AttackerHome);
            var missionaryIds = new[]
            {
                session.Provinces[AttackerHome].Units[1].Id,
                session.Provinces[AttackerHome].Units[2].Id
            };
            session.MoveArmy(new MoveUnitsCommand(AttackerHome, DefenderHome, missionaryIds));
            session.EndTurn();
            session.EndTurn();

            // Roll differential = 9 - 0 = 9, capped at (2 missionaries + 1) = 3 converts
            _dice.AttackerRoll = (3, 3, 3);
            _dice.DefenderRoll = (0, 0, 0);

            Assert.AreEqual(ActionResult.Ok, session.DivineConversion(DefenderHome));

            var zealotMilitary = session.GetUnitsForPlayer(DefenderHome, Attacker, UnitType.RegularMilitary);
            var zealotMissionaries = session.GetUnitsForPlayer(DefenderHome, Attacker, UnitType.Missionary);
            var defenderMilitary = session.GetUnitsForPlayer(DefenderHome, Defender, UnitType.RegularMilitary);

            Assert.AreEqual(3, zealotMilitary.Count, "2 missionaries + 1 cap = 3 enemy soldiers converted.");
            Assert.AreEqual(1, zealotMissionaries.Count, "1 missionary expended, 1 remains.");
            Assert.AreEqual(1, defenderMilitary.Count, "1 defender remains; zero casualties inflicted.");
            Assert.AreEqual(FactionType.Militants, zealotMilitary[0].OriginalFaction);
            Assert.IsTrue(zealotMilitary[0].HasActed);
        }

        [Test]
        public void DivineConversion_LostOrTiedContest_ExpendsMissionaryWithZeroConverts()
        {
            var session = CreateZealotSession(defenderUnits: 2);
            session.BuyMissionary(AttackerHome);
            int missionaryId = session.Provinces[AttackerHome].Units[1].Id;
            Move(session, AttackerHome, DefenderHome, missionaryId);
            session.EndTurn();
            session.EndTurn();

            _dice.AttackerRoll = (1, 1, 1);
            _dice.DefenderRoll = (1, 1, 1);

            Assert.AreEqual(ActionResult.Ok, session.DivineConversion(DefenderHome));

            Assert.AreEqual(0, session.GetUnitsForPlayer(DefenderHome, Attacker).Count);
            Assert.AreEqual(2, session.GetUnitsForPlayer(DefenderHome, Defender).Count);
            Assert.AreEqual(ActionResult.NoMissionary, session.DivineConversion(DefenderHome));
        }

        [Test]
        public void ChurchTithe_NonZealotUnitsPayChurchOwnerDuringUpkeep()
        {
            var session = CreateZealotSession(defenderUnits: 3, defenderChurchOwner: Attacker);

            session.EndTurn();

            Assert.AreEqual(Defender, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(GameSession.StartingGold - 3, session.CurrentPlayer.Gold);
            Assert.AreEqual(GameSession.StartingGold + 1 + 3, session.GetPlayer(Attacker).Gold);
            Assert.AreEqual(3, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void ChurchTithe_UnitsTheTreasuryCannotCover_AreDisbanded()
        {
            var session = CreateZealotSession(defenderUnits: 3, defenderGold: 1, defenderChurchOwner: Attacker);

            session.EndTurn();

            Assert.AreEqual(0, session.CurrentPlayer.Gold);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void Sanctuary_ChurchDoublesRebellionCellCost()
        {
            var session = CreateZealotSession(defenderChurchOwner: Attacker);

            Assert.AreEqual(4, session.GetRebellionCellCost(DefenderHome));
            Assert.AreEqual(2, session.GetRebellionCellCost(NeutralLand));
        }

        [Test]
        public void PlaceRebellionCell_CostsTwoGold_OrFourInChurchProvince()
        {
            var session = CreateSession(attackerFaction: FactionType.Guerrillas, defenderChurchOwner: Defender);

            Assert.AreEqual(ActionResult.Ok, session.PlaceRebellionCell(NeutralLand));
            Assert.AreEqual(ActionResult.Ok, session.PlaceRebellionCell(DefenderHome));
            Assert.AreEqual(ActionResult.AlreadyBuilt, session.PlaceRebellionCell(DefenderHome));

            Assert.AreEqual(GameSession.StartingGold - 2 - 4, session.CurrentPlayer.Gold);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].RebellionCell.OwnerId);
            Assert.IsFalse(session.Provinces[DefenderHome].RebellionCell.IsOpenRebellion);
            Assert.AreEqual(ActionResult.WrongFaction, CreateSession().PlaceRebellionCell(NeutralLand));
        }

        [Test]
        public void InciteRebellion_OpensOwnUndercoverCell()
        {
            var session = CreateSession(attackerFaction: FactionType.Guerrillas);
            Assert.AreEqual(ActionResult.NoRebellionCell, session.InciteRebellion(DefenderHome));
            session.PlaceRebellionCell(DefenderHome);

            Assert.AreEqual(ActionResult.Ok, session.InciteRebellion(DefenderHome));
            Assert.AreEqual(ActionResult.AlreadyBuilt, session.InciteRebellion(DefenderHome));

            Assert.IsTrue(session.Provinces[DefenderHome].RebellionCell.IsOpenRebellion);
        }

        [Test]
        public void MartialLaw_PurgesOpenRebellionInOccupiedProvince()
        {
            var session = CreateSession(defenderFaction: FactionType.Guerrillas);
            Assert.AreEqual(ActionResult.NothingToPurge, session.MartialLaw(AttackerHome));
            session.EndTurn();
            session.PlaceRebellionCell(AttackerHome);
            session.InciteRebellion(AttackerHome);
            session.EndTurn();

            Assert.AreEqual(ActionResult.Ok, session.MartialLaw(AttackerHome));

            Assert.IsNull(session.Provinces[AttackerHome].RebellionCell);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 1, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void MartialLaw_IsRejected_WithoutOccupyingMilitaryOrForOtherFactions()
        {
            var session = CreateSession();
            MoveAllMovable(session, AttackerHome, NeutralLand);

            Assert.AreEqual(ActionResult.NoMilitaryUnits, session.MartialLaw(AttackerHome));
            Assert.AreEqual(ActionResult.WrongFaction,
                CreateSession(attackerFaction: FactionType.Zealots).MartialLaw(AttackerHome));
        }

        [Test]
        public void Inquisition_PurgesReligiousPresenceToken()
        {
            var session = CreateSession(defenderUnits: 0, defenderPresence: true);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(ActionResult.Ok, session.Inquisition(DefenderHome));

            Assert.IsNull(session.Provinces[DefenderHome].PresenceToken);
        }

        [Test]
        public void CleanseProvince_OccupyingLandedArmy_DismantlesChurchAndToken()
        {
            var session = CreateSession(defenderUnits: 0, defenderChurchOwner: Defender, defenderPresence: true);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(ActionResult.Ok, session.CleanseProvince(DefenderHome));
            Assert.AreEqual(ActionResult.NothingToPurge, session.CleanseProvince(DefenderHome));

            Assert.IsFalse(session.Provinces[DefenderHome].HasChurch);
            Assert.IsNull(session.Provinces[DefenderHome].PresenceToken);
        }

        [Test]
        public void MartialLaw_CannotTargetUndercoverCell()
        {
            var session = CreateSession(defenderFaction: FactionType.Guerrillas);
            session.EndTurn();
            session.PlaceRebellionCell(AttackerHome);
            session.EndTurn();

            Assert.AreEqual(ActionResult.NothingToPurge, session.MartialLaw(AttackerHome));
            Assert.IsNotNull(session.Provinces[AttackerHome].RebellionCell);
        }

        [Test]
        public void CapturedChurch_TaxesOccupier_AndFortifiedChurchCannotBeDismantled()
        {
            var session = CreateSession(defenderUnits: 0, defenderFort: true, defenderChurchOwner: Defender,
                defenderPresence: true);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);
            session.EndTurn();
            session.EndTurn();

            Assert.AreEqual(GameSession.StartingGold - 6 + 2 - 3, session.CurrentPlayer.Gold,
                "6G spent recruiting, two provinces of revenue (+2G), three units of tithe (-3G).");
            Assert.AreEqual(ActionResult.Ok, session.CleanseProvince(DefenderHome));
            Assert.IsTrue(session.Provinces[DefenderHome].HasChurch);
            Assert.IsNull(session.Provinces[DefenderHome].PresenceToken);
        }

        [Test]
        public void GuerrillaActions_AreRejected_WhenActionPointsAreDepleted()
        {
            var session = CreateSession(attackerFaction: FactionType.Guerrillas);
            session.PlaceRebellionCell(NeutralLand);
            session.InciteRebellion(NeutralLand);
            session.PlaceRebellionCell(DefenderHome);
            session.InciteRebellion(DefenderHome);

            Assert.AreEqual(ActionResult.NoActionPoints, session.PlaceRebellionCell(AttackerHome));
            Assert.IsNull(session.Provinces[AttackerHome].RebellionCell);
        }

        [Test]
        public void ChurchTithe_GuerrillasAreExempt()
        {
            var session = CreateSession(defenderUnits: 2, defenderFaction: FactionType.Guerrillas,
                attackerFaction: FactionType.Zealots, defenderChurchOwner: Attacker);

            session.EndTurn();

            Assert.AreEqual(GameSession.StartingGold, session.CurrentPlayer.Gold);
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IntoOccupiedProvince_CoexistsWithoutAutomaticBattle()
        {
            var session = CreateSession();
            int battles = 0;
            session.BattleResolved += _ => battles++;

            Assert.AreEqual(ActionResult.Ok, MoveAllMovable(session, AttackerHome, DefenderHome));

            Assert.AreEqual(0, battles);
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(ProvinceState.NeutralOwner, session.Provinces[DefenderHome].OwnerId,
                "Equal strength (1 vs 1) results in a tie and neutral/contested hegemony.");
        }

        [Test]
        public void ProvinceHegemony_StrictMajorityOfStrengthPoints_DeterminesOwnership()
        {
            var session =
                CreateSession(defenderUnits: 1, defenderFort: true); // Defender strength = 1 unit + 1 fort = 2
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome); // Attacker now has 3 units

            // Move 2 units into DefenderHome (2 vs 2 strength -> tie -> NeutralOwner)
            var twoUnits = new[] { 1, session.Provinces[AttackerHome].Units[1].Id };
            Assert.AreEqual(ActionResult.Ok,
                session.MoveArmy(new MoveUnitsCommand(AttackerHome, DefenderHome, twoUnits)));
            Assert.AreEqual(ProvinceState.NeutralOwner, session.Provinces[DefenderHome].OwnerId);

            // Move 3rd unit into DefenderHome (3 vs 1 strength since fort is no longer Defender-owned -> Attacker takes hegemony)
            int thirdUnitId = session.Provinces[AttackerHome].Units[0].Id;
            Assert.AreEqual(ActionResult.Ok, Move(session, AttackerHome, DefenderHome, thirdUnitId));
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(4, session.Provinces[DefenderHome].Units.Count,
                "Defender unit still co-exists in the province.");
        }

        [Test]
        public void InitiateBattle_ResolvesCombatBetweenCoexistingForces_AndSpendsActionPoint()
        {
            var session = CreateSession();
            CombatReport report = null;
            session.BattleResolved += battle => report = battle;
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);

            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.IsNull(report, "Moving into DefenderHome must not trigger battle automatically.");
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 2, session.CurrentPlayer.ActionPoints);

            Assert.AreEqual(ActionResult.Ok, session.InitiateBattle(DefenderHome, Defender));

            Assert.IsNotNull(report);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 3, session.CurrentPlayer.ActionPoints);
            Assert.AreEqual(1, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void InitiateBattle_IsRejected_WhenTargetOrAttackerMilitaryMissing()
        {
            var session = CreateSession();

            // Attacker has no units in DefenderHome yet
            Assert.AreEqual(ActionResult.NoMilitaryUnits, session.InitiateBattle(DefenderHome, Defender));

            // Move to NeutralLand where Defender has no units
            MoveAllMovable(session, AttackerHome, NeutralLand);
            Assert.AreEqual(ActionResult.NoTargetUnits, session.InitiateBattle(NeutralLand, Defender));
            Assert.AreEqual(ActionResult.InvalidBattleTarget, session.InitiateBattle(NeutralLand, Attacker));
        }
    }
}