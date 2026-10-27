using ColourCascade.Core;
using UnityEngine;

namespace ColourCascade.App
{
    /// <summary>
    /// Every tunable number in the game, editable in the Inspector:
    /// Assets/ColourCascade/Resources/ColourCascadeConfig.asset (created automatically by the editor
    /// setup the first time the project is opened). If the asset is missing, these defaults are used.
    /// Use the component menu "Reset" to restore defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "ColourCascadeConfig", menuName = "Colour Cascade/Game Config")]
    public sealed class ColourCascadeConfig : ScriptableObject
    {
        public const string ResourcePath = "ColourCascadeConfig";

        [Header("Scoring (points = groupSize ^ exponent × pointsPerTile)")]
        public ScoringRules scoring = new ScoringRules();

        [Header("Campaign: board sizes, difficulty curve and hand-made levels")]
        public CampaignConfig campaign = new CampaignConfig { overrides = DefaultLevels.Create() };

        [Header("Endless mode")]
        public EndlessConfig endless = new EndlessConfig();

        [Header("Daily Challenge")]
        public DailyConfig daily = new DailyConfig();

        [Header("Weekly Rush (the only timed mode)")]
        public RushConfig rush = new RushConfig();

        [Header("Coins and rewards")]
        public EconomyConfig economy = new EconomyConfig();

        [Header("Power-ups (prices, unlock levels, free trial uses)")]
        public PowerUpDefinition[] powerUps = PowerUpCatalog.CreateDefaults();

        // Deliberately 0 when the field is missing from an older asset; the editor setup then moves the asset's
        // rewards to the current tuning (see EconomyTuning) and records the version here.
        [Tooltip("Reward tuning version of this asset. Older assets are updated automatically when the project opens; values edited by hand are kept.")]
        public int rewardTuningVersion;

        public GameConfigData ToData()
        {
            var d = GameConfigData.CreateDefault();
            if (scoring != null) d.Scoring = scoring;
            if (campaign != null) d.Campaign = campaign;
            if (endless != null) d.Endless = endless;
            if (daily != null) d.Daily = daily;
            if (rush != null) d.Rush = rush;
            if (economy != null) d.Economy = economy;
            if (powerUps != null && powerUps.Length > 0) d.PowerUps = powerUps;
            return d;
        }

        void Reset()
        {
            scoring = new ScoringRules();
            campaign = new CampaignConfig { overrides = DefaultLevels.Create() };
            endless = new EndlessConfig();
            daily = new DailyConfig();
            rush = new RushConfig();
            economy = new EconomyConfig();
            powerUps = PowerUpCatalog.CreateDefaults();
            rewardTuningVersion = EconomyTuning.Current;
        }

        void OnValidate()
        {
            if (scoring != null)
            {
                scoring.pointsPerTile = Mathf.Max(1, scoring.pointsPerTile);
                scoring.exponent = Mathf.Clamp(scoring.exponent, 1, 4);
            }
            if (rush != null) rush.seconds = Mathf.Max(10, rush.seconds);
            if (endless != null) endless.startMoves = Mathf.Max(1, endless.startMoves);
            if (powerUps != null)
                foreach (var p in powerUps)
                    if (p != null) p.price = Mathf.Max(0, p.price);
        }
    }

    /// <summary>Device clock with an optional day offset, used by the developer tools to test challenge rotation.</summary>
    public sealed class DebugClock : IClock
    {
        public int OffsetDays;
        public System.DateTime UtcNow => System.DateTime.UtcNow.AddDays(OffsetDays);
    }
}
