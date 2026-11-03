namespace ColourCascade.Core
{
    /// <summary>
    /// Where the tutorial hand goes so its fingertip lands on what it points at, in overlay coordinates (origin at
    /// the centre, y up). Pure arithmetic, so it is the same at every resolution and aspect ratio, and testable.
    /// The hand art is 128 × 128 with the fingertip at (40, 94.5) from its bottom-left corner.
    /// </summary>
    public static class HandPointer
    {
        /// <summary>Fingertip offset from the hand's centre, as a fraction of its size.</summary>
        public const float TipX = 40f / 128f - 0.5f, TipY = 94.5f / 128f - 0.5f;
        /// <summary>The fingertip rests this fraction of the hand's size below the target and taps up onto it.</summary>
        public const float Rest = 0.12f;
        /// <summary>Space kept between the hand and the screen edge.</summary>
        public const float Margin = 8f;

        /// <summary>
        /// The hand's centre for a fingertip resting just below (targetX, targetY), on a screen halfWidth wide each side
        /// of the centre. If the hand would go off the right edge it is mirrored (flip: pointing from the left), so it
        /// stays whole next to a button in the corner.
        /// </summary>
        public static void Place(float targetX, float targetY, float size, float halfWidth, out float x, out float y, out bool flip)
        {
            flip = false;
            x = targetX - TipX * size;
            y = targetY - (TipY + Rest) * size;
            if (x + size * 0.5f > halfWidth - Margin)
            {
                flip = true;
                x = targetX + TipX * size;
            }
            if (x - size * 0.5f < -halfWidth + Margin && !flip) x = -halfWidth + Margin + size * 0.5f;
        }

        /// <summary>Where the fingertip is for a hand centred at (x, y).</summary>
        public static void Tip(float x, float y, float size, bool flip, out float tipX, out float tipY)
        {
            tipX = x + (flip ? -TipX : TipX) * size;
            tipY = y + TipY * size;
        }
    }
}
