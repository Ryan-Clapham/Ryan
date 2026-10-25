using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// The only code that changes the coin balance. Every change is validated and recorded in the ledger
    /// (time, amount, resulting balance and reason) so economy bugs can be diagnosed.
    /// </summary>
    public sealed class Wallet
    {
        readonly SaveData data;
        readonly IClock clock;
        readonly int ledgerCapacity;

        public Wallet(SaveData data, IClock clock, int ledgerCapacity = 200)
        {
            this.data = data;
            this.clock = clock;
            this.ledgerCapacity = Math.Max(10, ledgerCapacity);
        }

        /// <summary>(previous balance, new balance)</summary>
        public event Action<int, int> Changed;

        public int Balance => data.Coins;
        public IReadOnlyList<LedgerEntry> Ledger => data.Ledger;

        public bool CanAfford(int amount) => amount >= 0 && data.Coins >= amount;

        public bool Credit(int amount, string reason)
        {
            if (amount <= 0) return false;
            int before = data.Coins;
            long after = (long)before + amount;
            data.Coins = after > int.MaxValue ? int.MaxValue : (int)after;
            Record(data.Coins - before, reason);
            Changed?.Invoke(before, data.Coins);
            return true;
        }

        public bool TryDebit(int amount, string reason)
        {
            if (amount <= 0 || amount > data.Coins) return false;
            int before = data.Coins;
            data.Coins -= amount;
            Record(-amount, reason);
            Changed?.Invoke(before, data.Coins);
            return true;
        }

        void Record(int delta, string reason)
        {
            data.Ledger.Add(new LedgerEntry { Ticks = clock.UtcNow.Ticks, Delta = delta, Balance = data.Coins, Reason = reason ?? "" });
            if (data.Ledger.Count > ledgerCapacity) data.Ledger.RemoveRange(0, data.Ledger.Count - ledgerCapacity);
        }
    }

    /// <summary>Owned power-ups plus free trial uses. Trials are spent before purchased stock.</summary>
    public sealed class Inventory
    {
        readonly SaveData data;

        public Inventory(SaveData data)
        {
            this.data = data;
        }

        public event Action<PowerUpId> Changed;

        public int Owned(PowerUpId id) => data.GetOrCreateInventory(id).Owned;
        public int Trials(PowerUpId id) => data.GetOrCreateInventory(id).Trials;
        public int Available(PowerUpId id)
        {
            var e = data.GetOrCreateInventory(id);
            return e.Owned + e.Trials;
        }

        public void AddOwned(PowerUpId id, int count)
        {
            if (count <= 0) return;
            data.GetOrCreateInventory(id).Owned += count;
            Changed?.Invoke(id);
        }

        public void AddTrials(PowerUpId id, int count)
        {
            if (count <= 0) return;
            data.GetOrCreateInventory(id).Trials += count;
            Changed?.Invoke(id);
        }

        public bool TryConsume(PowerUpId id, out bool usedTrial)
        {
            var e = data.GetOrCreateInventory(id);
            usedTrial = false;
            if (e.Trials > 0)
            {
                e.Trials--;
                usedTrial = true;
            }
            else if (e.Owned > 0)
            {
                e.Owned--;
            }
            else
            {
                return false;
            }
            Changed?.Invoke(id);
            return true;
        }
    }
}
