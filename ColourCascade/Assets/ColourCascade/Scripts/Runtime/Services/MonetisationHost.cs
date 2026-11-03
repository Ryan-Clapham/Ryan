using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Where the SDK integrations plug in (docs/MONETISATION.md §6). The AdMob and Unity IAP adapters live in their own
    /// assemblies (Scripts/Integrations), compiled only when those SDKs are installed; they register factories here
    /// before the first scene loads. With nothing registered the game runs without adverts or purchases.
    /// </summary>
    public static class MonetisationBackends
    {
        public static Func<IAdNetwork> AdNetwork;
        public static Func<IConsentProvider> Consent;
        public static Func<IStoreBackend> Store;
    }

    /// <summary>
    /// Runs SDK callbacks on Unity's main thread. Adapters post here whenever an SDK might call back on another
    /// thread; the queue is drained every frame by MonetisationHost.
    /// </summary>
    public static class MainThread
    {
        static readonly Queue<Action> queue = new Queue<Action>();
        static readonly object gate = new object();

        public static void Post(Action action)
        {
            if (action == null) return;
            lock (gate) queue.Enqueue(action);
        }

        public static void Drain()
        {
            while (true)
            {
                Action next;
                lock (gate)
                {
                    if (queue.Count == 0) return;
                    next = queue.Dequeue();
                }
                try
                {
                    next();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }

    /// <summary>
    /// Starts and runs the monetisation services in the app (consent, then the ad SDK; the store), forwards app
    /// pauses, keeps the game quiet and untouchable while an advert is on screen, and tells the player about
    /// purchases wherever they are (a pending payment may complete at the next start).
    /// </summary>
    public sealed class MonetisationHost : MonoBehaviour
    {
        public static MonetisationHost Instance { get; private set; }

        bool started;
        RectTransform blocker;
        float noAdsToastAt = -100f;

        static GameContext Ctx => GameApp.Instance.Ctx;
        static MonetisationServices M => Ctx.Monetisation;

        /// <summary>Google's test ad units are in use (always in the editor and development builds).</summary>
        public bool TestAds { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        /// <summary>Starts everything; called once the home screen is up (so a consent form appears over the game).</summary>
        public void Begin()
        {
            if (started) return;
            started = true;
            var cfg = Ctx.Config.Monetisation;
            bool test = cfg.ads.forceTestAds || Application.isEditor || Debug.isDebugBuild;
            var units = PickAdUnits(cfg.ads, ref test);
            TestAds = test;
            if (Application.isEditor || Debug.isDebugBuild) M.Analytics.Sinks.Add(new DebugLogSink());
            M.Ads.ShowingChanged += OnAdShowing;
            M.Ads.LateRewardGranted += OnLateReward;
            M.Purchases.Event += OnPurchaseEvent;
            IAdNetwork network = null;
            IConsentProvider consent = null;
            IStoreBackend store = null;
            try
            {
                network = MonetisationBackends.AdNetwork?.Invoke();
                consent = MonetisationBackends.Consent?.Invoke();
                store = MonetisationBackends.Store?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[ColourCascade] Monetisation: ads " + (network != null ? network.Name : "not installed") + (TestAds ? " (test ads)" : " (live ads)") +
                      ", consent " + (consent != null ? "UMP" : "none") + ", store " + (store != null ? store.Name : "not installed"));
            M.Start(network, consent, units, TestAds, store);
        }

        /// <summary>The platform's live ad units, or Google's test units in test mode or while the live ones are missing.</summary>
        static AdUnitIds PickAdUnits(AdConfig ads, ref bool test)
        {
#if UNITY_IOS
            var live = ads.iosLive;
            var testUnits = AdUnitIds.GoogleTestIos;
#else
            var live = ads.androidLive;
            var testUnits = AdUnitIds.GoogleTestAndroid;
#endif
            if (test) return testUnits;
            if (live == null || !live.IsComplete)
            {
                Debug.LogWarning("[ColourCascade] Live ad units aren't set in the config (Monetisation > Ads), so Google's test adverts are used. See docs/MONETISATION.md.");
                test = true;
                return testUnits;
            }
            return live;
        }

        void Update()
        {
            MainThread.Drain();
            if (started) M.Update();
        }

        void OnApplicationPause(bool paused)
        {
            if (started) M.NotifyAppPaused(paused);
        }

        // ------------------------------------------------------------------ While an advert is on screen

        void OnAdShowing(bool showing)
        {
            // The game's own sound would play over the advert's, and taps must not reach the game behind it.
            AudioListener.pause = showing;
            if (showing)
            {
                if (blocker == null)
                {
                    blocker = UI.Rect("AdBlocker", GameApp.Instance.OverlayLayer);
                    blocker.Fill();
                    var img = blocker.gameObject.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0.35f);
                    img.raycastTarget = true;
                }
                blocker.gameObject.SetActive(true);
                blocker.SetAsLastSibling();
            }
            else if (blocker != null)
            {
                blocker.gameObject.SetActive(false);
            }
        }

        void OnLateReward(string placement)
        {
            // A reward the SDK confirmed after the advert had ended (the screen that asked has already moved on).
            if (placement == RewardedPlacements.ContinueMoves) return; // the game screen resumes the level itself
            Toast.Show("Your advert reward has been added.", Icon.Gift, Theme.Gold);
        }

        // ------------------------------------------------------------------ Purchases, wherever the player is

        void OnPurchaseEvent(PurchaseEvent e)
        {
            var store = M.Purchases;
            var product = store.Find(e.ProductId);
            bool noAds = product != null && product.Kind == ProductKind.NonConsumable;
            switch (e.Kind)
            {
                case PurchaseEventKind.Completed:
                    AudioService.Sfx(SfxNames.Purchase);
                    Haptics.Medium();
                    if (noAds)
                    {
                        noAdsToastAt = Time.realtimeSinceStartup;
                        Toast.Show(e.Restored ? "No Ads restored: no adverts between levels." : "Thank you! No more adverts between levels.", Icon.Crown, Theme.Gold);
                    }
                    else Toast.Show((e.Restored ? "Purchase complete: +" : "+") + NumberFormat.Thousands(e.Coins) + " coins", Icon.Gift, Theme.Gold);
                    break;
                case PurchaseEventKind.RestoreFinished:
                    if (!e.Success) Toast.Show("Purchases couldn't be restored. Check your connection and try again.", Icon.Info, Theme.Danger);
                    else if (Time.realtimeSinceStartup - noAdsToastAt < 5f) break; // "No Ads restored" was just shown
                    else if (store.NoAdsOwned) Toast.Show("Your purchases are up to date: No Ads is active.", Icon.Check, Theme.Success);
                    else Toast.Show("There were no purchases to restore.", Icon.Info, Theme.TextSecondary);
                    break;
                case PurchaseEventKind.NoAdsRevoked:
                    Toast.Show("No Ads was refunded, so adverts between levels are back.", Icon.Info, Theme.TextSecondary);
                    break;
                case PurchaseEventKind.Pending:
                    Toast.Show(e.Message, Icon.Timer, Theme.Gold);
                    break;
                case PurchaseEventKind.Cancelled:
                    Toast.Show(e.Message, Icon.Info, Theme.TextSecondary);
                    break;
                default:
                    Toast.Show(e.Message, Icon.Info, Theme.Danger);
                    break;
            }
            GameApp.Instance.RequestSave();
        }

        /// <summary>Platform name for store wording.</summary>
        public static string StoreName
        {
            get
            {
#if UNITY_IOS
                return "the App Store";
#else
                return "Google Play";
#endif
            }
        }

        sealed class DebugLogSink : IAnalyticsSink
        {
            public void Record(string name, IReadOnlyDictionary<string, string> parameters)
            {
                var parts = new List<string>();
                foreach (var kv in parameters) parts.Add(kv.Key + "=" + kv.Value);
                Debug.Log("[Monetisation] " + name + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : ""));
            }
        }
    }
}
