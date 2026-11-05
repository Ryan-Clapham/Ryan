using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum AchievementState
    {
        Locked,
        /// <summary>Earned; its reward is waiting to be claimed.</summary>
        Unlocked,
        Claimed,
    }

    public enum AchievementClaim
    {
        Claimed,
        NotUnlocked,
        AlreadyClaimed,
        Unknown,
    }

    /// <summary>
    /// Achievements (docs/ACHIEVEMENTS.md). A fixed catalog of definitions; progress is read from what the game already
    /// records (campaign progress and stars, the Endless record, lifetime statistics, claimed challenge rewards), never
    /// kept twice. Evaluate unlocks what has been earned (on load, so existing players get what their save proves, and
    /// after every run); Claim pays a reward once, into the wallet and the power-up inventory, guarded by a claim key.
    /// </summary>
    public sealed class AchievementService
    {
        /// <summary>The most stars a campaign level can have (the star rating's maximum).</summary>
        public const int MaxStarsPerLevel = 3;

        readonly AchievementCatalog catalog;
        readonly Func<SaveData> data;
        readonly Func<Wallet> wallet;
        readonly Func<Inventory> inventory;
        readonly PowerUpCatalog powerUps;
        readonly int levelsPerRegion;
        readonly IClock clock;
        readonly Func<bool> save;

        public AchievementService(AchievementCatalog catalog, Func<SaveData> data, Func<Wallet> wallet, Func<Inventory> inventory,
            PowerUpCatalog powerUps, int levelsPerRegion, IClock clock, Func<bool> save)
        {
            this.catalog = catalog ?? new AchievementCatalog(null);
            this.data = data;
            this.wallet = wallet;
            this.inventory = inventory;
            this.powerUps = powerUps;
            this.levelsPerRegion = Math.Max(1, levelsPerRegion);
            this.clock = clock ?? new SystemClock();
            this.save = save ?? (() => true);
        }

        /// <summary>Achievements just unlocked (one call can unlock several: they arrive together, in catalog order).</summary>
        public event Action<IReadOnlyList<AchievementDefinition>> Unlocked;
        /// <summary>Something changed (unlocked or claimed): refresh what shows achievements.</summary>
        public event Action Changed;

        public AchievementCatalog Catalog => catalog;
        public IReadOnlyList<AchievementDefinition> All => catalog.All;
        public int Total => catalog.Count;

        public static string ClaimKey(string id) => "ach." + id;

        SaveData Data => data();

        public bool IsUnlocked(AchievementDefinition d) => d != null && Data.Achievements.Unlocked.ContainsKey(d.id);
        public bool IsClaimed(AchievementDefinition d) => d != null && Data.Claims.Contains(ClaimKey(d.id));

        public AchievementState State(AchievementDefinition d)
        {
            if (!IsUnlocked(d)) return AchievementState.Locked;
            return IsClaimed(d) ? AchievementState.Claimed : AchievementState.Unlocked;
        }

        /// <summary>Unlocked (claimed or not) out of the total.</summary>
        public int Completed
        {
            get
            {
                int n = 0;
                foreach (var d in catalog.All) if (IsUnlocked(d)) n++;
                return n;
            }
        }

        public int Unclaimed
        {
            get
            {
                int n = 0;
                foreach (var d in catalog.All) if (State(d) == AchievementState.Unlocked) n++;
                return n;
            }
        }

        /// <summary>Whole-number completion percentage (100 only when every achievement is done).</summary>
        public int Percent => Total == 0 ? 0 : Completed * 100 / Total;

        public List<AchievementDefinition> InCategory(AchievementCategory category)
        {
            var list = new List<AchievementDefinition>();
            foreach (var d in catalog.All) if (d.category == category) list.Add(d);
            return list;
        }

        // ------------------------------------------------------------------ Progress

        /// <summary>Where the player stands on this achievement's measure (compare with its target).</summary>
        public long Current(AchievementDefinition d)
        {
            var s = Data;
            switch (d.stat)
            {
                case AchievementStat.LevelReached: return s.HighestUnlocked;
                case AchievementStat.TotalStars:
                    {
                        long n = 0;
                        foreach (var r in s.Levels) n += Math.Min(MaxStarsPerLevel, r.Stars);
                        return n;
                    }
                case AchievementStat.PerfectRegions: return PerfectRegions(s);
                case AchievementStat.EndlessBestScore: return s.Endless.BestScore;
                case AchievementStat.EndlessBestStage: return s.Endless.BestStage;
                case AchievementStat.BiggestGroup: return s.Stats.BiggestGroup;
                case AchievementStat.MostTilesInOneMove: return s.Stats.MostTilesInOneMove;
                case AchievementStat.BiggestCombo: return s.Stats.BiggestCombo;
                case AchievementStat.BestLevelScore:
                    {
                        long best = 0;
                        foreach (var r in s.Levels) if (r.BestScore > best) best = r.BestScore;
                        return best;
                    }
                case AchievementStat.PureThreeStarLevels:
                    {
                        long n = 0;
                        foreach (var level in s.Stats.PureThreeStarLevels) if (level >= d.param) n++;
                        return n;
                    }
                case AchievementStat.StonesBroken: return s.Stats.StonesBroken;
                case AchievementStat.IceLayersRemoved: return s.Stats.IceLayersRemoved;
                case AchievementStat.ChainsReleased: return s.Stats.ChainsReleased;
                case AchievementStat.TreasuresCollected: return s.Stats.TreasuresCollected;
                case AchievementStat.MossRemoved: return s.Stats.MossRemoved;
                case AchievementStat.TilesCleared: return s.Stats.TilesCleared;
                case AchievementStat.PowerUpMastery:
                    {
                        long n = 0;
                        int each = Math.Max(1, d.param);
                        if (powerUps != null) foreach (var p in powerUps.All) n += Math.Min(each, s.Stats.UsesOf(p.id));
                        return n;
                    }
                case AchievementStat.DailyChallengesCompleted: return DailyCompleted(s);
                case AchievementStat.WeeklyBestMedal: return WeeklyBestMedal(s);
                default: return 0;
            }
        }

        /// <summary>Progress from 0 to 1.</summary>
        public float Fraction(AchievementDefinition d)
        {
            if (IsUnlocked(d)) return 1f;
            long t = Math.Max(1, d.target);
            return (float)Math.Min(1.0, Math.Max(0, Current(d)) / (double)t);
        }

        /// <summary>Regions in which every level has the maximum stars (levels 1-20 are region 1, and so on).</summary>
        int PerfectRegions(SaveData s)
        {
            var perfectLevels = new Dictionary<int, int>();
            foreach (var r in s.Levels)
            {
                if (r.Level < 1 || r.Stars < MaxStarsPerLevel) continue;
                int region = (r.Level - 1) / levelsPerRegion;
                int n;
                perfectLevels.TryGetValue(region, out n);
                perfectLevels[region] = n + 1;
            }
            int count = 0;
            foreach (var kv in perfectLevels) if (kv.Value >= levelsPerRegion) count++;
            return count;
        }

        /// <summary>Distinct days whose Daily Challenge was completed: the completion rewards claimed (kept for every day)
        /// together with the day records (the last 60 days).</summary>
        static int DailyCompleted(SaveData s)
        {
            var days = new HashSet<string>();
            foreach (var c in s.Claims)
                if (c.Length > 7 && c.StartsWith("d.", StringComparison.Ordinal) && c.EndsWith(".done", StringComparison.Ordinal)) days.Add(c.Substring(2, c.Length - 7));
            foreach (var r in s.Daily) if (r.Completed && !string.IsNullOrEmpty(r.Day)) days.Add(r.Day);
            return days.Count;
        }

        /// <summary>Best Weekly Rush medal from the medal rewards claimed: 1 bronze, 2 silver, 3 gold.</summary>
        static int WeeklyBestMedal(SaveData s)
        {
            int best = 0;
            foreach (var c in s.Claims)
            {
                if (!c.StartsWith("w.", StringComparison.Ordinal)) continue;
                int t = c.LastIndexOf(".t", StringComparison.Ordinal);
                int tier;
                if (t > 1 && int.TryParse(c.Substring(t + 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out tier))
                    best = Math.Max(best, tier + 1);
            }
            return best;
        }

        // ------------------------------------------------------------------ Unlocking

        /// <summary>
        /// Unlocks every achievement whose target is now met, recording when. Safe to call any time and repeatedly:
        /// an achievement unlocks once. Returns the newly unlocked ones (also raised as Unlocked). Does not save: the
        /// caller saves (FinishRun, AbandonRun and Load all do).
        /// </summary>
        public List<AchievementDefinition> Evaluate()
        {
            var newly = new List<AchievementDefinition>();
            var rec = Data.Achievements;
            foreach (var d in catalog.All)
            {
                if (rec.Unlocked.ContainsKey(d.id)) continue;
                if (Current(d) >= d.target)
                {
                    rec.Unlocked[d.id] = clock.UtcNow.Ticks;
                    newly.Add(d);
                }
            }
            if (newly.Count > 0)
            {
                Unlocked?.Invoke(newly);
                Changed?.Invoke();
            }
            return newly;
        }

        // ------------------------------------------------------------------ Claiming

        /// <summary>Pays an unlocked achievement's reward, once: coins to the wallet, power-ups to the inventory.</summary>
        public AchievementClaim Claim(string id)
        {
            var d = catalog.Get(id);
            if (d == null) return AchievementClaim.Unknown;
            if (!IsUnlocked(d)) return AchievementClaim.NotUnlocked;
            string key = ClaimKey(d.id);
            var s = Data;
            if (s.Claims.Contains(key)) return AchievementClaim.AlreadyClaimed;
            s.Claims.Add(key);
            var reward = d.reward ?? new AchievementReward();
            if (reward.coins > 0) wallet()?.Credit(reward.coins, key);
            var inv = inventory();
            if (reward.powerUps != null && inv != null)
                foreach (var g in reward.powerUps)
                    if (g != null && g.count > 0 && PaysOut(g.id)) inv.AddOwned(g.id, g.count);
            save();
            Changed?.Invoke();
            return AchievementClaim.Claimed;
        }

        /// <summary>Claims every unlocked reward; returns how many.</summary>
        public int ClaimAll()
        {
            int n = 0;
            foreach (var d in catalog.All)
                if (State(d) == AchievementState.Unlocked && Claim(d.id) == AchievementClaim.Claimed) n++;
            return n;
        }

        /// <summary>A reward power-up that is still in the game (retired ones are skipped).</summary>
        public bool PaysOut(PowerUpId id) => !RetiredPowerUps.IsRetired(id) && (powerUps == null || powerUps.Get(id) != null);
    }
}
