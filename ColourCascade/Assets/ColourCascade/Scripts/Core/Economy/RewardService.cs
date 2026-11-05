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

        /// <summary>Readable text for a coin ledger reason (a claim key above, a purchase or a grant).</summary>
        public static string Describe(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "Unknown";
            var p = reason.Split('.');
            int n;
            switch (p[0])
            {
                case "c":
                    if (p.Length == 3 && p[1] == "first") return "Level " + p[2] + " cleared";
                    if (p.Length == 4 && p[1] == "star") return "Level " + p[2] + ", star " + p[3];
                    if (p.Length == 3 && p[1] == "mile") return "Level " + p[2] + " milestone";
                    break;
                case "d":
                    if (p.Length == 3 && p[2] == "done") return "Daily " + p[1] + " complete";
                    if (p.Length == 3 && p[2] == "3star") return "Daily " + p[1] + ", 3 stars";
                    break;
                case "w":
                    if (p.Length == 3 && p[2].Length > 1 && p[2][0] == 't' && int.TryParse(p[2].Substring(1), out n)) return "Weekly Rush " + p[1] + ", tier " + (n + 1);
                    break;
                case "e":
                    if (p.Length == 3 && p[1] == "mile" && int.TryParse(p[2], out n)) return "Endless, " + NumberFormat.Thousands(n) + " points";
                    break;
                case "shop":
                    if (p.Length == 3 && p[2].Length > 1 && p[2][0] == 'x' && int.TryParse(p[2].Substring(1), out n))
                        return "Bought " + SplitWords(p[1]) + (n == 1 ? "" : " ×" + n);
                    break;
                case "debug":
                    return "Developer coins";
                case "ad":
                    return p.Length == 2 && p[1] == "shortfall" ? "Advert: coins for a power-up" : "Advert reward";
                case "iap":
                    return "Bought coins";
                case "ach":
                    return "Achievement reward";
                case "refund":
                    if (p.Length == 3 && p[1] == "ExtraMoves" && p[2].Length > 1 && p[2][0] == 'x' && int.TryParse(p[2].Substring(1), out n))
                        return "Extra Moves retired: refund for " + n;
                    break;
                case "continue":
                    if (p.Length >= 2 && p[1].Length > 1 && p[1][0] == 'L')
                        return (p.Length == 3 && p[2] == "refund" ? "Refund: extra moves, level " : "Extra moves, level ") + p[1].Substring(1);
                    break;
            }
            return reason;
        }

        static string SplitWords(string identifier)
        {
            var sb = new System.Text.StringBuilder(identifier.Length + 4);
            for (int i = 0; i < identifier.Length; i++)
            {
                if (i > 0 && char.IsUpper(identifier[i])) sb.Append(' ');
                sb.Append(identifier[i]);
            }
            return sb.ToString();
        }
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

        public RewardSummary ForWeekly(string weekKey, int score) => ForWeekly(weekKey, score, rush.tierScores);

        /// <summary>Medal rewards for a Weekly Rush score, against the medal scores of the board that was played (a board
        /// with mechanics has its medal scores adjusted, so the same effort earns the same medals).</summary>
        public RewardSummary ForWeekly(string weekKey, int score, int[] tierScores)
        {
            var summary = new RewardSummary();
            if (tierScores == null || tierScores.Length == 0) tierScores = rush.tierScores;
            for (int i = 0; i < tierScores.Length && i < rush.tierRewards.Length; i++)
            {
                if (tierScores[i] <= 0 || score < tierScores[i]) continue;
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

        /// <summary>
        /// The first win of a power-up's introduction level: the free uses lent for that attempt and left unused are
        /// kept, in the normal inventory, once ever (docs/DESIGN.md §7). Uses the same claim as the free trials older
        /// versions granted at the start of that level, so nobody gets them twice. Returns the uses kept.
        /// </summary>
        public int KeepTutorialPowerUps(PowerUpDefinition def, int unused, Inventory inventory)
        {
            if (def == null) return 0;
            string key = RewardKeys.PowerUpTrial(def.id);
            if (data.Claims.Contains(key)) return 0;
            data.Claims.Add(key);
            if (unused <= 0) return 0;
            inventory.AddOwned(def.id, unused);
            return unused;
        }

        /// <summary>Whether a power-up's tutorial reward has been settled (kept, or granted by an older version).</summary>
        public bool TutorialPowerUpsSettled(PowerUpId id) => data.Claims.Contains(RewardKeys.PowerUpTrial(id));

        /// <summary>Older versions: grants the free trial uses for a newly introduced power-up, exactly once. Returns uses granted.</summary>
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
