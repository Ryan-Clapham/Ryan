using System;

namespace ColourCascade.Core
{
    public enum PurchaseBlock
    {
        None = 0,
        UnknownItem,
        Locked,
        InsufficientFunds,
        ChallengeInProgress,
        InvalidQuantity,
    }

    public struct PurchaseCheck
    {
        public PurchaseBlock Block;
        public int Price;
        public int Shortfall;
        public int UnlockLevel;
        public bool Allowed => Block == PurchaseBlock.None;

        public string Message
        {
            get
            {
                switch (Block)
                {
                    case PurchaseBlock.None: return "";
                    case PurchaseBlock.Locked: return "Unlocks at level " + UnlockLevel;
                    case PurchaseBlock.InsufficientFunds: return "You need " + NumberFormat.Thousands(Shortfall) + " more coins";
                    case PurchaseBlock.ChallengeInProgress: return "The shop is closed during challenges";
                    case PurchaseBlock.InvalidQuantity: return "Invalid quantity";
                    default: return "This item is not available";
                }
            }
        }
    }

    /// <summary>Validated power-up purchases with coins. Only completed purchases deduct coins.</summary>
    public sealed class ShopService
    {
        readonly PowerUpCatalog catalog;
        readonly Wallet wallet;
        readonly Inventory inventory;
        readonly Func<int> highestUnlocked;
        readonly ActivityTracker activity;

        public ShopService(PowerUpCatalog catalog, Wallet wallet, Inventory inventory, Func<int> highestUnlocked, ActivityTracker activity)
        {
            this.catalog = catalog;
            this.wallet = wallet;
            this.inventory = inventory;
            this.highestUnlocked = highestUnlocked;
            this.activity = activity;
        }

        public event Action<PowerUpId, int> Purchased;

        public PurchaseCheck Check(PowerUpId id, int quantity = 1)
        {
            var check = new PurchaseCheck();
            var def = catalog.Get(id);
            if (def == null)
            {
                check.Block = PurchaseBlock.UnknownItem;
                return check;
            }
            check.Price = def.price * Math.Max(1, quantity);
            check.UnlockLevel = def.unlockLevel;
            if (quantity < 1 || quantity > 99) check.Block = PurchaseBlock.InvalidQuantity;
            else if (!ActionPolicy.ShopPurchasesAllowed(activity)) check.Block = PurchaseBlock.ChallengeInProgress;
            else if (!ActionPolicy.IsUnlocked(def, highestUnlocked())) check.Block = PurchaseBlock.Locked;
            else if (!wallet.CanAfford(check.Price))
            {
                check.Block = PurchaseBlock.InsufficientFunds;
                check.Shortfall = check.Price - wallet.Balance;
            }
            return check;
        }

        public PurchaseCheck TryBuy(PowerUpId id, int quantity = 1)
        {
            var check = Check(id, quantity);
            if (!check.Allowed) return check;
            if (!wallet.TryDebit(check.Price, "shop." + id + ".x" + quantity))
            {
                check.Block = PurchaseBlock.InsufficientFunds;
                check.Shortfall = check.Price - wallet.Balance;
                return check;
            }
            inventory.AddOwned(id, quantity);
            Purchased?.Invoke(id, quantity);
            return check;
        }
    }

    public enum UseBlock
    {
        None = 0,
        UnknownPowerUp,
        NotAllowedInMode,
        Locked,
        NoneOwned,
        NotPlaying,
    }

    /// <summary>Using power-ups during play: checks policy, unlock and ownership, applies the effect, then
    /// consumes one use only if the effect was actually applied.</summary>
    public sealed class PowerUpService
    {
        readonly PowerUpCatalog catalog;
        readonly Inventory inventory;
        readonly Func<int> highestUnlocked;

        public PowerUpService(PowerUpCatalog catalog, Inventory inventory, Func<int> highestUnlocked)
        {
            this.catalog = catalog;
            this.inventory = inventory;
            this.highestUnlocked = highestUnlocked;
        }

        public event Action<PowerUpId> Used;

        /// <summary>Whether this power-up should appear in the in-game bar for this mode.</summary>
        public bool IsVisibleIn(PowerUpId id, GameMode mode)
        {
            var def = catalog.Get(id);
            return ActionPolicy.IsPowerUpAllowed(def, mode) && ActionPolicy.IsUnlocked(def, highestUnlocked());
        }

        /// <summary>Checks everything except the board target. Ownership is not required when ignoreOwnership is set
        /// (used to decide whether to offer a quick purchase).</summary>
        public UseBlock CanSelect(PuzzleSession session, PowerUpId id, bool ignoreOwnership = false)
        {
            var def = catalog.Get(id);
            if (def == null || PowerUpEffects.Get(id) == null) return UseBlock.UnknownPowerUp;
            if (!ActionPolicy.IsPowerUpAllowed(def, session.Mode)) return UseBlock.NotAllowedInMode;
            if (!ActionPolicy.IsUnlocked(def, highestUnlocked())) return UseBlock.Locked;
            bool revive = id == PowerUpId.ExtraMoves && session.State == SessionState.Failed && session.EndReason == EndReason.OutOfMoves;
            if (!session.AcceptsMoves && !revive) return UseBlock.NotPlaying;
            if (!ignoreOwnership && inventory.Available(id) <= 0) return UseBlock.NoneOwned;
            return UseBlock.None;
        }

        public bool TryUse(PuzzleSession session, PowerUpRequest request, MoveOutcome outcome, out UseBlock block, out PowerUpFailure failure)
        {
            failure = PowerUpFailure.None;
            block = CanSelect(session, request.Id);
            if (block != UseBlock.None)
            {
                outcome.Reset();
                return false;
            }
            var def = catalog.Get(request.Id);
            if (request.Amount <= 0) request.Amount = def.amount;
            if (!session.TryApplyPowerUp(request, outcome, out failure)) return false;
            bool usedTrial;
            inventory.TryConsume(request.Id, out usedTrial);
            outcome.UsedTrial = usedTrial;
            Used?.Invoke(request.Id);
            return true;
        }
    }
}
