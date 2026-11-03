using System;

namespace ColourCascade.Core
{
    /// <summary>
    /// Everything monetisation, built once per game (GameContext.Monetisation). The services read the current save
    /// and wallet through delegates, so they keep working after a load or a progress reset. The Unity project starts
    /// them with the real SDKs (MonetisationHost); tests start them with scripted fakes.
    /// </summary>
    public sealed class MonetisationServices
    {
        public MonetisationServices(MonetisationConfig config, Func<SaveData> data, Func<Wallet> wallet, IClock clock,
            Func<double> monotonicSeconds, Func<bool> save, ActivityTracker activity)
        {
            Config = config ?? new MonetisationConfig();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var seconds = monotonicSeconds ?? (() => sw.Elapsed.TotalSeconds);
            Analytics = new MonetisationAnalytics(data, clock);
            Interstitials = new InterstitialPolicy(Config.interstitials, data, clock, () => NoAds, () => Config.ads.enabled);
            Ads = new AdService(Config.ads, Config.privacy, Interstitials, data, clock, seconds, Analytics, () => NoAds);
            Continue = new ContinueService(Config.ads, Ads, wallet, Analytics, Interstitials);
            Coins = new CoinOffers(Config.ads, Ads, wallet, Analytics, save);
            Purchases = new PurchaseService(Config.store, data, wallet, clock, seconds, Analytics, save, Interstitials, activity);
        }

        public MonetisationConfig Config { get; }
        public MonetisationAnalytics Analytics { get; }
        public AdService Ads { get; }
        public InterstitialPolicy Interstitials { get; }
        public ContinueService Continue { get; }
        public CoinOffers Coins { get; }
        public PurchaseService Purchases { get; }

        /// <summary>The player owns No Ads: no interstitials (rewarded adverts stay available, by choice).</summary>
        public bool NoAds => Purchases != null && Purchases.NoAdsOwned;

        /// <summary>Starts consent, the ad SDK and the store. Any of them may be null (not in this build).</summary>
        public void Start(IAdNetwork adNetwork, IConsentProvider consent, AdUnitIds adUnits, bool testAds, IStoreBackend store)
        {
            Ads.Start(adNetwork, consent, adUnits, testAds);
            Purchases.Start(store);
        }

        /// <summary>Call every frame.</summary>
        public void Update()
        {
            Ads.Update();
            Purchases.Update();
        }

        public void NotifyAppPaused(bool paused)
        {
            Ads.NotifyAppPaused(paused);
            Purchases.NotifyAppPaused(paused);
        }
    }
}
