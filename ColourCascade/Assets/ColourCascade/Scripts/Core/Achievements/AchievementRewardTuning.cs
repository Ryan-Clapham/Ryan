using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Achievement rewards before update 3.1 (reward tuning version 2), kept so a config asset saved with them can be
    /// moved to the current rewards. Only definitions whose reward still equals its old default change; rewards edited
    /// by hand are kept. Requirements, titles and ids are never touched.
    /// </summary>
    public static class AchievementRewardTuning
    {
        const PowerUpId B = PowerUpId.Blast, T = PowerUpId.TileBreaker, S = PowerUpId.Swap, C = PowerUpId.ColourShift, X = PowerUpId.ColourBurst;

        static AchievementReward R(int coins, params (PowerUpId id, int count)[] items)
        {
            var grants = new PowerUpGrant[items.Length];
            for (int i = 0; i < items.Length; i++) grants[i] = new PowerUpGrant { id = items[i].id, count = items[i].count };
            return new AchievementReward { coins = coins, powerUps = grants };
        }

        static (PowerUpId, int)[] Each(int n) => new[] { (B, n), (T, n), (S, n), (C, n), (X, n) };

        static (PowerUpId, int)[] Plus((PowerUpId, int)[] a, params (PowerUpId, int)[] b)
        {
            var r = new (PowerUpId, int)[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        /// <summary>The 67 rewards of tuning version 2 (the achievements update), by id.</summary>
        static readonly Dictionary<string, AchievementReward> Version2 = new Dictionary<string, AchievementReward>
        {
            { "level_10", R(25) },
            { "level_25", R(50) },
            { "level_50", R(75, (B, 1)) },
            { "level_100", R(125, (T, 1)) },
            { "level_150", R(250, (C, 1), (B, 1)) },
            { "level_200", R(400, (X, 1), (T, 1)) },
            { "level_300", R(600, Each(1)) },
            { "level_400", R(900, (X, 2), (C, 2)) },
            { "level_500", R(1500, Each(2)) },
            { "stars_30", R(30) },
            { "stars_100", R(60, (S, 1)) },
            { "stars_250", R(150, (B, 1)) },
            { "stars_500", R(300, (C, 1), (T, 1)) },
            { "stars_750", R(450, (X, 1), (B, 1)) },
            { "stars_1000", R(800, Each(1)) },
            { "stars_1250", R(1200, Plus(Each(1), (X, 1))) },
            { "regions_1", R(100, (S, 1)) },
            { "regions_3", R(300, (C, 1), (S, 1)) },
            { "regions_5", R(500, (X, 1), (T, 1)) },
            { "regions_10", R(1000, Each(1)) },
            { "regions_15", R(2000, Each(2)) },
            { "endless_5000", R(40) },
            { "endless_10000", R(75, (B, 1)) },
            { "endless_20000", R(250, (C, 1)) },
            { "endless_35000", R(500, (X, 1), (T, 1)) },
            { "endless_50000", R(1000, Each(1)) },
            { "endless_stage_6", R(0, (S, 1)) },
            { "endless_stage_12", R(300, (X, 1)) },
            { "group_12", R(30) },
            { "group_18", R(0, (S, 1), (C, 1)) },
            { "group_25", R(600, (C, 2), (S, 2)) },
            { "move_30", R(75, (B, 1)) },
            { "move_45", R(250, (X, 1)) },
            { "move_60", R(600, (X, 2)) },
            { "combo_2", R(30) },
            { "combo_3", R(75, (T, 1)) },
            { "combo_4", R(300, (B, 2)) },
            { "combo_5", R(700, Each(1)) },
            { "score_15000", R(100) },
            { "score_30000", R(300, (X, 1)) },
            { "score_45000", R(700, (X, 2), (C, 1)) },
            { "pure_10", R(150) },
            { "pure_50", R(450) },
            { "stones_25", R(30) },
            { "stones_200", R(0, (T, 2)) },
            { "stones_600", R(300, (T, 2)) },
            { "ice_50", R(30) },
            { "ice_400", R(100, (B, 1)) },
            { "ice_1200", R(350, (B, 2)) },
            { "chains_25", R(40) },
            { "chains_150", R(50, (C, 1)) },
            { "chains_400", R(350, (C, 2)) },
            { "treasure_5", R(40) },
            { "treasure_40", R(100, (T, 1)) },
            { "treasure_100", R(400, (X, 1)) },
            { "moss_30", R(40) },
            { "moss_200", R(150, (B, 1)) },
            { "tiles_10000", R(50) },
            { "tiles_100000", R(800, Each(1)) },
            { "powerups_each_1", R(100) },
            { "powerups_each_10", R(300, Each(1)) },
            { "daily_1", R(30) },
            { "daily_7", R(75, (B, 1)) },
            { "daily_30", R(300, (X, 1)) },
            { "daily_100", R(1000, Each(1)) },
            { "weekly_bronze", R(40) },
            { "weekly_gold", R(250, (C, 1)) },
        };

        /// <summary>
        /// Moves every definition whose reward is still its version 2 default to the current default reward (in place).
        /// Returns the ids changed.
        /// </summary>
        public static List<string> Upgrade(AchievementDefinition[] definitions)
        {
            var changed = new List<string>();
            if (definitions == null) return changed;
            var current = new Dictionary<string, AchievementDefinition>();
            foreach (var d in AchievementCatalog.CreateDefaults()) current[d.id] = d;
            foreach (var d in definitions)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                AchievementReward old;
                AchievementDefinition now;
                if (!Version2.TryGetValue(d.id, out old) || !current.TryGetValue(d.id, out now)) continue;
                if (!Same(d.reward, old)) continue;
                d.reward = now.reward;
                changed.Add(d.id);
            }
            return changed;
        }

        /// <summary>A copy of the definitions with the version 2 rewards (the economy simulation compares versions).</summary>
        public static AchievementDefinition[] WithVersion2Rewards(AchievementDefinition[] definitions)
        {
            if (definitions == null) return null;
            var copy = new AchievementDefinition[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                var d = definitions[i];
                if (d == null) continue;
                AchievementReward old;
                copy[i] = new AchievementDefinition
                {
                    id = d.id, title = d.title, description = d.description, category = d.category, tier = d.tier, stat = d.stat,
                    target = d.target, param = d.param, reward = Version2.TryGetValue(d.id, out old) ? old : d.reward,
                };
            }
            return copy;
        }

        /// <summary>The version 2 reward of an achievement (null if it had none).</summary>
        public static AchievementReward Version2Reward(string id)
        {
            AchievementReward r;
            return id != null && Version2.TryGetValue(id, out r) ? r : null;
        }

        static bool Same(AchievementReward a, AchievementReward b)
        {
            if (a == null || b == null) return a == b;
            if (a.coins != b.coins) return false;
            var x = Totals(a); var y = Totals(b);
            if (x.Count != y.Count) return false;
            foreach (var kv in x)
            {
                int n;
                if (!y.TryGetValue(kv.Key, out n) || n != kv.Value) return false;
            }
            return true;
        }

        static Dictionary<PowerUpId, int> Totals(AchievementReward r)
        {
            var t = new Dictionary<PowerUpId, int>();
            if (r.powerUps != null)
                foreach (var g in r.powerUps)
                {
                    if (g == null || g.count <= 0) continue;
                    int n;
                    t.TryGetValue(g.id, out n);
                    t[g.id] = n + g.count;
                }
            return t;
        }
    }
}
