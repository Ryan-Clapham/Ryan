using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// A model of a thoughtful player, used to calibrate and test levels. For every legal group it weighs:
    /// progress towards the level's unfinished goals, the points the group scores, and the best group left once
    /// the tiles fall (one move ahead). It does not peek at refills, since a player cannot know them either.
    /// Deterministic: the same board always gives the same choice.
    /// </summary>
    public sealed class PlannerBot
    {
        /// <summary>Value of one unit of goal progress (a collected tile, a freed obstacle): about a 6-tile group.</summary>
        public const int GoalUnitValue = 400;
        /// <summary>Value of one step towards freeing an obstacle that a goal needs.</summary>
        public const int ObstacleStepValue = 150;
        /// <summary>Weight of the best follow-up group's points.</summary>
        public const double LookaheadWeight = 0.5;

        readonly GroupFinder finder = new GroupFinder();
        readonly GroupFinder lookFinder = new GroupFinder();
        readonly List<GroupInfo> groups = new List<GroupInfo>();
        readonly List<GroupInfo> lookGroups = new List<GroupInfo>();
        readonly List<GridPos> cells = new List<GridPos>();
        readonly HashSet<GridPos> iceTouched = new HashSet<GridPos>();
        Board scratch;

        /// <summary>Picks the group to play. Returns false when there is no legal group.</summary>
        public bool Choose(PuzzleSession session, out GridPos pick)
        {
            pick = default;
            var board = session.Board;
            if (board == null || finder.FindAllGroups(board, groups) == 0) return false;
            double best = double.MinValue;
            int bestSize = -1;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                finder.FindGroup(board, g.Anchor, cells);
                double v = Evaluate(session, g);
                if (v > best || (v == best && g.Size > bestSize))
                {
                    best = v;
                    bestSize = g.Size;
                    pick = g.Anchor;
                }
            }
            return true;
        }

        /// <summary>The value of clearing the group whose cells are in <c>cells</c>.</summary>
        double Evaluate(PuzzleSession s, GroupInfo g)
        {
            var board = s.Board;
            double value = s.Scoring.GroupPoints(g.Size);
            var goals = s.Goals;
            if (goals != null)
            {
                for (int i = 0; i < goals.Count; i++)
                {
                    if (goals.IsComplete(i)) continue;
                    var goal = goals.Goal(i);
                    int remaining = goals.Remaining(i);
                    switch (goal.kind)
                    {
                        case GoalKind.CollectColour:
                            {
                                int removed = 0;
                                for (int k = 0; k < cells.Count; k++)
                                {
                                    var t = board[cells[k]];
                                    if (t.Colour == goal.colour && !TileRules.StaysWhenItsGroupClears(t)) removed++;
                                }
                                value += System.Math.Min(removed, remaining) * GoalUnitValue;
                                break;
                            }
                        case GoalKind.BreakChains:
                            {
                                int freed = 0, steps = 0;
                                for (int k = 0; k < cells.Count; k++)
                                {
                                    var t = board[cells[k]];
                                    if (t.Obstacle != ObstacleKind.Chain) continue;
                                    if (t.Strength <= 1) freed++;
                                    else steps++;
                                }
                                value += System.Math.Min(freed, remaining) * GoalUnitValue + steps * ObstacleStepValue;
                                break;
                            }
                        case GoalKind.FreeIce:
                            {
                                iceTouched.Clear();
                                for (int k = 0; k < cells.Count; k++)
                                {
                                    if (TileRules.StaysWhenItsGroupClears(board[cells[k]])) continue;
                                    for (int d = 0; d < 4; d++)
                                    {
                                        var n = new GridPos(cells[k].Col + Board.DCol[d], cells[k].Row + Board.DRow[d]);
                                        if (board.InBounds(n) && board[n].Obstacle == ObstacleKind.Ice) iceTouched.Add(n);
                                    }
                                }
                                int freed = 0, steps = 0;
                                foreach (var p in iceTouched)
                                {
                                    if (board[p].Strength <= 1) freed++;
                                    else steps++;
                                }
                                value += System.Math.Min(freed, remaining) * GoalUnitValue + steps * ObstacleStepValue;
                                break;
                            }
                    }
                }
            }
            value += LookaheadWeight * s.Scoring.GroupPoints(BestGroupAfter(board));
            return value;
        }

        /// <summary>Size of the biggest group left after the group in <c>cells</c> clears and the tiles fall (no refill).</summary>
        int BestGroupAfter(Board board)
        {
            if (scratch == null || scratch.Width != board.Width || scratch.Height != board.Height) scratch = board.Clone();
            else for (int i = 0; i < board.CellCount; i++) scratch.SetByIndex(i, board.GetByIndex(i));
            for (int k = 0; k < cells.Count; k++)
                if (!TileRules.StaysWhenItsGroupClears(scratch[cells[k]])) scratch[cells[k]] = default;
            Gravity.Apply(scratch, null, null, null, false);
            int best = 0;
            lookFinder.FindAllGroups(scratch, lookGroups);
            for (int i = 0; i < lookGroups.Count; i++) if (lookGroups[i].Size > best) best = lookGroups[i].Size;
            return best;
        }
    }
}
