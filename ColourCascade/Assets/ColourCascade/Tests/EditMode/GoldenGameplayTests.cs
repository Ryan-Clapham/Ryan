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
        // Levels 1-20 (before the first mechanic). The spare-move bonus was removed in 2.1, so its field left the level
        // fingerprint and seeded games now play every move: both were then checked against the previous build (the
        // same fingerprints computed there matched exactly: levels 1-20 are unchanged, and a level played to its last
        // move never paid a bonus). Earlier values: 6328615708927920260 / 8421993498075673067 (recorded on the
        // pre-mechanics build, with the bonus, finishing at the target).
        const ulong ExpectedOpeningLevels = 17149871321450646362UL;
        const ulong ExpectedOpeningPlay = 1635603827691466495UL;
        // Levels 1-60 and seeded games on 1-40: re-recorded for each deliberate change to levels 21 and up (phase 2;
        // cross-mode; play-on; 2.1; and 2.1's 5 x 5 bombs, groups of specials and no spare-move bonus). Earlier values:
        // pre-overhaul 16043548939747815137 / 15566285578730975039; before play-on 15906189200241180985 /
        // 5795225104261133210; before 2.1 8601877286259485673 / 14890508832734008440; 2.1 11215332883839779233 /
        // 3186517301294407716.
        const ulong ExpectedCampaignLevels = 17907502106875071358UL;
        const ulong ExpectedSeededPlay = 8694822094800872972UL;
        // Endless, Daily and Rush: re-recorded for 2.1 because Endless runs now walk the stage journey (Daily and Weekly
        // boards for a player with nothing unlocked were checked to play exactly as before); unchanged by 2.1's later
        // changes. Before 2.1: 318364672465816974.
        // Update 3.3 re-recorded it (6941777045657669987 before it) for the Endless journey's new order (rockets from
        // stage 2, bombs from 7): the Endless part changed (19659783481413691 before 3.3) and the Daily and Weekly part
        // did not, which ExpectedDailyAndRush now pins on its own.
        const ulong ExpectedChallengeModes = 7925516612437713790UL;
        const ulong ExpectedEndless = 9339972918220277480UL;
        // Daily Challenges and Weekly Rush boards for a player who has unlocked nothing: unchanged since 2.1. Update
        // 3.3's advanced challenges start at campaign level 121, so new players keep exactly these puzzles.
        const ulong ExpectedDailyAndRush = 2670500139227997825UL;
        // Scripted power-ups: re-recorded for 2.1 because a stone broken by Tile Breaker now scores 100 (before:
        // 2022348128171972134), and for 3.0.1 because the script's Extra Moves step was dropped with the retired
        // power-up (before: 13805293097089027243); the 3.0 build gives this same value with only that step removed.
        const ulong ExpectedScriptedPowerUps = 12798169050663499098UL;

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
                        // Every move played, as the game is now played (and specials left at the end go off).
                        Bots.PlayOut(s, kind, new Rng((ulong)(n * 77 + seed)));
                        h = Fingerprint(h, s);
                    }
            return h;
        }

        static ulong ChallengeModes() => DailyAndRush(Endless(47));

        static ulong Endless(ulong h)
        {
            var ctx = TestUtil.Context();
            for (int i = 0; i < 6; i++)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = (ulong)(500 + i);
                var s = new PuzzleSession(setup);
                s.Start();
                Bots.PlayOut(s, BotKind.Mixed, new Rng((ulong)(900 + i)), 400);
                h = Fingerprint(h, s);
            }
            return h;
        }

        /// <summary>Daily Challenges and Weekly Rush boards for a player who has unlocked nothing in the campaign.</summary>
        static ulong DailyAndRush(ulong h)
        {
            var ctx = TestUtil.Context();
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
                CampaignLevels(20) + " " + SeededPlay(20) + " " + CampaignLevels() + " " + SeededPlay() + " " + ChallengeModes() + " " + ScriptedPowerUps() +
                " endless " + Endless(47) + " daily+rush " + DailyAndRush(47));
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
            Assert.AreEqual(ExpectedDailyAndRush, DailyAndRush(47), "Daily and Weekly for a player with nothing unlocked");
            Assert.AreEqual(ExpectedEndless, Endless(47), "Endless runs");
            Assert.AreEqual(ExpectedChallengeModes, ChallengeModes());
        }

        [Test]
        public void PowerUpsBehaveExactlyAsBefore()
        {
            Assert.AreEqual(ExpectedScriptedPowerUps, ScriptedPowerUps());
        }
    }
}
