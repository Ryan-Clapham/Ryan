using System;

namespace ColourCascade.Core
{
    public enum GameMode
    {
        Campaign = 0,
        Endless = 1,
        Daily = 2,
        WeeklyRush = 3,
        /// <summary>Sandbox boards used by How to Play. Never ends, never rewards.</summary>
        Practice = 4,
    }

    [Flags]
    public enum ModeMask
    {
        None = 0,
        Campaign = 1 << 0,
        Endless = 1 << 1,
        Daily = 1 << 2,
        WeeklyRush = 1 << 3,
        Practice = 1 << 4,
    }

    public static class ModeMaskExt
    {
        public static ModeMask ToMask(this GameMode mode) => (ModeMask)(1 << (int)mode);
        public static bool Has(this ModeMask mask, GameMode mode) => (mask & mode.ToMask()) != 0;
    }

    public enum PraiseTier
    {
        None = 0,
        Good = 1,
        Great = 2,
        Amazing = 3,
        Cascade = 4,
    }

    /// <summary>Scoring formula. Base rule: points = groupSize ^ exponent × pointsPerTile (2 tiles = 40, 3 = 90 ...).</summary>
    [Serializable]
    public class ScoringRules
    {
        public int pointsPerTile = 10;
        public int exponent = 2;
        /// <summary>Flat points per colour tile removed by a power-up (power-ups are not "groups").</summary>
        public int powerUpPointsPerTile = 20;
        /// <summary>Points for each layer of ice removed (docs/MECHANICS.md §5).</summary>
        public int iceLayerPoints = 100;
        /// <summary>Points for each chain link removed.</summary>
        public int chainLinkPoints = 150;
        /// <summary>Points for each mossy tile cleared (on top of the tile's own points).</summary>
        public int mossPoints = 100;
        /// <summary>Points for each treasure collected.</summary>
        public int treasurePoints = 500;
        /// <summary>Points for each stone destroyed (by a rocket, a bomb or Tile Breaker; docs/MECHANICS.md §2.6).</summary>
        public int stonePoints = 100;
        /// <summary>Group sizes that earn special tiles, and the points specials score (campaign only).</summary>
        public SpecialRules specials = new SpecialRules();
        public int praiseGoodSize = 5;
        public int praiseGreatSize = 8;
        public int praiseAmazingSize = 12;
        public int praiseCascadeSize = 16;

        public int GroupPoints(int groupSize)
        {
            if (groupSize < 2) return 0;
            long p = pointsPerTile;
            for (int i = 0; i < exponent; i++) p *= groupSize;
            return p > int.MaxValue ? int.MaxValue : (int)p;
        }

        public int PowerUpPoints(int tilesRemoved)
        {
            return tilesRemoved <= 0 ? 0 : tilesRemoved * powerUpPointsPerTile;
        }

        /// <summary>Points for one step of strength taken off an obstacle.</summary>
        public int ObstacleStepPoints(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Ice: return iceLayerPoints;
                case ObstacleKind.Chain: return chainLinkPoints;
                case ObstacleKind.Moss: return mossPoints;
                default: return 0;
            }
        }

