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

        /// <summary>
        /// Plays until the session stops accepting moves or maxMoves is reached. A passed campaign level is played on
        /// to its last move (as a player chasing stars would), unless stopWhenPassed is set. Returns the final score.
        /// </summary>
        public static int PlayOut(PuzzleSession session, BotKind kind, Rng rng, int maxMoves = 10000, bool stopWhenPassed = false)
        {
            var finder = new GroupFinder();
            var groups = new List<GroupInfo>();
            var outcome = new MoveOutcome();
            var planner = kind == BotKind.Planner ? new PlannerBot() : null;
            int moves = 0;
            while (session.AcceptsMoves && moves < maxMoves && !(stopWhenPassed && session.Passed))
            {
                GridPos pick;
                int colour = -1;
                bool found = planner != null ? planner.Choose(session, out pick, out colour) : Choose(session.Board, kind, rng, finder, groups, out pick);
                if (!found) break;
                // Anyone firing a prism picks the colour their goal needs (or the most common one).
                if (planner == null && session.NeedsColourChoice(pick)) colour = PlannerBot.PrismColour(session);
                if (!session.TryPlay(pick, outcome, colour)) break;
                moves++;
            }
            // Specials left on the board when the moves run out go off before the result.
            if (session.FinalePending) session.RunFinale(outcome);
            return session.FinalScore;
        }
    }

    /// <summary>Estimates how well a reference bot scores on a level, so targets scale with any config change.</summary>
    public static class LevelCalibrator
    {
        /// <summary>Median score over several attempts when every move is played (the target is ignored).</summary>
        public static int MedianFullScore(LevelSpec spec, ScoringRules scoring, int runs, BotKind kind)
        {
            var scores = FullScores(spec, scoring, runs, kind);
            return scores[scores.Length / 2];
        }

        /// <summary>Scores of several attempts when every move is played (the target is ignored), sorted.</summary>
        public static int[] FullScores(LevelSpec spec, ScoringRules scoring, int runs, BotKind kind)
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
            return scores;
        }

        static LevelSpec CloneForProbe(LevelSpec s)
        {
            return new LevelSpec
            {
                Id = s.Id, Mode = GameMode.Campaign, Number = s.Number, Layout = s.Layout, Colours = s.Colours,
                ColourWeights = s.ColourWeights, AssistPermille = s.AssistPermille, Moves = s.Moves > 0 ? s.Moves : 20,
                Target = int.MaxValue, Seed = s.Seed, FixedSeed = s.FixedSeed, Specials = s.Specials, MossCap = s.MossCap,
            };
        }

        /// <summary>A copy of a goal level for bot games: same board, goals and specials, with the given move budget.</summary>
        static LevelSpec CloneGoalProbe(LevelSpec s, int moves)
        {
            var c = CloneForProbe(s);
            c.Moves = moves;
            c.Target = s.Target;
            c.Goals = s.Goals;
            return c;
        }

        static ulong BoardSeedFor(LevelSpec spec, int i) => spec.FixedSeed ? spec.Seed : StableHash.Combine(spec.Seed, (ulong)(i + 1));

        /// <summary>
        /// Median number of moves a bot needs to complete every goal, given a generous budget (it stops as soon as the
        /// level is passed). Games that run out (or get stuck) count as budget + 10, so a level the bot can't finish
        /// reads as needing more than the budget.
        /// </summary>
        public static int MedianMovesNeeded(LevelSpec spec, ScoringRules scoring, int runs, int budget, BotKind kind) => MovesNeeded(spec, scoring, runs, budget, kind, 50);

        /// <summary>As MedianMovesNeeded, at another percentile of the games (75: three games in four pass within it).</summary>
        public static int MovesNeeded(LevelSpec spec, ScoringRules scoring, int runs, int budget, BotKind kind, int percentile)
        {
            if (runs < 1) runs = 1;
            var needed = new int[runs];
            for (int i = 0; i < runs; i++)
            {
                var session = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = CloneGoalProbe(spec, budget), Scoring = scoring, BoardSeed = BoardSeedFor(spec, i) });
                session.Start();
                Bots.PlayOut(session, kind, new Rng(StableHash.Combine(spec.Seed, (ulong)(2000 + i))), 10000, true);
                needed[i] = session.Passed ? session.PassedAtMove : budget + 10;
            }
            System.Array.Sort(needed);
            return needed[System.Math.Min(runs - 1, runs * percentile / 100)];
        }

        public struct GameResult
        {
            /// <summary>Final score of a passed game played to its last move (specials left at the end included).</summary>
            public int Score;
            /// <summary>Moves made when every goal was met.</summary>
            public int PassedAtMove;
        }

        /// <summary>Median Weekly Rush score after a fixed number of moves by the Mixed bot (the clock is not run).</summary>
        public static int MedianRushScore(LevelSpec spec, ScoringRules scoring, int runs, int moves)
        {
            if (runs < 1) runs = 1;
            var scores = new int[runs];
            for (int i = 0; i < runs; i++)
            {
                var session = new PuzzleSession(new SessionSetup { Mode = GameMode.WeeklyRush, Level = spec, Scoring = scoring, BoardSeed = spec.Seed });
                session.Start();
                session.StartTimer();
                scores[i] = Bots.PlayOut(session, BotKind.Mixed, new Rng(StableHash.Combine(spec.Seed, (ulong)(3000 + i))), moves);
            }
            System.Array.Sort(scores);
            return scores[runs / 2];
        }

        /// <summary>Passed games by a bot with the level's own moves, every move played, sorted by final score.</summary>
        public static List<GameResult> PassedResults(LevelSpec spec, ScoringRules scoring, int runs, BotKind kind, int seedOffset = 100)
        {
            var results = new List<GameResult>();
            for (int i = 0; i < runs; i++)
            {
                var session = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = CloneGoalProbe(spec, spec.Moves), Scoring = scoring, BoardSeed = BoardSeedFor(spec, seedOffset + i) });
                session.Start();
                Bots.PlayOut(session, kind, kind == BotKind.Planner ? null : new Rng(StableHash.Combine(spec.Seed, (ulong)(4000 + seedOffset + i))));
                if (session.State == SessionState.Won) results.Add(new GameResult { Score = session.FinalScore, PassedAtMove = session.PassedAtMove });
            }
            results.Sort((a, b) => a.Score.CompareTo(b.Score));
            return results;
        }

        /// <summary>Passed games by the Planner bot (see PassedResults).</summary>
        public static List<GameResult> PlannerResults(LevelSpec spec, ScoringRules scoring, int runs) => PassedResults(spec, scoring, runs, BotKind.Planner);
    }
}
