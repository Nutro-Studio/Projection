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
            int middleOwner = ProvinceState.NeutralOwner)
        {
            var players = new[]
            {
                new PlayerProfile(Attacker, FactionType.Militants, PrototypeGameSession.StartingGold,
                    attackerVictoryPoints, new List<CombatUnit>()),
                new PlayerProfile(Defender, defenderFaction, PrototypeGameSession.StartingGold, 0,
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
                    new List<CombatUnit> { new(1, FactionType.Militants, 1, 1) }),
                new ProvinceState(NeutralLand, "N", false, new[] { AttackerHome, DefenderHome },
                    middleOwner, new List<CombatUnit>()),
                new ProvinceState(DefenderHome, "D", defenderFort, new[] { AttackerHome, NeutralLand }, Defender,
                    defenders)
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
            _dice.AttackerRoll = (4, 3, 3);
            _dice.DefenderRoll = (1, 1, 1);

            session.BuyUnit(AttackerHome);
            MoveAllMovable(session, AttackerHome, DefenderHome);

            Assert.AreEqual((4, 3, 3), report.Result.AttackerRolls);
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
            _dice.AttackerRoll = (4, 3, 3);
            _dice.DefenderRoll = (1, 1, 1);

            MoveAllMovable(session, AttackerHome, DefenderHome);

            StringAssert.Contains("(10): 1 hit(s)", HudText.BattleSummary(report));
            StringAssert.Contains("Defender holds D.", HudText.BattleSummary(report));
        }
    }
}
