using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColourCascade.Core;
using ColourCascade.Procedural;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    public class ChallengeTests
    {
        [Test]
        public void DayKeysUseUtc()
        {
            Assert.AreEqual("2026-10-25", ChallengeCalendar.DayKey(new DateTime(2026, 10, 25, 23, 59, 0, DateTimeKind.Utc)));
            Assert.AreEqual(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc), ChallengeCalendar.NextDailyReset(new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)));
        }

        [TestCase(2020, 12, 31, "2020-W53")]
        [TestCase(2021, 1, 1, "2020-W53")]
        [TestCase(2021, 1, 4, "2021-W01")]
        [TestCase(2024, 12, 30, "2025-W01")]
        [TestCase(2026, 1, 1, "2026-W01")]
        [TestCase(2026, 10, 25, "2026-W43")]
        [TestCase(2026, 10, 26, "2026-W44")]
        public void WeekKeysFollowIso8601(int y, int m, int d, string expected)
        {
            Assert.AreEqual(expected, ChallengeCalendar.WeekKey(new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void WeeksStartOnMondayUtc()
        {
            var sunday = new DateTime(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc), ChallengeCalendar.WeekStart(sunday));
            Assert.AreEqual(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc), ChallengeCalendar.NextWeeklyReset(sunday));
        }

        [Test]
        public void DailyChallengeIsIdenticalForTheSameDate()
        {
            var a = TestUtil.Context();
            var b = TestUtil.Context();
            var x = new PuzzleSession(a.CreateDailyRun("2026-10-25"));
            var y = new PuzzleSession(b.CreateDailyRun("2026-10-25"));
            x.Start();
            y.Start();
            Assert.AreEqual(x.Board.ToDebugString(), y.Board.ToDebugString());
            Assert.AreEqual(x.Level.Target, y.Level.Target);
            Assert.AreEqual(x.Level.Moves, y.Level.Moves);
            // Same moves produce the same refills too.
            var o = new MoveOutcome();
            for (int i = 0; i < 5; i++)
            {
                var groups = new List<GroupInfo>();
                new GroupFinder().FindAllGroups(x.Board, groups);
                x.TryPlay(groups[0].Anchor, o);
                y.TryPlay(groups[0].Anchor, o);
                Assert.AreEqual(x.Board.ToDebugString(), y.Board.ToDebugString());
            }
        }

        [Test]
        public void DailyChallengeChangesWithTheDate()
        {
            var ctx = TestUtil.Context();
            var days = new HashSet<string>();
            for (int d = 0; d < 7; d++)
            {
                var s = new PuzzleSession(ctx.CreateDailyRun(ChallengeCalendar.DayKey(TestUtil.Today.AddDays(d))));
                s.Start();
                days.Add(s.Board.ToDebugString());
            }
            Assert.AreEqual(7, days.Count);
        }

        [Test]
        public void DailyIsUntimedWithMovesAndWeeklyIsSixtySecondsWithoutMoves()
        {
            var ctx = TestUtil.Context();
            var daily = ctx.Challenges.Daily("2026-10-25");
            Assert.AreEqual(0, daily.TimeLimitSeconds);
            Assert.AreEqual(20, daily.Moves);
            Assert.IsTrue(daily.FixedSeed);
            var weekly = ctx.Challenges.Weekly("2026-W43");
            Assert.AreEqual(60, weekly.TimeLimitSeconds);
            Assert.AreEqual(0, weekly.Moves);
            Assert.IsTrue(weekly.FixedSeed);
        }

        [Test]
        public void WeeklyRushIsConsistentWithinAWeek()
        {
            var a = TestUtil.Context(new DateTime(2026, 10, 19, 0, 30, 0, DateTimeKind.Utc));
            var b = TestUtil.Context(new DateTime(2026, 10, 25, 23, 30, 0, DateTimeKind.Utc));
            Assert.AreEqual(a.ThisWeekKey, b.ThisWeekKey);
            var x = new PuzzleSession(a.CreateWeeklyRun());
            var y = new PuzzleSession(b.CreateWeeklyRun());
            x.Start();
            y.Start();
            Assert.AreEqual(x.Board.ToDebugString(), y.Board.ToDebugString());
            var next = TestUtil.Context(new DateTime(2026, 10, 26, 0, 0, 1, DateTimeKind.Utc));
            Assert.AreNotEqual(a.ThisWeekKey, next.ThisWeekKey);
        }

        [Test]
        public void EndlessStartsAFreshBoardEveryRun()
        {
            var ctx = TestUtil.Context();
            var boards = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                var s = new PuzzleSession(ctx.CreateEndlessRun());
                s.Start();
                boards.Add(s.Board.ToDebugString());
            }
            Assert.AreEqual(10, boards.Count);
        }

        [Test]
        public void DailyResultsAreRecordedPerDay()
        {
            var ctx = TestUtil.Context();
            var setup = ctx.CreateDailyRun();
            var s = new PuzzleSession(setup);
            s.Start();
            ctx.BeginRun(s);
            Bots.PlayOut(s, BotKind.Greedy, new Rng(1));
            var r = ctx.FinishRun(s);
            var rec = ctx.TodayRecord;
            Assert.IsNotNull(rec);
            Assert.AreEqual(1, rec.Plays);
            Assert.AreEqual(s.FinalScore, rec.BestScore);
            Assert.AreEqual(r.Success, rec.Completed);
        }
    }

    public class SaveTests
    {
        static SaveData Populated()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            d.Coins = 1234;
            d.HighestUnlocked = 7;
            d.Ledger.Add(new LedgerEntry { Ticks = 5, Delta = 10, Balance = 10, Reason = "c.first.1" });
            d.GetOrCreateInventory(PowerUpId.Blast).Owned = 3;
            d.GetOrCreateInventory(PowerUpId.Swap).Trials = 2;
            var l = d.GetOrCreateLevel(3);
            l.BestScore = 4200;
            l.Stars = 2;
            l.Completed = true;
            l.Plays = 4;
            l.Wins = 2;
            d.Claims.Add("c.first.3");
            d.Claims.Add("w.2026-W43.t0");
            var day = d.GetOrCreateDaily("2026-10-25");
            day.BestScore = 3100;
            day.Completed = true;
            day.Stars = 1;
            d.GetOrCreateWeekly("2026-W43").BestScore = 6400;
            d.Endless.BestScore = 12000;
            d.Endless.BestGroup = 14;
            d.Tutorials.Add(TutorialKeys.TapGroup);
            d.Settings.Symbols = true;
            d.Settings.SfxVolume = 0.3f;
            d.Settings.Haptics = false;
            d.Settings.ReducedMotion = true;
            d.Stats.TilesCleared = 999999999L;
            d.LatestDayKey = "2026-10-25";
            return d;
        }

        [Test]
        public void SaveRoundTripsEveryField()
        {
            var d = SaveCodec.Decode(SaveCodec.Encode(Populated()));
            Assert.AreEqual(1234, d.Coins);
            Assert.AreEqual(7, d.HighestUnlocked);
            Assert.AreEqual("c.first.1", d.Ledger[0].Reason);
            Assert.AreEqual(3, d.GetOrCreateInventory(PowerUpId.Blast).Owned);
            Assert.AreEqual(2, d.GetOrCreateInventory(PowerUpId.Swap).Trials);
            var l = d.FindLevel(3);
            Assert.AreEqual(4200, l.BestScore);
            Assert.AreEqual(2, l.Stars);
            Assert.IsTrue(l.Completed);
            Assert.AreEqual(4, l.Plays);
            CollectionAssert.AreEquivalent(new[] { "c.first.3", "w.2026-W43.t0" }, d.Claims);
            Assert.AreEqual(3100, d.FindDaily("2026-10-25").BestScore);
            Assert.AreEqual(6400, d.FindWeekly("2026-W43").BestScore);
            Assert.AreEqual(12000, d.Endless.BestScore);
            Assert.IsTrue(d.Tutorials.Contains(TutorialKeys.TapGroup));
            Assert.IsTrue(d.Settings.Symbols);
            Assert.AreEqual(0.3f, d.Settings.SfxVolume, 1e-5);
            Assert.IsFalse(d.Settings.Haptics);
            Assert.IsTrue(d.Settings.ReducedMotion);
            Assert.AreEqual(999999999L, d.Stats.TilesCleared);
            Assert.AreEqual("2026-10-25", d.LatestDayKey);
            Assert.AreEqual(SaveData.CurrentVersion, d.Version);
        }

        [Test]
        public void MissingSaveStartsANewPlayer()
        {
            var sys = new SaveSystem(new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            Assert.AreEqual(LoadStatus.NewPlayer, sys.Load());
            Assert.AreEqual(0, sys.Data.Coins);
            Assert.AreEqual(1, sys.Data.HighestUnlocked);
        }

        [Test]
        public void CorruptMainSaveRecoversFromBackup()
        {
            var store = new MemorySaveStorage();
            var sys = new SaveSystem(store, new FixedClock(TestUtil.Today));
            sys.Load();
            sys.Data.Coins = 50;
            sys.Save();
            sys.Data.Coins = 80;
            sys.Save();
            store.Main = store.Main.Substring(0, store.Main.Length / 2);
            var again = new SaveSystem(store, new FixedClock(TestUtil.Today));
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, again.Load());
            Assert.AreEqual(50, again.Data.Coins);
        }

        [Test]
        public void UnreadableSavesResetGracefully()
        {
            var store = new MemorySaveStorage { Main = "garbage", Backup = "{\"format\":\"other\"}", Temp = "{" };
            var sys = new SaveSystem(store, new FixedClock(TestUtil.Today));
            Assert.AreEqual(LoadStatus.CorruptReset, sys.Load());
            Assert.AreEqual(0, sys.Data.Coins);
        }

        [Test]
        public void WriteFailureKeepsDataInMemory()
        {
            var store = new MemorySaveStorage { FailWrites = true };
            var sys = new SaveSystem(store, new FixedClock(TestUtil.Today));
            sys.Load();
            sys.Data.Coins = 10;
            Assert.IsFalse(sys.Save());
            Assert.IsNotNull(sys.LastError);
            Assert.AreEqual(10, sys.Data.Coins);
        }

        [Test]
        public void FileStorageIsAtomicAndRecoversInterruptedWrites()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cc-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(dir);
                var sys = new SaveSystem(storage, new FixedClock(TestUtil.Today));
                sys.Load();
                sys.Data.Coins = 1;
                Assert.IsTrue(sys.Save());
                sys.Data.Coins = 2;
                Assert.IsTrue(sys.Save());
                Assert.IsTrue(File.Exists(Path.Combine(dir, "save.json")));
                Assert.IsTrue(File.Exists(Path.Combine(dir, "save.json.bak")));
                Assert.IsFalse(File.Exists(Path.Combine(dir, "save.json.tmp")));

                // Interrupted after rotating main to backup but before the rename: only tmp + bak exist.
                File.Copy(Path.Combine(dir, "save.json"), Path.Combine(dir, "save.json.tmp"), true);
                File.Delete(Path.Combine(dir, "save.json"));
                var recovered = new SaveSystem(new FileSaveStorage(dir), new FixedClock(TestUtil.Today));
                Assert.AreEqual(LoadStatus.RecoveredFromTemp, recovered.Load());
                Assert.AreEqual(2, recovered.Data.Coins);

                // A half-written temp file next to a good main save is ignored.
                Assert.IsTrue(recovered.Save());
                File.WriteAllText(Path.Combine(dir, "save.json.tmp"), "{\"format\":\"colour-cascade-save\",\"coi");
                var normal = new SaveSystem(new FileSaveStorage(dir), new FixedClock(TestUtil.Today));
                Assert.AreEqual(LoadStatus.Loaded, normal.Load());
                Assert.AreEqual(2, normal.Data.Coins);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void NormalizeRepairsInconsistentData()
        {
            var json = "{\"format\":\"colour-cascade-save\",\"version\":1,\"coins\":-50,\"highestUnlocked\":1," +
                       "\"levels\":[{\"n\":5,\"stars\":7,\"done\":true},{\"n\":5,\"stars\":1,\"best\":900}]," +
                       "\"settings\":{\"sfx\":3.5},\"futureField\":{\"x\":1}}";
            var d = SaveCodec.Decode(json);
            Assert.AreEqual(0, d.Coins);
            Assert.AreEqual(6, d.HighestUnlocked, "a completed level always unlocks the next");
            Assert.AreEqual(1, d.Levels.Count, "duplicate records are merged");
            Assert.AreEqual(3, d.FindLevel(5).Stars);
            Assert.AreEqual(900, d.FindLevel(5).BestScore);
            Assert.AreEqual(1f, d.Settings.SfxVolume);
            Assert.IsTrue(d.Settings.Haptics, "missing settings use defaults");
        }

        [Test]
        public void ProgressSurvivesAReloadThroughTheContext()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            ctx.Wallet.Credit(77, "t");
            ctx.Campaign.RecordWin(1, 5000, 2);
            ctx.Settings.Symbols = true;
            ctx.Inventory.AddOwned(PowerUpId.Swap, 2);
            ctx.Save();
            var reloaded = TestUtil.Context(null, store);
            Assert.AreEqual(77, reloaded.Wallet.Balance);
            Assert.AreEqual(2, reloaded.Campaign.HighestUnlocked);
            Assert.AreEqual(2, reloaded.Campaign.StarsFor(1));
            Assert.IsTrue(reloaded.Settings.Symbols);
            Assert.AreEqual(2, reloaded.Inventory.Owned(PowerUpId.Swap));
        }

        [Test]
        public void ResetProgressRebindsServicesToFreshData()
        {
            var ctx = TestUtil.Context();
            ctx.Wallet.Credit(500, "t");
            ctx.Campaign.RecordWin(1, 100, 1);
            bool raised = false;
            ctx.DataReplaced += () => raised = true;
            ctx.ResetProgress();
            Assert.IsTrue(raised);
            Assert.AreEqual(0, ctx.Wallet.Balance);
            Assert.AreEqual(1, ctx.Campaign.HighestUnlocked);
            ctx.Wallet.Credit(5, "after");
            Assert.AreEqual(5, ctx.Data.Coins, "services write to the new save");
        }

        [Test]
        public void JsonHandlesEscapesAndNumbers()
        {
            var obj = Json.Parse("{\"a\":\"line\\nbreak \\\"q\\\" \\u00e9\",\"b\":-12,\"c\":1.5e2,\"d\":[true,false,null]}") as Dictionary<string, object>;
            Assert.AreEqual("line\nbreak \"q\" é", obj["a"]);
            Assert.AreEqual(-12L, obj["b"]);
            Assert.AreEqual(150.0, (double)obj["c"], 1e-9);
            var round = Json.Parse(Json.Write(obj)) as Dictionary<string, object>;
            Assert.AreEqual(obj["a"], round["a"]);
            Assert.Throws<FormatException>(() => Json.Parse("{\"a\":}"));
            Assert.Throws<FormatException>(() => Json.Parse("[1,2"));
        }
    }

    public class SettingsTests
    {
        [Test]
        public void SwitchingSoundOffAndOnKeepsTheChosenVolume()
        {
            var s = TestUtil.Context().Settings;
            s.SfxVolume = 0.3f;
            s.MusicVolume = 0.2f;
            s.SfxOn = false;
            s.MusicOn = false;
            Assert.AreEqual(0f, s.SfxVolume);
            Assert.AreEqual(0f, s.MusicVolume);
            s.SfxOn = true;
            s.MusicOn = true;
            Assert.AreEqual(0.3f, s.SfxVolume, 1e-5, "not reset to the default 0.8");
            Assert.AreEqual(0.2f, s.MusicVolume, 1e-5, "not reset to the default 0.5");
        }

        [Test]
        public void SwitchingOnAfterTheSliderReachedZeroRestoresTheLastAudibleVolume()
        {
            var s = TestUtil.Context().Settings;
            s.SfxVolume = 0.4f;
            s.SfxVolume = 0f; // slider dragged to the bottom
            Assert.IsFalse(s.SfxOn);
            s.SfxOn = true;
            Assert.AreEqual(0.4f, s.SfxVolume, 1e-5);
        }

        [Test]
        public void TheVolumeToRestoreSurvivesARestart()
        {
            var storage = new MemorySaveStorage();
            var ctx = TestUtil.Context(storage: storage);
            ctx.Settings.MusicVolume = 0.25f;
            ctx.Settings.MusicOn = false;
            ctx.Save();
            var reloaded = TestUtil.Context(storage: storage);
            Assert.IsFalse(reloaded.Settings.MusicOn);
            reloaded.Settings.MusicOn = true;
            Assert.AreEqual(0.25f, reloaded.Settings.MusicVolume, 1e-5);
        }

        [Test]
        public void OlderSavesRestoreTheirSavedVolume()
        {
            var json = "{\"format\":\"colour-cascade-save\",\"version\":1,\"settings\":{\"sfx\":0.35,\"music\":0}}";
            var d = SaveCodec.Decode(json);
            Assert.AreEqual(0.35f, d.Settings.SfxVolumeWhenOn, 1e-5);
            Assert.AreEqual(new SettingsData().MusicVolumeWhenOn, d.Settings.MusicVolumeWhenOn, 1e-5, "a muted old save comes back at the default");
        }
    }

    public class NavigationTests
    {
        enum S { Home, Map, Game, Shop, Settings }

        [Test]
        public void BackReturnsToThePreviousScreenAndStopsAtHome()
        {
            var nav = new NavStack<S>(S.Home);
            nav.Show(S.Shop);
            Assert.AreEqual(S.Shop, nav.Current);
            Assert.IsTrue(nav.Back());
            Assert.AreEqual(S.Home, nav.Current);
            Assert.IsFalse(nav.Back(), "home is the root");
            Assert.AreEqual(S.Home, nav.Current);
        }

        [Test]
        public void RepeatedTapsDoNotStackDuplicates()
        {
            var nav = new NavStack<S>(S.Home);
            Assert.IsTrue(nav.Show(S.Map));
            Assert.IsFalse(nav.Show(S.Map));
            Assert.AreEqual(2, nav.Depth);
        }

        [Test]
        public void ShowingAScreenAlreadyInHistoryReturnsToItWithoutLoops()
        {
            var nav = new NavStack<S>(S.Home);
            nav.Show(S.Map);
            nav.Show(S.Game);
            nav.Show(S.Map);
            Assert.AreEqual(2, nav.Depth);
            Assert.AreEqual(S.Map, nav.Current);
        }

        [Test]
        public void LeavingTheGameReplacesItInHistory()
        {
            var nav = new NavStack<S>(S.Home);
            nav.Show(S.Game);
            nav.Replace(S.Map);
            Assert.AreEqual(S.Map, nav.Current);
            nav.Back();
            Assert.AreEqual(S.Home, nav.Current, "back from the map after a game goes home, not into the finished game");

            nav.Show(S.Map);
            nav.Show(S.Game);
            nav.Replace(S.Map);
            Assert.AreEqual(2, nav.Depth);
            nav.Show(S.Game);
            nav.Replace(S.Home);
            Assert.AreEqual(S.Home, nav.Current);
            Assert.AreEqual(1, nav.Depth);
        }

        [Test]
        public void HomeAlwaysReturnsToTheRoot()
        {
            var nav = new NavStack<S>(S.Home);
            nav.Show(S.Map);
            nav.Show(S.Game);
            nav.Show(S.Settings);
            nav.Home();
            Assert.AreEqual(S.Home, nav.Current);
            Assert.AreEqual(1, nav.Depth);
        }
    }

    public class ProceduralTests
    {
        static List<ImageData> images;
        static AtlasResult atlas;

        static void Build()
        {
            if (atlas != null) return;
            images = ArtBook.BuildAtlasImages();
            atlas = AtlasPacker.Pack(images);
        }

        [Test]
        public void AtlasContainsEverySpriteWithoutOverlaps()
        {
            Build();
            Assert.AreEqual(images.Count, atlas.Sprites.Count, "sprite names must be unique");
            var rects = atlas.Sprites.Values.Select(m => m.Rect).ToList();
            foreach (var r in rects)
            {
                Assert.IsTrue(r.X >= 0 && r.Y >= 0 && r.X + r.Width <= atlas.Image.Width && r.Y + r.Height <= atlas.Image.Height);
            }
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    var a = rects[i];
                    var b = rects[j];
                    bool overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                    Assert.IsFalse(overlap);
                }
        }

        [Test]
        public void EveryReferencedSpriteExists()
        {
            Build();
            var names = new List<string>
            {
                ArtNames.Round, ArtNames.RoundShadow, ArtNames.RoundStroke, ArtNames.Gloss, ArtNames.Circle, ArtNames.CircleGloss, ArtNames.CircleShadow,
                ArtNames.Ring, ArtNames.Glow, ArtNames.Gradient, ArtNames.Dot, ArtNames.Ribbon, ArtNames.Gem, ArtNames.Coin, ArtNames.StarGold,
                ArtNames.StarEmpty, ArtNames.Hand, ArtNames.Stone, ArtNames.TileGlow, ArtNames.TileFlash, ArtNames.ParticleSoft,
                ArtNames.ParticleSpark, ArtNames.ParticleShard, ArtNames.ParticleRing,
            };
            for (int c = 0; c < 6; c++)
            {
                names.Add(ArtNames.Tile(c));
                names.Add(ArtNames.Symbol(c));
                names.Add(ArtNames.PowerUp(c));
            }
            foreach (Icon i in Enum.GetValues(typeof(Icon))) names.Add(ArtNames.IconName(i));
            foreach (Decor d in Enum.GetValues(typeof(Decor))) names.Add(ArtNames.DecorName(d));
            foreach (var n in names) Assert.IsTrue(atlas.Sprites.ContainsKey(n), "missing sprite " + n);
        }

        [Test]
        public void SymbolsAreDistinctShapes()
        {
            // Each colour's symbol must differ from every other, so colour-blind players can tell tiles apart.
            Build();
            var masks = new List<bool[]>();
            for (int c = 0; c < 6; c++)
            {
                var img = images.First(i => i.Name == ArtNames.Symbol(c));
                var mask = new bool[img.Width * img.Height];
                for (int p = 0; p < mask.Length; p++) mask[p] = img.Pixels[p * 4] > 200 && img.Pixels[p * 4 + 3] > 200;
                masks.Add(mask);
            }
            // Jaccard distance (pixels that differ / pixels covered by either shape): 0 = identical, 1 = disjoint.
            for (int a = 0; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                {
                    int diff = 0, union = 0;
                    for (int p = 0; p < masks[a].Length; p++)
                    {
                        if (masks[a][p] != masks[b][p]) diff++;
                        if (masks[a][p] || masks[b][p]) union++;
                    }
                    Assert.Greater(diff / (double)union, 0.3, "symbols " + a + " and " + b + " are too similar");
                }
        }

        [Test]
        public void AtlasCacheRoundTripsAndRejectsStaleKeys()
        {
            Build();
            var ms = new MemoryStream();
            AtlasCache.Write(ms, atlas, "v1");
            ms.Position = 0;
            var back = AtlasCache.Read(ms, "v1");
            Assert.IsNotNull(back);
            Assert.AreEqual(atlas.Sprites.Count, back.Sprites.Count);
            CollectionAssert.AreEqual(atlas.Image.Pixels, back.Image.Pixels);
            ms.Position = 0;
            Assert.IsNull(AtlasCache.Read(ms, "v2"));
            var truncated = new MemoryStream(ms.ToArray().Take((int)ms.Length / 2).ToArray());
            Assert.IsNull(AtlasCache.Read(truncated, "v1"));
        }

        [Test]
        public void SoundsAreValidAudio()
        {
            var effects = SoundSynth.BuildEffects();
            Assert.Greater(effects.Count, 20);
            foreach (var s in effects)
            {
                Assert.Greater(s.Samples.Length, 100, s.Name);
                Assert.IsTrue(s.Samples.All(v => !float.IsNaN(v) && Math.Abs(v) <= 1f), s.Name);
                Assert.IsTrue(s.Samples.Any(v => Math.Abs(v) > 0.05f), s.Name + " is silent");
            }
            for (int i = 0; i < SfxNames.PopVariants; i++) Assert.IsTrue(effects.Any(e => e.Name == SfxNames.Pop(i)));
            var music = SoundSynth.BuildMusic(8000);
            Assert.IsTrue(music.Loop);
            Assert.Greater(music.Samples.Length / (float)music.SampleRate, 10f);
        }

        [Test]
        public void BackgroundsGenerateForEveryTheme()
        {
            for (int t = -1; t < 6; t++)
            {
                var bg = ArtBook.Background(t, 32, 64);
                Assert.AreEqual(32 * 64 * 4, bg.Pixels.Length);
            }
        }
    }
}
