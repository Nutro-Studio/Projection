using System.Collections.Generic;
using Core;
using Core.Combat;
using Core.Economy;
using Core.Units;
using NUnit.Framework;

namespace Tests.Editor
{
    [TestFixture]
    public class TreasuryTests
    {
        [Test]
        public void TrySpend_FailsWithoutChangingBalance_WhenNotEnoughGold()
        {
            var treasury = new Treasury(2);
            Assert.IsFalse(treasury.TrySpend(3, "unit"));
            Assert.AreEqual(2, treasury.Gold);
            Assert.IsEmpty(treasury.Ledger);
        }

        [Test]
        public void EveryChangeIsRecordedInLedger()
        {
            var treasury = new Treasury(10);
            treasury.Deposit(8, "income");
            treasury.TrySpend(3, "unit");

            Assert.AreEqual(15, treasury.Gold);
            Assert.AreEqual(2, treasury.Ledger.Count);
            Assert.AreEqual(8, treasury.Ledger[0].Amount);
            Assert.AreEqual(-3, treasury.Ledger[1].Amount);
            Assert.AreEqual(15, treasury.Ledger[1].BalanceAfter);
        }

        [Test]
        public void Transfer_PaysOnlyWhatPayerHas()
        {
            var payer = new Treasury(1);
            var payee = new Treasury(0);
            Assert.AreEqual(1, Treasury.Transfer(payer, payee, 3, "tithe"));
            Assert.AreEqual(0, payer.Gold);
            Assert.AreEqual(1, payee.Gold);
        }

        [Test]
        public void Wipe_SetsGoldToZero()
        {
            var treasury = new Treasury(7);
            treasury.Wipe("crisis");
            Assert.AreEqual(0, treasury.Gold);
        }
    }

    [TestFixture]
    public class EconomyServiceTests
    {
        private Match _match;

        [SetUp]
        public void SetUp() => _match = new Match();

        [Test]
        public void Income_IsOneGoldPerControlledProvince()
        {
            Assert.AreEqual(8, _match.Economy.CollectIncome(_match.Militants));
            Assert.AreEqual(EconomyRules.StartingGold + 8, _match.Militants.Treasury.Gold);
        }

        [Test]
        public void Income_IsZeroForNonLandedRuleset()
        {
            _match.Militants.Ruleset = _match.Rulesets[FactionType.Guerrillas];
            Assert.AreEqual(0, _match.Economy.CollectIncome(_match.Militants));
            Assert.AreEqual(EconomyRules.StartingGold, _match.Militants.Treasury.Gold);
        }

        [Test]
        public void Recruit_MilitaryUnit_Costs3GoldAndSpawnsUnit()
        {
            var bastion = _match.Province(1, 1);
            int before = _match.State.CountUnits(_match.Militants, bastion);

            var result = _match.Economy.Recruit(_match.Militants, bastion, UnitType.Military);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(7, _match.Militants.Treasury.Gold);
            Assert.AreEqual(before + 1, _match.State.CountUnits(_match.Militants, bastion));
        }

        [Test]
        public void Recruit_FailsInForeignProvince()
        {
            var sanctum = _match.Province(5, 1);
            Assert.IsFalse(_match.Economy.Recruit(_match.Militants, sanctum, UnitType.Military).Success);
            Assert.AreEqual(EconomyRules.StartingGold, _match.Militants.Treasury.Gold);
        }

        [Test]
        public void Recruit_FailsForUnitOfAnotherArchetype()
        {
            Assert.IsFalse(_match.Economy.Recruit(_match.Militants, _match.Province(1, 1), UnitType.Missionary).Success);
        }

        [Test]
        public void Recruit_FailsWhenTreasuryIsShort()
        {
            var bastion = _match.Province(1, 1);
            for (int i = 0; i < 3; i++) _match.Economy.Recruit(_match.Militants, bastion, UnitType.Military);

            Assert.AreEqual(1, _match.Militants.Treasury.Gold);
            Assert.IsFalse(_match.Economy.Recruit(_match.Militants, bastion, UnitType.Military).Success);
        }

        [Test]
        public void Build_Fortification_Costs5AndGives1Vp_OnlyOncePerProvince()
        {
            var ironhold = _match.Province(1, 0);
            Assert.IsTrue(_match.Economy.Build(_match.Militants, ironhold, BuildingType.Fortification).Success);
            Assert.IsTrue(ironhold.HasFortification);
            Assert.AreEqual(5, _match.Militants.Treasury.Gold);
            Assert.AreEqual(1, _match.Militants.VictoryPoints);

            Assert.IsFalse(_match.Economy.Build(_match.Militants, ironhold, BuildingType.Fortification).Success);
            Assert.AreEqual(5, _match.Militants.Treasury.Gold);
        }

        [Test]
        public void Build_Church_IsZealotOnly()
        {
            Assert.IsFalse(_match.Economy.Build(_match.Militants, _match.Province(1, 0), BuildingType.Church).Success);
            Assert.IsTrue(_match.Economy.Build(_match.Zealots, _match.Province(6, 0), BuildingType.Church).Success);
            Assert.AreEqual(2, _match.Zealots.VictoryPoints);
        }

        [Test]
        public void ChurchTithe_EachForeignUnitPays1Gold()
        {
            var sanctum = _match.Province(5, 1);
            _match.State.SpawnUnit(UnitType.Military, _match.Militants, sanctum);
            _match.State.SpawnUnit(UnitType.Military, _match.Militants, sanctum);

            Assert.AreEqual(2, _match.Economy.ApplyChurchTithes(_match.Militants));
            Assert.AreEqual(8, _match.Militants.Treasury.Gold);
            Assert.AreEqual(12, _match.Zealots.Treasury.Gold);
        }

        [Test]
        public void ChurchTithe_GuerrillasAreExempt()
        {
            _match.State.SpawnUnit(UnitType.Military, _match.Militants, _match.Province(5, 1));
            _match.Militants.Ruleset = _match.Rulesets[FactionType.Guerrillas];
            Assert.AreEqual(0, _match.Economy.ApplyChurchTithes(_match.Militants));
        }
    }

    /// <summary>Returns the queued 3d4 rolls in order (attacker first, then defender), repeating the last one.</summary>
    internal class FixedDiceRoller : IDiceRoller
    {
        private readonly Queue<(int, int, int)> _rolls;
        private (int, int, int) _last;

        public FixedDiceRoller(params (int, int, int)[] rolls)
        {
            _rolls = new Queue<(int, int, int)>(rolls);
            _last = rolls[rolls.Length - 1];
        }

        public int RollD4() => Roll3D4().Item1;

        public (int roll1, int roll2, int roll3) Roll3D4()
        {
            if (_rolls.Count > 0) _last = _rolls.Dequeue();
            return _last;
        }
    }
}
