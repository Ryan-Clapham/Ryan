using System.Collections.Generic;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Fingerprints of the game's behaviour, recorded before the campaign progression overhaul began
    /// (checkpoint branch backup/before-progression-v2). Engine refactors must reproduce them exactly:
    /// the same generated levels, the same results for seeded games in every mode, and the same power-up
    /// outcomes. Update a value only when a change to gameplay is intended, and say so in the commit.
    /// </summary>
    public class GoldenGameplayTests
    {
        // Levels 1-20 (before the first mechanic): recorded on the pre-mechanics build (backup/before-phase2-mechanics)
        // and unchanged since. They must never change: players have already played them.
        const ulong ExpectedOpeningLevels = 6328615708927920260UL;
        const ulong ExpectedOpeningPlay = 8421993498075673067UL;
        // Levels 1-60 and seeded games on 1-40: re-recorded when the campaign mechanics arrived (phase 2), which
        // deliberately changed levels 21 and up. The pre-overhaul values were 16043548939747815137 and 15566285578730975039.
        const ulong ExpectedCampaignLevels = 12182320859475582901UL;
        const ulong ExpectedSeededPlay = 2883777929054136064UL;
        const ulong ExpectedChallengeModes = 318364672465816974UL;
        const ulong ExpectedScriptedPowerUps = 2022348128171972134UL;

        static ulong Mix(ulong h, long v) => StableHash.Combine(h, unchecked((ulong)v));
        static ulong Mix(ulong h, string s) => StableHash.Combine(h, StableHash.Of(s ?? ""));

        static ulong Fingerprint(ulong h, PuzzleSession s)
        {
            h = Mix(h, s.Score);
            h = Mix(h, s.FinalScore);
            h = Mix(h, s.MovesMade);
            h = Mix(h, s.MovesLeft);
            h = Mix(h, s.Shuffles);
            h = Mix(h, (int)s.State);
            h = Mix(h, s.Stars);
            h = Mix(h, s.GroupsCleared);
            h = Mix(h, s.TilesCleared);
            h = Mix(h, s.BiggestGroup);
            return Mix(h, s.Board.ToDebugString());
        }

        static ulong Fingerprint(ulong h, MoveOutcome o)
        {
            h = Mix(h, (int)o.Kind);
            h = Mix(h, o.ScoreGained);
            h = Mix(h, o.Cleared.Count);
            h = Mix(h, o.Falls.Count);
            h = Mix(h, o.Spawns.Count);
            h = Mix(h, o.ShuffleMoves.Count);
            h = Mix(h, o.Recolours.Count);
            h = Mix(h, o.SwapMoves.Count);
            h = Mix(h, o.MovesAdded);
            return Mix(h, o.Ended ? 1 : 0);
        }

        static ulong CampaignLevels(int upTo = 60)
        {
            var cfg = GameConfigData.CreateDefault();
            var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            ulong h = 17;
            for (int n = 1; n <= upTo; n++)
            {
                var s = catalog.Get(n);
                h = Mix(h, s.Target);
                h = Mix(h, s.Star2);
                h = Mix(h, s.Star3);
                h = Mix(h, s.Moves);
                h = Mix(h, s.Colours);
                h = Mix(h, s.AssistPermille);
                h = Mix(h, s.LeftoverBonusPerMove);
                h = Mix(h, string.Join("/", s.Layout.ToRows()));
                h = Mix(h, s.Note);
            }
            return h;
        }

        static ulong SeededPlay(int upTo = 40)
        {
            var cfg = GameConfigData.CreateDefault();
            var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            ulong h = 31;
            foreach (var kind in new[] { BotKind.Greedy, BotKind.Mixed, BotKind.Casual })
                for (int n = 1; n <= upTo; n++)
                    for (int seed = 1; seed <= 2; seed++)
                    {
                        var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = catalog.Get(n), Scoring = cfg.Scoring, BoardSeed = (ulong)(n * 1000 + seed) });
                        s.Start();
                        Bots.PlayOut(s, kind, new Rng((ulong)(n * 77 + seed)));
                        h = Fingerprint(h, s);
                    }
            return h;
        }

        static ulong ChallengeModes()
        {
            var ctx = TestUtil.Context();
            var cfg = ctx.Config;
            ulong h = 47;
            for (int i = 0; i < 6; i++)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = (ulong)(500 + i);
                var s = new PuzzleSession(setup);
                s.Start();
                Bots.PlayOut(s, BotKind.Mixed, new Rng((ulong)(900 + i)), 400);
                h = Fingerprint(h, s);
            }
            foreach (var day in new[] { "2026-10-25", "2026-10-26", "2026-10-27", "2026-12-31" })
            {
                var s = new PuzzleSession(ctx.CreateDailyRun(day));
                s.Start();
                Bots.PlayOut(s, BotKind.Mixed, new Rng(StableHash.Of(day)));
                h = Fingerprint(h, s);
            }
            foreach (var week in new[] { "2026-W43", "2026-W44", "2027-W01" })
            {
                var s = new PuzzleSession(ctx.CreateWeeklyRun(week));
                s.Start();
                s.StartTimer();
                Bots.PlayOut(s, BotKind.Greedy, new Rng(StableHash.Of(week)), 45);
                s.Tick(1000);
                h = Fingerprint(h, s);
            }
            return h;
        }

        static ulong ScriptedPowerUps()
        {
            var s = TestUtil.Session(GameMode.Campaign, 30, 1000000, 4,
                "0123012",
                "1#30121",
                "2301#30",
                "3012301",
                "0120123",
                "1230120");
            var o = new MoveOutcome();
            ulong h = 61;
            PowerUpFailure f;
            var requests = new List<PowerUpRequest>
            {
                PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(3, 3)),
                PowerUpRequest.AtCell(PowerUpId.TileBreaker, new GridPos(1, 4)),
                PowerUpRequest.Pair(PowerUpId.Swap, new GridPos(0, 0), new GridPos(1, 0)),
                PowerUpRequest.CellColour(PowerUpId.ColourShift, new GridPos(2, 1), 3),
                PowerUpRequest.AtCell(PowerUpId.ColourBurst, new GridPos(5, 5)),
                PowerUpRequest.Instant(PowerUpId.ExtraMoves),
                PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(0, 5)),
                PowerUpRequest.AtCell(PowerUpId.TileBreaker, new GridPos(4, 3)),
            };
            foreach (var r in requests)
            {
                bool ok = s.TryApplyPowerUp(r, o, out f);
                h = Mix(h, ok ? 1 : 0);
                h = Mix(h, (int)f);
                h = Fingerprint(h, o);
                h = Fingerprint(h, s);
            }
            var groups = new List<GroupInfo>();
            var finder = new GroupFinder();
            for (int i = 0; i < 6 && finder.FindAllGroups(s.Board, groups) > 0; i++)
            {
                s.TryPlay(groups[i % groups.Count].Anchor, o);
                h = Fingerprint(h, o);
                h = Fingerprint(h, s);
            }
            return h;
        }

        [Test, Explicit("prints the current fingerprints for re-recording")]
        public void PrintFingerprints()
        {
            System.IO.File.WriteAllText(System.Environment.GetEnvironmentVariable("OUT"),
                CampaignLevels(20) + " " + SeededPlay(20) + " " + CampaignLevels() + " " + SeededPlay() + " " + ChallengeModes() + " " + ScriptedPowerUps());
        }

        [Test]
        public void OpeningLevelsAreExactlyAsBeforeTheMechanics()
        {
            Assert.AreEqual(ExpectedOpeningLevels, CampaignLevels(20), "levels 1-20");
            Assert.AreEqual(ExpectedOpeningPlay, SeededPlay(20), "seeded games on levels 1-20");
        }

        [Test]
        public void CampaignLevelsAreGeneratedExactlyAsBefore()
        {
            Assert.AreEqual(ExpectedCampaignLevels, CampaignLevels());
        }

        [Test]
        public void SeededGamesPlayExactlyAsBefore()
        {
            Assert.AreEqual(ExpectedSeededPlay, SeededPlay());
        }

        [Test]
        public void EndlessDailyAndRushPlayExactlyAsBefore()
        {
            Assert.AreEqual(ExpectedChallengeModes, ChallengeModes());
        }

        [Test]
        public void PowerUpsBehaveExactlyAsBefore()
        {
            Assert.AreEqual(ExpectedScriptedPowerUps, ScriptedPowerUps());
        }
    }
}
