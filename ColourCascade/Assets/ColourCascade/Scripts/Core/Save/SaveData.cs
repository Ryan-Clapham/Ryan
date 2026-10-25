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
        public bool Haptics = true;
        public bool Symbols;
        public bool ReducedMotion;

        public void Clamp()
        {
            SfxVolume = Clamp01(SfxVolume);
            MusicVolume = Clamp01(MusicVolume);
        }

        static float Clamp01(float v) => float.IsNaN(v) ? 0.5f : v < 0 ? 0 : v > 1 ? 1 : v;
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
