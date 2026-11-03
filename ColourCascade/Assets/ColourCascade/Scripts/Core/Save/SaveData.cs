using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Persistent player data. Everything the player legitimately earns lives here. Temporary run state
    /// (the board in play) is deliberately NOT saved; a run that is interrupted is simply replayed.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>Bump when the format changes and add a migration step in SaveCodec.</summary>
        public const int CurrentVersion = 1;
        public const int MaxDailyRecords = 60;
        public const int MaxWeeklyRecords = 26;

        public int Version = CurrentVersion;
        public long CreatedUtcTicks;
        public long LastSavedUtcTicks;
        public int SaveCount;

        public int Coins;
        public List<LedgerEntry> Ledger = new List<LedgerEntry>();
        public List<InventoryEntry> Inventory = new List<InventoryEntry>();

        public int HighestUnlocked = 1;
        public List<LevelRecord> Levels = new List<LevelRecord>();
        /// <summary>Keys of every one-time reward already granted (see RewardKeys).</summary>
        public HashSet<string> Claims = new HashSet<string>();

        public List<DailyRecord> Daily = new List<DailyRecord>();
        /// <summary>Latest daily key the player has opened; rewards are not granted for earlier dates.</summary>
        public string LatestDayKey = "";
        public List<WeeklyRecord> Weekly = new List<WeeklyRecord>();
        public EndlessRecord Endless = new EndlessRecord();

        public HashSet<string> Tutorials = new HashSet<string>();
        public SettingsData Settings = new SettingsData();
        public StatsData Stats = new StatsData();
        /// <summary>Advert counters, processed purchases and the No Ads entitlement (update 3.0). Kept by a progress reset.</summary>
        public MonetisationRecord Monetisation = new MonetisationRecord();

        readonly Dictionary<int, LevelRecord> levelIndex = new Dictionary<int, LevelRecord>();

        public static SaveData CreateNew(DateTime utcNow, int startingCoins)
        {
            var d = new SaveData();
            d.CreatedUtcTicks = utcNow.Ticks;
            d.Coins = Math.Max(0, startingCoins);
            return d;
        }

        public LevelRecord FindLevel(int level)
        {
            LevelRecord r;
            return levelIndex.TryGetValue(level, out r) ? r : null;
        }

        public LevelRecord GetOrCreateLevel(int level)
        {
            var r = FindLevel(level);
            if (r != null) return r;
            r = new LevelRecord { Level = level };
            Levels.Add(r);
            levelIndex[level] = r;
            return r;
        }

        public DailyRecord FindDaily(string day)
        {
            for (int i = 0; i < Daily.Count; i++) if (Daily[i].Day == day) return Daily[i];
            return null;
        }

        public DailyRecord GetOrCreateDaily(string day)
        {
            var r = FindDaily(day);
            if (r != null) return r;
            r = new DailyRecord { Day = day };
            Daily.Add(r);
            if (Daily.Count > MaxDailyRecords)
            {
                Daily.Sort((a, b) => string.CompareOrdinal(a.Day, b.Day));
                Daily.RemoveRange(0, Daily.Count - MaxDailyRecords);
            }
            return r;
        }

        public WeeklyRecord FindWeekly(string week)
        {
            for (int i = 0; i < Weekly.Count; i++) if (Weekly[i].Week == week) return Weekly[i];
            return null;
        }

        public WeeklyRecord GetOrCreateWeekly(string week)
        {
            var r = FindWeekly(week);
            if (r != null) return r;
            r = new WeeklyRecord { Week = week };
            Weekly.Add(r);
            if (Weekly.Count > MaxWeeklyRecords)
            {
                Weekly.Sort((a, b) => string.CompareOrdinal(a.Week, b.Week));
                Weekly.RemoveRange(0, Weekly.Count - MaxWeeklyRecords);
            }
            return r;
        }

        public InventoryEntry GetOrCreateInventory(PowerUpId id)
        {
            for (int i = 0; i < Inventory.Count; i++) if (Inventory[i].Id == (int)id) return Inventory[i];
            var e = new InventoryEntry { Id = (int)id };
            Inventory.Add(e);
            return e;
        }

        /// <summary>Clamps impossible values and rebuilds lookup indexes. Called after every load.</summary>
        public void Normalize()
        {
            if (Coins < 0) Coins = 0;
            if (HighestUnlocked < 1) HighestUnlocked = 1;
            if (Ledger == null) Ledger = new List<LedgerEntry>();
            if (Inventory == null) Inventory = new List<InventoryEntry>();
            if (Levels == null) Levels = new List<LevelRecord>();
            if (Claims == null) Claims = new HashSet<string>();
            if (Daily == null) Daily = new List<DailyRecord>();
            if (Weekly == null) Weekly = new List<WeeklyRecord>();
            if (Endless == null) Endless = new EndlessRecord();
            if (Tutorials == null) Tutorials = new HashSet<string>();
            if (Settings == null) Settings = new SettingsData();
            if (Stats == null) Stats = new StatsData();
            if (LatestDayKey == null) LatestDayKey = "";
            if (Monetisation == null) Monetisation = new MonetisationRecord();
            Monetisation.Normalize();
            Settings.Clamp();

            levelIndex.Clear();
            var unique = new List<LevelRecord>(Levels.Count);
            foreach (var r in Levels)
            {
                if (r == null || r.Level < 1) continue;
                r.Stars = Math.Max(0, Math.Min(3, r.Stars));
                LevelRecord existing;
                if (levelIndex.TryGetValue(r.Level, out existing))
                {
                    existing.Stars = Math.Max(existing.Stars, r.Stars);
                    existing.BestScore = Math.Max(existing.BestScore, r.BestScore);
                    existing.Completed |= r.Completed;
                    continue;
                }
                levelIndex[r.Level] = r;
                unique.Add(r);
                if (r.Completed && r.Level + 1 > HighestUnlocked) HighestUnlocked = r.Level + 1;
            }
            Levels = unique;
            foreach (var e in Inventory)
            {
                if (e.Owned < 0) e.Owned = 0;
                if (e.Trials < 0) e.Trials = 0;
            }
        }
    }

    public sealed class LedgerEntry
    {
        public long Ticks;
        public int Delta;
        public int Balance;
        public string Reason = "";
    }

    public sealed class InventoryEntry
    {
        public int Id;
        public int Owned;
        public int Trials;
    }

    public sealed class LevelRecord
    {
        public int Level;
        public int BestScore;
        public int Stars;
        public bool Completed;
        public int Plays;
        public int Wins;
    }

    public sealed class DailyRecord
    {
        public string Day = "";
        public int BestScore;
        public int Stars;
        public bool Completed;
        public int Plays;
    }

    public sealed class WeeklyRecord
    {
        public string Week = "";
        public int BestScore;
        public int Runs;
    }

    public sealed class EndlessRecord
    {
        /// <summary>Endless progression: starts at 1 and goes up by one for every stage reached in any run. Unlocks
        /// tile mechanics in Endless (EndlessConfig.mechanics).</summary>
        public int Level = 1;
        public int BestScore;
        public int BestGroup;
        public int BestMoves;
        public int BestStage;
        public int Runs;
    }

    public sealed class SettingsData
    {
        public float SfxVolume = 0.8f;
        public float MusicVolume = 0.5f;
        /// <summary>The last audible volume chosen, restored when a sound toggle is switched back on.</summary>
        public float SfxVolumeWhenOn = 0.8f;
        public float MusicVolumeWhenOn = 0.5f;
        public bool Haptics = true;
        public bool Symbols;
        public bool ReducedMotion;

        public void Clamp()
        {
            SfxVolume = Clamp01(SfxVolume);
            MusicVolume = Clamp01(MusicVolume);
            SfxVolumeWhenOn = Clamp01(SfxVolumeWhenOn);
            MusicVolumeWhenOn = Clamp01(MusicVolumeWhenOn);
        }

        static float Clamp01(float v) => float.IsNaN(v) ? 0.5f : v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// Monetisation state that must survive restarts (docs/MONETISATION.md). Advert limits and the interstitial
    /// counter live here so closing the app can't reset them; processed purchase IDs stop a purchase the store
    /// delivers twice from paying twice; the No Ads entitlement is a cache of what the store last confirmed.
    /// </summary>
    public sealed class MonetisationRecord
    {
        public const int MaxTransactions = 300;

        /// <summary>Campaign levels completed since the last interstitial was actually displayed.</summary>
        public int CompletionsSinceInterstitial;
        /// <summary>When the last interstitial was displayed (UTC ticks; 0 = never).</summary>
        public long LastInterstitialTicks;
        /// <summary>When the last rewarded advert or purchase happened (UTC ticks), for the interstitial quiet period.</summary>
        public long LastQuietTicks;
        /// <summary>The day (ChallengeCalendar.DayKey) that AdsToday counts.</summary>
        public string AdDay = "";
        /// <summary>Rewarded adverts completed today, per placement.</summary>
        public Dictionary<string, int> AdsToday = new Dictionary<string, int>();
        /// <summary>When each placement's last rewarded advert was completed (UTC ticks), for cooldowns.</summary>
        public Dictionary<string, long> LastAdTicks = new Dictionary<string, long>();
        /// <summary>Store transactions already granted (oldest first, capped at MaxTransactions).</summary>
        public List<string> Transactions = new List<string>();
        /// <summary>Last known No Ads ownership, its transaction and when the store last confirmed it (UTC ticks).</summary>
        public bool NoAds;
        public string NoAdsTransaction = "";
        public long NoAdsCheckedTicks;
        /// <summary>Store checks in a row that reported No Ads as not owned while it was saved as owned.</summary>
        public int NoAdsMissingChecks;
        /// <summary>How many times each monetisation event happened on this device (no personal data).</summary>
        public Dictionary<string, int> EventCounts = new Dictionary<string, int>();

        public bool HasTransaction(string key) => !string.IsNullOrEmpty(key) && Transactions.Contains(key);

        public void AddTransaction(string key)
        {
            if (string.IsNullOrEmpty(key) || Transactions.Contains(key)) return;
            Transactions.Add(key);
            if (Transactions.Count > MaxTransactions) Transactions.RemoveRange(0, Transactions.Count - MaxTransactions);
        }

        public void Normalize()
        {
            if (CompletionsSinceInterstitial < 0) CompletionsSinceInterstitial = 0;
            if (AdDay == null) AdDay = "";
            if (AdsToday == null) AdsToday = new Dictionary<string, int>();
            if (LastAdTicks == null) LastAdTicks = new Dictionary<string, long>();
            if (Transactions == null) Transactions = new List<string>();
            if (NoAdsTransaction == null) NoAdsTransaction = "";
            if (NoAdsMissingChecks < 0) NoAdsMissingChecks = 0;
            if (EventCounts == null) EventCounts = new Dictionary<string, int>();
        }
    }

    public sealed class StatsData
    {
        public long TilesCleared;
        public int GroupsCleared;
        public int PowerUpsUsed;
        public int GamesPlayed;
        public int BiggestGroup;
        public int Shuffles;
    }
}
