using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Versions of the coin reward tuning, so a saved config (the Unity config asset) can be moved to new defaults.
    /// Version 2 cut income to about a third after the first playtest: with version 1 an active player earned
    /// about 230 coins a day, so every power-up cost less than a day of play (measured by "Sim -- economy").
    /// Version 3 (update 3.1, the coin economy audit) trims play rewards again, pays 2 / 3 / 5 coins for the three
    /// stars, cuts the coin adverts to 20 coins, three a day shared by both coin adverts, and rebalances the
    /// achievement rewards (AchievementRewardTuning).
    /// </summary>
    public static class EconomyTuning
    {
        public const int Current = 3;

        sealed class Field
        {
            public string Name;
            public Func<GameConfigData, int[]> Get;
            public Action<GameConfigData, int[]> Set;
            /// <summary>The default in each tuning version: [0] = version 1, [1] = version 2, [2] = version 3.</summary>
            public int[][] Versions;
        }

        static Field Int(string name, Func<GameConfigData, int> get, Action<GameConfigData, int> set, params int[] versions)
        {
            var v = new int[versions.Length][];
            for (int i = 0; i < versions.Length; i++) v[i] = new[] { versions[i] };
            return new Field { Name = name, Get = c => new[] { get(c) }, Set = (c, x) => set(c, x[0]), Versions = v };
        }

        static Field Ints(string name, Func<GameConfigData, int[]> get, Action<GameConfigData, int[]> set, params int[][] versions) =>
            new Field { Name = name, Get = get, Set = set, Versions = versions };

        static readonly Field[] Fields =
        {
            Int("economy.firstClearBase", c => c.Economy.firstClearBase, (c, v) => c.Economy.firstClearBase = v, 10, 5, 3),
            Int("economy.firstClearStep", c => c.Economy.firstClearStep, (c, v) => c.Economy.firstClearStep = v, 2, 1, 1),
            Int("economy.firstClearStepEvery", c => c.Economy.firstClearStepEvery, (c, v) => c.Economy.firstClearStepEvery = v, 5, 10, 25),
            Int("economy.firstClearMax", c => c.Economy.firstClearMax, (c, v) => c.Economy.firstClearMax = v, 30, 15, 8),
            Int("economy.coinsPerNewStar", c => c.Economy.coinsPerNewStar, (c, v) => c.Economy.coinsPerNewStar = v, 2, 1, 1),
            Int("economy.milestoneReward", c => c.Economy.milestoneReward, (c, v) => c.Economy.milestoneReward = v, 50, 20, 20),
            Int("economy.regionFinaleReward", c => c.Economy.regionFinaleReward, (c, v) => c.Economy.regionFinaleReward = v, 100, 40, 40),
            Int("daily.completionReward", c => c.Daily.completionReward, (c, v) => c.Daily.completionReward = v, 30, 15, 10),
            Int("daily.threeStarBonus", c => c.Daily.threeStarBonus, (c, v) => c.Daily.threeStarBonus = v, 10, 5, 5),
            Ints("rush.tierRewards", c => c.Rush.tierRewards, (c, v) => c.Rush.tierRewards = v, new[] { 20, 30, 50 }, new[] { 10, 15, 25 }, new[] { 10, 15, 25 }),
            Ints("endless.milestoneRewards", c => c.Endless.milestoneRewards, (c, v) => c.Endless.milestoneRewards = v,
                new[] { 20, 30, 50, 75, 100 }, new[] { 10, 15, 25, 40, 60 }, new[] { 5, 10, 15, 25, 40 }),
            // The coin adverts arrived in update 3.0 (tuning version 2), so version 1 configs have version 2's values.
            Int("ads.shopCoins.reward", c => c.Monetisation.ads.shopCoins.reward, (c, v) => c.Monetisation.ads.shopCoins.reward = v, 50, 50, 20),
            Int("ads.shopCoins.dailyLimit", c => c.Monetisation.ads.shopCoins.dailyLimit, (c, v) => c.Monetisation.ads.shopCoins.dailyLimit = v, 5, 5, 3),
            Int("ads.coinShortfall.dailyLimit", c => c.Monetisation.ads.coinShortfall.dailyLimit, (c, v) => c.Monetisation.ads.coinShortfall.dailyLimit = v, 5, 5, 3),
        };

        /// <summary>
        /// Moves a config saved with an older tuning version to the current defaults. Only values still equal to an
        /// older version's defaults change; anything edited by hand is kept. Returns the names of the changed values.
        /// Achievement rewards in the config are moved separately (AchievementRewardTuning.Upgrade).
        /// </summary>
        public static List<string> Upgrade(GameConfigData config, int fromVersion)
        {
            var changed = new List<string>();
            if (fromVersion >= Current) return changed;
            // Version 3 pays each star differently. A config whose single star value was edited by hand keeps it for
            // all three stars; any other gets 2 / 3 / 5.
            int perStar = config.Economy.coinsPerNewStar;
            int[] stars = perStar == 1 || perStar == 2 ? new[] { 2, 3, 5 } : new[] { perStar, perStar, perStar };
            var had = config.Economy.starRewards;
            bool untouched = had == null || had.Length == 0 || SameValues(had, new[] { 2, 3, 5 }) || SameValues(had, new[] { perStar, perStar, perStar }) ||
                             SameValues(had, new[] { 1, 1, 1 }) || SameValues(had, new[] { 2, 2, 2 });
            if (untouched && !SameValues(had, stars))
            {
                config.Economy.starRewards = stars;
                changed.Add("economy.starRewards");
            }
            foreach (var f in Fields)
            {
                var current = f.Get(config);
                var target = f.Versions[Current - 1];
                if (SameValues(current, target)) continue;
                bool old = false;
                for (int v = 0; v < Current - 1; v++)
                    if (SameValues(current, f.Versions[v])) old = true;
                if (!old) continue;
                f.Set(config, (int[])target.Clone());
                changed.Add(f.Name);
            }
            // The two coin adverts share one daily count from version 3.
            var ads = config.Monetisation.ads;
            bool grouped = false;
            foreach (var p in new[] { ads.shopCoins, ads.coinShortfall })
                if (p != null && string.IsNullOrEmpty(p.dailyLimitGroup))
                {
                    p.dailyLimitGroup = AdConfig.CoinAdvertGroup;
                    grouped = true;
                }
            if (grouped) changed.Add("ads.dailyLimitGroup");
            return changed;
        }

        /// <summary>Puts an older version's reward values back (the economy simulation compares versions).</summary>
        public static void ApplyVersion(GameConfigData config, int version)
        {
            int i = Math.Max(1, Math.Min(Current, version)) - 1;
            foreach (var f in Fields) f.Set(config, (int[])f.Versions[i].Clone());
            int perStar = config.Economy.coinsPerNewStar;
            config.Economy.starRewards = version >= 3 ? new[] { 2, 3, 5 } : new[] { perStar, perStar, perStar };
            string group = version >= 3 ? AdConfig.CoinAdvertGroup : "";
            config.Monetisation.ads.shopCoins.dailyLimitGroup = group;
            config.Monetisation.ads.coinShortfall.dailyLimitGroup = group;
            if (version < 3) config.Achievements = AchievementRewardTuning.WithVersion2Rewards(config.Achievements);
        }

        /// <summary>Puts the version 1 reward values back (used by the economy simulation for comparison).</summary>
        public static void ApplyVersion1(GameConfigData config) => ApplyVersion(config, 1);

        static bool SameValues(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
