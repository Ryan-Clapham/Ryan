using System;

namespace ColourCascade.Core
{
    /// <summary>
    /// Every monetisation rule in one place (docs/MONETISATION.md): rewarded adverts, interstitial frequency, the
    /// store's products and the privacy settings passed to the ad SDK. Part of the game config asset, so values can
    /// be tuned without code changes. Nothing here is a secret: ad unit IDs and product IDs are public identifiers.
    /// </summary>
    [Serializable]
    public sealed class MonetisationConfig
    {
        public AdConfig ads = new AdConfig();
        public InterstitialConfig interstitials = new InterstitialConfig();
        public StoreConfig store = new StoreConfig();
        public PrivacyConfig privacy = new PrivacyConfig();
    }

    /// <summary>One ad unit per format, for one platform.</summary>
    [Serializable]
    public sealed class AdUnitIds
    {
        public string rewarded = "";
        public string interstitial = "";

        public bool IsComplete => !string.IsNullOrEmpty(rewarded) && !string.IsNullOrEmpty(interstitial);

        /// <summary>Google's public test ad units (they always fill and never pay; safe for development).</summary>
        public static AdUnitIds GoogleTestAndroid => new AdUnitIds
        {
            rewarded = "ca-app-pub-3940256099942544/5224354917",
            interstitial = "ca-app-pub-3940256099942544/1033173712",
        };

        public static AdUnitIds GoogleTestIos => new AdUnitIds
        {
            rewarded = "ca-app-pub-3940256099942544/1712485313",
            interstitial = "ca-app-pub-3940256099942544/4411468910",
        };
    }

    /// <summary>Limits for one rewarded-advert opportunity ("placement"). Zero means no limit.</summary>
    [Serializable]
    public sealed class RewardedPlacementConfig
    {
        public bool enabled = true;
        /// <summary>What one completed advert gives: moves, or coins (for the shortfall offer, the most it gives).</summary>
        public int reward;
        /// <summary>Completed adverts allowed per day (UTC, the game's daily reset); 0 = no limit.</summary>
        public int dailyLimit;
        /// <summary>Completed adverts allowed per level attempt (Out of Moves only); 0 = no limit.</summary>
        public int perRunLimit;
        /// <summary>Seconds after a completed advert before this offer is available again; 0 = none.</summary>
        public int cooldownSeconds;

        public RewardedPlacementConfig Clone() => (RewardedPlacementConfig)MemberwiseClone();
    }

    [Serializable]
    public sealed class AdConfig
    {
        /// <summary>Master switch for every advert (rewarded and interstitial).</summary>
        public bool enabled = true;
        /// <summary>
        /// Use Google's test ad units even in release builds. Leave on until the live ad units below are filled in and
        /// the app is approved in AdMob. Editor and development builds always use test ads.
        /// </summary>
        public bool forceTestAds = true;
        /// <summary>Live ad units from the AdMob console (empty until created; see docs/MONETISATION.md).</summary>
        public AdUnitIds androidLive = new AdUnitIds();
        public AdUnitIds iosLive = new AdUnitIds();

        /// <summary>Out of Moves: watch an advert for this many extra moves.</summary>
        public RewardedPlacementConfig continueMoves = new RewardedPlacementConfig { reward = 10, perRunLimit = 2 };
        /// <summary>Shop, Coins tab: watch an advert for coins.</summary>
        public RewardedPlacementConfig shopCoins = new RewardedPlacementConfig { reward = 50, dailyLimit = 5, cooldownSeconds = 30 };
        /// <summary>Not enough coins for a power-up: an advert pays the shortfall, up to `reward` coins.</summary>
        public RewardedPlacementConfig coinShortfall = new RewardedPlacementConfig { reward = 50, dailyLimit = 5 };

        /// <summary>Out of Moves: spend this many coins for continueCoinMoves extra moves.</summary>
        public int continueCoinCost = 100;
        public int continueCoinMoves = 5;
        /// <summary>Coin continues allowed per level attempt; 0 = no limit.</summary>
        public int continueCoinPerRunLimit;

