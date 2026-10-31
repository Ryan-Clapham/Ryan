using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum BotKind
    {
        /// <summary>Always clears the biggest available group.</summary>
        Greedy,
        /// <summary>Picks groups at random, favouring bigger ones (models a relaxed player).</summary>
        Casual,
        /// <summary>Greedy 75% of the time, casual otherwise.</summary>
        Mixed,
        /// <summary>Goal-aware planner with one move of lookahead (PlannerBot). Models a thoughtful player.</summary>
        Planner,
    }

    /// <summary>Automated players. Used to calibrate level targets and by the balance simulation.</summary>
    public static class Bots
    {
        public static bool Choose(Board board, BotKind kind, Rng rng, GroupFinder finder, List<GroupInfo> groups, out GridPos pick)
        {
            pick = default;
            if (kind == BotKind.Planner) throw new System.ArgumentException("The planner needs the session: use PlannerBot.Choose or Bots.PlayOut.");
            finder.FindAllGroups(board, groups);
            // Special tiles are moves too (campaign only). They join the list as "groups" sized by the colour tiles
            // they reach, after the real groups, so boards without specials choose exactly as before.
            AddSpecials(board, groups);
            if (groups.Count == 0) return false;
            if (kind == BotKind.Mixed) kind = rng.ChancePermille(750) ? BotKind.Greedy : BotKind.Casual;
            if (kind == BotKind.Greedy)
            {
                int best = 0;
                for (int i = 1; i < groups.Count; i++)
                    if (groups[i].Size > groups[best].Size) best = i;
                pick = groups[best].Anchor;
                return true;
            }
            int total = 0;
            for (int i = 0; i < groups.Count; i++) total += groups[i].Size;
            int x = rng.Next(total);
            for (int i = 0; i < groups.Count; i++)
            {
                x -= groups[i].Size;
                if (x < 0)
                {
                    pick = groups[i].Anchor;
                    return true;
                }
            }
            pick = groups[groups.Count - 1].Anchor;
            return true;
        }

        [System.ThreadStatic] static List<GridPos> reachScratch;

        static void AddSpecials(Board board, List<GroupInfo> groups)
        {
            for (int i = 0; i < board.CellCount; i++)
            {
                if (board.GetByIndex(i).Type != CellType.Special) continue;
                if (reachScratch == null) reachScratch = new List<GridPos>();
                var p = board.PosOf(i);
                int reach = SpecialEffects.EstimateReach(board, p, reachScratch);
                groups.Add(new GroupInfo { Anchor = p, Size = System.Math.Max(1, reach), Colour = -1 });
            }
        }

        /// <summary>Plays until the session stops accepting moves or maxMoves is reached. Returns the final score.</summary>
        public static int PlayOut(PuzzleSession session, BotKind kind, Rng rng, int maxMoves = 10000)
        {
            var finder = new GroupFinder();
            var groups = new List<GroupInfo>();
            var outcome = new MoveOutcome();
            var planner = kind == BotKind.Planner ? new PlannerBot() : null;
            int moves = 0;
            while (session.AcceptsMoves && moves < maxMoves)
            {
                GridPos pick;
                int colour = -1;
                bool found = planner != null ? planner.Choose(session, out pick, out colour) : Choose(session.Board, kind, rng, finder, groups, out pick);
                if (!found) break;
                if (!session.TryPlay(pick, outcome, colour)) break;
                moves++;
            }
            return session.FinalScore;
        }
    }

    /// <summary>Estimates how well a reference bot scores on a level, so targets scale with any config change.</summary>
    public static class LevelCalibrator
    {
        /// <summary>Median score over several attempts when every move is played (the target is ignored).</summary>
        public static int MedianFullScore(LevelSpec spec, ScoringRules scoring, int runs, BotKind kind)
        {
            if (runs < 1) runs = 1;
            var scores = new int[runs];
            for (int i = 0; i < runs; i++)
            {
                var probe = CloneForProbe(spec);
                ulong boardSeed = spec.FixedSeed ? spec.Seed : StableHash.Combine(spec.Seed, (ulong)(i + 1));
                var session = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = probe, Scoring = scoring, BoardSeed = boardSeed });
                session.Start();
                scores[i] = Bots.PlayOut(session, kind, new Rng(StableHash.Combine(spec.Seed, (ulong)(1000 + i))));
            }
            System.Array.Sort(scores);
            return scores[runs / 2];
        }

        static LevelSpec CloneForProbe(LevelSpec s)
        {
            return new LevelSpec
            {
                Id = s.Id, Mode = GameMode.Campaign, Number = s.Number, Layout = s.Layout, Colours = s.Colours,
                ColourWeights = s.ColourWeights, AssistPermille = s.AssistPermille, Moves = s.Moves > 0 ? s.Moves : 20,
                Target = int.MaxValue, Seed = s.Seed, FixedSeed = s.FixedSeed,
            };
        }
    }
}
