using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// What each star of a campaign level asks for (docs/MECHANICS.md §4.3), in one place so the level card, the
    /// game screen and the result screen always agree with how stars are actually awarded (PuzzleSession.Stars):
    /// one star for passing the level (every goal met, the target score included when the level has one), two and
    /// three for the final score, spare-move bonus included.
    /// </summary>
    public static class StarChecklist
    {
        public struct Item
        {
            public int Stars;
            /// <summary>The requirement: "Complete every goal", "Score 2,800 points".</summary>
            public string Text;
            public bool Met;
            /// <summary>What the player achieved against it ("2,350 of 2,800", "3 of 4 goals"), or "" before playing.</summary>
            public string Result;
        }

        /// <summary>The level's star requirements, before it is played (level card).</summary>
        public static List<Item> Requirements(LevelSpec level)
        {
            return new List<Item>
            {
                new Item { Stars = 1, Text = PassText(level), Result = "" },
                new Item { Stars = 2, Text = "Score " + NumberFormat.Thousands(level.Star2) + " points", Result = "" },
                new Item { Stars = 3, Text = "Score " + NumberFormat.Thousands(level.Star3) + " points", Result = "" },
            };
        }

        /// <summary>The requirements ticked against a finished run (result screen).</summary>
        public static List<Item> For(LevelSpec level, GoalTracker goals, bool passed, int finalScore, int stars)
        {
            var items = Requirements(level);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.Met = stars >= item.Stars;
                if (item.Stars == 1) item.Result = PassResult(level, goals, passed, finalScore);
                else
                {
                    int needed = item.Stars == 2 ? level.Star2 : level.Star3;
                    item.Result = passed ? NumberFormat.Thousands(finalScore) + " of " + NumberFormat.Thousands(needed) : "Pass first";
                    if (!passed) item.Met = false;
                }
                items[i] = item;
            }
            return items;
        }

        /// <summary>One star: "Complete every goal" (and the target score, when the level has one), or "Reach 2,200 points".</summary>
        public static string PassText(LevelSpec level)
        {
            bool goals = level.Goals != null && level.Goals.Length > 0;
            if (!goals) return "Reach " + NumberFormat.Thousands(level.Target) + " points";
            int scoreGoal = 0;
            foreach (var g in level.Goals) if (g.kind == GoalKind.Score) scoreGoal = g.amount;
            int others = level.Goals.Length - (scoreGoal > 0 ? 1 : 0);
            if (scoreGoal > 0 && others > 0) return "Complete every goal, " + NumberFormat.Thousands(scoreGoal) + " points included";
            return level.Goals.Length == 1 ? "Complete the goal" : "Complete every goal";
        }

        static string PassResult(LevelSpec level, GoalTracker goals, bool passed, int finalScore)
        {
            if (passed) return level.Goals != null && level.Goals.Length > 0 ? "Done" : NumberFormat.Thousands(finalScore) + " of " + NumberFormat.Thousands(level.Target);
            if (goals == null || goals.Count == 0) return NumberFormat.Thousands(finalScore) + " of " + NumberFormat.Thousands(level.Target);
            int done = 0;
            for (int i = 0; i < goals.Count; i++) if (goals.IsComplete(i)) done++;
            return done + " of " + goals.Count + " goals";
        }

        /// <summary>
        /// Why the run stopped short of the next star, in one sentence; null with three stars. Uses the real numbers
        /// (final score, requirement, how far short) so the player knows exactly what to beat.
        /// </summary>
        public static string WhyNotNext(LevelSpec level, int finalScore, int stars, bool finishedEarly, int movesLeftAtFinish)
        {
            if (stars >= 3) return null;
            if (stars <= 0) return "The level was not passed: every goal has to be met before the moves run out.";
            int needed = stars == 1 ? level.Star2 : level.Star3;
            string line = (stars + 1) + " stars need " + NumberFormat.Thousands(needed) + " points: you finished with " +
                          NumberFormat.Thousands(finalScore) + " (" + NumberFormat.Thousands(needed - finalScore) + " short).";
            if (finishedEarly && movesLeftAtFinish > 0)
                line += " You finished with " + movesLeftAtFinish + (movesLeftAtFinish == 1 ? " move" : " moves") + " to spare: playing them usually scores more than their bonus.";
            return line;
        }

        /// <summary>A hint for scoring more next time.</summary>
        public static string Tip(LevelSpec level)
        {
            return "After passing, keep playing: big groups and special tiles add the most points.";
        }
    }
}
