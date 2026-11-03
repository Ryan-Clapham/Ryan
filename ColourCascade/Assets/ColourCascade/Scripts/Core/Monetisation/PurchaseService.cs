using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum StoreState
    {
        NotStarted,
        Initialising,
        Ready,
        Unavailable,
    }

    /// <summary>A product as the shop shows it: what it gives (config) and its price (from the store only).</summary>
    public sealed class StoreProduct
    {
        public string Id;
        public ProductKind Kind;
        public int Coins;
        public string Title;
        public bool Highlight;
        /// <summary>The store's localised price ("£4.99"), or null until the store has answered.</summary>
        public string Price;
        public bool Available;
    }

    public enum PurchaseStart
    {
        Started,
        StoreNotReady,
        UnknownProduct,
        ProductUnavailable,
        AlreadyOwned,
        AnotherInProgress,
        BlockedDuringChallenge,
    }

    public enum PurchaseEventKind
    {
        /// <summary>Granted. Restored = delivered at start-up or by a restore (e.g. a pending payment that completed).</summary>
        Completed,
        Pending,
        Cancelled,
        Failed,
        AlreadyOwned,
        RestoreFinished,
        NoAdsRevoked,
    }

    public struct PurchaseEvent
    {
        public PurchaseEventKind Kind;
        public string ProductId;
        public int Coins;
        public bool Restored;
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// In-app purchases (docs/MONETISATION.md §4). The store backend (Unity IAP / Google Play Billing) reports
    /// purchases here; this decides what they give:
    /// <list type="bullet">
    /// <item>A paid purchase is granted, the save is written, and only then is the purchase confirmed with the store
    /// (consumed or acknowledged). If the app dies before confirming, the store delivers it again and the saved
    /// transaction ID stops it paying twice. If the save fails, it is left unconfirmed so it is delivered again.</item>
    /// <item>Pending payments grant nothing until the store reports them paid; cancelled and failed purchases change
    /// nothing.</item>
    /// <item>No Ads is a non-consumable entitlement: owned when the store says so (on every start, after a purchase
    /// or a restore). The save keeps the last confirmed answer so the game works offline; a refund is applied when
    /// the store reports it not owned on noAdsRevokeAfterChecks separate starts.</item>
    /// </list>
    /// </summary>
    public sealed class PurchaseService : IStoreCallbacks
    {
        readonly StoreConfig config;
        readonly Func<SaveData> data;
        readonly Func<Wallet> wallet;
        readonly IClock clock;
        readonly Func<double> now;
        readonly MonetisationAnalytics analytics;
        readonly Func<bool> save;
        readonly InterstitialPolicy interstitials;
        readonly ActivityTracker activity;
        readonly List<StoreProduct> products = new List<StoreProduct>();

        IStoreBackend backend;
        string purchasing;
        double purchaseStartedAt;
        bool appPaused;
        double resumedAt;
        bool restoring;
        double restoreStartedAt;
        bool missingCountedThisSession;

        /// <summary>A restore with no answer from the store for this long (back in the game) is reported as failed.</summary>
        public const double RestoreTimeoutSeconds = 30;

        public PurchaseService(StoreConfig config, Func<SaveData> data, Func<Wallet> wallet, IClock clock, Func<double> monotonicSeconds,
            MonetisationAnalytics analytics, Func<bool> save, InterstitialPolicy interstitials, ActivityTracker activity)
        {
            this.config = config ?? new StoreConfig();
            this.data = data;
            this.wallet = wallet;
            this.clock = clock ?? new SystemClock();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            now = monotonicSeconds ?? (() => sw.Elapsed.TotalSeconds);
            this.analytics = analytics;
            this.save = save ?? (() => true);
            this.interstitials = interstitials;
            this.activity = activity;
            if (this.config.coinPacks != null)
                foreach (var p in this.config.coinPacks)
                    if (p != null && !string.IsNullOrEmpty(p.productId) && p.coins > 0 && Find(p.productId) == null)
                        products.Add(new StoreProduct { Id = p.productId, Kind = ProductKind.Consumable, Coins = p.coins, Title = p.title, Highlight = p.highlight });
            if (!string.IsNullOrEmpty(this.config.noAdsProductId) && Find(this.config.noAdsProductId) == null)
                products.Add(new StoreProduct { Id = this.config.noAdsProductId, Kind = ProductKind.NonConsumable, Title = "No Ads" });
        }

        public StoreState State { get; private set; } = StoreState.NotStarted;
        public string UnavailableReason { get; private set; } = "";
        public string BackendName => backend != null ? backend.Name : "none";
        public bool IsPurchasing => purchasing != null;
        public string PurchasingProduct => purchasing;
        public bool IsRestoring => restoring;

        public IReadOnlyList<StoreProduct> Products => products;
        public StoreProduct NoAdsProduct => Find(config.noAdsProductId);

        public IEnumerable<StoreProduct> CoinPacks
        {
            get
            {
                foreach (var p in products)
                    if (p.Kind == ProductKind.Consumable) yield return p;
            }
        }

        /// <summary>The No Ads entitlement: what the store last confirmed (kept in the save for offline starts).</summary>
        public bool NoAdsOwned => Record.NoAds;

        /// <summary>Products, prices, ownership or store state changed.</summary>
        public event Action Changed;
        /// <summary>Something the player should hear about (a completed, pending, cancelled or failed purchase...).</summary>
        public event Action<PurchaseEvent> Event;

        MonetisationRecord Record => data().Monetisation;

        public StoreProduct Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in products) if (p.Id == id) return p;
            return null;
        }

        // ------------------------------------------------------------------ Start-up

        public void Start(IStoreBackend storeBackend)
        {
            if (State != StoreState.NotStarted) return;
            if (!config.enabled)
            {
                SetUnavailable("Purchases are switched off in the config.");
                return;
            }
            backend = storeBackend;
            if (backend == null)
            {
                SetUnavailable("This build has no store.");
                return;
            }
            State = StoreState.Initialising;
            var requests = new List<StoreProductRequest>();
            foreach (var p in products) requests.Add(new StoreProductRequest { Id = p.Id, Kind = p.Kind });
            backend.Initialize(requests, this);
            Changed?.Invoke();
        }

        void SetUnavailable(string reason)
        {
            State = StoreState.Unavailable;
            UnavailableReason = reason ?? "";
        }

        public void Update()
        {
            if (restoring && !appPaused && now() - Math.Max(restoreStartedAt, resumedAt) > RestoreTimeoutSeconds)
                OnRestoreFinished(false, "The store didn't answer.");
            if (purchasing == null || appPaused) return;
            // The store sheet pauses the app while the player is in it; only time spent back in the game counts.
            double since = Math.Max(purchaseStartedAt, resumedAt);
            if (now() - since > Math.Max(5f, config.purchaseTimeoutSeconds))
            {
                string id = purchasing;
                purchasing = null;
                analytics?.Record(MonetisationEvents.PurchaseFailed, ("product", id), ("reason", "timeout"));
                Raise(PurchaseEventKind.Failed, id, 0, false, false, "The store didn't answer. If you were charged, the purchase will be added when the store confirms it.");
                Changed?.Invoke();
            }
        }

        public void NotifyAppPaused(bool paused)
        {
            appPaused = paused;
            if (!paused) resumedAt = now();
        }

        // ------------------------------------------------------------------ Buying

        public PurchaseStart Buy(string productId)
        {
            var p = Find(productId);
            if (p == null) return PurchaseStart.UnknownProduct;
            if (State != StoreState.Ready) return PurchaseStart.StoreNotReady;
            if (!ActionPolicy.ShopPurchasesAllowed(activity)) return PurchaseStart.BlockedDuringChallenge;
            if (purchasing != null || restoring) return PurchaseStart.AnotherInProgress;
            if (p.Kind == ProductKind.NonConsumable && p.Id == config.noAdsProductId && NoAdsOwned) return PurchaseStart.AlreadyOwned;
            if (!p.Available) return PurchaseStart.ProductUnavailable;
            purchasing = p.Id;
            purchaseStartedAt = now();
            analytics?.Record(MonetisationEvents.PurchaseAttempted, ("product", p.Id), ("kind", p.Kind));
            Changed?.Invoke();
            backend.Purchase(p.Id);
            return PurchaseStart.Started;
        }

        /// <summary>Asks the store for everything the player owns (the Premium tab's Restore Purchases).</summary>
        public bool Restore()
        {
            if (backend == null || State != StoreState.Ready || restoring) return false;
            restoring = true;
            restoreStartedAt = now();
            analytics?.Record(MonetisationEvents.RestoreAttempted);
            Changed?.Invoke();
            backend.Restore();
            return true;
        }

        // ------------------------------------------------------------------ Store callbacks

        public void OnStoreReady(IList<StoreProductInfo> infos)
        {
            foreach (var p in products) p.Available = false;
            if (infos != null)
                foreach (var info in infos)
                {
                    var p = info != null ? Find(info.Id) : null;
                    if (p == null) continue;
                    p.Price = string.IsNullOrEmpty(info.LocalizedPrice) ? null : info.LocalizedPrice;
                    p.Available = info.Available && p.Price != null;
                }
            State = StoreState.Ready;
            UnavailableReason = "";
            int available = 0;
            foreach (var p in products) if (p.Available) available++;
            analytics?.Record(MonetisationEvents.StoreInitialised, ("store", BackendName), ("products", available));
            Changed?.Invoke();
            // Ownership of No Ads comes from the store on every start, not from the save alone.
            if (!string.IsNullOrEmpty(config.noAdsProductId)) backend.CheckOwnership(config.noAdsProductId);
        }

        public void OnStoreUnavailable(string reason)
        {
            SetUnavailable(string.IsNullOrEmpty(reason) ? "The store isn't available." : reason);
            analytics?.Record(MonetisationEvents.StoreUnavailable, ("reason", UnavailableReason));
            if (purchasing != null)
            {
                string id = purchasing;
                purchasing = null;
                Raise(PurchaseEventKind.Failed, id, 0, false, false, "The store isn't available right now.");
            }
            restoring = false;
            Changed?.Invoke();
        }

        public void OnPurchaseSucceeded(PurchaseReceipt receipt)
        {
            if (receipt == null) return;
            var p = Find(receipt.ProductId);
            bool ours = purchasing != null && purchasing == receipt.ProductId;
            if (p == null)
            {
                // Not one of this version's products: grant nothing and don't confirm it (the store refunds unconfirmed
                // purchases), so nothing is taken from the player for an unknown item.
                analytics?.Record(MonetisationEvents.PurchaseFailed, ("product", receipt.ProductId ?? ""), ("reason", "unknown product"));
                if (ours) purchasing = null;
                Changed?.Invoke();
                return;
            }
            var rec = Record;
            string key = TransactionKey(receipt);
            if (key.Length > 0 && rec.HasTransaction(key))
            {
                // Already granted (delivered again after a crash, or reported twice): just finish it with the store.
                analytics?.Record(MonetisationEvents.PurchaseDuplicateIgnored, ("product", p.Id));
                backend?.Confirm(receipt);
                if (ours) purchasing = null;
                Changed?.Invoke();
                return;
            }
            bool restored = receipt.Restored || !ours;
            int coins = 0;
            if (p.Kind == ProductKind.Consumable)
            {
                coins = p.Coins;
                var w = wallet();
                if (w == null || !w.Credit(coins, "iap." + p.Id))
                {
                    analytics?.Record(MonetisationEvents.PurchaseFailed, ("product", p.Id), ("reason", "grant failed"));
                    if (ours) purchasing = null;
                    Changed?.Invoke();
                    return;
                }
            }
            else if (p.Id == config.noAdsProductId)
            {
                rec.NoAds = true;
                rec.NoAdsTransaction = receipt.TransactionId ?? "";
                rec.NoAdsCheckedTicks = clock.UtcNow.Ticks;
                rec.NoAdsMissingChecks = 0;
            }
            rec.AddTransaction(key);
            if (!restored) interstitials?.NoteRewardedOrPurchase();
            if (save())
            {
                backend?.Confirm(receipt);
            }
            else
            {
                // Not saved: leave the purchase unconfirmed so the store delivers it again rather than risk losing it.
                analytics?.Record(MonetisationEvents.PurchaseFailed, ("product", p.Id), ("reason", "save failed; left unconfirmed"));
            }
            analytics?.Record(MonetisationEvents.PurchaseCompleted, ("product", p.Id), ("restored", restored));
            analytics?.Record(p.Kind == ProductKind.Consumable ? MonetisationEvents.CoinPackCompleted : MonetisationEvents.NoAdsCompleted,
                ("product", p.Id), ("coins", coins), ("restored", restored));
            if (ours) purchasing = null;
            Raise(PurchaseEventKind.Completed, p.Id, coins, restored, true, "");
            Changed?.Invoke();
        }

        public void OnPurchaseDeferred(string productId)
        {
            analytics?.Record(MonetisationEvents.PurchasePending, ("product", productId ?? ""));
            if (purchasing != null && (productId == null || purchasing == productId)) purchasing = null;
            Raise(PurchaseEventKind.Pending, productId, 0, false, false, "Your payment is pending. The purchase will be added as soon as it's paid.");
            Changed?.Invoke();
        }

        public void OnPurchaseFailed(string productId, PurchaseFailure reason, string detail)
        {
            string id = productId ?? purchasing;
            if (purchasing != null && (productId == null || purchasing == productId)) purchasing = null;
            if (reason == PurchaseFailure.UserCancelled)
            {
                analytics?.Record(MonetisationEvents.PurchaseCancelled, ("product", id ?? ""));
                Raise(PurchaseEventKind.Cancelled, id, 0, false, false, "Purchase cancelled. You haven't been charged.");
            }
            else if (reason == PurchaseFailure.AlreadyOwned)
            {
                analytics?.Record(MonetisationEvents.PurchaseFailed, ("product", id ?? ""), ("reason", reason));
                Raise(PurchaseEventKind.AlreadyOwned, id, 0, false, false, "You already own this. Restoring it now.");
                if (id == config.noAdsProductId && backend != null) backend.CheckOwnership(id);
            }
            else
            {
                analytics?.Record(MonetisationEvents.PurchaseFailed, ("product", id ?? ""), ("reason", reason), ("detail", detail ?? ""));
                Raise(PurchaseEventKind.Failed, id, 0, false, false, FailureMessage(reason));
            }
            Changed?.Invoke();
        }

        public void OnOwnershipChecked(string productId, bool owned, string transactionId)
        {
            if (productId != config.noAdsProductId) return;
            var rec = Record;
            rec.NoAdsCheckedTicks = clock.UtcNow.Ticks;
            if (owned)
            {
                bool wasOwned = rec.NoAds;
                rec.NoAds = true;
                if (!string.IsNullOrEmpty(transactionId)) rec.NoAdsTransaction = transactionId;
                rec.NoAdsMissingChecks = 0;
                save();
                if (!wasOwned) Raise(PurchaseEventKind.Completed, productId, 0, true, true, "");
            }
            else if (rec.NoAds && !missingCountedThisSession)
            {
                missingCountedThisSession = true;
                rec.NoAdsMissingChecks++;
                if (rec.NoAdsMissingChecks >= Math.Max(1, config.noAdsRevokeAfterChecks))
                {
                    rec.NoAds = false;
                    rec.NoAdsMissingChecks = 0;
                    analytics?.Record(MonetisationEvents.NoAdsRevoked);
                    Raise(PurchaseEventKind.NoAdsRevoked, productId, 0, false, false, "");
                }
                save();
            }
            Changed?.Invoke();
        }

        public void OnRestoreFinished(bool success, string message)
        {
            if (!restoring) return;
            restoring = false;
            analytics?.Record(MonetisationEvents.RestoreCompleted, ("success", success));
            Raise(PurchaseEventKind.RestoreFinished, null, 0, true, success, message ?? "");
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>The ID a purchase is remembered by: the store's transaction ID, else a hash of its receipt.</summary>
        public static string TransactionKey(PurchaseReceipt r)
        {
            if (r == null) return "";
            if (!string.IsNullOrEmpty(r.TransactionId)) return "t:" + r.TransactionId;
            if (!string.IsNullOrEmpty(r.Receipt)) return "r:" + StableHash.Of(r.Receipt).ToString("x16");
            return "";
        }

        static string FailureMessage(PurchaseFailure reason)
        {
            switch (reason)
            {
                case PurchaseFailure.PaymentDeclined: return "The payment was declined. You haven't been charged.";
                case PurchaseFailure.ProductUnavailable: return "This item isn't available right now.";
                case PurchaseFailure.StoreUnavailable: return "The store isn't available right now. Check your connection and try again.";
                case PurchaseFailure.NetworkError: return "No connection to the store. Check your connection and try again.";
                case PurchaseFailure.PurchaseInProgress: return "Another purchase is still in progress.";
                default: return "The purchase didn't go through. You haven't been charged.";
            }
        }

        void Raise(PurchaseEventKind kind, string productId, int coins, bool restored, bool success, string message)
        {
            Event?.Invoke(new PurchaseEvent { Kind = kind, ProductId = productId, Coins = coins, Restored = restored, Success = success, Message = message });
        }
    }
}
