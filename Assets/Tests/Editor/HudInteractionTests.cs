using System.Collections.Generic;
using Core.Combat;
using NUnit.Framework;
using Presentation.Hud;
using Presentation.Prototype;

namespace Tests.Editor
{
    [TestFixture]
    public class HudInteractionTests
    {
        private const int MilitantHome = 0;
        private const int Frontier = 1;
        private const int ZealotHome = 2;

        private ScriptedDiceRoller _dice;
        private PrototypeGameSession _session;
        private HudInteraction _interaction;
        private List<HudAction> _actions;

        [SetUp]
        public void Setup()
        {
            _dice = new ScriptedDiceRoller();
            _actions = new List<HudAction>();
        }

        private void CreateGame(int zealotSoldiers = 0)
        {
            var seeds = new[]
            {
                new ProvinceSeed(MilitantHome, "M", true, new[] { Frontier, ZealotHome },
                    PrototypeSetup.MilitantsPlayerId, 2, rebellionCellOwnerId: PrototypeSetup.GuerrillasPlayerId),
                new ProvinceSeed(Frontier, "F", false, new[] { MilitantHome, ZealotHome },
                    ProvinceState.NeutralOwner, 0),
                new ProvinceSeed(ZealotHome, "Z", false, new[] { MilitantHome, Frontier },
                    PrototypeSetup.ZealotsPlayerId, zealotSoldiers, missionaries: 1, hasChurch: true)
            };

            var players = PrototypeSetup.CreatePlayers();
            _session = new PrototypeGameSession(new CoreRules(_dice), players,
                PrototypeSetup.BuildProvinces(seeds, players), false);
            _interaction = new HudInteraction(_session);
        }

        private List<HudAction> ActionsIn(int provinceId)
        {
            _interaction.ClickProvince(provinceId);
            _interaction.GetAvailableActions(_actions);
            return _actions;
        }

