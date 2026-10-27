using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Versions of the coin reward tuning, so a saved config (the Unity config asset) can be moved to new defaults.
    /// Version 2 cut income to about a third after the first playtest: with version 1 an active player earned
    /// about 230 coins a day, so every power-up cost less than a day of play (measured by "Sim -- economy").
    /// </summary>
    public static class EconomyTuning
    {
        public const int Current = 2;

        sealed class Field
        {
            public string Name;
            public Func<GameConfigData, int[]> Get;
            public Action<GameConfigData, int[]> Set;
            public int[] V1, V2;
        }

        static Field Int(string name, Func<GameConfigData, int> get, Action<GameConfigData, int> set, int v1, int v2) => new Field
        {
            Name = name,
            Get = c => new[] { get(c) },
            Set = (c, v) => set(c, v[0]),
            V1 = new[] { v1 },
            V2 = new[] { v2 },
        };

        static readonly Field[] Fields =
        {
            Int("economy.firstClearBase", c => c.Economy.firstClearBase, (c, v) => c.Economy.firstClearBase = v, 10, 5),
            Int("economy.firstClearStep", c => c.Economy.firstClearStep, (c, v) => c.Economy.firstClearStep = v, 2, 1),
            Int("economy.firstClearStepEvery", c => c.Economy.firstClearStepEvery, (c, v) => c.Economy.firstClearStepEvery = v, 5, 10),
            Int("economy.firstClearMax", c => c.Economy.firstClearMax, (c, v) => c.Economy.firstClearMax = v, 30, 15),
            Int("economy.coinsPerNewStar", c => c.Economy.coinsPerNewStar, (c, v) => c.Economy.coinsPerNewStar = v, 2, 1),
            Int("economy.milestoneReward", c => c.Economy.milestoneReward, (c, v) => c.Economy.milestoneReward = v, 50, 20),
            Int("economy.regionFinaleReward", c => c.Economy.regionFinaleReward, (c, v) => c.Economy.regionFinaleReward = v, 100, 40),
            Int("daily.completionReward", c => c.Daily.completionReward, (c, v) => c.Daily.completionReward = v, 30, 15),
            Int("daily.threeStarBonus", c => c.Daily.threeStarBonus, (c, v) => c.Daily.threeStarBonus = v, 10, 5),
            new Field { Name = "rush.tierRewards", Get = c => c.Rush.tierRewards, Set = (c, v) => c.Rush.tierRewards = v, V1 = new[] { 20, 30, 50 }, V2 = new[] { 10, 15, 25 } },
            new Field { Name = "endless.milestoneRewards", Get = c => c.Endless.milestoneRewards, Set = (c, v) => c.Endless.milestoneRewards = v, V1 = new[] { 20, 30, 50, 75, 100 }, V2 = new[] { 10, 15, 25, 40, 60 } },
        };

        /// <summary>
        /// Moves a config saved with an older tuning version to the current defaults. Only values still equal to the
        /// old defaults change; anything edited by hand is kept. Returns the names of the changed values.
        /// </summary>
        public static List<string> Upgrade(GameConfigData config, int fromVersion)
        {
            var changed = new List<string>();
            if (fromVersion >= Current) return changed;
            foreach (var f in Fields)
            {
                if (!SameValues(f.Get(config), f.V1)) continue;
                f.Set(config, (int[])f.V2.Clone());
                changed.Add(f.Name);
            }
            return changed;
        }

        /// <summary>Puts the version 1 reward values back (used by the economy simulation for comparison).</summary>
        public static void ApplyVersion1(GameConfigData config)
        {
            foreach (var f in Fields) f.Set(config, (int[])f.V1.Clone());
        }

        static bool SameValues(int[] a, int[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
