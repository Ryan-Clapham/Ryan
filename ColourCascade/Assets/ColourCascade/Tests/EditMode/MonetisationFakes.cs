using System;
using System.Collections.Generic;
using ColourCascade.Core;

namespace ColourCascade.Tests
{
    // Scripted stand-ins for the ad, consent and store SDKs. They are NOT the SDKs: they only replay the callbacks
    // the real SDKs are documented to make, in whatever order a test asks for, so the game's own rules (rewards
    // granted exactly once, purchases granted before they are confirmed, interstitial timing) can be tested.

    public sealed class FakeAdNetwork : IAdNetwork
    {
        public string Name => "fake";
        public bool InitSucceeds = true;
        public bool AnswerInitImmediately = true;
        public Action<bool, string> PendingInit;
        public int InitCalls;
        public AdNetworkSettings Settings;

        public readonly Dictionary<AdFormat, bool> LoadSucceeds = new Dictionary<AdFormat, bool> { [AdFormat.Rewarded] = true, [AdFormat.Interstitial] = true };
        public readonly Dictionary<AdFormat, bool> Ready = new Dictionary<AdFormat, bool> { [AdFormat.Rewarded] = false, [AdFormat.Interstitial] = false };
        public readonly Dictionary<AdFormat, int> LoadCalls = new Dictionary<AdFormat, int> { [AdFormat.Rewarded] = 0, [AdFormat.Interstitial] = 0 };
        public readonly List<string> LoadedUnits = new List<string>();

        public int ShowCalls;
        public AdFormat LastShownFormat;
        public AdShowCallbacks Last;

        public void Initialize(AdNetworkSettings settings, Action<bool, string> done)
        {
            InitCalls++;
            Settings = settings;
            if (AnswerInitImmediately) done(InitSucceeds, InitSucceeds ? null : "init failed");
            else PendingInit = done;
        }

        public void Load(AdFormat format, string adUnitId, Action<bool, string> done)
        {
            LoadCalls[format]++;
            LoadedUnits.Add(adUnitId);
            bool ok = LoadSucceeds[format];
            Ready[format] = ok;
            done(ok, ok ? null : "no fill");
        }

        public bool IsReady(AdFormat format) => Ready[format];

        public void Show(AdFormat format, AdShowCallbacks callbacks)
        {
            ShowCalls++;
            LastShownFormat = format;
            Ready[format] = false;
            Last = callbacks;
        }

        public void Open() => Last.Opened?.Invoke();
        public void Earn() => Last.Earned?.Invoke();
        public void Close() => Last.Closed?.Invoke();
        public void Fail(string error = "failed to show") => Last.Failed?.Invoke(error);

        /// <summary>A complete rewarded advert: opened, watched to the end, closed.</summary>
        public void WatchToTheEnd()
        {
            Open();
            Earn();
            Close();
        }
    }

    public sealed class FakeConsent : IConsentProvider
    {
        public bool CanRequestAds { get; set; }
        public bool PrivacyOptionsRequired { get; set; }
        public bool AnswerImmediately = true;
        /// <summary>What the consent form ends with (the player's answer, or an error).</summary>
        public ConsentOutcome Answer = new ConsentOutcome { CanRequestAds = true };
        public Action<ConsentOutcome> Pending;
        public ConsentRequest LastRequest;
        public int GatherCalls, PrivacyOptionsShown;
        public bool? PrivacyOptionsChangeTo;

        public void Gather(ConsentRequest request, Action<ConsentOutcome> done)
        {
            GatherCalls++;
            LastRequest = request;
            if (AnswerImmediately) Respond(done);
            else Pending = done;
        }

        public void Respond(Action<ConsentOutcome> done = null)
        {
            CanRequestAds = Answer.CanRequestAds;
            (done ?? Pending)?.Invoke(Answer);
            Pending = null;
        }

        public void ShowPrivacyOptions(Action<string> done)
        {
            PrivacyOptionsShown++;
            if (PrivacyOptionsChangeTo.HasValue) CanRequestAds = PrivacyOptionsChangeTo.Value;
            done(null);
        }
    }

    public sealed class FakeStore : IStoreBackend
    {
        public string Name => "fake store";
        public bool ReadyImmediately = true;
        public IStoreCallbacks Callbacks;
        public IList<StoreProductRequest> Requested;
        public readonly Dictionary<string, string> Prices = new Dictionary<string, string>();
        public readonly Dictionary<string, bool> Owned = new Dictionary<string, bool>();
        public readonly List<string> Purchases = new List<string>();
        public readonly List<PurchaseReceipt> Confirmed = new List<PurchaseReceipt>();
        public int Restores, OwnershipChecks;
        int nextOrder;

