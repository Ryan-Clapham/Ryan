using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>What each star of a level asks for, ticked against a result: shown when a level is complete with fewer than
    /// three stars, so the player knows how to earn the rest.</summary>
    public static class StarChecklist
    {
        public struct Item
        {
            public int Stars;
            public string Text;
            public bool Met;
        }

        public static List<Item> For(LevelSpec level, int finalScore, int stars)
        {
            bool goals = level.Goals != null && level.Goals.Length > 0;
            return new List<Item>
            {
                new Item { Stars = 1, Met = stars >= 1, Text = goals ? "Complete every goal" : "Reach the target of " + NumberFormat.Thousands(level.Target) + " points" },
                new Item { Stars = 2, Met = stars >= 2, Text = "Finish with " + NumberFormat.Thousands(level.Star2) + " points" + Short(level.Star2, finalScore) },
                new Item { Stars = 3, Met = stars >= 3, Text = "Finish with " + NumberFormat.Thousands(level.Star3) + " points" + Short(level.Star3, finalScore) },
            };
        }

        static string Short(int needed, int score) => score >= needed ? "" : " (" + NumberFormat.Thousands(needed - score) + " more)";

        /// <summary>A hint for scoring more next time.</summary>
        public static string Tip(LevelSpec level)
        {
            if (level.LeftoverBonusPerMove > 0)
                return "Every spare move adds " + NumberFormat.Thousands(level.LeftoverBonusPerMove) + " points, and big groups score far more.";
            return "Big groups score far more than small ones.";
        }
    }
}
