using System;

namespace ColourCascade.Core
{
    /// <summary>All tunable data. Unity wraps this in a ScriptableObject; tests use CreateDefault().</summary>
    public sealed class GameConfigData
    {
        public ScoringRules Scoring = new ScoringRules();
        public CampaignConfig Campaign = new CampaignConfig();
        public EndlessConfig Endless = new EndlessConfig();
        public DailyConfig Daily = new DailyConfig();
        public RushConfig Rush = new RushConfig();
        public EconomyConfig Economy = new EconomyConfig();
        public PowerUpDefinition[] PowerUps = PowerUpCatalog.CreateDefaults();

        public static GameConfigData CreateDefault()
        {
            var c = new GameConfigData();
            c.Campaign.overrides = DefaultLevels.Create();
            return c;
        }
    }

    public enum SettingKind
    {
        SfxVolume,
        MusicVolume,
        Haptics,
        Symbols,
        ReducedMotion,
    }

    /// <summary>Player preferences with change notification. Values live in SaveData.Settings.</summary>
    public sealed class GameSettings
    {
        readonly Func<SaveData> data;

        public GameSettings(Func<SaveData> data)
        {
            this.data = data;
        }

        public event Action<SettingKind> Changed;

        SettingsData S => data().Settings;

        /// <summary>Volumes at or below this count as switched off.</summary>
        public const float Silent = 0.01f;

        public float SfxVolume
        {
            get => S.SfxVolume;
            set
            {
                S.SfxVolume = Clamp(value);
                if (S.SfxVolume > Silent) S.SfxVolumeWhenOn = S.SfxVolume;
                Changed?.Invoke(SettingKind.SfxVolume);
            }
        }

        public float MusicVolume
        {
            get => S.MusicVolume;
            set
            {
                S.MusicVolume = Clamp(value);
                if (S.MusicVolume > Silent) S.MusicVolumeWhenOn = S.MusicVolume;
                Changed?.Invoke(SettingKind.MusicVolume);
            }
        }

        /// <summary>On/off switch (the pause menu): off silences, on restores the last audible volume chosen.</summary>
        public bool SfxOn
        {
            get => S.SfxVolume > Silent;
            set
            {
                if (value == SfxOn) return;
                SfxVolume = value ? (S.SfxVolumeWhenOn > Silent ? S.SfxVolumeWhenOn : new SettingsData().SfxVolume) : 0f;
            }
        }

        public bool MusicOn
        {
            get => S.MusicVolume > Silent;
            set
            {
                if (value == MusicOn) return;
                MusicVolume = value ? (S.MusicVolumeWhenOn > Silent ? S.MusicVolumeWhenOn : new SettingsData().MusicVolume) : 0f;
            }
        }

        public bool Haptics
        {
            get => S.Haptics;
            set { S.Haptics = value; Changed?.Invoke(SettingKind.Haptics); }
        }

        /// <summary>Shows a distinct shape on every tile colour, so tiles can be told apart without relying on colour.</summary>
        public bool Symbols
        {
            get => S.Symbols;
            set { S.Symbols = value; Changed?.Invoke(SettingKind.Symbols); }
        }

        public bool ReducedMotion
        {
            get => S.ReducedMotion;
            set { S.ReducedMotion = value; Changed?.Invoke(SettingKind.ReducedMotion); }
        }

        static float Clamp(float v) => float.IsNaN(v) ? 0 : v < 0 ? 0 : v > 1 ? 1 : v;
    }

    public sealed class TutorialService
    {
        readonly Func<SaveData> data;

        public TutorialService(Func<SaveData> data)
        {
            this.data = data;
        }

        public bool Seen(string key) => data().Tutorials.Contains(key);

        /// <summary>Marks a hint as shown. Returns true the first time only.</summary>
        public bool MarkSeen(string key) => !string.IsNullOrEmpty(key) && data().Tutorials.Add(key);

        public void ResetAll() => data().Tutorials.Clear();
    }