        public void Initialize(IList<StoreProductRequest> products, IStoreCallbacks callbacks)
        {
            Requested = products;
            Callbacks = callbacks;
            if (ReadyImmediately) BecomeReady();
        }

        public void BecomeReady()
        {
            var infos = new List<StoreProductInfo>();
            foreach (var p in Requested)
            {
                string price;
                if (!Prices.TryGetValue(p.Id, out price)) price = "£0.99";
                infos.Add(new StoreProductInfo { Id = p.Id, LocalizedPrice = price, Available = price != null });
            }
            Callbacks.OnStoreReady(infos);
        }

        /// <summary>Called as a purchase is confirmed (to check what had been saved by then).</summary>
        public Action<PurchaseReceipt> OnConfirm;

        public void Purchase(string productId) => Purchases.Add(productId);

        public void Confirm(PurchaseReceipt receipt)
        {
            OnConfirm?.Invoke(receipt);
            Confirmed.Add(receipt);
        }

        /// <summary>When false, Restore never reports back (a store that doesn't answer).</summary>
        public bool AnswerRestore = true;

        public void Restore()
        {
            Restores++;
            if (!AnswerRestore) return;
            foreach (var kv in Owned)
                if (kv.Value) Callbacks.OnPurchaseSucceeded(new PurchaseReceipt { ProductId = kv.Key, TransactionId = "GPA.owned." + kv.Key, Restored = true });
            Callbacks.OnRestoreFinished(true, "");
        }

        public void CheckOwnership(string productId)
        {
            OwnershipChecks++;
            bool owned;
            Owned.TryGetValue(productId, out owned);
            Callbacks.OnOwnershipChecked(productId, owned, owned ? "GPA.owned." + productId : null);
        }

        /// <summary>The store reports the purchase paid (what Google Play does once the player completes it).</summary>
        public PurchaseReceipt Pay(string productId, string transactionId = null, bool restored = false)
        {
            var r = new PurchaseReceipt { ProductId = productId, TransactionId = transactionId ?? "GPA." + (++nextOrder), Receipt = "{receipt}", Restored = restored };
            Callbacks.OnPurchaseSucceeded(r);
            return r;
        }
    }

    /// <summary>A game context whose monetisation runs on the fakes, with a hand-driven monotonic clock.</summary>
    public sealed class MonetisationRig
    {
        public readonly MemorySaveStorage Storage;
        public readonly FixedClock Clock;
        public double Seconds;
        public GameContext Ctx;
        public FakeAdNetwork Network = new FakeAdNetwork();
        public FakeConsent Consent = new FakeConsent { Answer = new ConsentOutcome { CanRequestAds = true } };
        public FakeStore Store = new FakeStore();
        public readonly List<PurchaseEvent> PurchaseEvents = new List<PurchaseEvent>();

        public MonetisationRig(Action<GameConfigData> configure = null, MemorySaveStorage storage = null, FixedClock clock = null)
        {
            Storage = storage ?? new MemorySaveStorage();
            Clock = clock ?? new FixedClock(TestUtil.Today);
            var config = GameConfigData.CreateDefault();
            configure?.Invoke(config);
            Ctx = new GameContext(config, Storage, Clock, () => Seconds);
            Ctx.Load();
            Ctx.Monetisation.Purchases.Event += e => PurchaseEvents.Add(e);
        }

        public MonetisationServices M => Ctx.Monetisation;

        public MonetisationRig Start(bool ads = true, bool store = true)
        {
            M.Start(ads ? Network : null, ads ? Consent : null, AdUnitIds.GoogleTestAndroid, true, store ? Store : null);
            return this;
        }

        /// <summary>Lets time pass (both clocks) and runs the per-frame update.</summary>
        public void Wait(double seconds)
        {
            Seconds += seconds;
            Clock.Advance(TimeSpan.FromSeconds(seconds));
            M.Update();
        }

        /// <summary>The same save loaded into a fresh game (an app restart), on fresh fakes.</summary>
        public MonetisationRig Restart(Action<GameConfigData> configure = null)
        {
            Ctx.Save();
            var next = new MonetisationRig(configure, Storage, Clock);
            next.Seconds = Seconds;
            return next;
        }
    }
}
