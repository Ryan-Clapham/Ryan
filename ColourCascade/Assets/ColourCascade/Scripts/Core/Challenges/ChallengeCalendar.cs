using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColourCascade.Core
{
    /// <summary>
    /// Challenge rotation. All boundaries use UTC so every player shares the same puzzle at the same time
    /// (needed for any future online leaderboard):
    ///  - Daily Challenge: a new puzzle at 00:00 UTC each day. Key "yyyy-MM-dd".
    ///  - Weekly Rush: a new puzzle at 00:00 UTC each Monday. Key is the ISO-8601 week, e.g. "2026-W43".
    /// Limitation: the time comes from the device clock (see IClock), so a player who changes their clock
    /// can see other days' puzzles. Rewards for dates earlier than the newest one seen are refused.
    /// </summary>
    public static class ChallengeCalendar
    {
        public static DateTime ToUtc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime();

        public static string DayKey(DateTime utc)
        {
            return ToUtc(utc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static DateTime NextDailyReset(DateTime utc)
        {
            var d = ToUtc(utc).Date;
            return DateTime.SpecifyKind(d.AddDays(1), DateTimeKind.Utc);
        }

        /// <summary>Monday 00:00 UTC of the week containing utc.</summary>
        public static DateTime WeekStart(DateTime utc)
        {
            var d = ToUtc(utc).Date;
            int offset = ((int)d.DayOfWeek + 6) % 7;
            return DateTime.SpecifyKind(d.AddDays(-offset), DateTimeKind.Utc);
        }

        public static DateTime NextWeeklyReset(DateTime utc) => WeekStart(utc).AddDays(7);

        public static string WeekKey(DateTime utc)
        {
            int year;
            int week = IsoWeek(ToUtc(utc), out year);
            return year.ToString("0000", CultureInfo.InvariantCulture) + "-W" + week.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>ISO-8601 week number (weeks start Monday; week 1 contains the year's first Thursday).</summary>
        public static int IsoWeek(DateTime date, out int isoYear)
        {
            var d = date.Date;
            int dayOfWeek = ((int)d.DayOfWeek + 6) % 7;
            var thursday = d.AddDays(3 - dayOfWeek);
            isoYear = thursday.Year;
            return (thursday.DayOfYear - 1) / 7 + 1;
        }
    }

    /// <summary>Builds the Daily Challenge and Weekly Rush puzzles from their date keys.</summary>
    public sealed class ChallengeFactory
    {
        readonly DailyConfig daily;
        readonly RushConfig rush;
        readonly ScoringRules scoring;

        public ChallengeFactory(DailyConfig daily, RushConfig rush, ScoringRules scoring)
        {
            this.daily = daily;
            this.rush = rush;
            this.scoring = scoring;
        }

        /// <summary>Today's puzzle for a player who has not unlocked any tile mechanics (the original daily).</summary>
        public LevelSpec Daily(string dayKey) => Daily(dayKey, MechanicSet.None);

        /// <summary>
        /// The Daily Challenge for a date, with the day's tile mechanics that the player has unlocked in the campaign
        /// (ChallengeMechanics). The board, moves and refills come from the date alone; everyone with the same unlocks
        /// gets exactly the same puzzle. With mechanics, the obstacles bring goals that must be met as well as the target.
        /// </summary>
        // Generated puzzles, by period and unlocks: calibrating one plays several bot games, so it is done once.
        readonly Dictionary<string, LevelSpec> made = new Dictionary<string, LevelSpec>();

        public LevelSpec Daily(string dayKey, MechanicSet unlocked)
        {
            string key = "d:" + dayKey + ":" + (int)unlocked;
            LevelSpec cached;
            if (made.TryGetValue(key, out cached)) return cached;
            var spec = BuildDaily(dayKey, unlocked);
            made[key] = spec;
            return spec;
        }

        LevelSpec BuildDaily(string dayKey, MechanicSet unlocked)
        {
            ulong seed = StableHash.Of("daily:" + dayKey);
            var rng = new Rng(seed);
            bool large = rng.ChancePermille(700);
            int cols = large ? 8 : 7, rows = large ? 9 : 7;
            int colours = rng.ChancePermille(550) ? 5 : 4;
            LevelLayout layout = rng.ChancePermille(500)
                ? LevelLayout.Open(cols, rows)
                : LayoutLibrary.Build(LayoutLibrary.StonePatterns[rng.Next(LayoutLibrary.StonePatterns.Length)], cols, rows, 1, rng);
            var spec = new LevelSpec
            {
                Id = "daily:" + dayKey,
                Mode = GameMode.Daily,
                Title = "Daily Challenge",
                Layout = layout,
                Colours = colours,
                AssistPermille = 40,
                Moves = Math.Max(5, daily.moves),
                Seed = seed,
                FixedSeed = true,
                Note = daily.moves + " moves. Score as much as you can!",
            };
            var set = ChallengeMechanics.DailyFor(dayKey, unlocked);
            List<GoalSpec> goals = null;
            if (set != MechanicSet.None)
            {
                spec.Specials = Mechanics.Tier(set);
                goals = DecorateDaily(spec, set, dayKey, colours);
            }
            int reference = LevelCalibrator.MedianFullScore(spec, scoring, daily.calibrationRuns, BotKind.Mixed);
            // A day with goals asks for more than points, so its target is set a little lower (config: daily).
            bool hasGoals = goals != null && goals.Count > 0;
            int targetPermille = hasGoals ? Math.Max(100, daily.targetPermille - daily.goalTargetEasingPermille) : daily.targetPermille;
            spec.Target = NumberFormat.RoundNice((int)((long)reference * targetPermille / 1000));
            spec.Star2 = Math.Max(spec.Target + 50, NumberFormat.RoundNice((int)((long)reference * daily.star2Permille / 1000)));
            spec.Star3 = Math.Max(spec.Star2 + 50, NumberFormat.RoundNice((int)((long)reference * daily.star3Permille / 1000)));
            if (hasGoals)
            {
                // The target is a goal like the others; the daily is complete when all of them are met.
                goals.Insert(0, GoalSpec.Score(spec.Target));
                spec.Goals = goals.ToArray();
                spec.Note = daily.moves + " moves. Reach the target and complete every goal!";
            }
            return spec;
        }

        /// <summary>
        /// Adds the day's obstacles to the board, scaled down (70%, then 50%) if a casual player could not usually
        /// finish them within the day's moves. Returns their goals.
        /// </summary>
        List<GoalSpec> DecorateDaily(LevelSpec spec, MechanicSet set, string dayKey, int colours)
        {
            var baseLayout = spec.Layout;
            List<GoalSpec> chosen = null;
            foreach (int pct in new[] { 100, 70, 50 })
            {
                var layout = baseLayout.Clone();
                var goals = ChallengeMechanics.Decorate(layout, set, false, colours, new Rng(StableHash.Of("daily-board:" + dayKey)), pct);
                string error;
                if (goals.Count == 0 || !layout.Validate(out error)) continue;
                spec.Layout = layout;
                spec.Goals = goals.ToArray();
                int moss = layout.ObstacleCount(ObstacleKind.Moss);
                spec.MossCap = moss > 0 ? moss + 2 : 0;
                chosen = goals;
                int needed = LevelCalibrator.MedianMovesNeeded(spec, scoring, 8, spec.Moves * 2, BotKind.Casual);
                if (needed * 10 <= spec.Moves * 9) break;
            }
            spec.Goals = null;
            if (chosen == null) spec.Layout = baseLayout;
            return chosen;
        }

        /// <summary>This week's board for a player who has not unlocked any tile mechanics (the original Weekly Rush).</summary>
        public LevelSpec Weekly(string weekKey) => Weekly(weekKey, MechanicSet.None);

        /// <summary>
        /// The Weekly Rush board for a week: a theme built around one mechanic plus more of the player's unlocked ones
        /// (ChallengeMechanics.WeeklyFor). Rush is scored only, so obstacles bring points rather than goals; the medal
        /// scores are adjusted by how much the mechanics change a typical score, so medals stay as hard to earn.
        /// </summary>
        public LevelSpec Weekly(string weekKey, MechanicSet unlocked)
        {
            string key = "w:" + weekKey + ":" + (int)unlocked;
            LevelSpec cached;
            if (made.TryGetValue(key, out cached)) return cached;
            var spec = BuildWeekly(weekKey, unlocked);
            made[key] = spec;
            return spec;
        }

        LevelSpec BuildWeekly(string weekKey, MechanicSet unlocked)
        {
            ulong seed = StableHash.Of("weekly:" + weekKey);
            var rng = new Rng(seed);
            LevelLayout layout = rng.ChancePermille(650)
                ? LevelLayout.Open(rush.columns, rush.rows)
                : LayoutLibrary.Build(LayoutPattern.Posts, rush.columns, rush.rows, 0, rng);
            int last = rush.tierScores.Length - 1;
            var spec = new LevelSpec
            {
                Id = "weekly:" + weekKey,
                Mode = GameMode.WeeklyRush,
                Title = "Weekly Rush",
                Layout = layout,
                Colours = Math.Max(2, Math.Min(TileColours.Max, rush.colours)),
                AssistPermille = 40,
                Moves = 0,
                TimeLimitSeconds = Math.Max(10, rush.seconds),
                Seed = seed,
                FixedSeed = true,
                Target = rush.tierScores.Length > 0 ? rush.tierScores[0] : 0,
                Star2 = rush.tierScores.Length > 1 ? rush.tierScores[1] : 0,
                Star3 = last >= 2 ? rush.tierScores[2] : 0,
                Note = rush.seconds + " seconds. Clear as fast as you can!",
            };
            MechanicSet theme;
            var set = ChallengeMechanics.WeeklyFor(weekKey, unlocked, out theme);
            if (set == MechanicSet.None) return spec;

            var plain = LevelCalibrator.MedianRushScore(spec, scoring, 8, RushProbeMoves);
            var board = spec.Layout.Clone();
            var drng = new Rng(StableHash.Of("weekly-board:" + weekKey));
            ChallengeMechanics.Decorate(board, set, true, spec.Colours, drng);
            ChallengeMechanics.PlaceThemeSpecials(board, theme, drng);
            string error;
            if (!board.Validate(out error)) return spec;
            spec.Layout = board;
            spec.Specials = Mechanics.Tier(set);
            int moss = board.ObstacleCount(ObstacleKind.Moss);
            spec.MossCap = moss > 0 ? moss + 2 : 0;
            var with = LevelCalibrator.MedianRushScore(spec, scoring, 8, RushProbeMoves);
            // Medal scores move with the board: the same effort earns the same medal (within 0.7× to 1.8×).
            long ratio = plain > 0 ? Math.Max(700, Math.Min(1800, (long)with * 1000 / plain)) : 1000;
            spec.Target = ScaleTier(spec.Target, ratio);
            spec.Star2 = Math.Max(spec.Target + 50, ScaleTier(spec.Star2, ratio));
            spec.Star3 = Math.Max(spec.Star2 + 50, ScaleTier(spec.Star3, ratio));
            string name = ChallengeMechanics.ThemeName(theme);
            spec.Note = (name.Length > 0 ? name + ": " : "") + rush.seconds + " seconds. Clear as fast as you can!";
            return spec;
        }

        /// <summary>Moves a bot makes to stand in for a Weekly Rush run (about one move every 1.3 seconds).</summary>
        public const int RushProbeMoves = 45;

        static int ScaleTier(int tier, long ratioPermille) => tier <= 0 ? 0 : NumberFormat.RoundNice((int)(tier * ratioPermille / 1000));
    }
}