    /// <summary>What happened when a run ended, for the result screen.</summary>
    public sealed class RunResult
    {
        public GameMode Mode;
        public LevelSpec Level;
        public SessionState State;
        public EndReason EndReason;
        public bool Success;
        public int Score;
        public int LeftoverBonus;
        public int FinalScore;
        public int Stars;
        public int PreviousStars;
        public int PreviousBest;
        public bool NewBest;
        public bool FirstClear;
        public bool UnlockedNext;
        public int BiggestGroup;
        public int MovesMade;
        public int Stage;
        public string PeriodKey = "";
        public RewardSummary Rewards = new RewardSummary();
    }

    /// <summary>
    /// Composition root for the game rules: owns the save and every service built on it. Unity's GameApp
    /// creates exactly one; tests create their own with in-memory storage and a fixed clock.
    /// </summary>
    public sealed class GameContext
    {
        readonly ISaveStorage storage;
        object lastFinishedKey;
        RunResult lastFinishedResult;
        ulong attemptCounter;
        PuzzleSession statsSession;
        int statsTiles, statsGroups, statsPowerUps, statsShuffles;

        public GameContext(GameConfigData config, ISaveStorage storage, IClock clock)
        {
            Config = config ?? GameConfigData.CreateDefault();
            this.storage = storage;
            Clock = clock ?? new SystemClock();
            Saves = new SaveSystem(storage, Clock, Config.Economy.startingCoins);
            Activity = new ActivityTracker();
            Catalog = new CampaignCatalog(Config.Campaign, Config.Scoring);
            PowerUps = new PowerUpCatalog(Config.PowerUps);
            Challenges = new ChallengeFactory(Config.Daily, Config.Rush, Config.Scoring);
            Settings = new GameSettings(() => Saves.Data);
            Tutorials = new TutorialService(() => Saves.Data);
            BindToData();
        }

        public GameConfigData Config { get; }
        public IClock Clock { get; }
        public SaveSystem Saves { get; }
        public SaveData Data => Saves.Data;
        public ActivityTracker Activity { get; }
        public CampaignCatalog Catalog { get; }
        public PowerUpCatalog PowerUps { get; }
        public ChallengeFactory Challenges { get; }
        public GameSettings Settings { get; }
        public TutorialService Tutorials { get; }

        public Wallet Wallet { get; private set; }
        public Inventory Inventory { get; private set; }
        public RewardService Rewards { get; private set; }
        public CampaignProgress Campaign { get; private set; }
        public ShopService Shop { get; private set; }
        public PowerUpService PowerUpUse { get; private set; }

        /// <summary>Raised after the save data object is replaced (load or reset) so listeners can rebind.</summary>
        public event Action DataReplaced;

        public LoadStatus Load()
        {
            var status = Saves.Load();
            BindToData();
            DataReplaced?.Invoke();
            return status;
        }

        public bool Save() => Saves.Save();

        public void ResetProgress()
        {
            Saves.ResetAll();
            BindToData();
            DataReplaced?.Invoke();
        }

        void BindToData()
        {
            var d = Saves.Data;
            Wallet = new Wallet(d, Clock, Config.Economy.ledgerCapacity);
            Inventory = new Inventory(d);
            Rewards = new RewardService(d, Wallet, Config.Economy, Config.Daily, Config.Rush, Config.Endless);
            Campaign = new CampaignProgress(d);
            Shop = new ShopService(PowerUps, Wallet, Inventory, () => Saves.Data.HighestUnlocked, Activity);
            PowerUpUse = new PowerUpService(PowerUps, Inventory, () => Saves.Data.HighestUnlocked);
        }

        // ---- Challenge periods ----

        public string TodayKey => ChallengeCalendar.DayKey(Clock.UtcNow);
        public string ThisWeekKey => ChallengeCalendar.WeekKey(Clock.UtcNow);
        public TimeSpan TimeToDailyReset => ChallengeCalendar.NextDailyReset(Clock.UtcNow) - Clock.UtcNow;
        public TimeSpan TimeToWeeklyReset => ChallengeCalendar.NextWeeklyReset(Clock.UtcNow) - Clock.UtcNow;

        /// <summary>Records that the player has seen today's challenge (used to refuse rewards for rolled-back dates).</summary>
        public void NoteDaySeen(string dayKey)
        {
            if (string.IsNullOrEmpty(Data.LatestDayKey) || string.CompareOrdinal(dayKey, Data.LatestDayKey) > 0) Data.LatestDayKey = dayKey;
        }

        // ---- Starting runs ----

