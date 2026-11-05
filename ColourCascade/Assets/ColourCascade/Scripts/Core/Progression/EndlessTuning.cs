using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Moves an Endless config saved by an older build to the current defaults (update 3.3). Unity keeps the values a
    /// config asset was created with, so a project first opened before update 2.1 still asked for 26,000 points for stage
    /// 6 and played six colours from there: its runs rarely saw a special tile. Only values that still equal an older
    /// build's defaults are moved; anything edited by hand is kept. Running it again changes nothing.
    /// </summary>
    public static class EndlessTuning
    {
        /// <summary>Before update 2.1.</summary>
        public static readonly int[] StageScoresBefore21 = { 0, 2000, 5000, 10000, 17000, 26000, 38000 };
        public const int StageStepBefore21 = 15000;
        public static readonly int[] StageColoursBefore21 = { 4, 4, 5, 5, 5, 6, 6 };

        /// <summary>Returns what was moved (config paths), or nothing.</summary>
        public static List<string> Upgrade(EndlessConfig e)
        {
            var changed = new List<string>();
            if (e == null) return changed;
            var d = new EndlessConfig();
            if (Same(e.stageScores, StageScoresBefore21))
            {
                e.stageScores = (int[])d.stageScores.Clone();
                changed.Add("endless.stageScores");
                if (e.stageScoreStepAfter == StageStepBefore21)
                {
                    e.stageScoreStepAfter = d.stageScoreStepAfter;
                    changed.Add("endless.stageScoreStepAfter");
                }
            }
            if (Same(e.stageColours, StageColoursBefore21))
            {
                e.stageColours = (int[])d.stageColours.Clone();
                changed.Add("endless.stageColours");
            }
            var j = e.journey;
            if (j == null)
            {
                e.journey = new EndlessJourneyConfig();
                changed.Add("endless.journey");
            }
            else if (IsJourneyBefore33(j))
            {
                var fresh = new EndlessJourneyConfig { breatherEvery = j.breatherEvery, breatherBonusMoves = j.breatherBonusMoves };
                e.journey = fresh;
                changed.Add("endless.journey");
            }
            return changed;
        }

        /// <summary>The journey of updates 2.1-3.2 (rockets and bombs together at stage 6). A saved one has no rocketsAt, so
        /// that field reads as its new default; the others are what tell.</summary>
        public static bool IsJourneyBefore33(EndlessJourneyConfig j)
        {
            return j.stonesAt == 2 && j.iceAt == 3 && j.chainsAt == 5 && j.specialsAt == 6 && j.treasureAt == 7 && j.mossAt == 9 && j.prismsAt == 10 &&
                   j.mixFrom == 11 && Same(j.breathers, new[] { 4, 8 });
        }

        /// <summary>The journey before update 3.3, for tests and for anyone who wants the old order back.</summary>
        public static EndlessJourneyConfig JourneyBefore33() => new EndlessJourneyConfig
        {
            rocketsAt = 0, stonesAt = 2, iceAt = 3, chainsAt = 5, specialsAt = 6, treasureAt = 7, mossAt = 9, prismsAt = 10, mixFrom = 11, breathers = new[] { 4, 8 },
        };

        static bool Same(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
