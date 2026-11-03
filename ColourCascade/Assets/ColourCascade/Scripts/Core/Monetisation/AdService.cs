using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ColourCascade.Core
{
    public enum AdServiceState
    {
        NotStarted,
        /// <summary>Waiting for the consent tool (the consent form may be on screen).</summary>
        WaitingForConsent,
        Initialising,
        Ready,
        Unavailable,
    }

    public enum OfferStatus
    {
        Ready,
        /// <summary>No advert loaded yet (still starting, loading, or retrying after a failed load).</summary>
        Loading,
        LimitReached,
        CoolingDown,
        Disabled,
        /// <summary>The player's consent choice doesn't allow ad requests.</summary>
        NoConsent,
        Unavailable,
        /// <summary>Another advert is on screen.</summary>
        Busy,
    }

    /// <summary>Whether a rewarded advert can be offered right now, and why not.</summary>
    public struct RewardedOffer
    {
        public string Placement;
        public OfferStatus Status;
        public int Reward;
        /// <summary>Adverts left today for this placement; -1 = no daily limit.</summary>
        public int RemainingToday;
        /// <summary>Adverts left in this level attempt; -1 = no limit.</summary>
        public int RemainingThisRun;
        public TimeSpan Wait;

        public bool CanShow => Status == OfferStatus.Ready;

        /// <summary>A short explanation for the player when the offer can't be used.</summary>
        public string Message
        {
            get
            {
                switch (Status)
                {
                    case OfferStatus.Ready: return "";
                    case OfferStatus.Loading: return "Loading an advert... try again in a moment.";
                    case OfferStatus.LimitReached: return RemainingThisRun == 0 ? "No more adverts for this level." : "That's all the adverts for today. Come back tomorrow!";
                    case OfferStatus.CoolingDown: return "Another advert will be ready in " + NumberFormat.Duration(Wait) + ".";
                    case OfferStatus.Disabled: return "Adverts are switched off.";
                    case OfferStatus.NoConsent: return "Adverts aren't available with your privacy choices. You can change them in Settings.";
                    case OfferStatus.Busy: return "An advert is already playing.";
                    default: return "Adverts aren't available right now. Check your connection and try again.";
                }
            }
        }
    }

    public enum AdOutcome
    {
        /// <summary>Rewarded: the SDK confirmed the reward and it was granted.</summary>
        Rewarded,
        /// <summary>Rewarded: closed before the reward was earned (nothing granted).</summary>
        ClosedEarly,
        /// <summary>The advert couldn't be shown (nothing granted).</summary>
        FailedToShow,
        /// <summary>Interstitial: displayed and closed.</summary>
        Shown,
    }

    /// <summary>The rewarded-advert opportunities. New ones need a name here and a RewardedPlacementConfig.</summary>
    public static class RewardedPlacements
    {
        public const string ContinueMoves = "continue_moves";
        public const string ShopCoins = "shop_coins";
        public const string CoinShortfall = "coin_shortfall";
    }

    /// <summary>
    /// The central advertising manager (docs/MONETISATION.md §2). Every screen asks this service, never the SDK.
    /// <list type="bullet">
    /// <item>Start-up order: consent (Google UMP) first, the ad SDK only once ads may be requested, then one rewarded
    /// and (without No Ads) one interstitial advert are kept loaded, retrying failed loads with growing delays.</item>
    /// <item>Rewards: each show is a ticket. The grant runs only when the SDK reports the reward was earned, exactly
    /// once per ticket; duplicate callbacks are ignored. A close without the reward waits a short grace period for
    /// a late callback, then ends as "closed early" with nothing granted.</item>
    /// <item>Watchdogs: a show the SDK never answers counts as failed, and an advert still "open" well after the
    /// player is back in the game counts as closed, so the game can never get stuck behind an advert.</item>
    /// <item>Limits per placement (daily, per level attempt, cooldown) are saved, so restarting can't reset them.</item>
    /// </list>
    /// </summary>
    public sealed class AdService
    {
        sealed class Slot
        {
            public AdFormat Format;
            public bool Loading;
            public bool Loaded;
            public double RetryAt;
            public double LoadedAt;
            public int Failures;
            public int Generation;
        }

        /// <summary>Loaded adverts are replaced after this long (AdMob adverts expire after an hour).</summary>
        public const double MaxAdAgeSeconds = 55 * 60;

        sealed class Ticket
        {
            public int Id;
            public AdFormat Format;
            public string Placement;
            public object RunKey;
            public Func<bool> Grant;
            public Action<AdOutcome> Finished;
            public double ShowAt;
            /// <summary>The app went to the background after Show (on Android a full-screen advert always does this).</summary>
            public bool SawPause;
            public bool Opened, Earned, Granted, Closed, Done;
            public double ClosedAt;
        }

        readonly AdConfig config;
        readonly PrivacyConfig privacy;
        readonly Func<SaveData> data;
        readonly IClock clock;
        readonly Func<double> now;
        readonly MonetisationAnalytics analytics;
        readonly Func<bool> noAds;
        readonly Dictionary<string, RewardedPlacementConfig> placements = new Dictionary<string, RewardedPlacementConfig>();
        readonly Dictionary<string, KeyValuePair<object, int>> runCounts = new Dictionary<string, KeyValuePair<object, int>>();
        readonly Slot rewarded = new Slot { Format = AdFormat.Rewarded };
        readonly Slot interstitial = new Slot { Format = AdFormat.Interstitial };

        IAdNetwork network;
        IConsentProvider consent;
        AdNetworkSettings settings;
        AdUnitIds units;
        bool initStarted;
        double initRetryAt = -1;
        int initFailures;
        bool consentBlocked;
        Ticket current;
        int nextTicketId;
        bool appPaused;
        double resumedAt = -1;

        public AdService(AdConfig config, PrivacyConfig privacy, InterstitialPolicy interstitials, Func<SaveData> data, IClock clock,
            Func<double> monotonicSeconds, MonetisationAnalytics analytics, Func<bool> noAds)
        {
            this.config = config ?? new AdConfig();
            this.privacy = privacy ?? new PrivacyConfig();
            Interstitials = interstitials;
            this.data = data;
            this.clock = clock ?? new SystemClock();
            this.now = monotonicSeconds ?? StopwatchSeconds();
            this.analytics = analytics;
            this.noAds = noAds ?? (() => false);
            RegisterPlacement(RewardedPlacements.ContinueMoves, this.config.continueMoves);
            RegisterPlacement(RewardedPlacements.ShopCoins, this.config.shopCoins);
            RegisterPlacement(RewardedPlacements.CoinShortfall, this.config.coinShortfall);
        }

        public InterstitialPolicy Interstitials { get; }
        public AdServiceState State { get; private set; } = AdServiceState.NotStarted;
        public string UnavailableReason { get; private set; } = "";
        public string NetworkName => network != null ? network.Name : "none";
        public bool TestMode => settings != null && settings.TestMode;
        /// <summary>An advert is on screen (or being opened): the game shows nothing else and keeps quiet.</summary>
        public bool IsShowing => current != null;
        public bool PrivacyOptionsRequired => consent != null && consent.PrivacyOptionsRequired;

        /// <summary>Availability changed (an advert loaded or failed, the SDK started): refresh offer buttons.</summary>
        public event Action Changed;
        /// <summary>True when an advert starts showing, false when the game is back (mute audio, block input).</summary>
        public event Action<bool> ShowingChanged;
        /// <summary>A reward confirmed by the SDK only after the advert had already ended as "closed early".</summary>
        public event Action<string> LateRewardGranted;

        /// <summary>Adds (or replaces) a rewarded opportunity, e.g. a future "double the daily reward" offer.</summary>
        public void RegisterPlacement(string id, RewardedPlacementConfig limits)
        {
            if (string.IsNullOrEmpty(id)) return;
            placements[id] = limits ?? new RewardedPlacementConfig { enabled = false };
        }

        public RewardedPlacementConfig Placement(string id)
        {
            RewardedPlacementConfig p;
            return id != null && placements.TryGetValue(id, out p) ? p : null;
        }

        // ------------------------------------------------------------------ Start-up

        /// <summary>
        /// Starts consent, then the SDK. network null = no ad SDK in this build (offers report Unavailable);
        /// consent null = no consent tool (only for tests and builds without the consent SDK).
        /// </summary>
        public void Start(IAdNetwork adNetwork, IConsentProvider consentProvider, AdUnitIds adUnits, bool testMode)
        {
            if (State != AdServiceState.NotStarted) return;
            network = adNetwork;
            consent = consentProvider;
            units = adUnits ?? new AdUnitIds();
            settings = new AdNetworkSettings
            {
                TestMode = testMode,
                MaxAdContentRating = privacy.maxAdContentRating,
                ChildDirected = privacy.childDirected,
                UnderAgeOfConsent = privacy.underAgeOfConsent,
                TestDeviceIds = privacy.testDeviceIds ?? new string[0],
            };
            if (!config.enabled)
            {
                SetUnavailable("Adverts are switched off in the config.");
                return;
            }
            if (network == null)
            {
                SetUnavailable("This build has no ad SDK.");
                return;
            }
            if (consent == null)
            {
                InitNetwork();
                return;
            }
            State = AdServiceState.WaitingForConsent;
            var request = new ConsentRequest
            {
                UnderAgeOfConsent = privacy.underAgeOfConsent,
                DebugGeographyEea = testMode && privacy.debugGeographyEea,
                TestDeviceIds = privacy.testDeviceIds ?? new string[0],
            };
            // Google's recommended order: ask for the consent update on every start, but if a previous session's answer
            // already allows ads, start the SDK at once rather than waiting for the network round trip.
            if (consent.CanRequestAds) InitNetwork();
            consent.Gather(request, OnConsent);
            Changed?.Invoke();
        }

        void OnConsent(ConsentOutcome outcome)
        {
            bool can = outcome.CanRequestAds || (consent != null && consent.CanRequestAds);
            analytics?.Record(MonetisationEvents.ConsentResult, ("can_request_ads", can), ("error", outcome.Error ?? ""));
            if (can)
            {
                consentBlocked = false;
                InitNetwork();
            }
            else if (!initStarted)
            {
                consentBlocked = true;
                SetUnavailable(string.IsNullOrEmpty(outcome.Error) ? "Consent doesn't allow ad requests." : "Consent couldn't be checked: " + outcome.Error);
            }
            Changed?.Invoke();
        }

        /// <summary>Shows the consent tool's privacy options (Settings); ads start if the new choice allows them.</summary>
        public void ShowPrivacyOptions(Action<string> done)
        {
            if (consent == null)
            {
                done?.Invoke("No consent tool in this build.");
                return;
            }
            consent.ShowPrivacyOptions(error =>
            {
                if (consent.CanRequestAds && !initStarted)
                {
                    consentBlocked = false;
                    InitNetwork();
                }
                Changed?.Invoke();
                done?.Invoke(error);
            });
        }

        void InitNetwork()
        {
            if (initStarted || network == null) return;
            initStarted = true;
            initRetryAt = -1;
            State = AdServiceState.Initialising;
            network.Initialize(settings, (ok, error) =>
            {
                if (ok)
                {
                    State = AdServiceState.Ready;
                    UnavailableReason = "";
                    initFailures = 0;
                    analytics?.Record(MonetisationEvents.AdsInitialised, ("network", network.Name), ("test_mode", settings.TestMode));
                    LoadWanted();
                }
                else
                {
                    initStarted = false;
                    initFailures++;
                    initRetryAt = now() + RetryDelay(initFailures);
                    SetUnavailable("The ad SDK didn't start: " + (error ?? "unknown error"));
                }
                Changed?.Invoke();
            });
        }

        void SetUnavailable(string reason)
        {
            State = AdServiceState.Unavailable;
            UnavailableReason = reason ?? "";
        }

        double RetryDelay(int failures)
        {
            double first = Math.Max(1, config.retryFirstSeconds), max = Math.Max(first, config.retryMaxSeconds);
            double d = first * Math.Pow(2, Math.Max(0, Math.Min(failures, 20) - 1));
            return Math.Min(max, d);
        }

        // ------------------------------------------------------------------ Loading

        string UnitFor(AdFormat f) => f == AdFormat.Rewarded ? units.rewarded : units.interstitial;

        bool Wanted(AdFormat f)
        {
            if (State != AdServiceState.Ready || !config.enabled) return false;
            if (string.IsNullOrEmpty(UnitFor(f))) return false;
            if (f == AdFormat.Interstitial) return Interstitials != null && Interstitials.Enabled && !noAds();
            return true;
        }

        void LoadWanted()
        {
            TryLoad(rewarded);
            TryLoad(interstitial);
        }

        void TryLoad(Slot slot)
        {
            if (slot.Loading || slot.Loaded || !Wanted(slot.Format) || now() < slot.RetryAt) return;
            slot.Loading = true;
            int generation = ++slot.Generation;
            network.Load(slot.Format, UnitFor(slot.Format), (ok, error) =>
            {
                if (generation != slot.Generation) return;
                slot.Loading = false;
                bool isRewarded = slot.Format == AdFormat.Rewarded;
                if (ok)
                {
                    slot.Loaded = true;
                    slot.LoadedAt = now();
                    slot.Failures = 0;
                    slot.RetryAt = 0;
                    analytics?.Record(isRewarded ? MonetisationEvents.RewardedLoaded : MonetisationEvents.InterstitialLoaded);
                }
                else
                {
                    slot.Loaded = false;
                    slot.Failures++;
                    slot.RetryAt = now() + RetryDelay(slot.Failures);
                    analytics?.Record(isRewarded ? MonetisationEvents.RewardedLoadFailed : MonetisationEvents.InterstitialLoadFailed,
                        ("error", error ?? ""), ("attempt", slot.Failures));
                }
                Changed?.Invoke();
            });
        }

        bool SlotReady(Slot slot) => State == AdServiceState.Ready && slot.Loaded && network != null && network.IsReady(slot.Format);

        /// <summary>Call every frame: retries, expiry of loaded adverts, grace periods and watchdogs.</summary>
        public void Update()
        {
            double t = now();
            if (State == AdServiceState.Unavailable && !initStarted && !consentBlocked && initRetryAt >= 0 && t >= initRetryAt) InitNetwork();
            if (State == AdServiceState.Ready && current == null)
            {
                foreach (var slot in new[] { rewarded, interstitial })
                {
                    // Loaded adverts expire (about an hour for AdMob): load a fresh one.
                    if (slot.Loaded && (!network.IsReady(slot.Format) || t - slot.LoadedAt > MaxAdAgeSeconds))
                    {
                        slot.Loaded = false;
                        Changed?.Invoke();
                    }
                    TryLoad(slot);
                }
            }
            var c = current;
            if (c == null || c.Done) return;
            if (!c.Closed && !appPaused)
            {
                if (c.SawPause)
                {
                    // Back in the game after the advert, but the SDK never said it closed: count it as closed (a reward
                    // already confirmed still stands; otherwise the usual grace period applies).
                    if (resumedAt >= c.ShowAt && t - resumedAt > Math.Max(1f, config.resumeWatchdogSeconds))
                    {
                        analytics?.Record(c.Format == AdFormat.Rewarded ? MonetisationEvents.RewardedFailed : MonetisationEvents.InterstitialFailed,
                            ("placement", c.Placement ?? ""), ("error", "no close callback"));
                        OnClosed(c);
                        return;
                    }
                }
                else if (!c.Opened && t - c.ShowAt > Math.Max(1f, config.showTimeoutSeconds))
                {
                    OnFailed(c, "The advert didn't open.");
                    return;
                }
                else if (c.Opened && t - c.ShowAt > Math.Max(10f, config.maxShowSeconds))
                {
                    analytics?.Record(c.Format == AdFormat.Rewarded ? MonetisationEvents.RewardedFailed : MonetisationEvents.InterstitialFailed,
                        ("placement", c.Placement ?? ""), ("error", "no close callback"));
                    OnClosed(c);
                    return;
                }
            }
            if (c.Closed && !c.Earned && t - c.ClosedAt >= Math.Max(0f, config.rewardGraceSeconds)) Finish(c, AdOutcome.ClosedEarly);
        }

        /// <summary>The app went to the background (true) or came back (false). Adverts usually do both.</summary>
        public void NotifyAppPaused(bool paused)
        {
            appPaused = paused;
            if (paused && current != null) current.SawPause = true;
            if (!paused) resumedAt = now();
        }

        // ------------------------------------------------------------------ Rewarded

        /// <summary>Whether this rewarded opportunity can be offered now (runKey: the level attempt, for per-run limits).</summary>
        public RewardedOffer CheckRewarded(string placement, object runKey = null)
        {
            var p = Placement(placement);
            var offer = new RewardedOffer { Placement = placement, RemainingToday = -1, RemainingThisRun = -1, Reward = p != null ? p.reward : 0 };
            if (p == null || !p.enabled || !config.enabled)
            {
                offer.Status = OfferStatus.Disabled;
                return offer;
            }
            var rec = Record;
            if (rec != null)
            {
                RollDay(rec);
                if (p.dailyLimit > 0)
                {
                    int used;
                    rec.AdsToday.TryGetValue(placement, out used);
                    offer.RemainingToday = Math.Max(0, p.dailyLimit - used);
                }
            }
            if (p.perRunLimit > 0 && runKey != null) offer.RemainingThisRun = Math.Max(0, p.perRunLimit - RunCount(placement, runKey));
            if (offer.RemainingToday == 0 || offer.RemainingThisRun == 0)
            {
                offer.Status = OfferStatus.LimitReached;
                return offer;
            }
            if (p.cooldownSeconds > 0 && rec != null)
            {
                long last;
                if (rec.LastAdTicks.TryGetValue(placement, out last) && last > 0)
                {
                    long nowTicks = clock.UtcNow.Ticks;
                    var wait = TimeSpan.FromSeconds(p.cooldownSeconds) - TimeSpan.FromTicks(nowTicks - last);
                    // A clock moved back by more than the cooldown restarts it from now rather than blocking for good.
                    if (nowTicks < last && last - nowTicks > TimeSpan.FromSeconds(p.cooldownSeconds).Ticks)
                    {
                        rec.LastAdTicks[placement] = nowTicks;
                        wait = TimeSpan.FromSeconds(p.cooldownSeconds);
                    }
                    if (wait > TimeSpan.Zero)
                    {
                        offer.Status = OfferStatus.CoolingDown;
                        offer.Wait = wait;
                        return offer;
                    }
                }
            }
            if (current != null) offer.Status = OfferStatus.Busy;
            else if (State == AdServiceState.Unavailable) offer.Status = consentBlocked ? OfferStatus.NoConsent : OfferStatus.Unavailable;
            else if (!SlotReady(rewarded)) offer.Status = State == AdServiceState.Ready && rewarded.Failures > 0 && !rewarded.Loading ? OfferStatus.Unavailable : OfferStatus.Loading;
            else offer.Status = OfferStatus.Ready;
            return offer;
        }

        /// <summary>Records that an offer was shown to the player (for the "offered" event).</summary>
        public void NoteOffered(string placement, RewardedOffer offer)
        {
            analytics?.Record(MonetisationEvents.RewardedOffered, ("placement", placement), ("status", offer.Status));
        }

        /// <summary>
        /// Shows a rewarded advert. grant runs only after the SDK confirms the reward, at most once, and returns whether
        /// the reward could be applied; finished reports how the advert ended. Returns false (and calls nothing) if
        /// the offer isn't available right now.
        /// </summary>
        public bool ShowRewarded(string placement, object runKey, Func<bool> grant, Action<AdOutcome> finished)
        {
            var offer = CheckRewarded(placement, runKey);
            if (!offer.CanShow) return false;
            var t = new Ticket
            {
                Id = ++nextTicketId,
                Format = AdFormat.Rewarded,
                Placement = placement,
                RunKey = runKey,
                Grant = grant,
                Finished = finished,
                ShowAt = now(),
            };
            Begin(t, rewarded);
            return true;
        }

        void Begin(Ticket t, Slot slot)
        {
            current = t;
            // A shown advert is used up whatever happens next; a fresh one loads once this one has finished.
            slot.Loaded = false;
            slot.Generation++;
            slot.Loading = false;
            ShowingChanged?.Invoke(true);
            Changed?.Invoke();
            network.Show(t.Format, new AdShowCallbacks
            {
                Opened = () => OnOpened(t),
                Earned = () => OnEarned(t),
                Closed = () => OnClosed(t),
                Failed = e => OnFailed(t, e),
            });
        }

        void OnOpened(Ticket t)
        {
            if (t.Opened || t.Done) return;
            t.Opened = true;
            if (t.Format == AdFormat.Rewarded)
            {
                analytics?.Record(MonetisationEvents.RewardedStarted, ("placement", t.Placement));
            }
            else
            {
                Interstitials?.RecordShown();
                analytics?.Record(MonetisationEvents.InterstitialShown, ("completions", Interstitials != null ? Interstitials.LevelsAtLastShow : 0));
            }
        }

        void OnEarned(Ticket t)
        {
            if (t.Format != AdFormat.Rewarded) return;
            if (t.Earned)
            {
                analytics?.Record(MonetisationEvents.RewardDuplicateIgnored, ("placement", t.Placement));
                return;
            }
            t.Earned = true;
            CountCompleted(t);
            analytics?.Record(MonetisationEvents.RewardedCompleted, ("placement", t.Placement), ("late", t.Done));
            bool granted = false;
            try
            {
                granted = t.Grant != null && t.Grant();
            }
            catch (Exception e)
            {
                analytics?.Record(MonetisationEvents.RewardNotApplied, ("placement", t.Placement), ("error", e.Message));
            }
            t.Granted = granted;
            if (granted) analytics?.Record(MonetisationEvents.RewardGranted, ("placement", t.Placement), ("late", t.Done));
            else analytics?.Record(MonetisationEvents.RewardNotApplied, ("placement", t.Placement));
            if (t.Done)
            {
                if (granted) LateRewardGranted?.Invoke(t.Placement);
                return;
            }
            if (t.Closed) Finish(t, AdOutcome.Rewarded);
        }

        void CountCompleted(Ticket t)
        {
            var rec = Record;
            if (rec != null)
            {
                RollDay(rec);
                int used;
                rec.AdsToday.TryGetValue(t.Placement, out used);
                rec.AdsToday[t.Placement] = used + 1;
                rec.LastAdTicks[t.Placement] = clock.UtcNow.Ticks;
            }
            if (t.RunKey != null)
            {
                KeyValuePair<object, int> rc;
                int n = runCounts.TryGetValue(t.Placement, out rc) && ReferenceEquals(rc.Key, t.RunKey) ? rc.Value : 0;
                runCounts[t.Placement] = new KeyValuePair<object, int>(t.RunKey, n + 1);
            }
        }

        int RunCount(string placement, object runKey)
        {
            KeyValuePair<object, int> rc;
            return runCounts.TryGetValue(placement, out rc) && ReferenceEquals(rc.Key, runKey) ? rc.Value : 0;
        }

        void OnClosed(Ticket t)
        {
            if (t.Closed || t.Done) return;
            t.Closed = true;
            t.ClosedAt = now();
            // Watching any advert starts the interstitial quiet period, rewarded or not.
            Interstitials?.NoteRewardedOrPurchase();
            if (t.Format == AdFormat.Interstitial) Finish(t, t.Opened ? AdOutcome.Shown : AdOutcome.FailedToShow);
            else if (t.Earned) Finish(t, AdOutcome.Rewarded);
            // Otherwise wait for a late reward callback (Update ends it after the grace period).
            else if (config.rewardGraceSeconds <= 0) Finish(t, AdOutcome.ClosedEarly);
        }

        void OnFailed(Ticket t, string error)
        {
            if (t.Done) return;
            if (t.Opened)
            {
                // Failed after opening: treat as a close (a reward already earned still stands).
                OnClosed(t);
                return;
            }
            analytics?.Record(t.Format == AdFormat.Rewarded ? MonetisationEvents.RewardedFailed : MonetisationEvents.InterstitialFailed,
                ("placement", t.Placement ?? ""), ("error", error ?? ""));
            Finish(t, AdOutcome.FailedToShow);
        }

        void Finish(Ticket t, AdOutcome outcome)
        {
            if (t.Done) return;
            t.Done = true;
            if (outcome == AdOutcome.ClosedEarly) analytics?.Record(MonetisationEvents.RewardedClosedEarly, ("placement", t.Placement));
            if (ReferenceEquals(current, t)) current = null;
            ShowingChanged?.Invoke(false);
            try
            {
                t.Finished?.Invoke(outcome);
            }
            finally
            {
                if (State == AdServiceState.Ready) LoadWanted();
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------------ Interstitials

        public bool InterstitialReady => current == null && SlotReady(interstitial);

        /// <summary>
        /// A natural break (results screen to the next level, map or home). Shows an interstitial only if the policy
        /// allows it now and one is loaded; continueWith always runs exactly once, after the advert if one showed.
        /// </summary>
        public InterstitialDecision AtTransition(AdTransition transition, Action continueWith)
        {
            var decision = Interstitials != null ? Interstitials.Evaluate(transition, InterstitialReady) : InterstitialDecision.Disabled;
            if (decision == InterstitialDecision.Show)
            {
                var t = new Ticket
                {
                    Id = ++nextTicketId,
                    Format = AdFormat.Interstitial,
                    Placement = "interstitial",
                    Finished = outcome => continueWith?.Invoke(),
                    ShowAt = now(),
                };
                Begin(t, interstitial);
                return decision;
            }
            if (decision == InterstitialDecision.TooSoon || decision == InterstitialDecision.QuietAfterRewardOrPurchase || decision == InterstitialDecision.NotLoaded)
                analytics?.Record(MonetisationEvents.InterstitialSkipped, ("reason", decision));
            continueWith?.Invoke();
            return decision;
        }

        // ------------------------------------------------------------------ Helpers

        MonetisationRecord Record => data?.Invoke()?.Monetisation;

        void RollDay(MonetisationRecord rec)
        {
            string today = ChallengeCalendar.DayKey(clock.UtcNow);
            // Only moves forward: winding the clock back can't reset today's limits.
            if (string.IsNullOrEmpty(rec.AdDay) || string.CompareOrdinal(today, rec.AdDay) > 0)
            {
                rec.AdDay = today;
                rec.AdsToday.Clear();
            }
        }

        static Func<double> StopwatchSeconds()
        {
            var sw = Stopwatch.StartNew();
            return () => sw.Elapsed.TotalSeconds;
        }
    }
}