        ulong FreshSeed()
        {
            attemptCounter++;
            return StableHash.Combine((ulong)Clock.UtcNow.Ticks, StableHash.Combine(attemptCounter, (ulong)Environment.TickCount));
        }

        public SessionSetup CreateCampaignRun(int level)
        {
            if (!Campaign.IsUnlocked(level)) throw new InvalidOperationException("Level " + level + " is locked");
            var spec = Catalog.Get(level);
            return new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = Config.Scoring, BoardSeed = FreshSeed() };
        }

        /// <summary>Every Endless run gets a brand-new seed, so no two runs start with the same board.</summary>
        public SessionSetup CreateEndlessRun()
        {
            var e = Config.Endless;
            var spec = new LevelSpec
            {
                Id = "endless",
                Mode = GameMode.Endless,
                Title = "Endless",
                Layout = LevelLayout.Open(e.columns, e.rows),
                Colours = e.ColoursForStage(1),
                AssistPermille = e.assistPermille,
                Moves = Math.Max(1, e.startMoves),
                Note = "Big groups earn extra moves",
            };
            return new SessionSetup { Mode = GameMode.Endless, Level = spec, Scoring = Config.Scoring, Endless = e, BoardSeed = FreshSeed() };
        }

        public SessionSetup CreateDailyRun(string dayKey = null)
        {
            dayKey = dayKey ?? TodayKey;
            NoteDaySeen(dayKey);
            var spec = Challenges.Daily(dayKey);
            return new SessionSetup { Mode = GameMode.Daily, Level = spec, Scoring = Config.Scoring, BoardSeed = spec.Seed };
        }

        public SessionSetup CreateWeeklyRun(string weekKey = null)
        {
            var spec = Challenges.Weekly(weekKey ?? ThisWeekKey);
            return new SessionSetup { Mode = GameMode.WeeklyRush, Level = spec, Scoring = Config.Scoring, BoardSeed = spec.Seed };
        }

        public SessionSetup CreatePracticeRun(LevelLayout layout, int colours, ulong seed)
        {
            var spec = new LevelSpec { Id = "practice", Mode = GameMode.Practice, Title = "Practice", Layout = layout, Colours = colours, AssistPermille = 120 };
            return new SessionSetup { Mode = GameMode.Practice, Level = spec, Scoring = Config.Scoring, BoardSeed = seed };
        }

        public void BeginRun(PuzzleSession session)
        {
            Activity.BeginRun(session.Mode);
        }

        /// <summary>The player left a run before it ended (quit to map/home or restarted).</summary>
        public void AbandonRun(PuzzleSession session)
        {
            if (session != null && session.Mode == GameMode.Campaign && !session.IsOver && session.MovesMade > 0)
                Campaign.RecordAttempt(session.Level.Number, session.Score);
            Activity.EndRun();
            Save();
        }

