namespace ColourCascade.Core
{
    /// <summary>
    /// Where a run's points came from (docs/MECHANICS.md §5), for the result screen. Every point added to the score is
    /// counted in exactly one line, so Total always equals the session's Score; the spare-move bonus is separate
    /// (PuzzleSession.LeftoverBonus).
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

        public int Total => Groups + Specials + ComboBonus + PowerUps + Obstacles + Stones + Treasure + Finale;

        public ScoreBreakdown Clone() => (ScoreBreakdown)MemberwiseClone();
    }
}
