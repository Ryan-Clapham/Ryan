using System;
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

        public LevelSpec Daily(string dayKey)
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
            int reference = LevelCalibrator.MedianFullScore(spec, scoring, daily.calibrationRuns, BotKind.Mixed);
            spec.Target = NumberFormat.RoundNice((int)((long)reference * daily.targetPermille / 1000));
            spec.Star2 = Math.Max(spec.Target + 50, NumberFormat.RoundNice((int)((long)reference * daily.star2Permille / 1000)));
            spec.Star3 = Math.Max(spec.Star2 + 50, NumberFormat.RoundNice((int)((long)reference * daily.star3Permille / 1000)));
            return spec;
        }

        public LevelSpec Weekly(string weekKey)
        {
            ulong seed = StableHash.Of("weekly:" + weekKey);
            var rng = new Rng(seed);
            LevelLayout layout = rng.ChancePermille(650)
                ? LevelLayout.Open(rush.columns, rush.rows)
                : LayoutLibrary.Build(LayoutPattern.Posts, rush.columns, rush.rows, 0, rng);
            int last = rush.tierScores.Length - 1;
            return new LevelSpec
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
        }
    }
}