        public PraiseTier PraiseFor(int groupSize)
        {
            if (groupSize >= praiseCascadeSize) return PraiseTier.Cascade;
            if (groupSize >= praiseAmazingSize) return PraiseTier.Amazing;
            if (groupSize >= praiseGreatSize) return PraiseTier.Great;
            if (groupSize >= praiseGoodSize) return PraiseTier.Good;
            return PraiseTier.None;
        }
    }

    [Serializable]
    public class BoardSizeStep
    {
        public int fromLevel = 1;
        public int columns = 6;
        public int rows = 6;
    }

    /// <summary>A hand-authored campaign level. Zero values mean "use the generated value".</summary>
    [Serializable]
    public class LevelOverride
    {
        public int level = 1;
        public int columns;
        public int rows;
        public int colours;
        public int moves;
        public int target;
        public int star2;
        public int star3;
        public int assistPermille = -1;
        /// <summary>Rows top first: '.' random, '#' stone, 'x' void, R/Y/B/G/P/O fixed colour. Empty = open board.</summary>
        public string[] layout = new string[0];
        /// <summary>Optional obstacle rows, same size as the layout: '.' none, 'i'/'I' ice 1/2 layers, '1'-'3' chain links.</summary>
        public string[] obstacles = new string[0];
        /// <summary>Optional goals. Empty = a single score goal equal to the target.</summary>
        public GoalSpec[] goals = new GoalSpec[0];
        /// <summary>Special tiles big groups may create here: -1 = as the campaign schedule says, 0 none, 1 rockets, 2 +bombs, 3 +prisms.</summary>
        public int specials = -1;
        /// <summary>Most mossy tiles the board may hold (0 = the starting moss plus two).</summary>
        public int mossCap;
        /// <summary>Tutorial hint shown when the level starts (see TutorialKeys).</summary>
        public string tutorial = "";
        /// <summary>Short line shown on the level card, e.g. "New: stone barriers".</summary>
        public string note = "";
    }

    [Serializable]
    public class CampaignConfig
    {
        public int levelsPerRegion = 20;
        public int milestoneEvery = 10;
        public BoardSizeStep[] boardSizes =
        {
            new BoardSizeStep { fromLevel = 1, columns = 6, rows = 6 },
            new BoardSizeStep { fromLevel = 4, columns = 7, rows = 7 },
            new BoardSizeStep { fromLevel = 7, columns = 8, rows = 9 },
        };
        /// <summary>First level that can contain stone barriers in generated levels.</summary>
        public int stonesFromLevel = 8;
        /// <summary>First level that can contain shaped (void) boards in generated levels.</summary>
        public int shapedBoardsFromLevel = 25;
        /// <summary>
        /// First level of the progression (update 3.2, docs/DESIGN.md §3.5): from here generated levels get their
        /// difficulty from CampaignProgression, are played on the board shapes below, and have moves sized from a
        /// thoughtful player. Earlier levels are generated exactly as before.
        /// </summary>
        public int progressionFromLevel = 121;
        /// <summary>When each board shape joins the campaign (from progressionFromLevel on).</summary>
        public BoardShapeSchedule shapes = new BoardShapeSchedule();
        /// <summary>Keyframes: level -> target as a fraction (permille) of the reference bot's median score.</summary>
        public int[] targetCurveLevels = { 1, 4, 7, 10, 20, 35, 50, 75, 100, 150 };
        public int[] targetCurvePermille = { 330, 420, 450, 500, 560, 620, 670, 720, 760, 800 };
        /// <summary>Opening score levels (before the first mechanic): star thresholds as a multiple (permille) of the target.</summary>
        public int star2Permille = 1250;
        public int star3Permille = 1500;
        /// <summary>
        /// Calibrated stars, every level from the first mechanic on (docs/DESIGN.md §3.2). Bots play every move, as a
        /// player chasing stars would. Two stars sit halfway between a casual player's median final score and this
        /// percentile of a thoughtful player's (Planner bot); three stars at star3Percentile of the thoughtful player's.
        /// </summary>
        public int star2Percentile = 35;
        public int star3Percentile = 60;
        /// <summary>"...and reach a score" goals: this percentile of casual players' final scores among those who meet the other goals.</summary>
        public int scoreGoalPercentile = 20;
        public int calibrationRuns = 16;
        /// <summary>Bot games used to size a goal level's moves and stars (more is steadier but slower to generate).</summary>
        public int goalCalibrationRuns = 10;
        /// <summary>When each mechanic joins the campaign. Its introduction levels are built in (CampaignDesign).</summary>
        public MechanicSchedule mechanics = new MechanicSchedule();
        public LevelOverride[] overrides = new LevelOverride[0];
    }

    [Serializable]
    public class EndlessConfig
    {
        public int columns = 8;
        public int rows = 9;
        public int startMoves = 30;
        public int assistPermille = 40;
        /// <summary>Clearing a group at least this big earns the matching number of bonus moves.</summary>
        public int[] bonusGroupSizes = { 6, 9, 13 };
        public int[] bonusMoves = { 1, 2, 3 };
        /// <summary>
        /// Score needed to reach each stage (index 0 = stage 1). The gaps widen as stages rise. Tuned so a relaxed
        /// player's run reaches about stage 6 and a strong one 10-12, through the journey's introductions
        /// (docs/DESIGN.md §4; it was 2,000 / 5,000 / 10,000 / 17,000 / 26,000 / 38,000, +15,000, when runs stopped
        /// around stage 4-5).
        /// </summary>
        public int[] stageScores = { 0, 1200, 2800, 4800, 7200, 10000, 13500, 17500, 22000, 27000, 32500 };
        public int stageScoreStepAfter = 6000;
        /// <summary>Colours in play for each stage; the last entry applies to all later stages.</summary>
        /// <summary>Colours stay at five while the journey introduces things (stages 4-11); six from stage 12.</summary>
        public int[] stageColours = { 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 6 };
        /// <summary>What each stage of a run brings: one new mechanic at a time, breathers, then mixes (docs/DESIGN.md §4).</summary>
        public EndlessJourneyConfig journey = new EndlessJourneyConfig();
        /// <summary>Bonus moves for each treasure brought down in Endless.</summary>
        public int treasureBonusMoves = 3;
        /// <summary>
        /// Board shapes (update 3.3): once the player's best run has reached these stages, a run may start on a shaped
        /// board instead of the plain one: tall, wide, hollow or cut corners; then also twin towers, upper and lower,
        /// lopsided and stone walls; then also narrow passages, T, cross and diamond shapes.
        /// </summary>
        public int shapedRunsFromBestStage = 6;
        public int splitRunsFromBestStage = 9;
        public int advancedRunsFromBestStage = 12;
        /// <summary>Runs (permille) that stay on the plain board even then.</summary>
        public int plainRunPermille = 350;
        /// <summary>One-time coin rewards for reaching these run scores for the first time ever.</summary>
        public int[] milestoneScores = { 2500, 5000, 10000, 20000, 40000 };
        public int[] milestoneRewards = { 5, 10, 15, 25, 40 };

        public int BonusMovesFor(int groupSize)
        {
            int bonus = 0;
            for (int i = 0; i < bonusGroupSizes.Length && i < bonusMoves.Length; i++)
                if (groupSize >= bonusGroupSizes[i]) bonus = bonusMoves[i];
            return bonus;
        }

        public int ScoreForStage(int stage)
        {
            if (stage <= 1) return 0;
            int idx = stage - 1;
            if (idx < stageScores.Length) return stageScores[idx];
            int last = stageScores.Length > 0 ? stageScores[stageScores.Length - 1] : 0;
            return last + (idx - (stageScores.Length - 1)) * stageScoreStepAfter;
        }

        public int StageForScore(int score)
        {
            int stage = 1;
            while (stage < 999 && score >= ScoreForStage(stage + 1)) stage++;
            return stage;
        }

        public int ColoursForStage(int stage)
        {
            if (stageColours.Length == 0) return 5;
            int idx = Math.Min(Math.Max(stage - 1, 0), stageColours.Length - 1);
            return stageColours[idx];
        }
    }

    [Serializable]
    public class DailyConfig
    {
        public int moves = 20;
        public int targetPermille = 800;
        public int star2Permille = 1050;
        public int star3Permille = 1250;
        public int completionReward = 10;
        public int threeStarBonus = 5;
        public int calibrationRuns = 16;
        /// <summary>On days with goals (tile mechanics), the target is this much lower (permille of the reference score).</summary>
        public int goalTargetEasingPermille = 120;
        /// <summary>
        /// Update 3.3: from this campaign level the Daily Challenge is an advanced puzzle (docs/DESIGN.md §5): a board
        /// shape the player has met, two or three objectives with the target, and moves sized from a thoughtful player
        /// (from `moves` up to advancedMaxMoves). 0 keeps every player on the original daily.
        /// </summary>
        public int advancedFromLevel = 121;
        public int advancedMaxMoves = 30;
    }

    [Serializable]
    public class RushConfig
    {
        public int columns = 8;
        public int rows = 9;
        public int colours = 5;
        public int seconds = 60;
        public int countdownSeconds = 3;
        public string[] tierNames = { "Bronze", "Silver", "Gold" };
        public int[] tierScores = { 2500, 5000, 8000 };
        public int[] tierRewards = { 10, 15, 25 };
        /// <summary>
        /// Update 3.3: from this campaign level the Weekly Rush board is an advanced one (docs/DESIGN.md §6): split and
        /// irregular shapes, the theme with two more mechanics, and objectives that each add objectiveBonus points once
        /// completed (medals stay score-based). 0 keeps every player on the original board.
        /// </summary>
        public int advancedFromLevel = 121;
        public int objectiveBonus = 1000;
    }

    [Serializable]
    public class EconomyConfig
    {
        public int startingCoins = 0;
        /// <summary>
        /// First-clear coins = base + step × floor((level-1)/stepEvery), capped at max. Update 3.1: 3 coins on levels
        /// 1-25, +1 every 25 levels, 8 from level 126 (3, 4, 5, 6, 7, then 8).
        /// </summary>
        public int firstClearBase = 3;
        public int firstClearStep = 1;
        public int firstClearStepEvery = 25;
        public int firstClearMax = 8;
        /// <summary>
        /// Coins for each star the first time it is earned on a level: the first, second and third star (update 3.1:
        /// 2, 3 and 5, so 10 for three stars). Each star pays once (claim key c.star.LEVEL.STAR).
        /// </summary>
        public int[] starRewards = { 2, 3, 5 };
        /// <summary>Older configs: one value for every star. Used only when starRewards is empty.</summary>
        public int coinsPerNewStar = 1;
        public int milestoneReward = 20;
        public int regionFinaleReward = 40;
        public int ledgerCapacity = 200;

        public int FirstClearReward(int level)
        {
            if (level < 1) level = 1;
            int every = Math.Max(1, firstClearStepEvery);
            int value = firstClearBase + firstClearStep * ((level - 1) / every);
            return Math.Min(value, firstClearMax);
        }

        /// <summary>Coins for earning star 1, 2 or 3 of a level for the first time.</summary>
        public int StarReward(int star)
        {
            if (star < 1) return 0;
            if (starRewards != null && starRewards.Length > 0) return Math.Max(0, starRewards[Math.Min(star, starRewards.Length) - 1]);
            return Math.Max(0, coinsPerNewStar);
        }
    }

    public static class StarRating
    {
        public static int Compute(int finalScore, int target, int star2, int star3)
        {
            if (target <= 0) return 0;
            if (finalScore >= star3 && star3 > 0) return 3;
            if (finalScore >= star2 && star2 > 0) return 2;
            if (finalScore >= target) return 1;
            return 0;
        }
    }
}
