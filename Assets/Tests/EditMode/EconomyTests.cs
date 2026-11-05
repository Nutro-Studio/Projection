using System.Linq;
using NUnit.Framework;
using Projection.Core;
using Projection.Core.Economy;
using Projection.Core.Units;

namespace Projection.Tests
{
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
            CollectionAssert.AreEqual(new[] { 8, -3 }, treasury.Ledger.Select(t => t.Amount));
            Assert.AreEqual(15, treasury.Ledger.Last().BalanceAfter);
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
        public void Recruit_MilitaryUnit_Costs3GoldAndSpawnsUnit()
        {
            var bastion = _match.Province(1, 1);
            int before = _match.State.UnitsOf(_match.Militants, bastion).Count();

            var result = _match.Economy.Recruit(_match.Militants, bastion, UnitType.Military);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(7, _match.Militants.Treasury.Gold);
            Assert.AreEqual(before + 1, _match.State.UnitsOf(_match.Militants, bastion).Count());
        }

        [Test]
        public void Recruit_FailsInForeignProvince()
        {
            var sanctum = _match.Province(5, 1);
            Assert.IsFalse(_match.Economy.Recruit(_match.Militants, sanctum, UnitType.Military).Success);
            Assert.AreEqual(10, _match.Militants.Treasury.Gold);
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
            _match.Militants.Archetype = FactionArchetype.Guerrillas;
            Assert.AreEqual(0, _match.Economy.ApplyChurchTithes(_match.Militants));
        }
    }
}
