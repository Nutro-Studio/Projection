using System;
using System.Collections.Generic;

namespace Core.Economy
{
    public readonly struct GoldTransaction
    {
        public int Amount { get; }
        public int BalanceAfter { get; }
        public string Reason { get; }

        public GoldTransaction(int amount, int balanceAfter, string reason)
        {
            Amount = amount;
            BalanceAfter = balanceAfter;
            Reason = reason;
        }

        public override string ToString() => $"{(Amount >= 0 ? "+" : "")}{Amount}G ({Reason}) -> {BalanceAfter}G";
    }

    /// <summary>
    /// Faction gold. Every change goes through Deposit/TrySpend/Wipe so the ledger stays complete
    /// and the balance can never drop below zero.
    /// </summary>
    public class Treasury
    {
        private readonly List<GoldTransaction> _ledger = new List<GoldTransaction>();

        public int Gold { get; private set; }
        public IReadOnlyList<GoldTransaction> Ledger => _ledger;

        public event Action<GoldTransaction> Changed;

        public Treasury(int startingGold)
        {
            if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold));
            Gold = startingGold;
        }

        public bool CanAfford(int cost) => cost >= 0 && Gold >= cost;

        public void Deposit(int amount, string reason)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend for withdrawals.");
            if (amount == 0) return;
            Record(amount, reason);
        }

        public bool TrySpend(int cost, string reason)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (!CanAfford(cost)) return false;
            if (cost > 0) Record(-cost, reason);
            return true;
        }

        /// <summary>Takes up to <paramref name="amount"/> gold; returns how much was actually taken.</summary>
        public int WithdrawUpTo(int amount, string reason)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int taken = Math.Min(amount, Gold);
            if (taken > 0) Record(-taken, reason);
            return taken;
        }

        /// <summary>Crisis: treasury is wiped to 0.</summary>
        public void Wipe(string reason)
        {
            if (Gold > 0) Record(-Gold, reason);
        }

        /// <summary>Moves gold between treasuries, paying as much as the payer has. Returns the amount moved.</summary>
        public static int Transfer(Treasury from, Treasury to, int amount, string reason)
        {
            int paid = from.WithdrawUpTo(amount, reason);
            to.Deposit(paid, reason);
            return paid;
        }

        private void Record(int delta, string reason)
        {
            Gold += delta;
            var tx = new GoldTransaction(delta, Gold, reason);
            _ledger.Add(tx);
            Changed?.Invoke(tx);
        }
    }
}
