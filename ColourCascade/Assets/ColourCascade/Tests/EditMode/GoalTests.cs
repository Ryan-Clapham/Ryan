using System.Collections.Generic;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    public class GoalTests
    {
        [Test]
        public void ALevelWithoutGoalsHasOneScoreGoalEqualToItsTarget()
        {
            var spec = new LevelSpec { Target = 1234 };
            var goals = spec.EffectiveGoals();
            Assert.AreEqual(1, goals.Length);
            Assert.AreEqual(GoalKind.Score, goals[0].kind);
            Assert.AreEqual(1234, goals[0].amount);

            spec.Goals = new GoalSpec[0];
            Assert.AreEqual(GoalKind.Score, spec.EffectiveGoals()[0].kind, "an empty list means the same");

            spec.Goals = new[] { GoalSpec.Collect(2, 15), GoalSpec.FreeIce(4) };
            Assert.AreSame(spec.Goals, spec.EffectiveGoals());
        }

        [Test]
        public void TrackerBasics()
        {
            Assert.IsFalse(new GoalTracker(null).AllComplete, "no goals is never complete");
            Assert.IsFalse(new GoalTracker(new GoalSpec[0]).AllComplete);

            var specs = new[] { GoalSpec.Collect(1, 3), GoalSpec.Collect(2, 2), GoalSpec.Score(100) };
            var t = new GoalTracker(specs);
            specs[0].amount = 99;
            Assert.AreEqual(3, t.Goal(0).amount, "the tracker keeps its own copy");
            Assert.AreEqual(1, t.IndexOf(GoalKind.CollectColour, 2));
            Assert.AreEqual(2, t.IndexOf(GoalKind.Score));
            Assert.AreEqual(-1, t.IndexOf(GoalKind.FreeIce));

            var o = new MoveOutcome();
            o.Cleared.Add(new ClearedTile { Colour = 1 });
            o.Cleared.Add(new ClearedTile { Colour = 1 });
            o.Cleared.Add(new ClearedTile { Colour = 2 });
            o.Cleared.Add(new ClearedTile { Colour = 1, WasStone = true });
            Assert.IsTrue(t.Record(o, 60));
            Assert.AreEqual(2, t.Progress(0), "stones never count");
            Assert.AreEqual(1, t.Remaining(0));
            Assert.AreEqual(60, t.Progress(2));
            Assert.IsFalse(t.AllComplete);

            o.Reset();
            Assert.IsFalse(t.Record(o, 60), "nothing changed");
            o.Cleared.Add(new ClearedTile { Colour = 1 });
            o.Cleared.Add(new ClearedTile { Colour = 1 });
            o.Cleared.Add(new ClearedTile { Colour = 2 });
            Assert.IsTrue(t.Record(o, 150));
            Assert.AreEqual(0, t.Remaining(0), "never negative");
            Assert.IsTrue(t.AllComplete);
        }

        [Test]
        public void IceAndChainGoalsCountOnlyTheLastStep()
        {
            var t = new GoalTracker(new[] { GoalSpec.FreeIce(2), GoalSpec.BreakChains(2) });
            var o = new MoveOutcome();
            o.ObstacleHits.Add(new ObstacleHit { Kind = ObstacleKind.Ice, StrengthBefore = 2, StrengthAfter = 1 });
            o.ObstacleHits.Add(new ObstacleHit { Kind = ObstacleKind.Chain, StrengthBefore = 3, StrengthAfter = 2 });
            Assert.IsFalse(t.Record(o, 0));
            o.Reset();
            o.ObstacleHits.Add(new ObstacleHit { Kind = ObstacleKind.Ice, StrengthBefore = 1, StrengthAfter = 0 });
            o.ObstacleHits.Add(new ObstacleHit { Kind = ObstacleKind.Chain, StrengthBefore = 3, StrengthAfter = 0, Cause = HitCause.Strong });
            Assert.IsTrue(t.Record(o, 0));
            Assert.AreEqual(1, t.Progress(0));
            Assert.AreEqual(1, t.Progress(1));
        }

        [Test]
        public void CollectCountsTilesRemovedByAnyMeansButNotStones()
        {
            var s = Play.Level(new[] { "#000", "1122" }, null, 20, GoalSpec.Collect(0, 10));
            Play.Tap(s, 1, 1);
            Assert.AreEqual(3, s.Goals.Progress(0));
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 0, 1));
            Assert.AreEqual(3, s.Goals.Progress(0), "the stone was removed but is not a colour");

            var t = Play.Level(new[] { "000", "111" }, null, 20, GoalSpec.Collect(0, 10));
            var o = Play.Use(t, Play.At(PowerUpId.Blast, 1, 0));
            Assert.AreEqual(6, o.Cleared.Count);
            Assert.IsTrue(o.GoalsChanged);
            Assert.AreEqual(3, t.Goals.Progress(0), "power-ups collect too");
        }

        [Test]
        public void MeetingEveryGoalWinsAtOnceAndPaysTheSpareMoveBonus()
        {
            var s = Play.Level(new[] { "001", "223" }, null, 10, GoalSpec.Collect(0, 2));
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(SessionState.Won, s.State);
            Assert.AreEqual(EndReason.TargetReached, s.EndReason);
            Assert.IsTrue(o.Ended);
            Assert.AreEqual(9 * 100, s.LeftoverBonus);
            Assert.AreEqual(40 + 900, s.FinalScore);
            Assert.AreEqual(1, s.Stars, "a won level earns at least one star even below its score target");
            Assert.IsTrue(s.Succeeded);
        }

        [Test]
        public void RunningOutOfMovesWithAGoalUnmetFailsWhateverTheScore()
        {
            var s = Play.Level(new[] { "0012", "3345" }, new[] { "...i", "...." }, 1, GoalSpec.Score(10), GoalSpec.FreeIce(1));
            Play.Tap(s, 0, 1);
            Assert.IsTrue(s.Goals.IsComplete(0), "the score goal is met");
            Assert.IsFalse(s.Goals.IsComplete(1));
            Assert.AreEqual(SessionState.Failed, s.State);
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
            Assert.AreEqual(0, s.Stars);
            Assert.AreEqual(0, s.LeftoverBonus);
        }

        [Test]
        public void ExtraMovesReviveAGoalLevelAndItCanStillBeWon()
        {
            var s = Play.Level(new[] { "0012", "3345" }, new[] { "...i", "...." }, 1, GoalSpec.FreeIce(1));
            Play.Tap(s, 0, 1);
            Assert.AreEqual(SessionState.Failed, s.State);
            Play.Use(s, new PowerUpRequest { Id = PowerUpId.ExtraMoves, Amount = 3 });
            Assert.AreEqual(SessionState.Playing, s.State);
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 3, 1));
            Assert.AreEqual(SessionState.Won, s.State);
            Assert.AreEqual(3 * 100, s.LeftoverBonus);
        }

        [Test]
        public void ScoreOnlyLevelsBehaveAsBefore()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 50, 4, "0011", "2233");
            Assert.AreEqual(1, s.Goals.Count);
            Play.Tap(s, 0, 1);
            Assert.AreEqual(SessionState.Playing, s.State, "40 < 50");
            Play.Tap(s, 0, 0);
            Assert.AreEqual(SessionState.Won, s.State);
            Assert.AreEqual(80, s.Goals.Progress(0));
        }
    }
}