        private static bool Has(List<HudAction> actions, HudActionKind kind)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Kind == kind) return true;
            }

            return false;
        }

        private int CountUnits(int provinceId, int playerId, UnitType type) =>
            _session.GetUnitsForPlayer(provinceId, playerId, type).Count;

        [Test]
        public void Actions_Militants_OfferRecruitmentOnlyInTheirProvince()
        {
            CreateGame();

            Assert.IsTrue(Has(ActionsIn(MilitantHome), HudActionKind.BuyUnit));
            Assert.IsFalse(Has(ActionsIn(MilitantHome), HudActionKind.BuyMissionary));
            Assert.AreEqual(0, ActionsIn(ZealotHome).Count);
        }

        [Test]
        public void Actions_Zealots_ReplaceRecruitmentWithMissionaryChurchAndConversion()
        {
            CreateGame();
            _session.EndTurn();

            var actions = ActionsIn(ZealotHome);

            Assert.IsFalse(Has(actions, HudActionKind.BuyUnit));
            Assert.IsTrue(Has(actions, HudActionKind.BuyMissionary));
            Assert.IsFalse(Has(actions, HudActionKind.BuildChurch), "The province already has a Church.");
            Assert.IsTrue(Has(actions, HudActionKind.Consecrate));
            Assert.IsTrue(Has(actions, HudActionKind.DivineConversion));
        }

        [Test]
        public void Actions_Guerrillas_OfferInciteOnTheirCellAndPlaceCellElsewhere()
        {
            CreateGame();
            _session.EndTurn();
            _session.EndTurn();

            Assert.IsTrue(Has(ActionsIn(MilitantHome), HudActionKind.InciteRebellion));
            Assert.IsFalse(Has(ActionsIn(MilitantHome), HudActionKind.PlaceRebellionCell));
            Assert.IsTrue(Has(ActionsIn(Frontier), HudActionKind.PlaceRebellionCell));
        }

        [Test]
        public void MartialLaw_IsOfferedOnlyOnceTheRebellionIsOpen_AndPurgesIt()
        {
            CreateGame();
            Assert.IsFalse(Has(ActionsIn(MilitantHome), HudActionKind.MartialLaw), "Undercover cells are hidden.");

            _session.EndTurn();
            _session.EndTurn();
            _interaction.ClickProvince(MilitantHome);
            _interaction.Execute(new HudAction(HudActionKind.InciteRebellion));
            Assert.IsTrue(_session.Provinces[MilitantHome].RebellionCell.IsOpenRebellion);
            _session.EndTurn();

            Assert.IsTrue(Has(ActionsIn(MilitantHome), HudActionKind.MartialLaw));
            _interaction.Execute(new HudAction(HudActionKind.MartialLaw));

            Assert.AreEqual(CommandResult.Ok, _interaction.LastResult);
            Assert.IsNull(_session.Provinces[MilitantHome].RebellionCell);
        }

        [Test]
        public void Attack_IsOfferedPerCoexistingEnemy_AndStartsTheBattle()
        {
            CreateGame(zealotSoldiers: 1);
            BattleReport report = null;
            _session.BattleResolved += battle => report = battle;
            Assert.IsFalse(Has(ActionsIn(MilitantHome), HudActionKind.Attack));

            _interaction.BeginMove();
            _interaction.ClickProvince(ZealotHome);
            var actions = ActionsIn(ZealotHome);

            Assert.IsTrue(Has(actions, HudActionKind.Attack));
            int attackIndex = actions.FindIndex(a => a.Kind == HudActionKind.Attack);
            Assert.AreEqual(PrototypeSetup.ZealotsPlayerId, actions[attackIndex].TargetPlayerId);

            _interaction.Execute(actions[attackIndex]);

            Assert.AreEqual(CommandResult.Ok, _interaction.LastResult);
            Assert.IsNotNull(report);
            Assert.AreEqual(PrototypeSetup.ZealotsPlayerId, report.DefenderId);
        }

        [Test]
        public void DivineConversion_TargetsAnAdjacentEnemy_AndConvertsUnits()
        {
            CreateGame();
            _session.EndTurn();
            _interaction.ClickProvince(ZealotHome);

            _interaction.Execute(new HudAction(HudActionKind.DivineConversion));

            Assert.IsTrue(_interaction.IsTargetingConversion);
            Assert.IsTrue(_interaction.IsValidTarget(MilitantHome));
            Assert.IsFalse(_interaction.IsValidTarget(Frontier), "No enemy units to convert there.");

            _interaction.ClickProvince(MilitantHome);

            Assert.AreEqual(CommandResult.Ok, _interaction.LastResult);
            Assert.IsFalse(_interaction.IsTargetingConversion);
            Assert.AreEqual(0, CountUnits(ZealotHome, PrototypeSetup.ZealotsPlayerId, UnitType.Missionary));
            Assert.AreEqual(2, CountUnits(ZealotHome, PrototypeSetup.ZealotsPlayerId, UnitType.RegularMilitary));
        }

        [Test]
        public void SplitByType_MovesSoldiersAndLeavesTheMissionaryBehind()
        {
            CreateGame(zealotSoldiers: 1);
            _session.EndTurn();
            _interaction.ClickProvince(ZealotHome);
            Assert.AreEqual(1, _interaction.GetMovableUnitCount(UnitType.Missionary));

            _interaction.DecreaseUnitsToMove(UnitType.Missionary);
            _interaction.BeginMove();
            _interaction.ClickProvince(Frontier);

            Assert.AreEqual(CommandResult.Ok, _interaction.LastResult);
            Assert.AreEqual(1, CountUnits(Frontier, PrototypeSetup.ZealotsPlayerId, UnitType.RegularMilitary));
            Assert.AreEqual(0, CountUnits(Frontier, PrototypeSetup.ZealotsPlayerId, UnitType.Missionary));
            Assert.AreEqual(1, CountUnits(ZealotHome, PrototypeSetup.ZealotsPlayerId, UnitType.Missionary));
        }

        [Test]
        public void RequestShift_NeedsASecondClick_ThenChangesTheRuleset()
        {
            CreateGame();

            _interaction.RequestShift(FactionType.Zealots);

            Assert.AreEqual(FactionType.Zealots, _interaction.PendingShift);
            Assert.AreEqual(FactionType.Militants, _session.CurrentPlayer.Faction);

            _interaction.RequestShift(FactionType.Zealots);

            Assert.IsNull(_interaction.PendingShift);
            Assert.AreEqual(CommandResult.Ok, _interaction.LastResult);
            Assert.AreEqual(FactionType.Zealots, _session.CurrentPlayer.Faction);
            Assert.AreEqual(1, CountUnits(MilitantHome, PrototypeSetup.MilitantsPlayerId, UnitType.RegularMilitary),
                "A Militant shift loses half of the military units.");
        }

        [Test]
        public void RequestShift_IsCancelledByEscape()
        {
            CreateGame();
            _interaction.RequestShift(FactionType.Guilds);

            _interaction.CancelTargeting();

            Assert.IsNull(_interaction.PendingShift);
            Assert.AreEqual(FactionType.Militants, _session.CurrentPlayer.Faction);
        }

        [Test]
        public void Shifts_AreNotAvailableToGuerrillasWithoutFiveProvinces()
        {
            CreateGame();
            _session.EndTurn();
            _session.EndTurn();

            Assert.IsFalse(_session.CanShiftRuleset(FactionType.Militants));
            Assert.IsFalse(_session.CanDeclareAscension());
            Assert.AreEqual(CommandResult.ShiftNotAllowed, _session.DeclareAscension(FactionType.Militants));
        }

        [Test]
        public void HudText_Garrison_ListsCoexistingUnitsPerPlayerAndType()
        {
            CreateGame(zealotSoldiers: 1);
            _interaction.ClickProvince(MilitantHome);
            _interaction.BeginMove();
            _interaction.ClickProvince(ZealotHome);

            string garrison = HudText.Garrison(_session.Provinces[ZealotHome], _session);

            StringAssert.Contains("P2 Zealots: 1 Soldiers, 1 Missionaries", garrison);
            StringAssert.Contains("P1 Militants: 2 Soldiers", garrison);
        }
    }
}
