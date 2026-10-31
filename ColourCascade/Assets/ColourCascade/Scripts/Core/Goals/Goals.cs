using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>What a campaign level asks for (docs/MECHANICS.md §4). Values are stable: they are saved in the config asset.</summary>
    public enum GoalKind
    {
        /// <summary>Reach a score.</summary>
        Score = 0,
        /// <summary>Remove a number of tiles of one colour, by any means.</summary>
        CollectColour = 1,
        /// <summary>Free a number of frozen tiles (their last layer of ice removed).</summary>
        FreeIce = 2,
        /// <summary>Free a number of chained tiles (their last link removed).</summary>
        BreakChains = 3,
        /// <summary>Collect a number of treasures (they reach the bottom of their section).</summary>
        CollectTreasure = 4,
        /// <summary>Leave no moss on the board. The amount is the starting moss, for display.</summary>
        ClearMoss = 5,
    }

    /// <summary>One goal of a level. Serializable so hand-made levels can list goals in the config asset.</summary>
    [Serializable]
    public sealed class GoalSpec
    {
        public GoalKind kind;
        public int amount;
        /// <summary>The colour for CollectColour goals (TileColours index); unused otherwise.</summary>
        public int colour = -1;

        public static GoalSpec Score(int amount) => new GoalSpec { kind = GoalKind.Score, amount = amount };
        public static GoalSpec Collect(int colour, int amount) => new GoalSpec { kind = GoalKind.CollectColour, colour = colour, amount = amount };
        public static GoalSpec FreeIce(int amount) => new GoalSpec { kind = GoalKind.FreeIce, amount = amount };
        public static GoalSpec BreakChains(int amount) => new GoalSpec { kind = GoalKind.BreakChains, amount = amount };
        public static GoalSpec CollectTreasure(int amount) => new GoalSpec { kind = GoalKind.CollectTreasure, amount = amount };
        /// <summary>Clear all moss. Leave the amount at 0 to use the starting moss count.</summary>
        public static GoalSpec ClearMoss(int startingMoss = 0) => new GoalSpec { kind = GoalKind.ClearMoss, amount = startingMoss };

        public GoalSpec Clone() => new GoalSpec { kind = kind, amount = amount, colour = colour };

        public override string ToString() => kind == GoalKind.CollectColour ? kind + " " + colour + " x" + amount : kind + " " + amount;
    }

    /// <summary>
    /// Progress towards one level's goals during a run. Reads what each action did (MoveOutcome), the running
    /// score and, for moss, the board; knows nothing about the UI. A level is complete when every goal is.
    /// </summary>
    public sealed class GoalTracker
    {
        readonly GoalSpec[] goals;
        readonly int[] progress;
        // ClearMoss: moss on the board right now (it can grow, so progress is not a running count).
        readonly int[] live;

        public GoalTracker(IList<GoalSpec> goalSpecs, Board board = null)
        {
            int n = goalSpecs != null ? goalSpecs.Count : 0;
            goals = new GoalSpec[n];
            progress = new int[n];
            live = new int[n];
            for (int i = 0; i < n; i++)
            {
                goals[i] = goalSpecs[i].Clone();
                if (goals[i].kind != GoalKind.ClearMoss) continue;
                live[i] = board != null ? board.CountObstacle(ObstacleKind.Moss) : 0;
                if (goals[i].amount <= 0) goals[i].amount = live[i];
            }
        }

        public int Count => goals.Length;
        public GoalSpec Goal(int i) => goals[i];
        public int Progress(int i) => goals[i].kind == GoalKind.ClearMoss ? Math.Max(0, goals[i].amount - live[i]) : progress[i];
        public int Remaining(int i) => goals[i].kind == GoalKind.ClearMoss ? live[i] : Math.Max(0, goals[i].amount - progress[i]);
        public bool IsComplete(int i) => goals[i].kind == GoalKind.ClearMoss ? live[i] <= 0 : progress[i] >= goals[i].amount;

        /// <summary>True when every goal is met. A tracker with no goals is never complete.</summary>
        public bool AllComplete
        {
            get
            {
                if (goals.Length == 0) return false;
                for (int i = 0; i < goals.Length; i++) if (!IsComplete(i)) return false;
                return true;
            }
        }

        /// <summary>Index of the first goal of this kind (and colour, for CollectColour), or -1.</summary>
        public int IndexOf(GoalKind kind, int colour = -1)
        {
            for (int i = 0; i < goals.Length; i++)
                if (goals[i].kind == kind && (kind != GoalKind.CollectColour || goals[i].colour == colour)) return i;
            return -1;
        }

        /// <summary>Adds what one action achieved. Returns true if any goal's progress changed.</summary>
        public bool Record(MoveOutcome o, int score, Board board = null)
        {
            bool changed = false;
            for (int i = 0; i < goals.Length; i++)
            {
                if (goals[i].kind == GoalKind.ClearMoss)
                {
                    if (board == null) continue;
                    int now = board.CountObstacle(ObstacleKind.Moss);
                    if (now != live[i]) changed = true;
                    live[i] = now;
                    continue;
                }
                int before = progress[i];
                switch (goals[i].kind)
                {
                    case GoalKind.Score:
                        progress[i] = score;
                        break;
                    case GoalKind.CollectColour:
                        for (int k = 0; k < o.Cleared.Count; k++)
                            if (!o.Cleared[k].WasStone && o.Cleared[k].Colour == goals[i].colour) progress[i]++;
                        break;
                    case GoalKind.FreeIce:
                        progress[i] += CountFreed(o, ObstacleKind.Ice);
                        break;
                    case GoalKind.BreakChains:
                        progress[i] += CountFreed(o, ObstacleKind.Chain);
                        break;
                    case GoalKind.CollectTreasure:
                        progress[i] += o.Treasures.Count;
                        break;
                }
                if (progress[i] != before) changed = true;
            }
            return changed;
        }

        static int CountFreed(MoveOutcome o, ObstacleKind kind)
        {
            int n = 0;
            for (int k = 0; k < o.ObstacleHits.Count; k++)
                if (o.ObstacleHits[k].Kind == kind && o.ObstacleHits[k].Freed) n++;
            return n;
        }
    }
}
