using System;

namespace ColourCascade.Core
{
    /// <summary>What a stage of an Endless run is for (docs/DESIGN.md §4).</summary>
    public enum EndlessStep
    {
        /// <summary>Stage 1: ordinary matching on a clean board.</summary>
        WarmUp = 0,
        /// <summary>One new thing arrives (an obstacle, or special tiles).</summary>
        Introduce = 1,
        /// <summary>An easier stage: nothing new, and a few bonus moves.</summary>
        Breather = 2,
        /// <summary>Several mechanics met so far arrive together, more of them and more densely as stages rise.</summary>
        Mix = 3,
    }

    /// <summary>The plan for one stage of an Endless run.</summary>
    public struct EndlessStagePlan
    {
        public int Stage;
        public EndlessStep Step;
        /// <summary>Introduce: what arrives. Mix: everything the wave may draw from (the run picks Kinds of them).</summary>
        public MechanicSet Mechanics;
        /// <summary>Mix: how many different mechanics the wave adds.</summary>
        public int Kinds;
        /// <summary>How much of each the wave adds, in percent of an introduction's amount.</summary>
        public int DensityPercent;
        /// <summary>The special tiles big groups make from this stage on.</summary>
        public SpecialTier Specials;
        /// <summary>Breather: moves given.</summary>
        public int BonusMoves;
        /// <summary>For the stage banner: "Frozen tiles", "Breather", "Mix".</summary>
        public string Title;
    }

    /// <summary>
    /// When things arrive in an Endless run (config: endless → journey). One new thing per stage. Update 3.3 brought
    /// rockets forward to stage 2 (they used to arrive with bombs at stage 6, which many runs never reached); the stages
    /// before update 3.3 were stones 2, ice 3, breather 4, chains 5, rockets and bombs 6, treasure 7, breather 8, moss 9,
    /// prisms 10 and mixing from 11 (EndlessTuning moves a config asset that still has those).
    /// </summary>
    [Serializable]
    public class EndlessJourneyConfig
    {
        /// <summary>Rockets: groups of 6+ make them from here.</summary>
        public int rocketsAt = 2;
        public int stonesAt = 3;
        public int iceAt = 4;
        public int chainsAt = 6;
        /// <summary>Bombs (and so combinations of specials): groups of 9+ make them from here.</summary>
        public int specialsAt = 7;
        public int treasureAt = 8;
        public int mossAt = 10;
        public int prismsAt = 11;
        /// <summary>From here every stage mixes what came before (with a breather every breatherEvery stages).</summary>
        public int mixFrom = 12;
        /// <summary>Breathers before the mixing starts.</summary>
        public int[] breathers = { 5, 9 };
        public int breatherEvery = 4;
        public int breatherBonusMoves = 5;
    }

    /// <summary>
    /// The Endless journey (docs/DESIGN.md §4): every run walks through the game's mechanics one stage at a time:
    /// a warm-up, rockets, stones, frozen tiles, a breather, chains, bombs, treasure, a breather, moss, prisms, and
    /// then stages that mix them, two, three and four at a time and more densely, with a breather every few stages.
    /// Which mechanics a mixing stage uses, and where everything lands, comes from the run's own seed, so no two runs
    /// are alike. Deterministic for a given stage.
    /// </summary>
    public static class EndlessJourney
    {
        public static EndlessStagePlan For(int stage, EndlessJourneyConfig j)
        {
            j = j ?? new EndlessJourneyConfig();
            var plan = new EndlessStagePlan { Stage = stage, DensityPercent = 100, Specials = SpecialsAt(stage, j) };
            if (stage <= 1)
            {
                plan.Step = EndlessStep.WarmUp;
                plan.Title = "Warm up";
                return plan;
            }
            bool breather = stage < j.mixFrom ? Array.IndexOf(j.breathers, stage) >= 0 : j.breatherEvery > 0 && (stage - j.mixFrom + 1) % j.breatherEvery == 0;
            if (breather)
            {
                plan.Step = EndlessStep.Breather;
                plan.BonusMoves = j.breatherBonusMoves;
                plan.Title = "Breather: +" + j.breatherBonusMoves + " moves";
                return plan;
            }
            var introduced = Introduced(stage, j);
            if (introduced != MechanicSet.None)
            {
                plan.Step = EndlessStep.Introduce;
                plan.Mechanics = introduced;
                plan.Title = TitleFor(introduced);
                return plan;
            }
            if (stage >= j.mixFrom)
            {
                int k = stage - j.mixFrom;
                plan.Step = EndlessStep.Mix;
                plan.Mechanics = MetBy(stage, j) & Mechanics.EndlessObstacles;
                plan.Kinds = Math.Min(4, 2 + k / 4);
                plan.DensityPercent = Math.Min(200, 100 + k * 8);
                plan.Title = "Mix";
                return plan;
            }
            // A stage between introductions with nothing scheduled (only with a custom config): a light mix.
            plan.Step = EndlessStep.Mix;
            plan.Mechanics = MetBy(stage, j) & Mechanics.EndlessObstacles;
            plan.Kinds = 1;
            plan.Title = "Mix";
            return plan;
        }

