using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using NUnit.Framework;
using Presentation.Hud;
using Presentation.Prototype;

namespace Tests.Editor
{
    [TestFixture]
    public class PrototypeGameSessionTests
    {
        private const int Attacker = 1;
        private const int Defender = 2;
        private const int AttackerHome = 0;
        private const int NeutralLand = 1;
        private const int DefenderHome = 2;

        private class ScriptedDiceRoller : IDiceRoller
        {
            public (int, int, int) AttackerRoll = (2, 2, 2);
            public (int, int, int) DefenderRoll = (1, 1, 1);
            private bool _defenderNext;

            public int RollD4() => 1;

            public (int roll1, int roll2, int roll3) Roll3D4()
            {
                var roll = _defenderNext ? DefenderRoll : AttackerRoll;
                _defenderNext = !_defenderNext;
                return roll;
            }
        }

        private class CoreRules : IGameRules
        {
            private readonly ICombatService _combat;
            private readonly IDiceRoller _dice;
            private readonly CrisisDirector _director;
            private readonly Dictionary<FactionType, IFactionRuleset> _rulesets;

            public CoreRules(IDiceRoller dice)
            {
                _rulesets = new Dictionary<FactionType, IFactionRuleset>
                {
                    { FactionType.Militants, new MilitantRuleset() },
                    { FactionType.Zealots, new ZealotRuleset() },
                    { FactionType.Guerrillas, new GuerrillaRuleset() },
                    { FactionType.Guilds, new GuildRuleset() }
                };
                _dice = dice;
                _combat = new RootCombatService(dice);
                _director = new CrisisDirector(_rulesets);
            }

            public CombatResult ResolveSkirmish(
                IReadOnlyList<CombatUnit> attackers,
                IReadOnlyList<CombatUnit> defenders,
                FactionType defenderFaction,
                bool hasFortification) =>
                _combat.ResolveBattle(new CombatContext(attackers, defenders, hasFortification,
                    _rulesets[defenderFaction].ReceivesFortificationBonus));

            public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces)
            {
                if (_director.CheckVictory(player)) return player;

                return _director.CanTriggerCrisis(player, controlledProvinces)
                    ? _director.ExecuteForcedCrisis(player).UpdatedProfile
                    : player;
            }

            public int CalculateRevenue(FactionType faction, int controlledProvinces) =>
                _rulesets[faction].CalculateUpkeepRevenue(controlledProvinces);

            public int RollContest()
            {
                var (roll1, roll2, roll3) = _dice.Roll3D4();
                return roll1 + roll2 + roll3;
            }
        }

        private ScriptedDiceRoller _dice;

        [SetUp]
        public void Setup()
        {
            _dice = new ScriptedDiceRoller();
        }

        private PrototypeGameSession CreateSession(
            bool crisisEnabled = false,
            int attackerVictoryPoints = 0,
            int defenderUnits = 1,
            bool defenderFort = false,
            FactionType defenderFaction = FactionType.Zealots,
            int middleOwner = ProvinceState.NeutralOwner,
            FactionType attackerFaction = FactionType.Militants,
            int defenderGold = PrototypeGameSession.StartingGold,
            int defenderChurchOwner = ProvinceState.NoChurch,
            bool defenderPresence = false)
        {
            var players = new[]
            {
                new PlayerProfile(Attacker, attackerFaction, PrototypeGameSession.StartingGold,
                    attackerVictoryPoints, new List<CombatUnit>()),
                new PlayerProfile(Defender, defenderFaction, defenderGold, 0,
                    new List<CombatUnit>())
            };

            var defenders = new List<CombatUnit>(defenderUnits);
            for (int i = 0; i < defenderUnits; i++)
            {
                defenders.Add(new CombatUnit(100 + i, defenderFaction, 1, 1));
            }

            var provinces = new[]
            {
                new ProvinceState(AttackerHome, "A", false, new[] { NeutralLand, DefenderHome }, Attacker,
                    new List<CombatUnit> { new(1, attackerFaction, 1, 1) }),
                new ProvinceState(NeutralLand, "N", false, new[] { AttackerHome, DefenderHome },
                    middleOwner, new List<CombatUnit>()),
                new ProvinceState(DefenderHome, "D", defenderFort, new[] { AttackerHome, NeutralLand }, Defender,
                    defenders, defenderChurchOwner, defenderPresence ? new ReligiousPresenceToken(Defender) : null)
            };

            return new PrototypeGameSession(new CoreRules(_dice), players, provinces, crisisEnabled);
        }

