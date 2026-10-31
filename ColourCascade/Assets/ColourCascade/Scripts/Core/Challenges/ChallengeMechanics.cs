using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Which tile mechanics a Daily Challenge or Weekly Rush board uses. The plan comes from the date alone, so it is
    /// the same for everyone; a player then gets the parts of it they have unlocked in the campaign. Everyone who has
    /// unlocked the day's mechanics plays exactly the same puzzle; others play the same board without the parts they
    /// have not been taught yet. Mechanic rules are the shared engine rules (docs/MECHANICS.md §9).
    /// </summary>
    public static class ChallengeMechanics
    {
        // Daily plans: one mechanic, or a pair that simulation showed is fair on a 20-move board. Chains only ever appear
        // alone (with other mechanics they too often leave a chained tile with no matching neighbour).
        static readonly MechanicSet[] DailyPlans =
        {
            MechanicSet.Ice, MechanicSet.Ice | MechanicSet.ColourGoals, MechanicSet.Ice | MechanicSet.Treasure, MechanicSet.Ice | MechanicSet.Moss,
            MechanicSet.Chains, MechanicSet.Treasure, MechanicSet.Treasure | MechanicSet.ColourGoals, MechanicSet.Moss,
            MechanicSet.Moss | MechanicSet.ColourGoals, MechanicSet.Treasure | MechanicSet.Moss,
        };
        static readonly int[] DailyWeights = { 16, 12, 10, 8, 10, 10, 8, 8, 8, 6 };

        /// <summary>The mechanics planned for a date (before the player's unlocks are applied).</summary>
        public static MechanicSet DailyPlan(string dayKey)
        {
            var rng = new Rng(StableHash.Of("daily-mechanics:" + dayKey));
            int total = 0;
            foreach (var w in DailyWeights) total += w;
            int x = rng.Next(total);
            for (int i = 0; i < DailyPlans.Length; i++)
            {
                x -= DailyWeights[i];
                if (x < 0) return DailyPlans[i];
            }
            return DailyPlans[0];
        }

        /// <summary>The day's plan cut down to what the player has unlocked (special tiles: everything they have unlocked).</summary>
        public static MechanicSet DailyFor(string dayKey, MechanicSet unlocked)
        {
            var plan = DailyPlan(dayKey) & unlocked;
            // Thick ice and specials come with the player's own progress.
            if (plan.Has(MechanicSet.Ice) && unlocked.Has(MechanicSet.ThickIce)) plan |= MechanicSet.ThickIce;
            plan |= unlocked & (MechanicSet.Rockets | MechanicSet.Bombs | MechanicSet.Prisms);
            return plan;
        }

        /// <summary>A Weekly Rush theme. Specials themes start the board with several of that special.</summary>
        public static readonly MechanicSet[] WeeklyThemes =
        {
            MechanicSet.Ice, MechanicSet.Rockets, MechanicSet.Chains, MechanicSet.Bombs, MechanicSet.Treasure, MechanicSet.Prisms, MechanicSet.Moss,
        };

        public static MechanicSet WeeklyTheme(string weekKey)
        {
            var rng = new Rng(StableHash.Of("weekly-theme:" + weekKey));
            return WeeklyThemes[rng.Next(WeeklyThemes.Length)];
        }

        /// <summary>
        /// A week's mechanics for a player: the theme (or, if not yet unlocked, the player's most recent obstacle), plus
        /// up to two more of their unlocked obstacles, so weekly boards combine more than daily ones. Never treasure with
        /// chains. Special tiles: everything the player has unlocked.
        /// </summary>
        public static MechanicSet WeeklyFor(string weekKey, MechanicSet unlocked, out MechanicSet theme)
        {
            theme = WeeklyTheme(weekKey);
            var obstacles = Mechanics.ObstaclesIn(unlocked);
            if (!unlocked.Has(theme)) theme = obstacles.Count > 0 ? obstacles[obstacles.Count - 1] : MechanicSet.None;
            var set = theme;
            if (theme == MechanicSet.None) return unlocked & (MechanicSet.Rockets | MechanicSet.Bombs | MechanicSet.Prisms);
            var rng = new Rng(StableHash.Of("weekly-extras:" + weekKey));
            var extras = new List<MechanicSet>();
            foreach (var m in obstacles) if (m != theme) extras.Add(m);
            int wanted = Math.Min(2, extras.Count);
            for (int k = 0; k < wanted && extras.Count > 0; k++)
            {
                int i = rng.Next(extras.Count);
                var m = extras[i];
                extras.RemoveAt(i);
                bool clash = (m == MechanicSet.Treasure && set.Has(MechanicSet.Chains)) || (m == MechanicSet.Chains && set.Has(MechanicSet.Treasure));
                if (!clash) set |= m;
            }
            if (set.Has(MechanicSet.Ice) && unlocked.Has(MechanicSet.ThickIce)) set |= MechanicSet.ThickIce;
            set |= unlocked & (MechanicSet.Rockets | MechanicSet.Bombs | MechanicSet.Prisms);
            return set;
        }

        public static string ThemeName(MechanicSet theme)
        {
            switch (theme)
            {
                case MechanicSet.Ice: return "Frozen week";
                case MechanicSet.Rockets: return "Rocket week";
                case MechanicSet.Bombs: return "Bomb week";
                case MechanicSet.Prisms: return "Prism week";
                case MechanicSet.Chains: return "Chain week";
                case MechanicSet.Treasure: return "Treasure week";
                case MechanicSet.Moss: return "Moss week";
                default: return "";
            }
        }

        /// <summary>
        /// Puts a mechanic set on a challenge board. Amounts are per mode: Weekly Rush boards carry more of each. Returns
        /// the goals the obstacles bring (used by Daily Challenge; Weekly Rush is scored only). percent scales amounts
        /// down when a board turns out too demanding.
        /// </summary>
        public static List<GoalSpec> Decorate(LevelLayout layout, MechanicSet set, bool weekly, int colours, Rng rng, int percent = 100)
        {
            var goals = new List<GoalSpec>();
            int kinds = Mechanics.ObstaclesIn(set).Count + (set.Has(MechanicSet.ColourGoals) ? 1 : 0);
            int pct = (kinds > 1 ? 70 : 100) * percent / 100;
            int Scale(int v) => Math.Max(1, v * pct / 100);
            if (set.Has(MechanicSet.Treasure))
            {
                int placed = BoardDecorator.PlaceTreasure(layout, Math.Max(1, (weekly ? 2 : kinds > 1 ? 1 : 2) * percent / 100), rng);
                if (placed > 0) goals.Add(GoalSpec.CollectTreasure(placed));
            }
            if (set.Has(MechanicSet.Moss))
            {
                int placed = BoardDecorator.PlaceMoss(layout, weekly ? 3 : 2, rng);
                if (placed > 0) goals.Add(GoalSpec.ClearMoss(placed));
            }
            if (set.Has(MechanicSet.Ice))
            {
                int placed = BoardDecorator.PlaceIce(layout, Scale(weekly ? 10 : 7), set.Has(MechanicSet.ThickIce) ? (weekly ? 400 : 250) : 0, rng);
                if (placed > 0) goals.Add(GoalSpec.FreeIce(placed));
            }
            if (set.Has(MechanicSet.Chains))
            {
                int placed = BoardDecorator.PlaceChains(layout, Scale(weekly ? 6 : 4), 2, rng);
                if (placed > 0) goals.Add(GoalSpec.BreakChains(placed));
            }
            if (set.Has(MechanicSet.ColourGoals)) goals.Add(GoalSpec.Collect(rng.Next(Math.Max(1, colours)), Scale(16)));
            return goals;
        }

        /// <summary>Weekly Rush special themes: the board starts with a few of that special on it.</summary>
        public static void PlaceThemeSpecials(LevelLayout layout, MechanicSet theme, Rng rng)
        {
            char piece;
            switch (theme)
            {
                case MechanicSet.Rockets: piece = '-'; break;
                case MechanicSet.Bombs: piece = '*'; break;
                case MechanicSet.Prisms: piece = '@'; break;
                default: return;
            }
            int placed = 0;
            for (int guard = 0; placed < 3 && guard < 100; guard++)
            {
                int c = 1 + rng.Next(Math.Max(1, layout.Width - 2)), r = 1 + rng.Next(Math.Max(1, layout.Height - 2));
                if (!BoardDecorator.Free(layout, c, r)) continue;
                char p = piece == '-' && rng.Next(2) == 0 ? '|' : piece;
                layout.SetPiece(c, r, p);
                placed++;
            }
        }
    }
}