        /// <summary>What a stage introduces, or None.</summary>
        public static MechanicSet Introduced(int stage, EndlessJourneyConfig j)
        {
            var set = MechanicSet.None;
            if (stage == j.rocketsAt) set |= MechanicSet.Rockets;
            if (stage == j.stonesAt) set |= MechanicSet.Stones;
            if (stage == j.iceAt) set |= MechanicSet.Ice;
            if (stage == j.chainsAt) set |= MechanicSet.Chains;
            if (stage == j.specialsAt) set |= MechanicSet.Bombs | (j.rocketsAt <= 0 || j.rocketsAt >= j.specialsAt ? MechanicSet.Rockets : MechanicSet.None);
            if (stage == j.treasureAt) set |= MechanicSet.Treasure;
            if (stage == j.mossAt) set |= MechanicSet.Moss;
            if (stage == j.prismsAt) set |= MechanicSet.Prisms;
            return set;
        }

        /// <summary>Everything a run has met by this stage (thick ice comes with the mixing stages).</summary>
        public static MechanicSet MetBy(int stage, EndlessJourneyConfig j)
        {
            var set = MechanicSet.None;
            if (j.rocketsAt > 0 && stage >= j.rocketsAt) set |= MechanicSet.Rockets;
            if (stage >= j.stonesAt) set |= MechanicSet.Stones;
            if (stage >= j.iceAt) set |= MechanicSet.Ice;
            if (stage >= j.chainsAt) set |= MechanicSet.Chains;
            if (stage >= j.specialsAt) set |= MechanicSet.Rockets | MechanicSet.Bombs;
            if (stage >= j.treasureAt) set |= MechanicSet.Treasure;
            if (stage >= j.mossAt) set |= MechanicSet.Moss;
            if (stage >= j.prismsAt) set |= MechanicSet.Prisms;
            if (stage >= j.mixFrom && stage >= j.iceAt) set |= MechanicSet.ThickIce;
            return set;
        }

        public static SpecialTier SpecialsAt(int stage, EndlessJourneyConfig j)
        {
            if (stage >= j.prismsAt) return SpecialTier.Prisms;
            if (stage >= j.specialsAt) return SpecialTier.Bombs;
            if (j.rocketsAt > 0 && stage >= j.rocketsAt) return SpecialTier.Rockets;
            return SpecialTier.None;
        }

        /// <summary>The first stage after this one that introduces something, or 0 when everything has arrived.</summary>
        public static int NextIntroduction(int stage, EndlessJourneyConfig j, out MechanicSet what)
        {
            for (int s = stage + 1; s < Math.Max(stage + 1, j.mixFrom) + 1; s++)
            {
                what = Introduced(s, j);
                if (what != MechanicSet.None) return s;
            }
            what = MechanicSet.None;
            return 0;
        }

        public static string TitleFor(MechanicSet introduced)
        {
            if (introduced.Has(MechanicSet.Rockets) && introduced.Has(MechanicSet.Bombs)) return "Rockets and bombs";
            if (introduced.Has(MechanicSet.Bombs)) return "Bombs";
            if (introduced.Has(MechanicSet.Rockets)) return "Rockets";
            if (introduced.Has(MechanicSet.Prisms)) return "Prisms";
            if (introduced.Has(MechanicSet.Stones)) return "Stones";
            if (introduced.Has(MechanicSet.Ice)) return "Frozen tiles";
            if (introduced.Has(MechanicSet.Chains)) return "Chains";
            if (introduced.Has(MechanicSet.Treasure)) return "Treasure";
            if (introduced.Has(MechanicSet.Moss)) return "Moss";
            return "";
        }
    }
}
