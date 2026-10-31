using System;
using System.Collections.Generic;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>Helpers shared by the tests (identical in Unity's Test Runner and the .NET harness).</summary>
    public static class TestUtil
    {
        public static readonly DateTime Today = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>A session whose starting board is exactly the given rows (top first; digits are colours, '#' stone, 'x' void).</summary>
        public static PuzzleSession Session(GameMode mode, int moves, int target, int colours, params string[] rowsTopFirst)
        {
            return Session(mode, moves, target, colours, rowsTopFirst, null);
        }

        /// <summary>
        /// As above, plus obstacle rows in the layout notation ('.' none, 'i'/'I' ice 1/2 layers, '1'-'3' chain
        /// links) and the level's goals (none = a score goal equal to the target).
        /// </summary>
        public static PuzzleSession Session(GameMode mode, int moves, int target, int colours, string[] rowsTopFirst, string[] obstacleRowsTopFirst, params GoalSpec[] goals)
        {
            var s = new PuzzleSession(Setup(mode, moves, target, colours, rowsTopFirst, obstacleRowsTopFirst, goals));
            s.Start();
            return s;
        }

        /// <summary>The setup behind Session, for tests that change the level (special tiles, moss cap) before starting.</summary>
        public static SessionSetup Setup(GameMode mode, int moves, int target, int colours, string[] rowsTopFirst, string[] obstacleRowsTopFirst, params GoalSpec[] goals)
        {
            var layout = LevelLayout.Parse(rowsTopFirst);
            if (obstacleRowsTopFirst != null) layout.ApplyObstacleRows(obstacleRowsTopFirst);
            var spec = new LevelSpec
            {
                Id = "test",
                Mode = mode,
                Number = 1,
                Layout = layout,
                Goals = goals != null && goals.Length > 0 ? goals : null,
                Colours = colours,
                Moves = moves,
                Target = target,
                Star2 = target * 2,
                Star3 = target * 3,
                LeftoverBonusPerMove = 100,
                TimeLimitSeconds = mode == GameMode.WeeklyRush ? 60 : 0,
            };
            return new SessionSetup { Mode = mode, Level = spec, Scoring = new ScoringRules(), BoardSeed = 12345, Endless = mode == GameMode.Endless ? new EndlessConfig() : null };
        }

        public static GameContext Context(DateTime? now = null, MemorySaveStorage storage = null)
        {
            var ctx = new GameContext(GameConfigData.CreateDefault(), storage ?? new MemorySaveStorage(), new FixedClock(now ?? Today));
            ctx.Load();
            return ctx;
        }

        public static int CountColour(Board b, int colour) => b.CountColour(colour);

        /// <summary>Asserts the structural invariants that must hold after every move.</summary>
        public static void AssertBoardInvariants(Board b, LevelLayout layout)
        {
            var ids = new HashSet<int>();
            for (int r = 0; r < b.Height; r++)
                for (int c = 0; c < b.Width; c++)
                {
                    var t = b[c, r];
                    var expected = layout.TypeAt(c, r);
                    if (expected == CellType.Void) Assert.AreEqual(CellType.Void, t.Type, "void moved at " + c + "," + r);
                    Assert.AreNotEqual(CellType.Empty, t.Type, "empty cell left at " + c + "," + r);
                    if (t.Type == CellType.Colour || t.Type == CellType.Stone) Assert.IsTrue(ids.Add(t.Id), "duplicate tile id " + t.Id);
                }
        }
    }
}
