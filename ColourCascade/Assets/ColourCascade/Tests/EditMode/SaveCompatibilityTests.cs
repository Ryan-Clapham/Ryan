using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Players keep their progress and coins across the campaign progression overhaul. The fixture is a real save
    /// written by the checkpoint build from before the overhaul (branch backup/before-progression-v2, commit
    /// b0511bc) after running <see cref="PlayScript"/>. Update it only for a deliberate save format change, and
    /// then add a migration test for the old one.
    /// </summary>
    public class SaveCompatibilityTests
    {
        const string SaveBeforeOverhaul =
            "{\"format\":\"colour-cascade-save\",\"version\":1,\"created\":639285264000000000,\"saved\":639285264000000000," +
            "\"saveCount\":16,\"coins\":124,\"highestUnlocked\":13,\"latestDay\":\"2026-10-25\",\"ledger\":[{\"t\":63928526" +
            "4000000000,\"d\":5,\"b\":5,\"r\":\"c.first.1\"},{\"t\":639285264000000000,\"d\":1,\"b\":6,\"r\":\"c.star.1.1\"" +
            "},{\"t\":639285264000000000,\"d\":1,\"b\":7,\"r\":\"c.star.1.2\"},{\"t\":639285264000000000,\"d\":5,\"b\":12," +
            "\"r\":\"c.first.2\"},{\"t\":639285264000000000,\"d\":1,\"b\":13,\"r\":\"c.star.2.1\"},{\"t\":639285264000000000" +
            ",\"d\":1,\"b\":14,\"r\":\"c.star.2.2\"},{\"t\":639285264000000000,\"d\":5,\"b\":19,\"r\":\"c.first.3\"},{\"t\"" +
            ":639285264000000000,\"d\":1,\"b\":20,\"r\":\"c.star.3.1\"},{\"t\":639285264000000000,\"d\":1,\"b\":21,\"r\":\"" +
            "c.star.3.2\"},{\"t\":639285264000000000,\"d\":1,\"b\":22,\"r\":\"c.star.3.3\"},{\"t\":639285264000000000,\"d\"" +
            ":5,\"b\":27,\"r\":\"c.first.4\"},{\"t\":639285264000000000,\"d\":1,\"b\":28,\"r\":\"c.star.4.1\"},{\"t\":63928" +
            "5264000000000,\"d\":1,\"b\":29,\"r\":\"c.star.4.2\"},{\"t\":639285264000000000,\"d\":5,\"b\":34,\"r\":\"c.firs" +
            "t.5\"},{\"t\":639285264000000000,\"d\":1,\"b\":35,\"r\":\"c.star.5.1\"},{\"t\":639285264000000000,\"d\":1,\"b" +
            "\":36,\"r\":\"c.star.5.2\"},{\"t\":639285264000000000,\"d\":5,\"b\":41,\"r\":\"c.first.6\"},{\"t\":639285264000" +
            "000000,\"d\":1,\"b\":42,\"r\":\"c.star.6.1\"},{\"t\":639285264000000000,\"d\":1,\"b\":43,\"r\":\"c.star.6.2\"}" +
            ",{\"t\":639285264000000000,\"d\":1,\"b\":44,\"r\":\"c.star.6.3\"},{\"t\":639285264000000000,\"d\":5,\"b\":49," +
            "\"r\":\"c.first.7\"},{\"t\":639285264000000000,\"d\":1,\"b\":50,\"r\":\"c.star.7.1\"},{\"t\":639285264000000000" +
            ",\"d\":1,\"b\":51,\"r\":\"c.star.7.2\"},{\"t\":639285264000000000,\"d\":1,\"b\":52,\"r\":\"c.star.7.3\"},{\"t" +
            "\":639285264000000000,\"d\":5,\"b\":57,\"r\":\"c.first.8\"},{\"t\":639285264000000000,\"d\":1,\"b\":58,\"r\":\"" +
            "c.star.8.1\"},{\"t\":639285264000000000,\"d\":5,\"b\":63,\"r\":\"c.first.9\"},{\"t\":639285264000000000,\"d\":" +
            "1,\"b\":64,\"r\":\"c.star.9.1\"},{\"t\":639285264000000000,\"d\":1,\"b\":65,\"r\":\"c.star.9.2\"},{\"t\":63928" +
            "5264000000000,\"d\":1,\"b\":66,\"r\":\"c.star.9.3\"},{\"t\":639285264000000000,\"d\":5,\"b\":71,\"r\":\"c.firs" +
            "t.10\"},{\"t\":639285264000000000,\"d\":1,\"b\":72,\"r\":\"c.star.10.1\"},{\"t\":639285264000000000,\"d\":20," +
            "\"b\":92,\"r\":\"c.mile.10\"},{\"t\":639285264000000000,\"d\":6,\"b\":98,\"r\":\"c.first.11\"},{\"t\":639285264" +
            "000000000,\"d\":1,\"b\":99,\"r\":\"c.star.11.1\"},{\"t\":639285264000000000,\"d\":1,\"b\":100,\"r\":\"c.star.1" +
            "1.2\"},{\"t\":639285264000000000,\"d\":6,\"b\":106,\"r\":\"c.first.12\"},{\"t\":639285264000000000,\"d\":1,\"b" +
            "\":107,\"r\":\"c.star.12.1\"},{\"t\":639285264000000000,\"d\":1,\"b\":108,\"r\":\"c.star.12.2\"},{\"t\":639285" +
            "264000000000,\"d\":1,\"b\":109,\"r\":\"c.star.12.3\"},{\"t\":639285264000000000,\"d\":15,\"b\":124,\"r\":\"d.2" +
            "026-10-25.done\"},{\"t\":639285264000000000,\"d\":10,\"b\":134,\"r\":\"w.2026-W43.t0\"},{\"t\":639285264000000" +
            "000,\"d\":15,\"b\":149,\"r\":\"w.2026-W43.t1\"},{\"t\":639285264000000000,\"d\":25,\"b\":174,\"r\":\"w.2026-W4" +
            "3.t2\"},{\"t\":639285264000000000,\"d\":10,\"b\":184,\"r\":\"e.mile.2500\"},{\"t\":639285264000000000,\"d\":15" +
            ",\"b\":199,\"r\":\"e.mile.5000\"},{\"t\":639285264000000000,\"d\":25,\"b\":224,\"r\":\"e.mile.10000\"},{\"t\":" +
            "639285264000000000,\"d\":-100,\"b\":124,\"r\":\"shop.Blast.x1\"}],\"inventory\":[{\"id\":0,\"owned\":1,\"trial" +
            "s\":0}],\"levels\":[{\"n\":1,\"best\":5720,\"stars\":2,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":2,\"best\"" +
            ":7510,\"stars\":2,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":3,\"best\":4500,\"stars\":3,\"done\":true,\"pla" +
            "ys\":1,\"wins\":1},{\"n\":4,\"best\":4100,\"stars\":2,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":5,\"best\":" +
            "4320,\"stars\":2,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":6,\"best\":5860,\"stars\":3,\"done\":true,\"play" +
            "s\":1,\"wins\":1},{\"n\":7,\"best\":7320,\"stars\":3,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":8,\"best\":4" +
            "690,\"stars\":1,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":9,\"best\":8250,\"stars\":3,\"done\":true,\"plays" +
            "\":1,\"wins\":1},{\"n\":10,\"best\":4500,\"stars\":1,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":11,\"best\":" +
            "6590,\"stars\":2,\"done\":true,\"plays\":1,\"wins\":1},{\"n\":12,\"best\":6030,\"stars\":3,\"done\":true,\"pla" +
            "ys\":1,\"wins\":1}],\"claims\":[\"c.first.1\",\"c.first.10\",\"c.first.11\",\"c.first.12\",\"c.first.2\",\"c.f" +
            "irst.3\",\"c.first.4\",\"c.first.5\",\"c.first.6\",\"c.first.7\",\"c.first.8\",\"c.first.9\",\"c.mile.10\",\"c" +
            ".star.1.1\",\"c.star.1.2\",\"c.star.10.1\",\"c.star.11.1\",\"c.star.11.2\",\"c.star.12.1\",\"c.star.12.2\",\"c" +
            ".star.12.3\",\"c.star.2.1\",\"c.star.2.2\",\"c.star.3.1\",\"c.star.3.2\",\"c.star.3.3\",\"c.star.4.1\",\"c.sta" +
            "r.4.2\",\"c.star.5.1\",\"c.star.5.2\",\"c.star.6.1\",\"c.star.6.2\",\"c.star.6.3\",\"c.star.7.1\",\"c.star.7.2" +
            "\",\"c.star.7.3\",\"c.star.8.1\",\"c.star.9.1\",\"c.star.9.2\",\"c.star.9.3\",\"d.2026-10-25.done\",\"e.mile.1" +
            "0000\",\"e.mile.2500\",\"e.mile.5000\",\"w.2026-W43.t0\",\"w.2026-W43.t1\",\"w.2026-W43.t2\"],\"daily\":[{\"da" +
            "y\":\"2026-10-25\",\"best\":4060,\"stars\":2,\"done\":true,\"plays\":1}],\"weekly\":[{\"week\":\"2026-W43\",\"" +
            "best\":8950,\"runs\":1}],\"endless\":{\"best\":11580,\"bestGroup\":9,\"bestMoves\":46,\"bestStage\":4,\"runs\"" +
            ":1},\"tutorials\":[],\"settings\":{\"sfx\":0,\"music\":0.30000001192092896,\"haptics\":true,\"sfxWhenOn\":0.80" +
            "0000011920929,\"musicWhenOn\":0.30000001192092896,\"symbols\":true,\"reducedMotion\":false},\"stats\":{\"tiles" +
            "\":1217,\"groups\":227,\"powerUps\":0,\"games\":15,\"biggestGroup\":14,\"shuffles\":0}}";

        /// <summary>
        /// Fields added to the save since the fixture was written, with the value an old save gets. Everything else must
        /// read and write back exactly. (endless.level: Endless progression, added with cross-mode mechanics.)
        /// </summary>
        /// <summary>
        /// The save without what later updates deliberately changed: the Endless level (a new field); the claim
        /// recorded when a power-up's tutorial level is first won (2.1); since Endless runs now walk the stage
        /// journey (2.1), the Endless record and the lifetime group and tile counts its run adds to (checked for
        /// presence only); and the monetisation record (3.0, a new field that an old save gets empty). Everything
        /// else (campaign progress, stars, coins, ledger, Daily and Weekly records, inventory, settings) must match
        /// exactly.
        /// </summary>
        static string WithoutNewFields(string json, out long endlessLevel)
        {
            var root = (System.Collections.Generic.Dictionary<string, object>)Json.Parse(json);
            if (root.ContainsKey("monetisation"))
            {
                var fresh = (System.Collections.Generic.Dictionary<string, object>)Json.Parse(SaveCodec.Encode(new SaveData()));
                Assert.AreEqual(Json.Write(fresh["monetisation"]), Json.Write(root["monetisation"]), "an old save gets an empty monetisation record");
                root.Remove("monetisation");
            }
            // The achievements update: the unlocked list (checked in AnOldSaveUnlocksWhatItAlreadyProves) and new lifetime
            // counters, which an old save gets at zero.
            root.Remove("achievements");
            var stats0 = (System.Collections.Generic.Dictionary<string, object>)root["stats"];
            foreach (var k in NewStatsKeys)
            {
                if (!stats0.ContainsKey(k)) continue;
                var v = stats0[k];
                Assert.IsTrue(v is long l ? l == 0 : Json.Write(v) == "{}" || Json.Write(v) == "[]", "stats." + k + " starts empty");
                stats0.Remove(k);
            }
            var endless = (System.Collections.Generic.Dictionary<string, object>)root["endless"];
            endlessLevel = endless.ContainsKey("level") ? (long)endless["level"] : -1;
            endless.Remove("level");
            foreach (var k in new[] { "best", "bestGroup", "bestMoves", "bestStage" })
            {
                Assert.IsTrue(endless.ContainsKey(k), "endless." + k + " is saved");
                endless[k] = 0L;
            }
            var stats = (System.Collections.Generic.Dictionary<string, object>)root["stats"];
            foreach (var k in new[] { "groups", "tiles" })
            {
                Assert.IsTrue(stats.ContainsKey(k), "stats." + k + " is saved");
                stats[k] = 0L;
            }
            var claims = (System.Collections.Generic.List<object>)root["claims"];
            claims.RemoveAll(c => ((string)c).StartsWith("pu.trial.", System.StringComparison.Ordinal));
            return Json.Write(root);
        }

        [Test]
        public void ASaveFromBeforeTheOverhaulLoadsAndWritesBackUnchanged()
        {
            string written = SaveCodec.Encode(SaveCodec.Decode(SaveBeforeOverhaul));
            Assert.AreEqual(WithoutNewFields(SaveBeforeOverhaul, out _), WithoutNewFields(written, out long level), "nothing lost or changed");
            var old = SaveCodec.Decode(SaveBeforeOverhaul);
            Assert.AreEqual(System.Math.Max(1, old.Endless.BestStage), level, "an old save's Endless level starts from its best stage");

            var ctx = TestUtil.Context(null, new MemorySaveStorage { Main = SaveBeforeOverhaul });
            Assert.AreEqual(13, ctx.Campaign.HighestUnlocked);
            Assert.AreEqual(124, ctx.Wallet.Balance);
            int[] stars = { 2, 2, 3, 2, 2, 3, 3, 1, 3, 1, 2, 3 };
            for (int n = 1; n <= stars.Length; n++) Assert.AreEqual(stars[n - 1], ctx.Campaign.StarsFor(n), "level " + n);
            Assert.IsTrue(ctx.Settings.Symbols);
            Assert.IsFalse(ctx.Settings.SfxOn);

            // The player carries on where they left off.
            var s = new PuzzleSession(ctx.CreateCampaignRun(13));
            s.Start();
            ctx.BeginRun(s);
            Bots.PlayOut(s, BotKind.Greedy, new Rng(1));
            ctx.FinishRun(s);
            Assert.IsTrue(ctx.Save());
        }

        // The whole save the play script produces with today's rules (see SameRulesSameSave). Re-record only for a
        // deliberate gameplay change, and say so in the commit. Recorded for 2.1's later changes (5 x 5 bombs, groups
        // of specials, no spare-move bonus); it matched the build before them exactly, since levels played to the end
        // never paid a bonus. 3.0 added the monetisation record (here: the interstitial level count); the rest of the
        // save is still exactly the 2.1 save, checked by ExpectedSaveFingerprintBefore3.
        // The achievements update added lifetime counters and the unlocked list (3316499532689436422 before it).
        // Update 3.1 re-recorded it for the new reward values (coins, ledger amounts); with the old values the play still
        // writes the earlier save exactly (ExpectedSaveFingerprintBefore31, checked in SameRulesSameSave).
        const ulong ExpectedSaveFingerprint = 14645177115692661380UL;
        const ulong ExpectedSaveFingerprintBefore31 = 13269928510684773610UL;
        const ulong ExpectedSaveFingerprintBefore3 = 7841276991854770152UL;

        static readonly string[] NewStatsKeys = { "stones", "ice", "chains", "moss", "treasures", "mostInMove", "biggestCombo", "powerUpUses", "pureLevels" };

        [Test]
        public void TheSamePlayStillMakesTheSameProgress()
        {
            // The fixture comes from the build before the progression overhaul, when a level ended at its target and
            // spare moves became bonus points. Levels are now played to their last move and unused moves score
            // nothing, so the same play scores differently: best scores, stars, star rewards (coins, claims, ledger)
            // and the Endless record and lifetime counts are expected to differ. Everything else must not: which
            // levels are unlocked and completed, plays and wins, first-clear rewards, Daily and Weekly plays, the
            // purchase, inventory, tutorials and settings.
            // Update 3.1 changed what first clears, milestones and the Daily pay, so the fixture's play is compared under
            // the reward values it was recorded with (tuning version 2), and must match exactly.
            var store = new MemorySaveStorage();
            PlayScript(store, 2);
            Assert.AreEqual(Progress(SaveBeforeOverhaul), Progress(store.Main));
            // With today's values the same rewards are paid for the same reasons, at today's amounts.
            var now = new MemorySaveStorage();
            var ctx = PlayScript(now);
            Assert.AreEqual(Reasons(SaveBeforeOverhaul), Reasons(now.Main), "the same one-time rewards, for the same reasons");
            var eco = ctx.Config.Economy;
            foreach (var e in ctx.Wallet.Ledger)
            {
                var p = e.Reason.Split('.');
                if (e.Reason.StartsWith("c.first.", System.StringComparison.Ordinal)) Assert.AreEqual(eco.FirstClearReward(int.Parse(p[2])), e.Delta, e.Reason);
                if (e.Reason.StartsWith("c.star.", System.StringComparison.Ordinal)) Assert.AreEqual(eco.StarReward(int.Parse(p[3])), e.Delta, e.Reason);
                if (e.Reason.EndsWith(".done", System.StringComparison.Ordinal)) Assert.AreEqual(10, e.Delta, e.Reason);
            }
        }

        /// <summary>The reasons of the one-time rewards that don't depend on score, without their amounts.</summary>
        static string Reasons(string json)
        {
            var root = (System.Collections.Generic.Dictionary<string, object>)Json.Parse(json);
            var list = new System.Collections.Generic.List<object>();
            foreach (System.Collections.Generic.Dictionary<string, object> e in (System.Collections.Generic.List<object>)root["ledger"])
            {
                string r = (string)e["r"];
                if (r.StartsWith("c.first.", System.StringComparison.Ordinal) || r.StartsWith("c.mile.", System.StringComparison.Ordinal) ||
                    r.StartsWith("shop.", System.StringComparison.Ordinal) || r.StartsWith("d.", System.StringComparison.Ordinal))
                    list.Add(r);
            }
            return Json.Write(list);
        }

        [Test]
        public void SameRulesSameSave()
        {
            // Update 3.1 changed only reward values: with the version 2 values the same play writes exactly the save it
            // wrote before 3.1 (so the Endless finale fix and the rest of 3.1 changed nothing else in it).
            var before = new MemorySaveStorage();
            PlayScript(before, 2);
            Assert.AreEqual(ExpectedSaveFingerprintBefore31, StableHash.Of(before.Main), "with the old reward values, the old save exactly");
            var store = new MemorySaveStorage();
            PlayScript(store);
            if (System.Environment.GetEnvironmentVariable("PRINT_SAVE_FINGERPRINT") != null) System.Console.WriteLine("save fingerprint " + StableHash.Of(store.Main));
            Assert.AreEqual(ExpectedSaveFingerprint, StableHash.Of(store.Main), "the same play gives exactly the same save");
            var root = (System.Collections.Generic.Dictionary<string, object>)Json.Parse(store.Main);
            var m = (System.Collections.Generic.Dictionary<string, object>)root["monetisation"];
            Assert.AreEqual(0L, m["lastInterstitial"], "no advert was involved");
            Assert.AreEqual(false, m["noAds"]);
            // Without what 3.0 and the achievements update added, the version 2 play is still exactly the 2.1 save.
            root = (System.Collections.Generic.Dictionary<string, object>)Json.Parse(before.Main);
            root.Remove("monetisation");
            root.Remove("achievements");
            var stats = (System.Collections.Generic.Dictionary<string, object>)root["stats"];
            foreach (var k in NewStatsKeys) stats.Remove(k);
            Assert.AreEqual(ExpectedSaveFingerprintBefore3, StableHash.Of(Json.Write(root)), "updates 3.0 and achievements changed nothing else in the save");
        }

        [Test]
        public void AnOldSaveUnlocksWhatItAlreadyProves()
        {
            // The real pre-overhaul save: 12 levels (27 stars), an Endless run of 11,580 (stage 4, best group 9), a Daily
            // completed, all three Weekly Rush medals, a best group of 14 and 1,217 tiles cleared.
            var store = new MemorySaveStorage { Main = SaveBeforeOverhaul };
            var ctx = TestUtil.Context(null, store);
            var unlocked = new System.Collections.Generic.List<string>();
            foreach (var d in ctx.UnlockedOnLoad) unlocked.Add(d.id);
            CollectionAssert.AreEquivalent(new[] { "level_10", "endless_5000", "endless_10000", "group_12", "daily_1", "weekly_bronze", "weekly_gold" }, unlocked);
            Assert.AreEqual(124, ctx.Wallet.Balance, "unlocked, not paid: the player claims each reward");
            Assert.AreEqual(7, ctx.Achievements.Unclaimed);
            Assert.IsTrue(SaveCodec.Decode(store.Main).Achievements.Unlocked.ContainsKey("weekly_gold"), "saved at once");
            var again = TestUtil.Context(null, store);
            Assert.AreEqual(0, again.UnlockedOnLoad.Count, "never unlocked twice");
        }

        /// <summary>The parts of a save that don't depend on how points are scored.</summary>
        static string Progress(string json)
        {
            var root = (System.Collections.Generic.Dictionary<string, object>)Json.Parse(json);
            var keep = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var k in new[] { "format", "version", "highestUnlocked", "latestDay", "inventory", "tutorials", "settings" })
                if (root.ContainsKey(k)) keep[k] = root[k];
            var levels = new System.Collections.Generic.List<object>();
            foreach (System.Collections.Generic.Dictionary<string, object> l in (System.Collections.Generic.List<object>)root["levels"])
                levels.Add(new System.Collections.Generic.Dictionary<string, object> { ["n"] = l["n"], ["done"] = l["done"], ["plays"] = l["plays"], ["wins"] = l["wins"] });
            keep["levels"] = levels;
            var daily = new System.Collections.Generic.List<object>();
            foreach (System.Collections.Generic.Dictionary<string, object> d in (System.Collections.Generic.List<object>)root["daily"])
                daily.Add(new System.Collections.Generic.Dictionary<string, object> { ["day"] = d["day"], ["done"] = d["done"], ["plays"] = d["plays"] });
            keep["daily"] = daily;
            var weekly = new System.Collections.Generic.List<object>();
            foreach (System.Collections.Generic.Dictionary<string, object> w in (System.Collections.Generic.List<object>)root["weekly"])
                weekly.Add(new System.Collections.Generic.Dictionary<string, object> { ["week"] = w["week"], ["runs"] = w["runs"] });
            keep["weekly"] = weekly;
            keep["endlessRuns"] = ((System.Collections.Generic.Dictionary<string, object>)root["endless"])["runs"];
            var firstClears = new System.Collections.Generic.List<object>();
            foreach (System.Collections.Generic.Dictionary<string, object> e in (System.Collections.Generic.List<object>)root["ledger"])
            {
                string r = (string)e["r"];
                if (r.StartsWith("c.first.", System.StringComparison.Ordinal) || r.StartsWith("c.mile.", System.StringComparison.Ordinal) ||
                    r.StartsWith("shop.", System.StringComparison.Ordinal) || r.StartsWith("d.", System.StringComparison.Ordinal))
                    firstClears.Add(r + " " + e["d"]);
            }
            keep["rewardsThatDontDependOnScore"] = firstClears;
            return Json.Write(keep);
        }

        /// <summary>A fixed sequence of real play through GameContext: campaign levels, every challenge mode, a purchase and settings.</summary>
        internal static GameContext PlayScript(MemorySaveStorage store, int economyVersion = EconomyTuning.Current)
        {
            var config = GameConfigData.CreateDefault();
            if (economyVersion != EconomyTuning.Current) EconomyTuning.ApplyVersion(config, economyVersion);
            var ctx = new GameContext(config, store, new FixedClock(TestUtil.Today));
            ctx.Load();
            for (int n = 1; n <= 12; n++)
            {
                for (int attempt = 0; attempt < 3 && ctx.Campaign.HighestUnlocked <= n; attempt++)
                {
                    var setup = ctx.CreateCampaignRun(n);
                    setup.BoardSeed = (ulong)(n * 100 + attempt); // runs normally get a fresh random board; fixed here
                    var s = new PuzzleSession(setup);
                    s.Start();
                    ctx.BeginRun(s);
                    // Every move played, as levels are now played.
                    Bots.PlayOut(s, BotKind.Greedy, new Rng((ulong)(n * 10 + attempt)));
                    ctx.FinishRun(s);
                }
            }
            var daily = new PuzzleSession(ctx.CreateDailyRun());
            daily.Start();
            ctx.BeginRun(daily);
            Bots.PlayOut(daily, BotKind.Mixed, new Rng(5));
            ctx.FinishRun(daily);

            var rush = new PuzzleSession(ctx.CreateWeeklyRun());
            rush.Start();
            ctx.BeginRun(rush);
            rush.StartTimer();
            Bots.PlayOut(rush, BotKind.Greedy, new Rng(6), 45);
            rush.Tick(1000);
            ctx.FinishRun(rush);

            var endlessSetup = ctx.CreateEndlessRun();
            endlessSetup.BoardSeed = 77;
            var endless = new PuzzleSession(endlessSetup);
            endless.Start();
            ctx.BeginRun(endless);
            Bots.PlayOut(endless, BotKind.Mixed, new Rng(7), 300);
            ctx.FinishRun(endless);

            ctx.Shop.TryBuy(PowerUpId.Blast);
            ctx.Settings.Symbols = true;
            ctx.Settings.MusicVolume = 0.3f;
            ctx.Settings.SfxOn = false;
            ctx.Save();
            return ctx;
        }
    }
}
