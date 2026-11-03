using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>Names of the monetisation events (docs/MONETISATION.md §8). Stable strings: dashboards depend on them.</summary>
    public static class MonetisationEvents
    {
        public const string ConsentResult = "consent_result";
        public const string AdsInitialised = "ads_initialised";

        public const string RewardedOffered = "rewarded_offered";
        public const string RewardedLoaded = "rewarded_loaded";
        public const string RewardedLoadFailed = "rewarded_load_failed";
        public const string RewardedStarted = "rewarded_started";
        public const string RewardedCompleted = "rewarded_completed";
        public const string RewardGranted = "reward_granted";
        public const string RewardNotApplied = "reward_not_applied";
        public const string RewardDuplicateIgnored = "reward_duplicate_ignored";
        public const string RewardedClosedEarly = "rewarded_closed_early";
        public const string RewardedFailed = "rewarded_failed";

        public const string InterstitialLoaded = "interstitial_loaded";
        public const string InterstitialLoadFailed = "interstitial_load_failed";
        public const string InterstitialShown = "interstitial_shown";
        public const string InterstitialFailed = "interstitial_failed";
        public const string InterstitialSkipped = "interstitial_skipped";

        public const string ExtraMovesAd = "extra_moves_ad";
        public const string ExtraMovesCoins = "extra_moves_coins";
        public const string CoinsAd = "coins_ad";
        public const string ShortfallOffered = "shortfall_offered";
        public const string ShortfallAd = "shortfall_ad";

        public const string StoreInitialised = "store_initialised";
        public const string StoreUnavailable = "store_unavailable";
        public const string PurchaseAttempted = "purchase_attempted";
        public const string PurchaseCompleted = "purchase_completed";
        public const string PurchaseCancelled = "purchase_cancelled";
        public const string PurchaseFailed = "purchase_failed";
        public const string PurchasePending = "purchase_pending";
        public const string PurchaseDuplicateIgnored = "purchase_duplicate_ignored";
        public const string CoinPackCompleted = "coin_pack_completed";
        public const string NoAdsCompleted = "no_ads_completed";
        public const string NoAdsRevoked = "no_ads_revoked";
        public const string RestoreAttempted = "restore_attempted";
        public const string RestoreCompleted = "restore_completed";
    }

    /// <summary>Where monetisation events go. Add an implementation to send them to an analytics service later.</summary>
    public interface IAnalyticsSink
    {
        void Record(string name, IReadOnlyDictionary<string, string> parameters);
    }

    public struct AnalyticsEntry
    {
        public long Ticks;
        public string Name;
        public IReadOnlyDictionary<string, string> Parameters;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Parameters != null) foreach (var kv in Parameters) parts.Add(kv.Key + "=" + kv.Value);
            return Name + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "");
        }
    }

    /// <summary>
    /// Lightweight monetisation event log. Keeps the most recent events in memory (the developer tools show them),
    /// counts every event in the save (no personal data, nothing leaves the device) and forwards events to any
    /// registered sinks. Parameters are short, non-identifying values: placement, product ID, reason, amounts.
    /// </summary>
    public sealed class MonetisationAnalytics
    {
        public const int RecentCapacity = 120;

        readonly Func<SaveData> data;
        readonly IClock clock;
        readonly List<AnalyticsEntry> recent = new List<AnalyticsEntry>();

        public MonetisationAnalytics(Func<SaveData> data, IClock clock)
        {
            this.data = data;
            this.clock = clock ?? new SystemClock();
        }

        public readonly List<IAnalyticsSink> Sinks = new List<IAnalyticsSink>();

        /// <summary>Most recent events, oldest first.</summary>
        public IReadOnlyList<AnalyticsEntry> Recent => recent;

        public int Count(string name)
        {
            var d = data?.Invoke();
            int n;
            return d != null && d.Monetisation.EventCounts.TryGetValue(name, out n) ? n : 0;
        }

        public void Record(string name, params (string key, object value)[] parameters)
        {
            if (string.IsNullOrEmpty(name)) return;
            var p = new Dictionary<string, string>();
            if (parameters != null)
                foreach (var kv in parameters)
                    if (!string.IsNullOrEmpty(kv.key)) p[kv.key] = Convert.ToString(kv.value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            var entry = new AnalyticsEntry { Ticks = clock.UtcNow.Ticks, Name = name, Parameters = p };
            recent.Add(entry);
            if (recent.Count > RecentCapacity) recent.RemoveRange(0, recent.Count - RecentCapacity);
            var d = data?.Invoke();
            if (d != null)
            {
                int n;
                d.Monetisation.EventCounts.TryGetValue(name, out n);
                d.Monetisation.EventCounts[name] = n + 1;
            }
            foreach (var sink in Sinks)
            {
                try
                {
                    sink.Record(name, p);
                }
                catch (Exception)
                {
                    // A failing analytics sink must never affect rewards or purchases.
                }
            }
        }
    }
}
