using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum RewardKind
    {
        FirstClear,
        NewStar,
        Milestone,
        RegionFinale,
        DailyComplete,
        DailyThreeStars,
        WeeklyTier,
        EndlessMilestone,
    }

    public struct RewardLine
    {
        public RewardKind Kind;
        public string Label;
        public int Coins;
    }

    public sealed class RewardSummary
    {
        public readonly List<RewardLine> Lines = new List<RewardLine>();
        public int TotalCoins;
        public bool Any => Lines.Count > 0;

        public void Add(RewardKind kind, string label, int coins)
        {
            Lines.Add(new RewardLine { Kind = kind, Label = label, Coins = coins });
            TotalCoins += coins;
        }
    }

    /// <summary>Claim keys for one-time rewards. A key, once in SaveData.Claims, is never paid again.</summary>
    public static class RewardKeys
    {
        public static string CampaignFirstClear(int level) => "c.first." + level;
        public static string CampaignStar(int level, int star) => "c.star." + level + "." + star;
        public static string CampaignMilestone(int level) => "c.mile." + level;
        public static string DailyComplete(string day) => "d." + day + ".done";
        public static string DailyThreeStars(string day) => "d." + day + ".3star";
        public static string WeeklyTier(string week, int tier) => "w." + week + ".t" + tier;
        public static string EndlessMilestone(int score) => "e.mile." + score;
        public static string PowerUpTrial(PowerUpId id) => "pu.trial." + (int)id;
    }

    /// <summary>
    /// Central, idempotent reward granting. Every reward has a claim key; calling any method twice for the
    /// same result (double taps, reloads, replays) can never pay twice.
    /// </summary>
    public sealed class RewardService
    {
        readonly SaveData data;
        readonly Wallet wallet;
        readonly EconomyConfig economy;
        readonly DailyConfig daily;
        readonly RushConfig rush;
        readonly EndlessConfig endless;

        public RewardService(SaveData data, Wallet wallet, EconomyConfig economy, DailyConfig daily, RushConfig rush, EndlessConfig endless)
        {
            this.data = data;
            this.wallet = wallet;
            this.economy = economy;
            this.daily = daily;
            this.rush = rush;
            this.endless = endless;
        }

        public bool IsClaimed(string key) => data.Claims.Contains(key);

        bool Claim(string key, int coins, RewardKind kind, string label, RewardSummary summary)
        {
            if (coins <= 0 || data.Claims.Contains(key)) return false;
            data.Claims.Add(key);
            wallet.Credit(coins, key);
            summary.Add(kind, label, coins);
            return true;
        }

        /// <summary>What a first clear of this level would pay (for the level card).</summary>
        public int FirstClearPreview(LevelSpec level)
        {
            if (IsClaimed(RewardKeys.CampaignFirstClear(level.Number))) return 0;
            int coins = economy.FirstClearReward(level.Number);
            if (level.IsRegionFinale) coins += economy.regionFinaleReward;
            else if (level.IsMilestone) coins += economy.milestoneReward;
            return coins;
        }

        public RewardSummary ForCampaignWin(LevelSpec level, int stars)
        {
            var summary = new RewardSummary();
            int n = level.Number;
            Claim(RewardKeys.CampaignFirstClear(n), economy.FirstClearReward(n), RewardKind.FirstClear, "Level complete", summary);
            int newStars = 0;
            for (int s = 1; s <= stars && s <= 3; s++)
                if (Claim(RewardKeys.CampaignStar(n, s), economy.coinsPerNewStar, RewardKind.NewStar, "Star " + s, summary)) newStars++;
            if (newStars > 1) MergeStarLines(summary, newStars);
            if (level.IsRegionFinale)
                Claim(RewardKeys.CampaignMilestone(n), economy.regionFinaleReward, RewardKind.RegionFinale, "Region complete!", summary);
            else if (level.IsMilestone)
                Claim(RewardKeys.CampaignMilestone(n), economy.milestoneReward, RewardKind.Milestone, "Milestone level", summary);
            return summary;
        }

        static void MergeStarLines(RewardSummary summary, int count)
        {
            int total = 0;
            for (int i = summary.Lines.Count - 1; i >= 0; i--)
            {
                if (summary.Lines[i].Kind != RewardKind.NewStar) continue;
                total += summary.Lines[i].Coins;
                summary.Lines.RemoveAt(i);
            }
            summary.Lines.Add(new RewardLine { Kind = RewardKind.NewStar, Label = count + " new stars", Coins = total });
        }

        /// <summary>Daily rewards are only paid for the newest date the player has opened, so winding the
        /// device clock back cannot be used to re-claim old days.</summary>
        public RewardSummary ForDaily(string dayKey, bool completed, int stars)
        {
            var summary = new RewardSummary();
            if (!completed) return summary;
            if (!string.IsNullOrEmpty(data.LatestDayKey) && string.CompareOrdinal(dayKey, data.LatestDayKey) < 0) return summary;
            Claim(RewardKeys.DailyComplete(dayKey), daily.completionReward, RewardKind.DailyComplete, "Daily complete", summary);
            if (stars >= 3) Claim(RewardKeys.DailyThreeStars(dayKey), daily.threeStarBonus, RewardKind.DailyThreeStars, "Three stars", summary);
            return summary;
        }

        public RewardSummary ForWeekly(string weekKey, int score)
        {
            var summary = new RewardSummary();
            for (int i = 0; i < rush.tierScores.Length && i < rush.tierRewards.Length; i++)
            {
                if (score < rush.tierScores[i]) continue;
                string name = i < rush.tierNames.Length ? rush.tierNames[i] : "Tier " + (i + 1);
                Claim(RewardKeys.WeeklyTier(weekKey, i), rush.tierRewards[i], RewardKind.WeeklyTier, name + " tier", summary);
            }
            return summary;
        }

        public RewardSummary ForEndless(int score)
        {
            var summary = new RewardSummary();
            for (int i = 0; i < endless.milestoneScores.Length && i < endless.milestoneRewards.Length; i++)
            {
                int m = endless.milestoneScores[i];
                if (score >= m)
                    Claim(RewardKeys.EndlessMilestone(m), endless.milestoneRewards[i], RewardKind.EndlessMilestone, "First " + NumberFormat.Thousands(m) + " run", summary);
            }
            return summary;
        }

        /// <summary>Grants the free trial uses for a newly introduced power-up, exactly once. Returns uses granted.</summary>
        public int GrantTrial(PowerUpDefinition def, Inventory inventory)
        {
            if (def == null || def.trialUses <= 0) return 0;
            string key = RewardKeys.PowerUpTrial(def.id);
            if (data.Claims.Contains(key)) return 0;
            data.Claims.Add(key);
            inventory.AddTrials(def.id, def.trialUses);
            return def.trialUses;
        }
    }
}
