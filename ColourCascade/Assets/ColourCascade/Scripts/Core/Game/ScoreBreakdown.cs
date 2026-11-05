namespace ColourCascade.Core
{
    /// <summary>
    /// Where a run's points came from (docs/MECHANICS.md §5), for the result screen. Every point added to the score is
    /// counted in exactly one line, so Total always equals the session's Score (and the final score: there is no
    /// bonus for unused moves since 2.1).
    /// </summary>
    public sealed class ScoreBreakdown
    {
        /// <summary>Groups cleared by tapping: n² × 10.</summary>
        public int Groups;
        /// <summary>Colour tiles removed by special tiles, at the single-special rate.</summary>
        public int Specials;
        /// <summary>The extra a combination of two specials pays per tile on top of the single rate.</summary>
        public int ComboBonus;
        /// <summary>Colour tiles removed by power-ups.</summary>
        public int PowerUps;
        /// <summary>Ice layers, chain links and moss removed.</summary>
        public int Obstacles;
        /// <summary>Stones destroyed.</summary>
        public int Stones;
        /// <summary>Treasure collected.</summary>
        public int Treasure;
        /// <summary>Everything the specials left on the board scored when the moves ran out (or at Finish).</summary>
        public int Finale;
        /// <summary>Weekly Rush: bonuses for completed objectives (update 3.3).</summary>
        public int Objectives;

        public int Total => Groups + Specials + ComboBonus + PowerUps + Obstacles + Stones + Treasure + Finale + Objectives;

        public ScoreBreakdown Clone() => (ScoreBreakdown)MemberwiseClone();
    }
}
