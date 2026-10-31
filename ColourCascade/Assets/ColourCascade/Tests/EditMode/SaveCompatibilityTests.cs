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

        [Test]
        public void ASaveFromBeforeTheOverhaulLoadsAndWritesBackUnchanged()
        {
            Assert.AreEqual(SaveBeforeOverhaul, SaveCodec.Encode(SaveCodec.Decode(SaveBeforeOverhaul)), "nothing lost or changed");

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

        [Test]
        public void TheSamePlayStillProducesExactlyTheSameSave()
        {
            var store = new MemorySaveStorage();
            PlayScript(store);
            Assert.AreEqual(SaveBeforeOverhaul, store.Main, "same progress, stars, coins, ledger and records as before the overhaul");
        }

        /// <summary>A fixed sequence of real play through GameContext: campaign levels, every challenge mode, a purchase and settings.</summary>
        internal static GameContext PlayScript(MemorySaveStorage store)
        {
            var ctx = TestUtil.Context(null, store);
            for (int n = 1; n <= 12; n++)
            {
                for (int attempt = 0; attempt < 3 && ctx.Campaign.HighestUnlocked <= n; attempt++)
                {
                    var setup = ctx.CreateCampaignRun(n);
                    setup.BoardSeed = (ulong)(n * 100 + attempt); // runs normally get a fresh random board; fixed here
                    var s = new PuzzleSession(setup);
                    s.Start();
                    ctx.BeginRun(s);
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
