using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>The words the achievement screen and notifications show (kept here so they are tested).</summary>
    public static class AchievementText
    {
        public static string Tier(AchievementTier t)
        {
            switch (t)
            {
                case AchievementTier.Easy: return "Easy";
                case AchievementTier.Medium: return "Medium";
                case AchievementTier.Hard: return "Hard";
                default: return "Very hard";
            }
        }

        public static string Category(AchievementCategory c)
        {
            switch (c)
            {
                case AchievementCategory.Campaign: return "Campaign";
                case AchievementCategory.Stars: return "Stars";
                case AchievementCategory.Regions: return "Perfect regions";
                case AchievementCategory.Endless: return "Endless";
                case AchievementCategory.Skill: return "Skill";
                case AchievementCategory.Mechanics: return "Board mastery";
                default: return "Challenges";
            }
        }

        static string Name(PowerUpId id, PowerUpCatalog powerUps)
        {
            var d = powerUps?.Get(id);
            return d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : id.ToString();
        }

        static readonly string[] Counts = { "", "one", "two", "three", "four", "five" };

        /// <summary>
        /// "125 coins + Tile Breaker", "300 coins + Blast ×2", "1,500 coins + two of each power-up". Power-ups the game no
        /// longer has are left out, as they are when the reward is paid.
        /// </summary>
        public static string Reward(AchievementReward reward, PowerUpCatalog powerUps)
        {
            var parts = new List<string>();
            if (reward == null) return "";
            if (reward.coins > 0) parts.Add(NumberFormat.Thousands(reward.coins) + (reward.coins == 1 ? " coin" : " coins"));
            // Count the grants per power-up (a reward may list one twice), keeping their order.
            var order = new List<PowerUpId>();
            var count = new Dictionary<PowerUpId, int>();
            if (reward.powerUps != null)
                foreach (var g in reward.powerUps)
                {
                    if (g == null || g.count <= 0 || RetiredPowerUps.IsRetired(g.id) || (powerUps != null && powerUps.Get(g.id) == null)) continue;
                    if (!count.ContainsKey(g.id))
                    {
                        order.Add(g.id);
                        count[g.id] = 0;
                    }
                    count[g.id] += g.count;
                }
            // "of each": every power-up in the game, the same number of times (the rest listed as extras).
            int each = 0;
            if (powerUps != null && powerUps.All.Count > 1)
            {
                each = int.MaxValue;
                foreach (var p in powerUps.All)
                {
                    int n;
                    each = Math.Min(each, count.TryGetValue(p.id, out n) ? n : 0);
                }
                if (each == int.MaxValue) each = 0;
            }
            if (each > 0)
            {
                parts.Add((each < Counts.Length ? Counts[each] : each.ToString()) + " of each power-up");
                foreach (var p in powerUps.All) count[p.id] -= each;
            }
            foreach (var id in order)
            {
                int n = count[id];
                if (n <= 0) continue;
                parts.Add(n == 1 ? Name(id, powerUps) : Name(id, powerUps) + " ×" + n);
            }
            return string.Join(" + ", parts.ToArray());
        }

        static string Medal(int medal, string[] names)
        {
            if (medal <= 0) return "No medal yet";
            int i = medal - 1;
            return names != null && i < names.Length && !string.IsNullOrEmpty(names[i]) ? names[i] : "Medal " + medal;
        }

        /// <summary>Where the player stands, for a locked achievement: "37 / 50", "Level 37 / 50", "Best: Silver".</summary>
        public static string Progress(AchievementDefinition d, long current, string[] medalNames)
        {
            long shown = Math.Max(0, Math.Min(current, d.target));
            switch (d.stat)
            {
                case AchievementStat.WeeklyBestMedal: return current <= 0 ? Medal(0, medalNames) : "Best: " + Medal((int)Math.Min(int.MaxValue, current), medalNames);
                case AchievementStat.LevelReached: return "Level " + NumberFormat.Thousands(shown) + " / " + NumberFormat.Thousands(d.target);
                case AchievementStat.EndlessBestStage: return "Stage " + NumberFormat.Thousands(shown) + " / " + NumberFormat.Thousands(d.target);
                default: return NumberFormat.Thousands(shown) + " / " + NumberFormat.Thousands(d.target);
            }
        }

        /// <summary>The line a notification shows for several unlocks at once.</summary>
        public static string Several(int n) => n == 1 ? "1 achievement unlocked" : n + " achievements unlocked";
    }
}
