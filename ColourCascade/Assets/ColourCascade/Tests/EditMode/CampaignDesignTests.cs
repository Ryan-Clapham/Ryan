using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>The campaign's mechanics schedule, introductions, generated levels and level files.</summary>
    public class CampaignDesignTests
    {
        static CampaignCatalog Catalog()
        {
            var cfg = GameConfigData.CreateDefault();
            return new CampaignCatalog(cfg.Campaign, cfg.Scoring);
        }

        static bool HasThickIce(LevelLayout l)
        {
            for (int r = 0; r < l.Height; r++)
                for (int c = 0; c < l.Width; c++)
                    if (l.ObstacleAt(c, r) == ObstacleKind.Ice && l.StrengthAt(c, r) >= 2) return true;
            return false;
        }

        [Test]
        public void NoMechanicAppearsBeforeItIsTaught()
        {
            var cat = Catalog();
            var s = cat.Schedule;
            for (int n = 1; n <= 200; n++)
            {
                var spec = cat.Get(n);
                var l = spec.Layout;
                string at = "level " + n;
                if (n < s.iceFromLevel) Assert.AreEqual(0, l.ObstacleCount(ObstacleKind.Ice), at);
                if (n < s.thickIceFromLevel) Assert.IsFalse(HasThickIce(l), at);
                if (n < s.chainsFromLevel) Assert.AreEqual(0, l.ObstacleCount(ObstacleKind.Chain), at);
                if (n < s.treasureFromLevel) Assert.AreEqual(0, l.TreasureCount, at);
                if (n < s.mossFromLevel) Assert.AreEqual(0, l.ObstacleCount(ObstacleKind.Moss), at);
                if (n < s.colourGoalsFromLevel) Assert.IsFalse(spec.EffectiveGoals().Any(g => g.kind == GoalKind.CollectColour), at);
                Assert.AreEqual(CampaignDesign.SpecialsFor(n, s), spec.Specials, at);
                if (n < s.rocketsFromLevel) Assert.AreEqual(SpecialTier.None, spec.Specials, at);
                if (n < s.bombsFromLevel) Assert.LessOrEqual((int)spec.Specials, (int)SpecialTier.Rockets, at);
                if (n < s.prismsFromLevel) Assert.LessOrEqual((int)spec.Specials, (int)SpecialTier.Bombs, at);
                // Special tiles placed by hand only on the introduction levels that teach them.
                bool pieces = "-|*@".Any(ch => l.PieceCount(ch) > 0);
                if (pieces) Assert.IsTrue(CampaignDesign.IsIntroLevel(n, s), at);
            }
        }

        [Test]
        public void EachIntroductionTeachesOneThingWithATutorial()
        {
            var cat = Catalog();
            var s = cat.Schedule;
            var expected = new Dictionary<int, string>
            {
                [s.iceFromLevel] = TutorialKeys.Ice, [s.colourGoalsFromLevel] = TutorialKeys.ColourGoal, [s.thickIceFromLevel] = TutorialKeys.ThickIce,
                [s.rocketsFromLevel] = TutorialKeys.Rockets, [s.bombsFromLevel] = TutorialKeys.Bombs, [s.combosFromLevel] = TutorialKeys.Combos,
                [s.prismsFromLevel] = TutorialKeys.Prisms, [s.chainsFromLevel] = TutorialKeys.Chains, [s.treasureFromLevel] = TutorialKeys.Treasure,
                [s.mossFromLevel] = TutorialKeys.Moss,
            };
            foreach (var kv in expected)
            {
                var spec = cat.Get(kv.Key);
                Assert.AreEqual(kv.Value, spec.TutorialKey, "level " + kv.Key);
                Assert.IsTrue(spec.Note.StartsWith("New:"), "level " + kv.Key);
                Assert.IsNotNull(spec.Goals, "level " + kv.Key);
                Assert.AreEqual(1, spec.Goals.Length, "one goal, about the new thing (level " + kv.Key + ")");
            }
            // The newest mechanic on each introduction is the only thing on the board that is new.
            Assert.AreEqual(0, cat.Get(s.iceFromLevel).Layout.TreasureCount);
            Assert.IsFalse(HasThickIce(cat.Get(s.iceFromLevel).Layout));
            Assert.AreEqual(1, cat.Get(s.rocketsFromLevel).Layout.PieceCount('-'));
            Assert.AreEqual(1, cat.Get(s.bombsFromLevel).Layout.PieceCount('*'));
            Assert.AreEqual(1, cat.Get(s.prismsFromLevel).Layout.PieceCount('@'));
            Assert.Greater(cat.Get(s.chainsFromLevel).Layout.ObstacleCount(ObstacleKind.Chain), 0);
            Assert.AreEqual(0, cat.Get(s.chainsFromLevel).Layout.ObstacleCount(ObstacleKind.Ice), "chains are introduced alone");
            Assert.Greater(cat.Get(s.treasureFromLevel).Layout.TreasureCount, 0);
            Assert.AreEqual(0, cat.Get(s.treasureFromLevel).Layout.HasObstacles ? 1 : 0, "treasure is introduced alone");
            Assert.Greater(cat.Get(s.mossFromLevel).Layout.ObstacleCount(ObstacleKind.Moss), 0);
            Assert.AreEqual(0, cat.Get(s.mossFromLevel).Layout.ObstacleCount(ObstacleKind.Chain) + cat.Get(s.mossFromLevel).Layout.ObstacleCount(ObstacleKind.Ice));
        }

        [Test]
        public void PracticeLevelsFocusOnTheNewMechanic()
        {
            var cat = Catalog();
            var s = cat.Schedule;
            for (int k = 1; k <= s.practiceLevels; k++)
            {
                Assert.Greater(cat.Get(s.iceFromLevel + k).Layout.ObstacleCount(ObstacleKind.Ice), 0, "ice practice " + k);
                var chain = cat.Get(s.chainsFromLevel + k);
                Assert.Greater(chain.Layout.ObstacleCount(ObstacleKind.Chain), 0, "chain practice " + k);
                Assert.Greater(cat.Get(s.treasureFromLevel + k).Layout.TreasureCount, 0, "treasure practice " + k);
                Assert.Greater(cat.Get(s.mossFromLevel + k).Layout.ObstacleCount(ObstacleKind.Moss), 0, "moss practice " + k);
            }
        }

        [Test]
        public void GeneratedLevelsAreValidAndTheirGoalsCanBeMet()
        {
            var cat = Catalog();
            for (int n = 21; n <= 200; n++)
            {
                var spec = cat.Get(n);
                var l = spec.Layout;
                string at = "level " + n;
                Assert.IsTrue(l.Validate(out var error), at + ": " + error);
                Assert.Greater(spec.Moves, 0, at);
                var band = CampaignDesign.BandFor(n);
                Assert.LessOrEqual(spec.Moves, band.MaxMoves + (l.TreasureCount > 0 ? 5 : 0), at);
                Assert.Less(spec.Target, spec.Star2 + 1, at);
                Assert.Less(spec.Star2, spec.Star3, at);
                if (spec.Goals == null) continue;
                Assert.LessOrEqual(spec.Goals.Length, 3, at);
                foreach (var g in spec.Goals)
                {
                    switch (g.kind)
                    {
                        case GoalKind.FreeIce: Assert.LessOrEqual(g.amount, l.ObstacleCount(ObstacleKind.Ice), at); break;
                        case GoalKind.BreakChains: Assert.LessOrEqual(g.amount, l.ObstacleCount(ObstacleKind.Chain), at); break;
                        case GoalKind.CollectTreasure: Assert.LessOrEqual(g.amount, l.TreasureCount, at); break;
                        case GoalKind.ClearMoss: Assert.Greater(l.ObstacleCount(ObstacleKind.Moss), 0, at); break;
                        case GoalKind.CollectColour: Assert.Less(g.colour, spec.Colours, at); break;
                    }
                    // A moss goal may leave its amount at 0, meaning "all the moss the board starts with".
                    if (g.kind != GoalKind.ClearMoss) Assert.Greater(g.amount, 0, at);
                }
                if (l.ObstacleCount(ObstacleKind.Moss) > 0) Assert.Greater(spec.MossCap, l.ObstacleCount(ObstacleKind.Moss), at);
            }
        }

        [Test]
        public void TheCampaignIsTheSameOnEveryDevice()
        {
            var a = Catalog();
            var b = Catalog();
            for (int n = 21; n <= 120; n += 3)
                Assert.AreEqual(LevelCodec.Encode(a.Get(n)), LevelCodec.Encode(b.Get(n)), "level " + n);
        }

        [TestCase(21)]
        [TestCase(28)]
        [TestCase(31)]
        [TestCase(41)]
        [TestCase(47)]
        [TestCase(51)]
        [TestCase(61)]
        [TestCase(71)]
        [TestCase(101)]
        [TestCase(35)]
        [TestCase(75)]
        [TestCase(110)]
        [TestCase(150)]
        public void ALevelSavedToJsonLoadsBackExactly(int n)
        {
            var original = Catalog().Get(n);
            string json = LevelCodec.Encode(original);
            var ov = LevelCodec.Decode(json);
            var cfg = GameConfigData.CreateDefault();
            cfg.Campaign.overrides = cfg.Campaign.overrides.Where(o => o.level != n).Concat(new[] { ov }).ToArray();
            var loaded = new CampaignCatalog(cfg.Campaign, cfg.Scoring).Get(n);
            Assert.AreEqual(json, LevelCodec.Encode(loaded));
            Assert.AreEqual(original.LeftoverBonusPerMove, loaded.LeftoverBonusPerMove);

            // And it plays: same board, same goals.
            var x = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = original, Scoring = cfg.Scoring, BoardSeed = 9 });
            var y = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = loaded, Scoring = cfg.Scoring, BoardSeed = 9 });
            x.Start();
            y.Start();
            Assert.AreEqual(x.Board.ToDebugString(), y.Board.ToDebugString());
            Assert.AreEqual(x.Board.ToObstacleString(), y.Board.ToObstacleString());
        }

        [Test]
        public void AHandWrittenLevelLoadsAndBadOnesAreRefused()
        {
            string json = "{\"format\":\"colour-cascade-level\",\"version\":1,\"level\":300,\"moves\":20,\"colours\":4," +
                          "\"layout\":[\"......\",\"..$...\",\"......\",\"..-*..\",\"......\",\"......\"]," +
                          "\"obstacles\":[\"......\",\"......\",\"m....m\",\"......\",\"iI..12\",\"......\"]," +
                          "\"goals\":[{\"kind\":\"treasure\",\"amount\":1},{\"kind\":\"ice\",\"amount\":2},{\"kind\":\"colour\",\"colour\":\"green\",\"amount\":10}]," +
                          "\"specials\":\"prisms\",\"mossCap\":5}";
            var ov = LevelCodec.Decode(json);
            Assert.AreEqual(3, ov.goals.Length);
            Assert.AreEqual(TileColours.Green, ov.goals[2].colour);
            Assert.AreEqual(3, ov.specials);
            var cfg = GameConfigData.CreateDefault();
            cfg.Campaign.overrides = cfg.Campaign.overrides.Concat(new[] { ov }).ToArray();
            var spec = new CampaignCatalog(cfg.Campaign, cfg.Scoring).Get(300);
            Assert.AreEqual(20, spec.Moves, "imported levels keep their moves");
            Assert.AreEqual(SpecialTier.Prisms, spec.Specials);
            Assert.AreEqual(5, spec.MossCap);
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = cfg.Scoring, BoardSeed = 1 });
            s.Start();
            Assert.AreEqual(1, Play.CountPieces(s.Board, CellType.Treasure));
            Assert.AreEqual(2, Play.CountPieces(s.Board, CellType.Special));
            Assert.AreEqual(2, s.Board.CountObstacle(ObstacleKind.Moss));

            Assert.Throws<System.FormatException>(() => LevelCodec.Decode("{\"format\":\"something-else\"}"));
            Assert.Throws<System.FormatException>(() => LevelCodec.Decode(json.Replace("\"treasure\"", "\"dragons\"")));
            Assert.Throws<System.FormatException>(() => LevelCodec.Decode(json.Replace("..$...", "..$..")));
            Assert.Throws<System.FormatException>(() => LevelCodec.Decode(json.Replace("\"......\",\"..$...\"", "\"......\",\"..?...\"")));
            Assert.Throws<System.FormatException>(() => LevelCodec.Decode(json.Replace("\"version\":1", "\"version\":9")));
        }

        [Test]
        public void ProgressOnMechanicLevelsSavesAndLoads()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            int ice = ctx.Catalog.Schedule.iceFromLevel;
            ctx.Data.HighestUnlocked = ice;
            int stars = 0;
            for (int attempt = 0; attempt < 5 && stars == 0; attempt++)
            {
                var setup = ctx.CreateCampaignRun(ice);
                setup.BoardSeed = (ulong)(attempt + 1);
                var s = new PuzzleSession(setup);
                s.Start();
                ctx.BeginRun(s);
                Bots.PlayOut(s, BotKind.Planner, null);
                var r = ctx.FinishRun(s);
                stars = r.Stars;
            }
            Assert.Greater(stars, 0, "the planner completes the ice introduction");
            Assert.IsTrue(ctx.Save());
            var reloaded = TestUtil.Context(null, store);
            Assert.AreEqual(stars, reloaded.Campaign.StarsFor(ice));
            Assert.AreEqual(ice + 1, reloaded.Campaign.HighestUnlocked);
        }
    }
}
