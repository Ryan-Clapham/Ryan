using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum AchievementCategory
    {
        Campaign,
        Stars,
        Regions,
        Endless,
        Skill,
        Mechanics,
        Challenges,
    }

    public enum AchievementTier
    {
        Easy,
        Medium,
        Hard,
        VeryHard,
    }

    /// <summary>
    /// What an achievement measures. Each is read from data the game already keeps (campaign progress, stars, the
    /// Endless record, lifetime statistics, claimed rewards), so achievements never keep a second copy of progress.
    /// </summary>
    public enum AchievementStat
    {
        /// <summary>Highest campaign level unlocked ("reach level N").</summary>
        LevelReached,
        /// <summary>Stars across the campaign (each level's best, so replays never count twice).</summary>
        TotalStars,
        /// <summary>Regions in which every level has the maximum stars.</summary>
        PerfectRegions,
        EndlessBestScore,
        EndlessBestStage,
        BiggestGroup,
        MostTilesInOneMove,
        BiggestCombo,
        /// <summary>Best final score on any campaign level.</summary>
        BestLevelScore,
        /// <summary>Distinct campaign levels from level `param` won with three stars and no power-up in that attempt.</summary>
        PureThreeStarLevels,
        StonesBroken,
        IceLayersRemoved,
        ChainsReleased,
        TreasuresCollected,
        MossRemoved,
        TilesCleared,
        /// <summary>Uses of every current power-up, each counted up to `param` (target = param × number of power-ups).</summary>
        PowerUpMastery,
        DailyChallengesCompleted,
        /// <summary>Best Weekly Rush medal ever: 1 bronze, 2 silver, 3 gold.</summary>
        WeeklyBestMedal,
    }

    [Serializable]
    public sealed class PowerUpGrant
    {
        public PowerUpId id;
        public int count = 1;
    }

    [Serializable]
    public sealed class AchievementReward
    {
        public int coins;
        public PowerUpGrant[] powerUps = new PowerUpGrant[0];

        public bool IsEmpty
        {
            get
            {
                if (coins > 0) return false;
                if (powerUps != null)
                    foreach (var p in powerUps)
                        if (p != null && p.count > 0) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// One achievement, as data (docs/ACHIEVEMENTS.md). The catalog is a fixed list, in the game config: new
    /// achievements are added there, by hand, never generated from levels or regions.
    /// </summary>
    [Serializable]
    public sealed class AchievementDefinition
    {
        /// <summary>Stable ID, saved with the player's progress: never rename one that has shipped.</summary>
        public string id = "";
        public string title = "";
        public string description = "";
        public AchievementCategory category;
        public AchievementTier tier;
        public AchievementStat stat;
        public long target = 1;
        /// <summary>Extra setting for some stats (PureThreeStarLevels: first level that counts; PowerUpMastery: uses each).</summary>
        public int param;
        public AchievementReward reward = new AchievementReward();
    }

    /// <summary>The fixed achievement list: the defaults below, or the config asset's (edited) copy.</summary>
    public sealed class AchievementCatalog
    {
        readonly List<AchievementDefinition> all = new List<AchievementDefinition>();
        readonly Dictionary<string, AchievementDefinition> byId = new Dictionary<string, AchievementDefinition>();

        /// <summary>
        /// Takes the definitions in order, skipping unusable ones (no ID, a repeated ID, a target below 1). The
        /// definitions are not modified (in Unity they belong to the config asset); reward power-ups the game no
        /// longer has are skipped when a reward is paid (AchievementService).
        /// </summary>
        public AchievementCatalog(IEnumerable<AchievementDefinition> definitions)
        {
            if (definitions == null) return;
            foreach (var d in definitions)
            {
                if (d == null || string.IsNullOrEmpty(d.id) || byId.ContainsKey(d.id) || d.target < 1) continue;
                byId[d.id] = d;
                all.Add(d);
            }
        }

        public IReadOnlyList<AchievementDefinition> All => all;
        public int Count => all.Count;

        public AchievementDefinition Get(string id)
        {
            AchievementDefinition d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        static AchievementDefinition A(string id, AchievementCategory cat, AchievementTier tier, AchievementStat stat, long target,
            string title, string description, int coins, params (PowerUpId id, int count)[] items)
        {
            var grants = new PowerUpGrant[items.Length];
            for (int i = 0; i < items.Length; i++) grants[i] = new PowerUpGrant { id = items[i].id, count = items[i].count };
            return new AchievementDefinition
            {
                id = id, category = cat, tier = tier, stat = stat, target = target, title = title, description = description,
                reward = new AchievementReward { coins = coins, powerUps = grants },
            };
        }

        static (PowerUpId, int)[] Each(int n) => new[]
        {
            (PowerUpId.Blast, n), (PowerUpId.TileBreaker, n), (PowerUpId.Swap, n), (PowerUpId.ColourShift, n), (PowerUpId.ColourBurst, n),
        };

        static (PowerUpId, int)[] Plus((PowerUpId, int)[] a, params (PowerUpId, int)[] b)
        {
            var r = new (PowerUpId, int)[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        /// <summary>
        /// The 67 achievements of the achievements update, with thresholds measured with the campaign and Endless bots
        /// and rewards weighed against play income (docs/ACHIEVEMENTS.md has the reasoning).
        /// </summary>
        public static AchievementDefinition[] CreateDefaults()
        {
            const AchievementCategory Cam = AchievementCategory.Campaign, Sta = AchievementCategory.Stars, Reg = AchievementCategory.Regions,
                End = AchievementCategory.Endless, Ski = AchievementCategory.Skill, Mec = AchievementCategory.Mechanics, Cha = AchievementCategory.Challenges;
            const AchievementTier E = AchievementTier.Easy, M = AchievementTier.Medium, H = AchievementTier.Hard, V = AchievementTier.VeryHard;
            const PowerUpId B = PowerUpId.Blast, T = PowerUpId.TileBreaker, S = PowerUpId.Swap, C = PowerUpId.ColourShift, X = PowerUpId.ColourBurst;
            var list = new List<AchievementDefinition>
            {
                // A. Campaign progression ("reach" = the level is unlocked, by clearing the one before).
                A("level_10", Cam, E, AchievementStat.LevelReached, 10, "First Steps", "Reach level 10.", 25),
                A("level_25", Cam, E, AchievementStat.LevelReached, 25, "On Your Way", "Reach level 25.", 50),
                A("level_50", Cam, M, AchievementStat.LevelReached, 50, "Halfway to a Hundred", "Reach level 50.", 75, (B, 1)),
                A("level_100", Cam, M, AchievementStat.LevelReached, 100, "Century", "Reach level 100.", 125, (T, 1)),
                A("level_150", Cam, H, AchievementStat.LevelReached, 150, "Seasoned Cascader", "Reach level 150.", 250, (C, 1), (B, 1)),
                A("level_200", Cam, H, AchievementStat.LevelReached, 200, "Double Century", "Reach level 200.", 400, (X, 1), (T, 1)),
                A("level_300", Cam, V, AchievementStat.LevelReached, 300, "Trailblazer", "Reach level 300.", 600, Each(1)),
                A("level_400", Cam, V, AchievementStat.LevelReached, 400, "Unstoppable", "Reach level 400.", 900, (X, 2), (C, 2)),
                A("level_500", Cam, V, AchievementStat.LevelReached, 500, "Cascade Legend", "Reach level 500.", 1500, Each(2)),

                // B. Star collection (each level's best stars: replays never count twice).
                A("stars_30", Sta, E, AchievementStat.TotalStars, 30, "Starry Start", "Collect 30 stars in the campaign.", 30),
                A("stars_100", Sta, M, AchievementStat.TotalStars, 100, "Star Collector", "Collect 100 stars in the campaign.", 60, (S, 1)),
                A("stars_250", Sta, M, AchievementStat.TotalStars, 250, "Shining Bright", "Collect 250 stars in the campaign.", 150, (B, 1)),
                A("stars_500", Sta, H, AchievementStat.TotalStars, 500, "Constellation", "Collect 500 stars in the campaign.", 300, (C, 1), (T, 1)),
                A("stars_750", Sta, H, AchievementStat.TotalStars, 750, "Galaxy", "Collect 750 stars in the campaign.", 450, (X, 1), (B, 1)),
                A("stars_1000", Sta, V, AchievementStat.TotalStars, 1000, "A Thousand Stars", "Collect 1,000 stars in the campaign.", 800, Each(1)),
                A("stars_1250", Sta, V, AchievementStat.TotalStars, 1250, "Supernova", "Collect 1,250 stars in the campaign.", 1200, Plus(Each(1), (X, 1))),

                // C. Perfect regions (every level of a region at the maximum of three stars).
                A("regions_1", Reg, M, AchievementStat.PerfectRegions, 1, "Perfectionist", "Earn three stars on every level of a region.", 100, (S, 1)),
                A("regions_3", Reg, H, AchievementStat.PerfectRegions, 3, "Triple Perfect", "Perfect 3 regions: three stars on every level.", 300, (C, 1), (S, 1)),
                A("regions_5", Reg, H, AchievementStat.PerfectRegions, 5, "Flawless Five", "Perfect 5 regions: three stars on every level.", 500, (X, 1), (T, 1)),
                A("regions_10", Reg, V, AchievementStat.PerfectRegions, 10, "Perfect Ten", "Perfect 10 regions: three stars on every level.", 1000, Each(1)),
                A("regions_15", Reg, V, AchievementStat.PerfectRegions, 15, "Master of the Map", "Perfect 15 regions: three stars on every level.", 2000, Each(2)),

                // D. Endless (a finished run's score and stage).
                A("endless_5000", End, E, AchievementStat.EndlessBestScore, 5000, "Endless Explorer", "Score 5,000 in one Endless run.", 40),
                A("endless_10000", End, M, AchievementStat.EndlessBestScore, 10000, "Going the Distance", "Score 10,000 in one Endless run.", 75, (B, 1)),
                A("endless_20000", End, H, AchievementStat.EndlessBestScore, 20000, "Long Haul", "Score 20,000 in one Endless run.", 250, (C, 1)),
                A("endless_35000", End, V, AchievementStat.EndlessBestScore, 35000, "Marathon", "Score 35,000 in one Endless run.", 500, (X, 1), (T, 1)),
                A("endless_50000", End, V, AchievementStat.EndlessBestScore, 50000, "Infinite Cascade", "Score 50,000 in one Endless run.", 1000, Each(1)),
                A("endless_stage_6", End, E, AchievementStat.EndlessBestStage, 6, "Rockets Away", "Reach stage 6 of an Endless run.", 0, (S, 1)),
                A("endless_stage_12", End, H, AchievementStat.EndlessBestStage, 12, "Deep Run", "Reach stage 12 of an Endless run.", 300, (X, 1)),

                // E. Skill.
                A("group_12", Ski, E, AchievementStat.BiggestGroup, 12, "Big Group", "Clear a group of 12 tiles.", 30),
                A("group_18", Ski, M, AchievementStat.BiggestGroup, 18, "Huge Group", "Clear a group of 18 tiles.", 0, (S, 1), (C, 1)),
                A("group_25", Ski, V, AchievementStat.BiggestGroup, 25, "Colossal Group", "Clear a group of 25 tiles.", 600, (C, 2), (S, 2)),
                A("move_30", Ski, M, AchievementStat.MostTilesInOneMove, 30, "Clean Sweep", "Clear 30 tiles with a single tap.", 75, (B, 1)),
                A("move_45", Ski, H, AchievementStat.MostTilesInOneMove, 45, "Avalanche", "Clear 45 tiles with a single tap.", 250, (X, 1)),
                A("move_60", Ski, V, AchievementStat.MostTilesInOneMove, 60, "Board Wipe", "Clear 60 tiles with a single tap.", 600, (X, 2)),
                A("combo_2", Ski, E, AchievementStat.BiggestCombo, 2, "Double Trouble", "Fire two special tiles together.", 30),
                A("combo_3", Ski, M, AchievementStat.BiggestCombo, 3, "Triple Threat", "Fire three special tiles together in one combination.", 75, (T, 1)),
                A("combo_4", Ski, H, AchievementStat.BiggestCombo, 4, "Chain Reactor", "Fire four special tiles together in one combination.", 300, (B, 2)),
                A("combo_5", Ski, V, AchievementStat.BiggestCombo, 5, "Grand Combo", "Fire five special tiles together in one combination.", 700, Each(1)),
                A("score_15000", Ski, M, AchievementStat.BestLevelScore, 15000, "High Scorer", "Finish a campaign level with 15,000 points.", 100),
                A("score_30000", Ski, H, AchievementStat.BestLevelScore, 30000, "Score Master", "Finish a campaign level with 30,000 points.", 300, (X, 1)),
                A("score_45000", Ski, V, AchievementStat.BestLevelScore, 45000, "Score Legend", "Finish a campaign level with 45,000 points.", 700, (X, 2), (C, 1)),
                Pure(A("pure_10", Ski, M, AchievementStat.PureThreeStarLevels, 10, "Pure Skill", "Three-star 10 different levels from level 50 on without using a power-up.", 150)),
                Pure(A("pure_50", Ski, H, AchievementStat.PureThreeStarLevels, 50, "Purist", "Three-star 50 different levels from level 50 on without using a power-up.", 450)),

                // E. Mechanics (lifetime totals, all modes).
                A("stones_25", Mec, E, AchievementStat.StonesBroken, 25, "Stone Breaker", "Break 25 stones.", 30),
                A("stones_200", Mec, M, AchievementStat.StonesBroken, 200, "Rock Crusher", "Break 200 stones.", 0, (T, 2)),
                A("stones_600", Mec, H, AchievementStat.StonesBroken, 600, "Quarry Master", "Break 600 stones.", 300, (T, 2)),
                A("ice_50", Mec, E, AchievementStat.IceLayersRemoved, 50, "Thaw", "Remove 50 layers of ice.", 30),
                A("ice_400", Mec, M, AchievementStat.IceLayersRemoved, 400, "Icebreaker", "Remove 400 layers of ice.", 100, (B, 1)),
                A("ice_1200", Mec, H, AchievementStat.IceLayersRemoved, 1200, "The Big Melt", "Remove 1,200 layers of ice.", 350, (B, 2)),
                A("chains_25", Mec, E, AchievementStat.ChainsReleased, 25, "Unchained", "Free 25 chained tiles.", 40),
                A("chains_150", Mec, M, AchievementStat.ChainsReleased, 150, "Lock Picker", "Free 150 chained tiles.", 50, (C, 1)),
                A("chains_400", Mec, H, AchievementStat.ChainsReleased, 400, "Escape Artist", "Free 400 chained tiles.", 350, (C, 2)),
                A("treasure_5", Mec, E, AchievementStat.TreasuresCollected, 5, "Treasure Hunter", "Collect 5 treasures.", 40),
                A("treasure_40", Mec, M, AchievementStat.TreasuresCollected, 40, "Treasure Trove", "Collect 40 treasures.", 100, (T, 1)),
                A("treasure_100", Mec, H, AchievementStat.TreasuresCollected, 100, "Hoarder", "Collect 100 treasures.", 400, (X, 1)),
                A("moss_30", Mec, E, AchievementStat.MossRemoved, 30, "Weeder", "Clear 30 mossy tiles.", 40),
                A("moss_200", Mec, M, AchievementStat.MossRemoved, 200, "Moss Buster", "Clear 200 mossy tiles.", 150, (B, 1)),
                A("tiles_10000", Mec, E, AchievementStat.TilesCleared, 10000, "Ten Thousand", "Clear 10,000 tiles.", 50),
                A("tiles_100000", Mec, V, AchievementStat.TilesCleared, 100000, "Hundred Thousand", "Clear 100,000 tiles.", 800, Each(1)),
                Mastery(A("powerups_each_1", Mec, M, AchievementStat.PowerUpMastery, 5, "Toolkit", "Use each of the five power-ups at least once.", 100), 1),
                Mastery(A("powerups_each_10", Mec, H, AchievementStat.PowerUpMastery, 50, "Power User", "Use each of the five power-ups 10 times.", 300, Each(1)), 10),

                // Challenges (Daily Challenge and Weekly Rush).
                A("daily_1", Cha, E, AchievementStat.DailyChallengesCompleted, 1, "Daily Dabbler", "Complete a Daily Challenge.", 30),
                A("daily_7", Cha, M, AchievementStat.DailyChallengesCompleted, 7, "Daily Habit", "Complete 7 Daily Challenges.", 75, (B, 1)),
                A("daily_30", Cha, H, AchievementStat.DailyChallengesCompleted, 30, "Daily Devotee", "Complete 30 Daily Challenges.", 300, (X, 1)),
                A("daily_100", Cha, V, AchievementStat.DailyChallengesCompleted, 100, "Daily Legend", "Complete 100 Daily Challenges.", 1000, Each(1)),
                A("weekly_bronze", Cha, E, AchievementStat.WeeklyBestMedal, 1, "Rush Hour", "Earn a Bronze medal in the Weekly Rush.", 40),
                A("weekly_gold", Cha, H, AchievementStat.WeeklyBestMedal, 3, "Golden Rush", "Earn a Gold medal in the Weekly Rush.", 250, (C, 1)),
            };
            return list.ToArray();
        }

        static AchievementDefinition Pure(AchievementDefinition d)
        {
            d.param = 50;
            return d;
        }

        static AchievementDefinition Mastery(AchievementDefinition d, int usesEach)
        {
            d.param = usesEach;
            return d;
        }
    }
}