        /// <summary>
        /// Seconds to wait after an advert closes for its reward callback (some ad networks send it just after the
        /// close). Nothing is granted unless the SDK reports the reward.
        /// </summary>
        public float rewardGraceSeconds = 2f;
        /// <summary>Seconds without any answer from the SDK after Show before the attempt counts as failed.</summary>
        public float showTimeoutSeconds = 15f;
        /// <summary>Seconds back in the game after an advert, with no close callback, before it counts as closed.</summary>
        public float resumeWatchdogSeconds = 8f;
        /// <summary>
        /// Longest an open advert can keep the game waiting when the app never went to the background (iOS, the
        /// editor): after this it counts as closed even without the SDK's close callback.
        /// </summary>
        public float maxShowSeconds = 180f;
        /// <summary>Retry delays after a failed load: starts here and doubles up to retryMaxSeconds.</summary>
        public int retryFirstSeconds = 10;
        public int retryMaxSeconds = 300;
    }

    [Serializable]
    public sealed class InterstitialConfig
    {
        public bool enabled = true;
        /// <summary>An interstitial may show after this many successfully completed campaign levels.</summary>
        public int everyCompletedLevels = 5;
        /// <summary>Minimum seconds between two interstitials, whatever the level count.</summary>
        public int minSecondsBetween = 300;
        /// <summary>No interstitial within this many seconds of a rewarded advert or a purchase.</summary>
        public int quietSecondsAfterRewardOrPurchase = 120;
    }

    [Serializable]
    public sealed class CoinPackConfig
    {
        /// <summary>The product ID created in Google Play Console (and App Store Connect) for this pack.</summary>
        public string productId = "";
        public string title = "";
        public int coins;
        /// <summary>Highlighted in the shop as the best value.</summary>
        public bool highlight;
    }

    [Serializable]
    public sealed class StoreConfig
    {
        /// <summary>Master switch for in-app purchases (the Coins packs and the Premium tab).</summary>
        public bool enabled = true;
        /// <summary>Consumable coin packs. Prices come from the store, never from here.</summary>
        public CoinPackConfig[] coinPacks = DefaultCoinPacks();
        /// <summary>The permanent No Ads product (non-consumable).</summary>
        public string noAdsProductId = "no_ads";
        /// <summary>
        /// Store checks in a row (on separate app starts) that must report No Ads as not owned before a saved
        /// purchase is withdrawn (a refund). One check can be wrong when the store is briefly out of date.
        /// </summary>
        public int noAdsRevokeAfterChecks = 2;
        /// <summary>Seconds without any answer from the store after a purchase starts before the shop unlocks again.</summary>
        public float purchaseTimeoutSeconds = 120f;

        public static CoinPackConfig[] DefaultCoinPacks() => new[]
        {
            new CoinPackConfig { productId = "coins_small", title = "Handful of Coins", coins = 500 },
            new CoinPackConfig { productId = "coins_medium", title = "Pouch of Coins", coins = 1200 },
            new CoinPackConfig { productId = "coins_large", title = "Chest of Coins", coins = 2750, highlight = true },
            new CoinPackConfig { productId = "coins_premium", title = "Vault of Coins", coins = 6000 },
        };

        public CoinPackConfig FindPack(string productId)
        {
            if (coinPacks == null || string.IsNullOrEmpty(productId)) return null;
            foreach (var p in coinPacks)
                if (p != null && p.productId == productId) return p;
            return null;
        }
    }

    [Serializable]
    public sealed class PrivacyConfig
    {
        /// <summary>Web address of the privacy policy (required by Google Play for apps with ads); empty hides the link.</summary>
        public string privacyPolicyUrl = "";
        /// <summary>Highest ad content rating allowed: G, PG, T or MA. PG suits an all-ages puzzle game.</summary>
        public string maxAdContentRating = "PG";
        /// <summary>
        /// Only set these if the game is directed at children (Google Play target audience under 13) or the player is
        /// known to be under the age of consent. See docs/MONETISATION.md before changing.
        /// </summary>
        public bool childDirected;
        public bool underAgeOfConsent;
        /// <summary>Development builds only: pretend the device is in the EEA so the consent form can be tested.</summary>
        public bool debugGeographyEea;
        /// <summary>Hashed device IDs that get test ads and the debug consent geography (shown in the device log).</summary>
        public string[] testDeviceIds = new string[0];
    }
}
