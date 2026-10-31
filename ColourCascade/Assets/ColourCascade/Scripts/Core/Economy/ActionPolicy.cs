namespace ColourCascade.Core
{
    /// <summary>Tracks whether a run is in progress, so purchases can be blocked during challenge runs.</summary>
    public sealed class ActivityTracker
    {
        public bool InRun { get; private set; }
        public GameMode Mode { get; private set; }

        public void BeginRun(GameMode mode)
        {
            InRun = true;
            Mode = mode;
        }

        public void EndRun()
        {
            InRun = false;
        }
    }

    /// <summary>
    /// The single place that decides what the player may do. The UI asks these questions to decide what
    /// to show, and the services ask the same questions again before acting, so a hidden or disabled
    /// button can never be bypassed.
    /// </summary>
    public static class ActionPolicy
    {
        /// <summary>Daily Challenge and Weekly Rush are fair, fixed challenges: no power-ups, no purchases mid-run.</summary>
        public static bool IsChallengeMode(GameMode mode) => mode == GameMode.Daily || mode == GameMode.WeeklyRush;

        public static bool ModeAllowsPowerUps(GameMode mode) => !IsChallengeMode(mode);

        /// <summary>
        /// Obstacles and goals other than the score are campaign-only (docs/MECHANICS.md §4). Endless, Daily
        /// Challenge and Weekly Rush keep their own rules; a session refuses to start if one would get them.
        /// </summary>
        public static bool ModeAllowsCampaignMechanics(GameMode mode) => mode == GameMode.Campaign;

        public static bool IsPowerUpAllowed(PowerUpDefinition def, GameMode mode)
        {
            return def != null && ModeAllowsPowerUps(mode) && def.allowedModes.Has(mode);
        }

        /// <summary>A power-up is unlocked once the player has reached its introduction level.</summary>
        public static bool IsUnlocked(PowerUpDefinition def, int highestUnlockedLevel)
        {
            return def != null && (def.unlockLevel <= 0 || highestUnlockedLevel >= def.unlockLevel);
        }

        public static bool ShopPurchasesAllowed(ActivityTracker activity)
        {
            return activity == null || !(activity.InRun && IsChallengeMode(activity.Mode));
        }

        /// <summary>Only modes with a timer show one. Everything except Weekly Rush is untimed.</summary>
        public static bool ModeIsTimed(GameMode mode) => mode == GameMode.WeeklyRush;
    }
}
