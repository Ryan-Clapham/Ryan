using System;

namespace ColourCascade.Core
{
    /// <summary>Moments the game could show an interstitial. Only the two after a won level are natural breaks.</summary>
    public enum AdTransition
    {
        /// <summary>Results screen of a won level, going to the next level.</summary>
        LevelWonNext,
        /// <summary>Results screen of a won level, going to the map or home.</summary>
        LevelWonExit,
        LevelFailed,
        LevelRetry,
        LevelAbandoned,
        Gameplay,
    }

    public enum InterstitialDecision
    {
        Show,
        /// <summary>Not a natural break (failure, retry, abandoning, gameplay): never.</summary>
        NotATransition,
        Disabled,
        NoAds,
        NotEnoughLevels,
        /// <summary>Fewer than minSecondsBetween since the last interstitial.</summary>
        TooSoon,
        /// <summary>A rewarded advert or a purchase happened moments ago.</summary>
        QuietAfterRewardOrPurchase,
        /// <summary>The opportunity is due but no advert is loaded: it waits for the next break.</summary>
        NotLoaded,
    }

    /// <summary>
    /// When interstitials may appear (docs/MONETISATION.md §3), as one testable rule. Counts successfully completed
    /// campaign levels; once everyCompletedLevels is reached, an interstitial may show at the next natural break
    /// after a won level, provided minSecondsBetween has passed since the last one, nothing was watched or bought in
    /// the quiet period, No Ads isn't owned and an advert is loaded. Otherwise the opportunity is kept for the next
    /// break. The counter resets only when an interstitial is actually displayed. Everything is saved.
    /// </summary>
    public sealed class InterstitialPolicy
    {
        readonly InterstitialConfig config;
        readonly Func<SaveData> data;
        readonly IClock clock;
        readonly Func<bool> noAds;
        readonly Func<bool> adsEnabled;

        public InterstitialPolicy(InterstitialConfig config, Func<SaveData> data, IClock clock, Func<bool> noAds, Func<bool> adsEnabled = null)
        {
            this.config = config ?? new InterstitialConfig();
            this.data = data;
            this.clock = clock ?? new SystemClock();
            this.noAds = noAds ?? (() => false);
            this.adsEnabled = adsEnabled ?? (() => true);
        }

        MonetisationRecord Record => data().Monetisation;

        public bool Enabled => config.enabled && adsEnabled();
        public int LevelsPerInterstitial => Math.Max(1, config.everyCompletedLevels);
        public int CompletionsSinceLast => Record.CompletionsSinceInterstitial;
        /// <summary>The level count when the last interstitial was displayed (for the event log).</summary>
        public int LevelsAtLastShow { get; private set; }

        /// <summary>A due interstitial is waiting for a natural break.</summary>
        public bool OpportunityPending => Enabled && !noAds() && CompletionsSinceLast >= LevelsPerInterstitial;

        /// <summary>A campaign level was completed successfully (called once per win by GameContext.FinishRun).</summary>
        public void RecordLevelCompleted()
        {
            var r = Record;
            if (r.CompletionsSinceInterstitial < int.MaxValue) r.CompletionsSinceInterstitial++;
        }

        /// <summary>A rewarded advert was watched or something was bought: starts the quiet period.</summary>
        public void NoteRewardedOrPurchase()
        {
            Record.LastQuietTicks = clock.UtcNow.Ticks;
        }

        /// <summary>The SDK reports an interstitial on screen: only now does the count start again.</summary>
        public void RecordShown()
        {
            var r = Record;
            LevelsAtLastShow = r.CompletionsSinceInterstitial;
            r.CompletionsSinceInterstitial = 0;
            r.LastInterstitialTicks = clock.UtcNow.Ticks;
        }

        public InterstitialDecision Evaluate(AdTransition transition, bool advertLoaded)
        {
            if (transition != AdTransition.LevelWonNext && transition != AdTransition.LevelWonExit) return InterstitialDecision.NotATransition;
            if (!Enabled) return InterstitialDecision.Disabled;
            if (noAds()) return InterstitialDecision.NoAds;
            var r = Record;
            if (r.CompletionsSinceInterstitial < LevelsPerInterstitial) return InterstitialDecision.NotEnoughLevels;
            long nowTicks = clock.UtcNow.Ticks;
            if (!Elapsed(ref r.LastInterstitialTicks, nowTicks, config.minSecondsBetween)) return InterstitialDecision.TooSoon;
            if (!Elapsed(ref r.LastQuietTicks, nowTicks, config.quietSecondsAfterRewardOrPurchase)) return InterstitialDecision.QuietAfterRewardOrPurchase;
            if (!advertLoaded) return InterstitialDecision.NotLoaded;
            return InterstitialDecision.Show;
        }

        /// <summary>Time until minSecondsBetween has passed since the last interstitial (zero if it has).</summary>
        public TimeSpan TimeUntilIntervalPassed
        {
            get
            {
                long last = Record.LastInterstitialTicks;
                if (last <= 0) return TimeSpan.Zero;
                var left = TimeSpan.FromSeconds(Math.Max(0, config.minSecondsBetween)) - TimeSpan.FromTicks(clock.UtcNow.Ticks - last);
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Whether `seconds` have passed since `lastTicks`. A clock moved back by more than that restarts the wait from
        /// now (the stored time is moved), so a changed clock can neither block adverts for good nor skip the wait.
        /// </summary>
        static bool Elapsed(ref long lastTicks, long nowTicks, int seconds)
        {
            if (seconds <= 0 || lastTicks <= 0) return true;
            long span = TimeSpan.FromSeconds(seconds).Ticks;
            long diff = nowTicks - lastTicks;
            if (diff >= span) return true;
            if (diff < -span) lastTicks = nowTicks;
            return false;
        }
    }
}
