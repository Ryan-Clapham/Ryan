using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>The achievement system (docs/ACHIEVEMENTS.md): the catalog, every category, unlocking, claiming and saves.</summary>
    public class AchievementTests
    {
        static readonly Dictionary<PowerUpId, int> Prices = PowerUpCatalog.CreateDefaults().ToDictionary(p => p.id, p => p.price);

        static int Value(AchievementDefinition d) => d.reward.coins + d.reward.powerUps.Sum(g => Prices[g.id] * g.count);

        static List<string> Ids(IEnumerable<AchievementDefinition> defs) => defs.Select(d => d.id).ToList();

        /// <summary>A campaign level with this number on a tiny fixed board, passed by its first tap.</summary>
        static PuzzleSession QuickLevel(GameContext ctx, int number, bool usePowerUp = false)
        {
            var spec = new LevelSpec
            {
                Id = "quick" + number, Mode = GameMode.Campaign, Number = number, Layout = LevelLayout.Parse(new[] { "0#12", "0345", "0123" }),
                Colours = 6, Moves = 3, Target = 10, Star2 = 20, Star3 = 30,
            };
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = 1 });
            s.Start();
            ctx.BeginRun(s);
            if (usePowerUp) Play.Use(s, Play.At(PowerUpId.TileBreaker, 1, 2));
            Play.Tap(s, 0, 0);
            Assert.IsTrue(s.Finish(new MoveOutcome()));
            return s;
        }

        // ================================================================== The catalog

        [Test]
        public void TheCatalogIsAFixedListOf67WithUniqueIds()
        {
            var defs = AchievementCatalog.CreateDefaults();
            Assert.AreEqual(67, defs.Length);
            Assert.AreEqual(defs.Length, defs.Select(d => d.id).Distinct().Count(), "unique ids");
            var catalog = new AchievementCatalog(defs);
            Assert.AreEqual(67, catalog.Count);
            foreach (AchievementCategory c in Enum.GetValues(typeof(AchievementCategory))) Assert.IsTrue(defs.Any(d => d.category == c), c + " has achievements");
            foreach (AchievementTier t in Enum.GetValues(typeof(AchievementTier))) Assert.IsTrue(defs.Any(d => d.tier == t), t + " is used");
            foreach (var d in defs)
            {
                Assert.IsFalse(d.reward.IsEmpty, d.id + " has a reward");
                Assert.IsNotEmpty(d.title, d.id);
                Assert.IsNotEmpty(d.description, d.id);
                Assert.GreaterOrEqual(d.target, 1, d.id);
            }
        }

        [Test]
        public void TheTopTiersAreTheAgreedOnes()
        {
            var defs = AchievementCatalog.CreateDefaults();
            Assert.AreEqual(500, defs.Where(d => d.stat == AchievementStat.LevelReached).Max(d => d.target), "level 500 is the highest campaign milestone");
            CollectionAssert.AreEqual(new long[] { 10, 25, 50, 100, 150, 200, 300, 400, 500 }, defs.Where(d => d.stat == AchievementStat.LevelReached).Select(d => d.target));
            CollectionAssert.AreEqual(new long[] { 1, 3, 5, 10, 15 }, defs.Where(d => d.stat == AchievementStat.PerfectRegions).Select(d => d.target));
            Assert.AreEqual(50000, defs.Where(d => d.stat == AchievementStat.EndlessBestScore).Max(d => d.target), "a defined Endless ceiling");
        }

        [Test]
        public void RewardsGrowWithEveryStepOfEachSeries()
        {
            var defs = AchievementCatalog.CreateDefaults();
            foreach (var series in defs.GroupBy(d => new { d.stat, d.param }))
            {
                var steps = series.OrderBy(d => d.target).ToList();
                for (int i = 1; i < steps.Count; i++)
                    Assert.Greater(Value(steps[i]), Value(steps[i - 1]), steps[i].id + " pays more than " + steps[i - 1].id);
            }
            Assert.Greater(Value(defs.First(d => d.id == "level_500")), 50 * Value(defs.First(d => d.id == "level_10")), "level 500 is worth far more than level 10");
            // Within the agreed bands: easy small, very hard large.
            foreach (var d in defs.Where(x => x.tier == AchievementTier.Easy)) Assert.LessOrEqual(Value(d), 100, d.id);
            foreach (var d in defs.Where(x => x.tier == AchievementTier.VeryHard)) Assert.GreaterOrEqual(Value(d), 800, d.id);
        }

        [Test]
        public void TargetsAndRewardsComeFromTheConfigData()
        {
            var config = GameConfigData.CreateDefault();
            config.Achievements = new[]
            {
                new AchievementDefinition { id = "custom", title = "C", description = "d", stat = AchievementStat.LevelReached, target = 3,
                    reward = new AchievementReward { coins = 7, powerUps = new[] { new PowerUpGrant { id = PowerUpId.Swap, count = 2 }, new PowerUpGrant { id = PowerUpId.ExtraMoves, count = 5 } } } },
                new AchievementDefinition { id = "custom", title = "Duplicate", stat = AchievementStat.LevelReached, target = 1 },
                new AchievementDefinition { id = "", title = "No id", stat = AchievementStat.LevelReached, target = 1 },
                new AchievementDefinition { id = "zero", title = "Bad target", stat = AchievementStat.LevelReached, target = 0 },
            };
            var ctx = new GameContext(config, new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            ctx.Load();
            Assert.AreEqual(1, ctx.Achievements.Total, "unusable definitions are skipped");
            ctx.Data.HighestUnlocked = 3;
            ctx.Data.Coins = 0;
            ctx.Achievements.Evaluate();
            Assert.AreEqual(AchievementClaim.Claimed, ctx.Achievements.Claim("custom"));
            Assert.AreEqual(7, ctx.Wallet.Balance);
            Assert.AreEqual(2, ctx.Inventory.Owned(PowerUpId.Swap));
            Assert.AreEqual(0, ctx.Inventory.Owned(PowerUpId.ExtraMoves), "a retired power-up in a reward is skipped");
        }

        [Test]
        public void RewardsAndProgressReadNaturally()
        {
            var powerUps = new PowerUpCatalog(PowerUpCatalog.CreateDefaults());
            var c = new AchievementCatalog(AchievementCatalog.CreateDefaults());
            string R(string id) => AchievementText.Reward(c.Get(id).reward, powerUps);
            Assert.AreEqual("25 coins", R("level_10"));
            Assert.AreEqual("75 coins + Blast", R("level_50"));
            Assert.AreEqual("250 coins + Colour Shift + Blast", R("level_150"));
            Assert.AreEqual("900 coins + Colour Burst ×2 + Colour Shift ×2", R("level_400"));
            Assert.AreEqual("600 coins + one of each power-up", R("level_300"));
            Assert.AreEqual("1,500 coins + two of each power-up", R("level_500"));
            Assert.AreEqual("1,200 coins + one of each power-up + Colour Burst", R("stars_1250"));
            Assert.AreEqual("Swap + Colour Shift", R("group_18"));
            Assert.AreEqual("Swap", R("endless_stage_6"));
            foreach (var d in c.All) Assert.IsNotEmpty(R(d.id), d.id);
            var retired = new AchievementReward { coins = 5, powerUps = new[] { new PowerUpGrant { id = PowerUpId.ExtraMoves, count = 3 } } };
            Assert.AreEqual("5 coins", AchievementText.Reward(retired, powerUps), "a retired power-up isn't promised");

            var medals = new[] { "Bronze", "Silver", "Gold" };
            Assert.AreEqual("Level 37 / 50", AchievementText.Progress(c.Get("level_50"), 37, medals));
            Assert.AreEqual("12,345 / 100,000", AchievementText.Progress(c.Get("tiles_100000"), 12345, medals));
            Assert.AreEqual("Stage 4 / 6", AchievementText.Progress(c.Get("endless_stage_6"), 4, medals));
            Assert.AreEqual("No medal yet", AchievementText.Progress(c.Get("weekly_gold"), 0, medals));
            Assert.AreEqual("Best: Silver", AchievementText.Progress(c.Get("weekly_gold"), 2, medals));
            Assert.AreEqual("10 / 10", AchievementText.Progress(c.Get("pure_10"), 14, medals), "never shown past the target");
            Assert.AreEqual("Very hard", AchievementText.Tier(AchievementTier.VeryHard));
            Assert.AreEqual("Perfect regions", AchievementText.Category(AchievementCategory.Regions));
            Assert.AreEqual("3 achievements unlocked", AchievementText.Several(3));
        }

        // ================================================================== Categories

        [Test]
        public void ReachingALevelUnlocksItsMilestoneWhenTheRunEnds()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 9;
            var r = ctx.FinishRun(QuickLevel(ctx, 9));
            Assert.AreEqual(10, ctx.Data.HighestUnlocked);
            CollectionAssert.AreEqual(new[] { "level_10" }, Ids(r.NewAchievements));
            Assert.AreEqual(AchievementState.Unlocked, ctx.Achievements.State(ctx.Achievements.Catalog.Get("level_10")));
            var replay = ctx.FinishRun(QuickLevel(ctx, 9));
            Assert.AreEqual(0, replay.NewAchievements.Count, "replaying never awards it again");
        }

        [Test]
        public void StarsCountEachLevelsBestOnce()
        {
            var ctx = TestUtil.Context();
            for (int i = 0; i < 5; i++) ctx.Campaign.RecordWin(4, 1000, 3);
            for (int n = 1; n <= 9; n++) ctx.Campaign.RecordWin(n, 1000, 3);
            var stars30 = ctx.Achievements.Catalog.Get("stars_30");
            Assert.AreEqual(27, ctx.Achievements.Current(stars30), "nine levels × 3, replays not counted again");
            Assert.AreEqual(0, ctx.Achievements.Evaluate().Count(d => d.stat == AchievementStat.TotalStars));
            ctx.Campaign.RecordWin(10, 1000, 3);
            Assert.IsTrue(ctx.Achievements.Evaluate().Any(d => d.id == "stars_30"));
        }

        [Test]
        public void ARegionIsPerfectOnlyWhenEveryLevelHasThreeStars()
        {
            var ctx = TestUtil.Context();
            var regions1 = ctx.Achievements.Catalog.Get("regions_1");
            for (int n = 1; n <= 20; n++) ctx.Campaign.RecordWin(n, 1000, n == 13 ? 2 : 3);
            for (int n = 21; n <= 39; n++) ctx.Campaign.RecordWin(n, 1000, 3);
            Assert.AreEqual(0, ctx.Achievements.Current(regions1), "level 13 has two stars; level 40 isn't played");
            Assert.IsFalse(ctx.Achievements.Evaluate().Any(d => d.stat == AchievementStat.PerfectRegions));
            ctx.Campaign.RecordWin(13, 1000, 3);
            Assert.AreEqual(1, ctx.Achievements.Current(regions1));
            ctx.Campaign.RecordWin(40, 1000, 3);
            Assert.AreEqual(2, ctx.Achievements.Current(regions1));
            Assert.IsTrue(ctx.Achievements.IsUnlocked(regions1) || ctx.Achievements.Evaluate().Any(d => d.id == "regions_1"));
            for (int n = 41; n <= 300; n++) ctx.Campaign.RecordWin(n, 1000, 3);
            Assert.AreEqual(15, ctx.Achievements.Current(regions1));
            var unlocked = Ids(ctx.Achievements.Evaluate());
            CollectionAssert.IsSubsetOf(new[] { "regions_3", "regions_5", "regions_10", "regions_15" }, unlocked);
            for (int n = 301; n <= 400; n++) ctx.Campaign.RecordWin(n, 1000, 3);
            Assert.AreEqual(20, ctx.Achievements.Current(regions1));
            Assert.IsFalse(ctx.Achievements.Catalog.All.Any(d => d.stat == AchievementStat.PerfectRegions && d.target > 15), "no tier beyond 15, however many regions");
        }

        [Test]
        public void RegionsFollowTheConfiguredRegionSize()
        {
            var config = GameConfigData.CreateDefault();
            config.Campaign.levelsPerRegion = 10;
            var ctx = new GameContext(config, new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            ctx.Load();
            for (int n = 1; n <= 10; n++) ctx.Campaign.RecordWin(n, 1000, 3);
            Assert.AreEqual(1, ctx.Achievements.Current(ctx.Achievements.Catalog.Get("regions_1")));
        }

        [Test]
        public void EndlessCountsOnlyFinishedRuns()
        {
            var ctx = TestUtil.Context();
            var abandoned = new PuzzleSession(ctx.CreateEndlessRun());
            abandoned.Start();
            ctx.BeginRun(abandoned);
            Bots.PlayOut(abandoned, BotKind.Greedy, new Rng(3), 25);
            Assert.Greater(abandoned.Score, 0);
            ctx.AbandonRun(abandoned);
            Assert.AreEqual(0, ctx.Data.Endless.BestScore, "a run the player left records no score");
            Assert.IsFalse(ctx.Achievements.IsUnlocked(ctx.Achievements.Catalog.Get("endless_5000")));

            var run = new PuzzleSession(ctx.CreateEndlessRun());
            run.Start();
            ctx.BeginRun(run);
            Bots.PlayOut(run, BotKind.Greedy, new Rng(4));
            if (run.FinalePending) run.RunFinale(new MoveOutcome());
            var r = ctx.FinishRun(run);
            Assert.AreEqual(run.Score, ctx.Data.Endless.BestScore);
            Assert.AreEqual(run.Score >= 5000, r.NewAchievements.Any(d => d.id == "endless_5000"));
            Assert.AreEqual(run.Stage >= 6, ctx.Achievements.IsUnlocked(ctx.Achievements.Catalog.Get("endless_stage_6")));
            var again = ctx.FinishRun(run);
            Assert.AreSame(r, again, "the same finished run is never counted twice");
        }

        [Test]
        public void BoardWorkIsTalliedFromEveryCause()
        {
            // Tile Breaker on a stone, on thick ice, on a chain and on moss; a group; a combination of two specials.
            var s = Play.Level(new[] { "0#1203", "2345-*", "001234" }, new[] { "..I1m.", "......", "......" });
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 1, 2));
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 2, 2));
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 3, 2));
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 4, 2));
            Assert.AreEqual(1, s.StonesBroken);
            Assert.AreEqual(2, s.IceLayersRemoved, "thick ice: both layers");
            Assert.AreEqual(1, s.ChainsReleased);
            Assert.AreEqual(1, s.MossRemoved);
            Assert.AreEqual(4, s.PowerUpUses(PowerUpId.TileBreaker));
            Assert.AreEqual(0, s.MostTilesInOneMove, "power-ups aren't moves");
            var combo = Play.Tap(s, 4, 1);
            Assert.AreEqual(2, s.BiggestCombo, "the rocket and the bomb fired together");
            int fromCombo = combo.Cleared.Count(c => !c.WasStone);
            Assert.AreEqual(fromCombo, s.MostTilesInOneMove);
        }

        [Test]
        public void TreasureCollectedCounts()
        {
            var s = Play.Level(new[] { "$1", "00" }, null);
            Play.Tap(s, 0, 0);
            Assert.AreEqual(1, s.TreasuresCollected);
        }

        [Test]
        public void LifetimeTotalsAddUpAcrossRunsWithoutCountingTwice()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 30;
            var s = Play.Level(new[] { "0#12", "0345", "0123" }, new[] { "..i.", "....", "...." }, 1);
            ctx.BeginRun(s);
            Play.Use(s, Play.At(PowerUpId.TileBreaker, 1, 2));
            Play.Tap(s, 0, 0);
            Assert.AreEqual(SessionState.Failed, s.State);
            ctx.FinishRun(s);
            Assert.AreEqual(1, ctx.Data.Stats.StonesBroken);
            Assert.AreEqual(1, ctx.Data.Stats.UsesOf(PowerUpId.TileBreaker));
            int games = ctx.Data.Stats.GamesPlayed;
            // Continued from Out of Moves and left: only what happened after counts.
            s.AddMoves(5);
            Play.Use(s, Play.At(PowerUpId.Blast, 2, 1));
            ctx.AbandonRun(s);
            Assert.AreEqual(1, ctx.Data.Stats.StonesBroken, "not counted again");
            Assert.AreEqual(1, ctx.Data.Stats.UsesOf(PowerUpId.Blast));
            Assert.AreEqual(1, ctx.Data.Stats.IceLayersRemoved, "the Blast took the ice layer");
            Assert.AreEqual(games, ctx.Data.Stats.GamesPlayed, "leaving isn't another game played");
            ctx.AbandonRun(s);
            Assert.AreEqual(1, ctx.Data.Stats.UsesOf(PowerUpId.Blast), "leaving twice changes nothing");
        }

        [Test]
        public void SkillRecordsKeepTheBestEver()
        {
            var ctx = TestUtil.Context();
            ctx.Data.Stats.BiggestGroup = 17;
            ctx.Data.Stats.MostTilesInOneMove = 44;
            ctx.Data.Stats.BiggestCombo = 3;
            var unlocked = Ids(ctx.Achievements.Evaluate());
            CollectionAssert.IsSubsetOf(new[] { "group_12", "move_30", "combo_2", "combo_3" }, unlocked);
            CollectionAssert.DoesNotContain(unlocked, "group_18");
            CollectionAssert.DoesNotContain(unlocked, "move_45");
            var s = QuickLevel(ctx, 1);
            ctx.FinishRun(s);
            Assert.AreEqual(17, ctx.Data.Stats.BiggestGroup, "a smaller group doesn't lower the record");
        }

        [Test]
        public void PureThreeStarsCountDistinctLevelsWithoutPowerUps()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 70;
            for (int i = 0; i < 3; i++) ctx.FinishRun(QuickLevel(ctx, 60));
            ctx.FinishRun(QuickLevel(ctx, 61, usePowerUp: true));
            ctx.FinishRun(QuickLevel(ctx, 20));
            CollectionAssert.AreEqual(new[] { 20, 60 }, ctx.Data.Stats.PureThreeStarLevels, "61 used a power-up; 60 counts once");
            var pure10 = ctx.Achievements.Catalog.Get("pure_10");
            Assert.AreEqual(1, ctx.Achievements.Current(pure10), "only levels from 50 count");
            for (int n = 50; n < 59; n++) ctx.FinishRun(QuickLevel(ctx, n));
            Assert.IsTrue(ctx.Achievements.IsUnlocked(pure10));
        }

        [Test]
        public void PowerUpMasteryCountsEachPowerUpUpToItsTarget()
        {
            var ctx = TestUtil.Context();
            var uses = ctx.Data.Stats.PowerUpUses;
            uses[(int)PowerUpId.Blast] = 12;
            uses[(int)PowerUpId.Swap] = 3;
            uses[(int)PowerUpId.ExtraMoves] = 9;
            var each1 = ctx.Achievements.Catalog.Get("powerups_each_1");
            var each10 = ctx.Achievements.Catalog.Get("powerups_each_10");
            Assert.AreEqual(2, ctx.Achievements.Current(each1), "a retired power-up isn't one of the five");
            Assert.AreEqual(13, ctx.Achievements.Current(each10), "10 (capped) + 3");
            foreach (var p in ctx.PowerUps.All) uses[(int)p.id] = Math.Max(uses.TryGetValue((int)p.id, out var n) ? n : 0, 1);
            Assert.IsTrue(ctx.Achievements.Evaluate().Any(d => d.id == "powerups_each_1"));
        }

        [Test]
        public void ChallengeAchievementsReadTheChallengeRecords()
        {
            var ctx = TestUtil.Context();
            ctx.Data.Claims.Add("d.2026-01-01.done");
            ctx.Data.Claims.Add("d.2026-01-01.3star");
            ctx.Data.Claims.Add("d.2026-01-02.done");
            ctx.Data.GetOrCreateDaily("2026-01-02").Completed = true;
            ctx.Data.GetOrCreateDaily("2026-01-03").Completed = true;
            ctx.Data.GetOrCreateDaily("2026-01-04").Completed = false;
            Assert.AreEqual(3, ctx.Achievements.Current(ctx.Achievements.Catalog.Get("daily_7")), "three distinct completed days");
            ctx.Data.Claims.Add("w.2026-W02.t0");
            Assert.AreEqual(1, ctx.Achievements.Current(ctx.Achievements.Catalog.Get("weekly_gold")));
            ctx.Data.Claims.Add("w.2026-W05.t2");
            var unlocked = Ids(ctx.Achievements.Evaluate());
            CollectionAssert.IsSubsetOf(new[] { "daily_1", "weekly_bronze", "weekly_gold" }, unlocked);
        }

        // ================================================================== Unlocking and claiming

        [Test]
        public void SeveralAchievementsUnlockTogetherInOneEvent()
        {
            var ctx = TestUtil.Context();
            var events = new List<List<string>>();
            ctx.Achievements.Unlocked += list => events.Add(Ids(list));
            ctx.Data.HighestUnlocked = 100;
            ctx.Achievements.Evaluate();
            Assert.AreEqual(1, events.Count);
            CollectionAssert.AreEqual(new[] { "level_10", "level_25", "level_50", "level_100" }, events[0], "in catalog order");
            ctx.Achievements.Evaluate();
            Assert.AreEqual(1, events.Count, "nothing new, no event");
            Assert.AreEqual(4, ctx.Achievements.Completed);
            Assert.AreEqual(4, ctx.Achievements.Unclaimed);
            Assert.AreEqual(4 * 100 / 67, ctx.Achievements.Percent);
        }

        [Test]
        public void ClaimingPaysCoinsAndPowerUpsExactlyOnce()
        {
            var ctx = TestUtil.Context();
            ctx.Data.Coins = 10;
            var level50 = ctx.Achievements.Catalog.Get("level_50");
            Assert.AreEqual(AchievementClaim.NotUnlocked, ctx.Achievements.Claim("level_50"));
            Assert.AreEqual(AchievementClaim.Unknown, ctx.Achievements.Claim("no_such_thing"));
            ctx.Data.HighestUnlocked = 50;
            ctx.Achievements.Evaluate();
            int blasts = ctx.Inventory.Owned(PowerUpId.Blast);
            Assert.AreEqual(AchievementClaim.Claimed, ctx.Achievements.Claim("level_50"));
            Assert.AreEqual(10 + level50.reward.coins, ctx.Wallet.Balance);
            Assert.AreEqual(blasts + 1, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.AreEqual(AchievementState.Claimed, ctx.Achievements.State(level50));
            Assert.AreEqual(AchievementClaim.AlreadyClaimed, ctx.Achievements.Claim("level_50"));
            Assert.AreEqual(10 + level50.reward.coins, ctx.Wallet.Balance, "never twice");
            Assert.AreEqual(blasts + 1, ctx.Inventory.Owned(PowerUpId.Blast));
            var entry = ctx.Wallet.Ledger.Last(e => e.Reason.StartsWith("ach.", StringComparison.Ordinal));
            Assert.AreEqual("ach.level_50", entry.Reason);
            Assert.AreEqual("Achievement reward", RewardKeys.Describe(entry.Reason));
        }

        [Test]
        public void ClaimAllPaysEveryWaitingRewardOnce()
        {
            var ctx = TestUtil.Context();
            ctx.Data.Coins = 0;
            ctx.Data.HighestUnlocked = 25;
            ctx.Data.Stats.BiggestGroup = 12;
            ctx.Achievements.Evaluate();
            Assert.AreEqual(3, ctx.Achievements.ClaimAll());
            Assert.AreEqual(25 + 50 + 30, ctx.Wallet.Balance);
            Assert.AreEqual(0, ctx.Achievements.ClaimAll());
            Assert.AreEqual(0, ctx.Achievements.Unclaimed);
        }

        [Test]
        public void UnlocksAndClaimsSurviveAReload()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            ctx.Data.HighestUnlocked = 25;
            ctx.Achievements.Evaluate();
            ctx.Achievements.Claim("level_10");
            ctx.Save();
            int coins = ctx.Wallet.Balance;
            var again = TestUtil.Context(null, store);
            Assert.AreEqual(0, again.UnlockedOnLoad.Count);
            Assert.AreEqual(AchievementState.Claimed, again.Achievements.State(again.Achievements.Catalog.Get("level_10")));
            Assert.AreEqual(AchievementState.Unlocked, again.Achievements.State(again.Achievements.Catalog.Get("level_25")));
            Assert.AreEqual(AchievementClaim.AlreadyClaimed, again.Achievements.Claim("level_10"));
            Assert.AreEqual(coins, again.Wallet.Balance);
            Assert.AreEqual(AchievementClaim.Claimed, again.Achievements.Claim("level_25"));
        }

        [Test]
        public void ExistingProgressIsRecognisedWhenTheSaveLoads()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            d.HighestUnlocked = 160;
            for (int n = 1; n <= 20; n++) d.GetOrCreateLevel(n).Stars = 3;
            for (int n = 1; n < 160; n++) d.GetOrCreateLevel(n).Completed = true;
            d.GetOrCreateLevel(77).BestScore = 31000;
            d.Endless.BestScore = 21000;
            d.Endless.BestStage = 9;
            d.Stats.BiggestGroup = 19;
            d.Stats.TilesCleared = 12000;
            var store = new MemorySaveStorage { Main = SaveCodec.Encode(d) };
            var ctx = TestUtil.Context(null, store);
            var ids = Ids(ctx.UnlockedOnLoad);
            CollectionAssert.IsSubsetOf(new[] { "level_150", "stars_30", "regions_1", "score_30000", "endless_20000", "endless_stage_6", "group_18", "tiles_10000" }, ids);
            CollectionAssert.DoesNotContain(ids, "level_200");
            CollectionAssert.DoesNotContain(ids, "stones_25", "no record of stones broken before this update");
        }

        [Test]
        public void ATestSaveNeverTouchesThePlayersSave()
        {
            var main = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, main);
            ctx.Data.Coins = 77;
            ctx.Data.HighestUnlocked = 12;
            ctx.Achievements.Evaluate();
            var test = new MemorySaveStorage();
            ctx.UseStorage(test);
            string before = main.Main;
            Assert.AreEqual(1, ctx.Data.HighestUnlocked, "a fresh test save");
            ctx.Data.HighestUnlocked = 500;
            ctx.Wallet.Credit(5000, "debug.grant");
            ctx.Achievements.Evaluate();
            ctx.Achievements.ClaimAll();
            ctx.Save();
            Assert.AreEqual(before, main.Main, "the player's save is untouched");

            ctx.UseStorage(main);
            Assert.AreEqual(77, ctx.Wallet.Balance);
            Assert.AreEqual(12, ctx.Data.HighestUnlocked);
            Assert.IsFalse(ctx.Achievements.IsUnlocked(ctx.Achievements.Catalog.Get("level_500")));
        }

        [Test]
        public void OldSavesWithoutAchievementDataLoad()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            var root = (Dictionary<string, object>)Json.Parse(SaveCodec.Encode(d));
            root.Remove("achievements");
            var stats = (Dictionary<string, object>)root["stats"];
            foreach (var k in new[] { "stones", "ice", "chains", "moss", "treasures", "mostInMove", "biggestCombo", "powerUpUses", "pureLevels" }) stats.Remove(k);
            var loaded = SaveCodec.Decode(Json.Write(root));
            Assert.AreEqual(0, loaded.Achievements.Unlocked.Count);
            Assert.AreEqual(0, loaded.Stats.StonesBroken);
            Assert.AreEqual(0, loaded.Stats.PureThreeStarLevels.Count);
        }

        [Test]
        public void NewCountersRoundTripThroughTheSave()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            d.Stats.StonesBroken = 5;
            d.Stats.IceLayersRemoved = 6;
            d.Stats.ChainsReleased = 7;
            d.Stats.MossRemoved = 8;
            d.Stats.TreasuresCollected = 9;
            d.Stats.MostTilesInOneMove = 31;
            d.Stats.BiggestCombo = 4;
            d.Stats.PowerUpUses[(int)PowerUpId.ColourBurst] = 3;
            d.Stats.PureThreeStarLevels.AddRange(new[] { 55, 51 });
            d.Achievements.Unlocked["level_10"] = 123;
            var back = SaveCodec.Decode(SaveCodec.Encode(d));
            Assert.AreEqual(5, back.Stats.StonesBroken);
            Assert.AreEqual(6, back.Stats.IceLayersRemoved);
            Assert.AreEqual(7, back.Stats.ChainsReleased);
            Assert.AreEqual(8, back.Stats.MossRemoved);
            Assert.AreEqual(9, back.Stats.TreasuresCollected);
            Assert.AreEqual(31, back.Stats.MostTilesInOneMove);
            Assert.AreEqual(4, back.Stats.BiggestCombo);
            Assert.AreEqual(3, back.Stats.UsesOf(PowerUpId.ColourBurst));
            CollectionAssert.AreEqual(new[] { 51, 55 }, back.Stats.PureThreeStarLevels);
            Assert.AreEqual(123, back.Achievements.Unlocked["level_10"]);
        }

        [Test]
        public void AProgressResetStartsAchievementsAgain()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 50;
            ctx.Achievements.Evaluate();
            ctx.Achievements.ClaimAll();
            ctx.ResetProgress();
            Assert.AreEqual(0, ctx.Achievements.Completed, "achievements belong to the progress that earned them");
            Assert.AreEqual(AchievementClaim.NotUnlocked, ctx.Achievements.Claim("level_10"));
        }
    }
}
