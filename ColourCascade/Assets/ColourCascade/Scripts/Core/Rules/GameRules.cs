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
        /// <summary>Keyframes: level -> target as a fraction (permille) of the reference bot's median score.</summary>
        public int[] targetCurveLevels = { 1, 4, 7, 10, 20, 35, 50, 75, 100, 150 };
        public int[] targetCurvePermille = { 330, 420, 450, 500, 560, 620, 670, 720, 760, 800 };
        /// <summary>Star thresholds as a multiple (permille) of the target, applied to the final score.</summary>
        public int star2Permille = 1250;
        public int star3Permille = 1500;
        /// <summary>Leftover moves are converted to bonus points: each one is worth target / moves × this permille.</summary>
        public int leftoverBonusPermille = 1000;
        public int calibrationRuns = 16;
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
        /// <summary>Score needed to reach each stage (index 0 = stage 1).</summary>
        public int[] stageScores = { 0, 2000, 5000, 10000, 17000, 26000, 38000 };
        public int stageScoreStepAfter = 15000;
        /// <summary>Colours in play for each stage; the last entry applies to all later stages.</summary>
        public int[] stageColours = { 4, 4, 5, 5, 5, 6, 6 };
        /// <summary>One-time coin rewards for reaching these run scores for the first time ever.</summary>
        public int[] milestoneScores = { 2500, 5000, 10000, 20000, 40000 };
        public int[] milestoneRewards = { 10, 15, 25, 40, 60 };

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
        public int completionReward = 15;
        public int threeStarBonus = 5;
        public int calibrationRuns = 16;
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
    }

    [Serializable]
    public class EconomyConfig
    {
        public int startingCoins = 0;
        /// <summary>First-clear coins = base + step × floor((level-1)/stepEvery), capped at max.</summary>
        public int firstClearBase = 5;
        public int firstClearStep = 1;
        public int firstClearStepEvery = 10;
        public int firstClearMax = 15;
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