        private static CommandResult MoveAllMovable(PrototypeGameSession session, int from, int to) =>
            session.MoveArmy(new MoveArmyCommand(from, to, session.GetMovableUnitIds(from)));

        private static CommandResult Move(PrototypeGameSession session, int from, int to, params int[] unitIds) =>
            session.MoveArmy(new MoveArmyCommand(from, to, unitIds));

        [Test]
        public void BuyUnit_SpendsThreeGold_AndAddsUnitToProvince()
        {
            var session = CreateSession();

            Assert.AreEqual(CommandResult.Ok, session.BuyUnit(AttackerHome));

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

            Assert.AreEqual(CommandResult.NotEnoughGold, session.BuyUnit(AttackerHome));
            Assert.AreEqual(1, session.CurrentPlayer.Gold);
        }

        [Test]
        public void BuyUnit_IsRejected_OnForeignProvince()
        {
            var session = CreateSession();

            Assert.AreEqual(CommandResult.NotYourProvince, session.BuyUnit(DefenderHome));
            Assert.AreEqual(PrototypeGameSession.StartingGold, session.CurrentPlayer.Gold);
        }

        [Test]
        public void MoveArmy_IntoEmptyProvince_TransfersOwnershipWithoutBattle()
        {
            var session = CreateSession();
            int battles = 0;
            session.BattleResolved += _ => battles++;

            Assert.AreEqual(CommandResult.Ok, MoveAllMovable(session, AttackerHome, NeutralLand));

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

            Assert.AreEqual(CommandResult.Ok, Move(session, AttackerHome, NeutralLand, boughtUnitId));

            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units[0].Id);
            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(boughtUnitId, session.Provinces[NeutralLand].Units[0].Id);
            Assert.AreEqual(1, session.CountMovableUnits(AttackerHome));
        }

        [Test]
        public void MoveArmy_IsRejected_WhenSelectedUnitIsNotInOriginProvince()
        {
            var session = CreateSession();

            Assert.AreEqual(CommandResult.InvalidUnitSelection, Move(session, AttackerHome, NeutralLand, 1, 100));

            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(0, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenSelectedUnitIsListedTwice()
        {
            var session = CreateSession();

            Assert.AreEqual(CommandResult.InvalidUnitSelection, Move(session, AttackerHome, NeutralLand, 1, 1));
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenUnitsBelongToAnotherPlayer()
        {
            var session = CreateSession();

            Assert.AreEqual(CommandResult.NotYourProvince, Move(session, DefenderHome, NeutralLand, 100));
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenSelectionIsEmpty()
        {
            var session = CreateSession();

            Assert.AreEqual(CommandResult.NoUnitsSelected, Move(session, AttackerHome, NeutralLand));
            Assert.AreEqual(CommandResult.NoUnitsSelected,
                session.MoveArmy(new MoveArmyCommand(AttackerHome, NeutralLand, null)));
        }

        [Test]
        public void MoveArmy_IsRejected_WhenAnySelectedUnitHasAlreadyMoved()
        {
            var session = CreateSession();
            Move(session, AttackerHome, NeutralLand, 1);
            session.BuyUnit(NeutralLand);
            int freshUnitId = session.Provinces[NeutralLand].Units[1].Id;

            Assert.AreEqual(CommandResult.UnitsAlreadyMoved,
                Move(session, NeutralLand, DefenderHome, 1, freshUnitId));

            Assert.AreEqual(2, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
        }

        [Test]
        public void MoveArmy_UnitsCannotMoveTwiceInOneTurn()
        {
            var session = CreateSession();
            MoveAllMovable(session, AttackerHome, NeutralLand);

            Assert.AreEqual(CommandResult.UnitsAlreadyMoved, MoveAllMovable(session, NeutralLand, AttackerHome));
        }

        [Test]
        public void DemoScenario_BuyMoveRollDamage_TieKeepsOwner_ThenEmptyProvinceIsCaptured()
        {
            var session = CreateSession();
            BattleReport report = null;
            session.BattleResolved += battle => report = battle;
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);

            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual((3, 2, 2), report.Result.AttackerRolls);
            Assert.AreEqual((1, 1, 1), report.Result.DefenderRolls);
            Assert.AreEqual(1, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties);
            Assert.IsFalse(report.ProvinceCaptured, "Equal casualties go to the defender.");
            Assert.AreEqual(Defender, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);

            session.EndTurn();
            session.EndTurn();
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
        }

        [Test]
        public void MoveArmy_WinningBattle_CapturesProvinceAndRetreatsSurvivors()
        {
            var session = CreateSession(defenderUnits: 5, middleOwner: Defender);
            _dice.AttackerRoll = (2, 1, 1);
            _dice.DefenderRoll = (1, 1, 1);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            BattleReport report = null;
            session.BattleResolved += battle => report = battle;

            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(4, report.DefenderCasualties);
            Assert.AreEqual(3, report.AttackerCasualties);
            Assert.IsTrue(report.ProvinceCaptured);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void MoveArmy_WinningBattle_DefendersWithNoRetreatAreLost()
        {
            var session = CreateSession(defenderUnits: 5);
            _dice.AttackerRoll = (2, 1, 1);
            _dice.DefenderRoll = (1, 1, 1);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);
            session.BuyUnit(AttackerHome);

            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(0, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void MoveArmy_FortifiedMilitantDefender_AbsorbsHit_AndHoldsProvince()
        {
            var session = CreateSession(defenderFort: true, defenderFaction: FactionType.Militants);
            _dice.AttackerRoll = (2, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);
            BattleReport report = null;
            session.BattleResolved += battle => report = battle;

            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(0, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties);
            Assert.IsFalse(report.ProvinceCaptured);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units[0].CurrentHp);
            Assert.AreEqual(0, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void EndTurn_PaysRevenueAndVictoryPoints_ThenPassesTurn()
        {
            var session = CreateSession();

            session.EndTurn();

            Assert.AreEqual(Defender, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(PrototypeGameSession.StartingGold + 1, session.GetPlayer(Attacker).Gold);
            Assert.AreEqual(1, session.GetPlayer(Attacker).VictoryPoints);
            Assert.AreEqual(1, session.TurnNumber);

            session.EndTurn();

            Assert.AreEqual(Attacker, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(2, session.TurnNumber);
        }

        [Test]
        public void EndTurn_WithCrisisEnabled_ConvertsPlayerAndProvinceUnitsToGuerrillas()
        {
            var session = CreateSession(crisisEnabled: true, attackerVictoryPoints: 10);

            session.EndTurn();

            var player = session.GetPlayer(Attacker);
            Assert.AreEqual(FactionType.Guerrillas, player.Faction);
            Assert.AreEqual(0, player.Gold);
            Assert.AreEqual(8, player.VictoryPoints);
            Assert.AreEqual(FactionType.Guerrillas, session.Provinces[AttackerHome].Units[0].Faction);
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
            var session = CreateSession(attackerVictoryPoints: 49);

            session.EndTurn();

            Assert.IsTrue(session.IsGameOver);
            Assert.AreEqual(CommandResult.GameOver, session.BuyUnit(AttackerHome));
            Assert.AreEqual(CommandResult.GameOver, session.EndTurn());
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
        public void Interaction_BeginMoveOnForeignProvince_ReportsFailureWithoutTargeting()
        {
            var interaction = new HudInteraction(CreateSession());

            interaction.ClickProvince(DefenderHome);
            interaction.BeginMove();

            Assert.IsFalse(interaction.IsTargetingMove);
            Assert.AreEqual(CommandResult.NotYourProvince, interaction.LastResult);
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
            BattleReport report = null;
            session.BattleResolved += battle => report = battle;
            _dice.AttackerRoll = (3, 2, 2);
            _dice.DefenderRoll = (1, 1, 1);

            MoveAllMovable(session, AttackerHome, DefenderHome);

            StringAssert.Contains("(7): 1 hit(s)", HudText.BattleSummary(report));
            StringAssert.Contains("Defender holds D.", HudText.BattleSummary(report));
        }

        /// <summary>
        /// Buys units until one AP is left, then spends it moving the starting unit. The bought units stay at home
        /// and can still move, so only the AP pool blocks further commands.
        /// </summary>
        private static void SpendAllActionPoints(PrototypeGameSession session)
        {
            for (int i = 1; i < PlayerProfile.BaseActionPoints; i++)
            {
                Assert.AreEqual(CommandResult.Ok, session.BuyUnit(AttackerHome));
            }

            Assert.AreEqual(CommandResult.Ok, Move(session, AttackerHome, NeutralLand, 1));
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

            Assert.AreEqual(PlayerProfile.BaseActionPoints - 1, session.CurrentPlayer.ActionPoints);
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

            Assert.AreEqual(CommandResult.NoActionPoints, session.BuyUnit(AttackerHome));

            Assert.AreEqual(gold, session.CurrentPlayer.Gold);
            Assert.AreEqual(units, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void MoveArmy_IsRejected_WhenActionPointsAreDepleted()
        {
            var session = CreateSession();
            SpendAllActionPoints(session);

            Assert.AreEqual(CommandResult.NoActionPoints, session.ValidateMoveOrigin(AttackerHome));
            Assert.AreEqual(CommandResult.NoActionPoints, MoveAllMovable(session, AttackerHome, NeutralLand));

            Assert.AreEqual(1, session.Provinces[NeutralLand].Units.Count);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 1, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void EndTurn_IsFree_AndRefillsActionPointsForNextTurn()
        {
            var session = CreateSession();
            SpendAllActionPoints(session);

            Assert.AreEqual(CommandResult.Ok, session.EndTurn());
            Assert.AreEqual(PlayerProfile.BaseActionPoints, session.CurrentPlayer.ActionPoints);
            session.EndTurn();

            Assert.AreEqual(Attacker, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(PlayerProfile.BaseActionPoints, session.CurrentPlayer.ActionPoints);
        }

        private PrototypeGameSession CreateZealotSession(int defenderUnits = 1, int defenderGold = 10,
            int defenderChurchOwner = ProvinceState.NoChurch) =>
            CreateSession(defenderUnits: defenderUnits, defenderFaction: FactionType.Militants,
                attackerFaction: FactionType.Zealots, defenderGold: defenderGold,
                defenderChurchOwner: defenderChurchOwner);

        [Test]
        public void Zealots_CannotRecruitMilitaryUnits()
        {
            var session = CreateZealotSession();

            Assert.AreEqual(CommandResult.WrongFaction, session.BuyUnit(AttackerHome));

            Assert.AreEqual(PrototypeGameSession.StartingGold, session.CurrentPlayer.Gold);
            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
        }

        [Test]
        public void BuyMissionary_CostsThreeGold_AndIsZealotOnly()
        {
            var session = CreateZealotSession();

            Assert.AreEqual(CommandResult.Ok, session.BuyMissionary(AttackerHome));

            Assert.AreEqual(PrototypeGameSession.StartingGold - 3, session.CurrentPlayer.Gold);
            Assert.AreEqual(UnitKind.Missionary, session.Provinces[AttackerHome].Units[1].Kind);
            Assert.AreEqual(CommandResult.WrongFaction, CreateSession().BuyMissionary(AttackerHome));
        }

        [Test]
        public void BuildChurch_CostsFourGold_OncePerProvince()
        {
            var session = CreateZealotSession();

            Assert.AreEqual(CommandResult.Ok, session.BuildChurch(AttackerHome));
            Assert.AreEqual(CommandResult.AlreadyBuilt, session.BuildChurch(AttackerHome));

            Assert.AreEqual(Attacker, session.Provinces[AttackerHome].ChurchOwnerId);
            Assert.AreEqual(PrototypeGameSession.StartingGold - 4, session.CurrentPlayer.Gold);
            Assert.AreEqual(CommandResult.WrongFaction, CreateSession().BuildChurch(AttackerHome));
        }

        [Test]
        public void Consecrate_NeedsMissionary_AndPlacesOnePresenceToken()
        {
            var session = CreateZealotSession();
            Assert.AreEqual(CommandResult.NoMissionary, session.Consecrate(AttackerHome));
            session.BuyMissionary(AttackerHome);

            Assert.AreEqual(CommandResult.Ok, session.Consecrate(AttackerHome));
            Assert.AreEqual(CommandResult.AlreadyBuilt, session.Consecrate(AttackerHome));

            Assert.AreEqual(Attacker, session.Provinces[AttackerHome].PresenceToken.OwnerId);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 2, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void MoveArmy_MissionariesCannotConquer_InheritedMilitaryCan()
        {
            var session = CreateZealotSession();
            session.BuyMissionary(AttackerHome);
            int missionaryId = session.Provinces[AttackerHome].Units[1].Id;

            Assert.AreEqual(CommandResult.MissionariesCannotConquer,
                Move(session, AttackerHome, NeutralLand, missionaryId));
            Assert.AreEqual(CommandResult.MissionariesCannotConquer,
                Move(session, AttackerHome, NeutralLand, 1, missionaryId));
            Assert.AreEqual(CommandResult.Ok, Move(session, AttackerHome, NeutralLand, 1));
            Assert.AreEqual(CommandResult.Ok, Move(session, AttackerHome, NeutralLand, missionaryId));

            Assert.AreEqual(Attacker, session.Provinces[NeutralLand].OwnerId);
            Assert.AreEqual(2, session.Provinces[NeutralLand].Units.Count);
        }

        [Test]
        public void MoveArmy_MissionaryOnlyGarrison_FallsWithoutBattle_AndMissionariesRetreat()
        {
            var session = CreateSession();
            int battles = 0;
            session.BattleResolved += _ => battles++;
            session.EndTurn();
            Move(session, DefenderHome, NeutralLand, 100);
            session.BuyMissionary(DefenderHome);
            session.EndTurn();

            Assert.AreEqual(CommandResult.Ok, MoveAllMovable(session, AttackerHome, DefenderHome));

            Assert.AreEqual(0, battles);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].OwnerId);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(2, session.Provinces[NeutralLand].Units.Count, "The Missionary falls back.");
        }

        [Test]
        public void MoveArmy_DefendingMissionaries_DoNotFightOrRaiseHitCap()
        {
            var session = CreateSession();
            BattleReport report = null;
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

            Assert.AreEqual(1, report.DefenderCount);
            Assert.AreEqual(1, report.DefenderCasualties);
            Assert.AreEqual(1, report.AttackerCasualties, "Only the one soldier sets the defender's hit cap.");
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(UnitKind.Missionary, session.Provinces[DefenderHome].Units[0].Kind);
        }

        [Test]
        public void DivineConversion_WonContest_ConvertsUpToTwoUnitsAndExpendsMissionary()
        {
            var session = CreateZealotSession(defenderUnits: 3);
            session.BuyMissionary(AttackerHome);
            _dice.AttackerRoll = (3, 3, 3);
            _dice.DefenderRoll = (1, 1, 1);

            Assert.AreEqual(CommandResult.Ok, session.DivineConversion(AttackerHome, DefenderHome));

            var home = session.Provinces[AttackerHome].Units;
            Assert.AreEqual(3, home.Count, "Inherited unit plus two converts; the Missionary is spent.");
            Assert.AreEqual(100, home[1].Id);
            Assert.AreEqual(FactionType.Zealots, home[1].Faction);
            Assert.AreEqual(UnitKind.Military, home[1].Kind);
            Assert.AreEqual(1, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(1, session.CountMovableUnits(AttackerHome), "Converts cannot move this turn.");
        }

        [Test]
        public void DivineConversion_LostContest_StillExpendsMissionary()
        {
            var session = CreateZealotSession(defenderUnits: 2);
            session.BuyMissionary(AttackerHome);
            _dice.AttackerRoll = (1, 1, 1);
            _dice.DefenderRoll = (1, 1, 1);

            Assert.AreEqual(CommandResult.Ok, session.DivineConversion(AttackerHome, DefenderHome));

            Assert.AreEqual(1, session.Provinces[AttackerHome].Units.Count);
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count);
            Assert.AreEqual(CommandResult.NoMissionary, session.DivineConversion(AttackerHome, DefenderHome));
        }

        [Test]
        public void ChurchTithe_NonZealotUnitsPayChurchOwnerDuringUpkeep()
        {
            var session = CreateZealotSession(defenderUnits: 3, defenderChurchOwner: Attacker);

            session.EndTurn();

            Assert.AreEqual(Defender, session.CurrentPlayer.PlayerId);
            Assert.AreEqual(PrototypeGameSession.StartingGold - 3, session.CurrentPlayer.Gold);
            Assert.AreEqual(PrototypeGameSession.StartingGold + 1 + 3, session.GetPlayer(Attacker).Gold);
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

            Assert.AreEqual(CommandResult.Ok, session.PlaceRebellionCell(NeutralLand));
            Assert.AreEqual(CommandResult.Ok, session.PlaceRebellionCell(DefenderHome));
            Assert.AreEqual(CommandResult.AlreadyBuilt, session.PlaceRebellionCell(DefenderHome));

            Assert.AreEqual(PrototypeGameSession.StartingGold - 2 - 4, session.CurrentPlayer.Gold);
            Assert.AreEqual(Attacker, session.Provinces[DefenderHome].RebellionCell.OwnerId);
            Assert.IsFalse(session.Provinces[DefenderHome].RebellionCell.IsOpenRebellion);
            Assert.AreEqual(CommandResult.WrongFaction, CreateSession().PlaceRebellionCell(NeutralLand));
        }

        [Test]
        public void InciteRebellion_OpensOwnUndercoverCell()
        {
            var session = CreateSession(attackerFaction: FactionType.Guerrillas);
            Assert.AreEqual(CommandResult.NoRebellionCell, session.InciteRebellion(DefenderHome));
            session.PlaceRebellionCell(DefenderHome);

            Assert.AreEqual(CommandResult.Ok, session.InciteRebellion(DefenderHome));
            Assert.AreEqual(CommandResult.AlreadyBuilt, session.InciteRebellion(DefenderHome));

            Assert.IsTrue(session.Provinces[DefenderHome].RebellionCell.IsOpenRebellion);
        }

        [Test]
        public void MartialLaw_PurgesOpenRebellionInOccupiedProvince()
        {
            var session = CreateSession(defenderFaction: FactionType.Guerrillas);
            Assert.AreEqual(CommandResult.NothingToPurge, session.MartialLaw(AttackerHome));
            session.EndTurn();
            session.PlaceRebellionCell(AttackerHome);
            session.InciteRebellion(AttackerHome);
            session.EndTurn();

            Assert.AreEqual(CommandResult.Ok, session.MartialLaw(AttackerHome));

            Assert.IsNull(session.Provinces[AttackerHome].RebellionCell);
            Assert.AreEqual(PlayerProfile.BaseActionPoints - 1, session.CurrentPlayer.ActionPoints);
        }

        [Test]
        public void MartialLaw_IsRejected_WithoutOccupyingMilitaryOrForOtherFactions()
        {
            var session = CreateSession();
            MoveAllMovable(session, AttackerHome, NeutralLand);

            Assert.AreEqual(CommandResult.NoMilitaryUnits, session.MartialLaw(AttackerHome));
            Assert.AreEqual(CommandResult.WrongFaction,
                CreateSession(attackerFaction: FactionType.Zealots).MartialLaw(AttackerHome));
        }

        [Test]
        public void Inquisition_PurgesReligiousPresenceToken()
        {
            var session = CreateSession(defenderUnits: 0, defenderPresence: true);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(CommandResult.Ok, session.Inquisition(DefenderHome));

            Assert.IsNull(session.Provinces[DefenderHome].PresenceToken);
        }

        [Test]
        public void CleanseProvince_OccupyingLandedArmy_DismantlesChurchAndToken()
        {
            var session = CreateSession(defenderUnits: 0, defenderChurchOwner: Defender, defenderPresence: true);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual(CommandResult.Ok, session.CleanseProvince(DefenderHome));
            Assert.AreEqual(CommandResult.NothingToPurge, session.CleanseProvince(DefenderHome));

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

            Assert.AreEqual(CommandResult.NothingToPurge, session.MartialLaw(AttackerHome));
            Assert.IsNotNull(session.Provinces[AttackerHome].RebellionCell);
        }

        [Test]
        public void CapturedChurch_TaxesOccupier_AndFortifiedChurchCannotBeDismantled()
        {
            var session = CreateSession(defenderUnits: 0, defenderFort: true, defenderChurchOwner: Defender,
                defenderPresence: true);
            MoveAllMovable(session, AttackerHome, DefenderHome);
            session.EndTurn();
            session.EndTurn();

            Assert.AreEqual(PrototypeGameSession.StartingGold + 2 - 1, session.CurrentPlayer.Gold,
                "Two provinces of revenue, one unit of tithe.");
            Assert.AreEqual(CommandResult.Ok, session.CleanseProvince(DefenderHome));
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

            Assert.AreEqual(CommandResult.NoActionPoints, session.PlaceRebellionCell(AttackerHome));
            Assert.IsNull(session.Provinces[AttackerHome].RebellionCell);
        }

        [Test]
        public void ChurchTithe_GuerrillasAreExempt()
        {
            var session = CreateSession(defenderUnits: 2, defenderFaction: FactionType.Guerrillas,
                attackerFaction: FactionType.Zealots, defenderChurchOwner: Attacker);

            session.EndTurn();

            Assert.AreEqual(PrototypeGameSession.StartingGold, session.CurrentPlayer.Gold);
            Assert.AreEqual(2, session.Provinces[DefenderHome].Units.Count);
        }
    }
}
