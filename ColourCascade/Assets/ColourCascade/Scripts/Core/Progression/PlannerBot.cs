using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// A model of a thoughtful player, used to calibrate and test levels. For every legal move (each group and each
    /// special tile) it weighs: progress towards the level's unfinished goals, the points the move scores, special
    /// tiles it earns, and what is left once the tiles fall (one move ahead: the best next group and any treasure
    /// that would be collected). It does not peek at refills, since a player cannot know them either.
    /// Deterministic: the same board always gives the same choice.
    /// </summary>
    public sealed class PlannerBot
    {
        /// <summary>Value of one unit of goal progress (a collected tile, a freed obstacle, a treasure): about a 6-tile group.</summary>
        public const int GoalUnitValue = 400;
        /// <summary>Value of one step towards freeing an obstacle that a goal needs.</summary>
        public const int ObstacleStepValue = 150;
        /// <summary>Weight of the best follow-up group's points.</summary>
        public const double LookaheadWeight = 0.5;
        /// <summary>Value of earning a rocket, bomb or prism (roughly what firing it later is worth).</summary>
        public const int RocketValue = 350, BombValue = 500, PrismValue = 800;
        /// <summary>Cost of letting moss grow (a move that clears none while moss must be cleared).</summary>
        public const int MossGrowthCost = 250;

        readonly GroupFinder finder = new GroupFinder();
        readonly GroupFinder lookFinder = new GroupFinder();
        readonly List<GroupInfo> groups = new List<GroupInfo>();
        readonly List<GroupInfo> lookGroups = new List<GroupInfo>();
        readonly List<GridPos> cells = new List<GridPos>();
        readonly HashSet<GridPos> removed = new HashSet<GridPos>();
        readonly HashSet<GridPos> iceTouched = new HashSet<GridPos>();
        Board scratch;

        /// <summary>Picks the move to play. Returns false when there is none.</summary>
        public bool Choose(PuzzleSession session, out GridPos pick)
        {
            int colour;
            return Choose(session, out pick, out colour);
        }

        /// <summary>Picks the move to play and, for a prism, the colour to clear (-1 otherwise).</summary>
        public bool Choose(PuzzleSession session, out GridPos pick, out int colour)
        {
            pick = default;
            colour = -1;
            var board = session.Board;
            if (board == null) return false;
            finder.FindAllGroups(board, groups);
            double best = double.MinValue;
            int bestSize = -1;
            bool any = false;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                finder.FindGroup(board, g.Anchor, cells);
                double v = EvaluateGroup(session, g);
                if (v > best || (v == best && g.Size > bestSize))
                {
                    best = v;
                    bestSize = g.Size;
                    pick = g.Anchor;
                    colour = -1;
                    any = true;
                }
            }
            for (int i = 0; i < board.CellCount; i++)
            {
                if (board.GetByIndex(i).Type != CellType.Special) continue;
                var p = board.PosOf(i);
                int prismColour = session.NeedsColourChoice(p) ? PrismColour(session) : -1;
                GridPos partner;
                bool combo;
                session.PreviewSpecial(p, prismColour, cells, out partner, out combo);
                double v = EvaluateSpecial(session, combo);
                if (v > best)
                {
                    best = v;
                    bestSize = cells.Count;
                    pick = p;
                    colour = prismColour;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>The colour a prism should clear: an unfinished collect goal's colour if any is on the board, else the most common.</summary>
        static int PrismColour(PuzzleSession s)
        {
            var goals = s.Goals;
            if (goals != null)
                for (int i = 0; i < goals.Count; i++)
                    if (!goals.IsComplete(i) && goals.Goal(i).kind == GoalKind.CollectColour && s.Board.CountColour(goals.Goal(i).colour) > 0)
                        return goals.Goal(i).colour;
            return SpecialEffects.MostCommonColour(s.Board);
        }

        /// <summary>The value of clearing the group whose cells are in <c>cells</c>.</summary>
        double EvaluateGroup(PuzzleSession s, GroupInfo g)
        {
            var board = s.Board;
            double value = s.Scoring.GroupPoints(g.Size);
            removed.Clear();
            for (int k = 0; k < cells.Count; k++)
                if (!TileRules.StaysWhenItsGroupClears(board[cells[k]])) removed.Add(cells[k]);
            value += GoalValue(s, true);

            var tier = s.Level.Specials;
            if (tier != SpecialTier.None)
            {
                var kind = s.Scoring.specials.KindFor(g.Size, tier, false);
                if (kind == SpecialKind.Prism) value += PrismValue;
                else if (kind == SpecialKind.Bomb) value += BombValue;
                else if (kind != SpecialKind.None) value += RocketValue;
            }
            return value + Lookahead(s);
        }

        /// <summary>The value of firing the special whose reach is in <c>cells</c>.</summary>
        double EvaluateSpecial(PuzzleSession s, bool combo)
        {
            var board = s.Board;
            removed.Clear();
            int tiles = 0;
            for (int k = 0; k < cells.Count; k++)
            {
                var t = board[cells[k]];
                if (TileRules.IsFree(t) || TileRules.AreaHitRemovesTile(t))
                {
                    removed.Add(cells[k]);
                    tiles++;
                }
            }
            double value = tiles * (combo ? s.Scoring.specials.comboPointsPerTile : s.Scoring.specials.pointsPerTile);
            value += GoalValue(s, false);
            return value + Lookahead(s);
        }

        /// <summary>
        /// Goal progress from removing the tiles in <c>removed</c>. For a group, <c>cells</c> also holds chained
        /// tiles that lose a link; for a special, every reached obstacle takes one step.
        /// </summary>
        double GoalValue(PuzzleSession s, bool isGroup)
        {
            var goals = s.Goals;
            if (goals == null) return 0;
            var board = s.Board;
            double value = 0;
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals.IsComplete(i)) continue;
                var goal = goals.Goal(i);
                int remaining = goals.Remaining(i);
                switch (goal.kind)
                {
                    case GoalKind.CollectColour:
                        {
                            int n = 0;
                            foreach (var p in removed)
                                if (board[p].Colour == goal.colour) n++;
                            value += System.Math.Min(n, remaining) * GoalUnitValue;
                            break;
                        }
                    case GoalKind.BreakChains:
                        {
                            int freed = 0, steps = 0;
                            for (int k = 0; k < cells.Count; k++)
                            {
                                var t = board[cells[k]];
                                if (t.Type != CellType.Colour || t.Obstacle != ObstacleKind.Chain) continue;
                                if (t.Strength <= 1) freed++;
                                else steps++;
                            }
                            value += System.Math.Min(freed, remaining) * GoalUnitValue + steps * ObstacleStepValue;
                            break;
                        }
                    case GoalKind.FreeIce:
                        {
                            iceTouched.Clear();
                            if (!isGroup)
                                for (int k = 0; k < cells.Count; k++)
                                    if (board[cells[k]].Type == CellType.Colour && board[cells[k]].Obstacle == ObstacleKind.Ice) iceTouched.Add(cells[k]);
                            foreach (var p in removed)
                                for (int d = 0; d < 4; d++)
                                {
                                    var n = new GridPos(p.Col + Board.DCol[d], p.Row + Board.DRow[d]);
                                    if (board.InBounds(n) && board[n].Type == CellType.Colour && board[n].Obstacle == ObstacleKind.Ice) iceTouched.Add(n);
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
                    case GoalKind.ClearMoss:
                        {
                            int moss = 0;
                            foreach (var p in removed)
                                if (board[p].Obstacle == ObstacleKind.Moss) moss++;
                            value += moss * GoalUnitValue;
                            if (moss == 0) value -= MossGrowthCost;
                            break;
                        }
                    case GoalKind.CollectTreasure:
                        {
                            // Tiles cleared under a treasure bring it closer to the floor.
                            int under = 0;
                            foreach (var p in removed)
                                for (int r = p.Row + 1; r < board.Height; r++)
                                {
                                    var t = board[p.Col, r];
                                    if (t.Type == CellType.Treasure)
                                    {
                                        under++;
                                        break;
                                    }
                                    if (TileRules.IsGravityBarrier(t)) break;
                                }
                            value += under * ObstacleStepValue;
                            break;
                        }
                }
            }
            return value;
        }

        /// <summary>
        /// Drops the tiles once the cells in <c>removed</c> are gone (no refill): the best group left, plus any
        /// treasure that reaches the floor when a treasure goal is unfinished.
        /// </summary>
        double Lookahead(PuzzleSession s)
        {
            var board = s.Board;
            if (scratch == null || scratch.Width != board.Width || scratch.Height != board.Height) scratch = board.Clone();
            else for (int i = 0; i < board.CellCount; i++) scratch.SetByIndex(i, board.GetByIndex(i));
            foreach (var p in removed) scratch[p] = default;
            int treasureBefore = scratch.CountType(CellType.Treasure);
            Gravity.Apply(scratch, null, null, null, false);
            int collected = treasureBefore - scratch.CountType(CellType.Treasure);
            int best = 0;
            lookFinder.FindAllGroups(scratch, lookGroups);
            for (int i = 0; i < lookGroups.Count; i++) if (lookGroups[i].Size > best) best = lookGroups[i].Size;
            double value = LookaheadWeight * s.Scoring.GroupPoints(best);
            if (collected > 0)
            {
                var goals = s.Goals;
                int idx = goals != null ? goals.IndexOf(GoalKind.CollectTreasure) : -1;
                if (idx >= 0 && !goals.IsComplete(idx)) value += System.Math.Min(collected, goals.Remaining(idx)) * GoalUnitValue;
                value += collected * s.Scoring.treasurePoints;
            }
            return value;
        }
    }
}