        /// <summary>
        /// Applies the result of a finished run: progress, records and rewards, then saves immediately.
        /// Safe to call repeatedly: the same end state returns the same result without paying twice.
        /// A campaign level revived with Extra Moves and then won is a new end state and is processed.
        /// </summary>
        public RunResult FinishRun(PuzzleSession s)
        {
            if (s == null || !s.IsOver) throw new InvalidOperationException("Run has not ended");
            var key = Tuple.Create(s, s.State, s.MovesMade, s.PowerUpsUsed);
            if (lastFinishedKey != null && lastFinishedKey.Equals(key)) return lastFinishedResult;

            var level = s.Level;
            var r = new RunResult
            {
                Mode = s.Mode,
                Level = level,
                State = s.State,
                EndReason = s.EndReason,
                Success = s.Succeeded,
                Score = s.Score,
                LeftoverBonus = s.LeftoverBonus,
                FinalScore = s.FinalScore,
                Stars = s.Stars,
                BiggestGroup = s.BiggestGroup,
                MovesMade = s.MovesMade,
                Stage = s.Stage,
            };

            var d = Data;
            RecordStats(s);

            switch (s.Mode)
            {
                case GameMode.Campaign:
                    if (s.State == SessionState.Won)
                    {
                        var win = Campaign.RecordWin(level.Number, s.FinalScore, r.Stars);
                        r.FirstClear = win.FirstClear;
                        r.PreviousStars = win.PreviousStars;
                        r.PreviousBest = win.PreviousBest;
                        r.NewBest = win.NewBest;
                        r.UnlockedNext = win.UnlockedNext;
                        r.Rewards = Rewards.ForCampaignWin(level, r.Stars);
                    }
                    else
                    {
                        r.PreviousStars = Campaign.StarsFor(level.Number);
                        Campaign.RecordAttempt(level.Number, s.Score);
                    }
                    break;

                case GameMode.Daily:
                    {
                        string day = level.Id.StartsWith("daily:", StringComparison.Ordinal) ? level.Id.Substring(6) : TodayKey;
                        r.PeriodKey = day;
                        var rec = d.GetOrCreateDaily(day);
                        r.PreviousBest = rec.BestScore;
                        r.PreviousStars = rec.Stars;
                        rec.Plays++;
                        if (s.FinalScore > rec.BestScore)
                        {
                            r.NewBest = rec.BestScore > 0;
                            rec.BestScore = s.FinalScore;
                        }
                        rec.Stars = Math.Max(rec.Stars, r.Stars);
                        if (r.Success) rec.Completed = true;
                        r.Rewards = Rewards.ForDaily(day, r.Success, r.Stars);
                        break;
                    }

                case GameMode.WeeklyRush:
                    {
                        string week = level.Id.StartsWith("weekly:", StringComparison.Ordinal) ? level.Id.Substring(7) : ThisWeekKey;
                        r.PeriodKey = week;
                        var rec = d.GetOrCreateWeekly(week);
                        r.PreviousBest = rec.BestScore;
                        rec.Runs++;
                        if (s.Score > rec.BestScore)
                        {
                            r.NewBest = rec.BestScore > 0;
                            rec.BestScore = s.Score;
                        }
                        r.Rewards = Rewards.ForWeekly(week, s.Score);
                        break;
                    }

                case GameMode.Endless:
                    {
                        var e = d.Endless;
                        r.PreviousBest = e.BestScore;
                        e.Runs++;
                        if (s.Score > e.BestScore)
                        {
                            r.NewBest = e.BestScore > 0;
                            e.BestScore = s.Score;
                        }
                        if (s.BiggestGroup > e.BestGroup) e.BestGroup = s.BiggestGroup;
                        if (s.MovesMade > e.BestMoves) e.BestMoves = s.MovesMade;
                        if (s.Stage > e.BestStage) e.BestStage = s.Stage;
                        r.Rewards = Rewards.ForEndless(s.Score);
                        break;
                    }
            }

            Activity.EndRun();
            Save();
            lastFinishedKey = key;
            lastFinishedResult = r;
            return r;
        }

        /// <summary>Adds this session's lifetime statistics, counting only what was not already recorded
        /// (a revived campaign level can finish twice).</summary>
        void RecordStats(PuzzleSession s)
        {
            var st = Data.Stats;
            if (!ReferenceEquals(statsSession, s))
            {
                statsSession = s;
                statsTiles = statsGroups = statsPowerUps = statsShuffles = 0;
                st.GamesPlayed++;
            }
            st.TilesCleared += s.TilesCleared - statsTiles;
            st.GroupsCleared += s.GroupsCleared - statsGroups;
            st.PowerUpsUsed += s.PowerUpsUsed - statsPowerUps;
            st.Shuffles += s.Shuffles - statsShuffles;
            statsTiles = s.TilesCleared;
            statsGroups = s.GroupsCleared;
            statsPowerUps = s.PowerUpsUsed;
            statsShuffles = s.Shuffles;
            if (s.BiggestGroup > st.BiggestGroup) st.BiggestGroup = s.BiggestGroup;
        }

        /// <summary>Daily challenge status for the home screen.</summary>
        public DailyRecord TodayRecord => Data.FindDaily(TodayKey);
        public WeeklyRecord ThisWeekRecord => Data.FindWeekly(ThisWeekKey);

        /// <summary>Grants trial uses if a power-up is introduced at this level and hasn't been granted yet.</summary>
        public PowerUpDefinition GrantIntroTrialsFor(int level)
        {
            var def = PowerUps.IntroducedAt(level);
            if (def == null) return null;
            if (Rewards.GrantTrial(def, Inventory) > 0)
            {
                Save();
                return def;
            }
            return null;
        }
    }
}
