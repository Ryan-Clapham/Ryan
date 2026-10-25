using System.Collections.Generic;

namespace ColourCascade.Core
{
    public struct PowerUpRequest
    {
        public PowerUpId Id;
        /// <summary>Primary target cell.</summary>
        public GridPos A;
        /// <summary>Second cell (Swap).</summary>
        public GridPos B;
        /// <summary>Chosen colour (Colour Shift).</summary>
        public int Colour;
        /// <summary>Strength from the definition (Extra Moves: moves to add). 0 = effect default.</summary>
        public int Amount;

        public static PowerUpRequest Instant(PowerUpId id) => new PowerUpRequest { Id = id };
        public static PowerUpRequest AtCell(PowerUpId id, GridPos a) => new PowerUpRequest { Id = id, A = a };
        public static PowerUpRequest Pair(PowerUpId id, GridPos a, GridPos b) => new PowerUpRequest { Id = id, A = a, B = b };
        public static PowerUpRequest CellColour(PowerUpId id, GridPos a, int colour) => new PowerUpRequest { Id = id, A = a, Colour = colour };
    }

    /// <summary>
    /// Behaviour of one power-up. New power-ups are added by implementing this interface and calling
    /// PowerUpEffects.Register; the shop, inventory and in-game bar pick them up from their definition.
    /// </summary>
    public interface IPowerUpEffect
    {
        PowerUpId Id { get; }
        /// <summary>False for effects that do not touch tiles (Extra Moves).</summary>
        bool ChangesBoard { get; }
        bool Validate(PuzzleSession session, PowerUpRequest request, out PowerUpFailure failure);
        void Apply(PuzzleSession session, PowerUpRequest request, MoveOutcome outcome);
        /// <summary>Cells the effect would touch, for targeting previews. May be empty.</summary>
        void Preview(PuzzleSession session, PowerUpRequest request, List<GridPos> cells);
    }

    public static class PowerUpEffects
    {
        static readonly Dictionary<PowerUpId, IPowerUpEffect> effects = new Dictionary<PowerUpId, IPowerUpEffect>();

        static PowerUpEffects()
        {
            Register(new BlastEffect());
            Register(new SwapEffect());
            Register(new ColourShiftEffect());
            Register(new TileBreakerEffect());
            Register(new ExtraMovesEffect());
            Register(new ColourBurstEffect());
        }

        public static void Register(IPowerUpEffect effect)
        {
            effects[effect.Id] = effect;
        }

        public static IPowerUpEffect Get(PowerUpId id)
        {
            IPowerUpEffect e;
            return effects.TryGetValue(id, out e) ? e : null;
        }

        internal static bool IsColour(PuzzleSession s, GridPos p) => s.Board.InBounds(p) && s.Board[p].Type == CellType.Colour;
    }

    /// <summary>Clears every colour tile in the 3×3 square centred on the target (clipped to the board).
    /// Stones and voids inside the area are untouched. The area must contain at least one colour tile.</summary>
    public sealed class BlastEffect : IPowerUpEffect
    {
        public const int Radius = 1;
        public PowerUpId Id => PowerUpId.Blast;
        public bool ChangesBoard => true;

        public bool Validate(PuzzleSession s, PowerUpRequest r, out PowerUpFailure failure)
        {
            var b = s.Board;
            if (!b.InBounds(r.A) || b[r.A].Type == CellType.Void)
            {
                failure = PowerUpFailure.InvalidTarget;
                return false;
            }
            for (int dr = -Radius; dr <= Radius; dr++)
                for (int dc = -Radius; dc <= Radius; dc++)
                    if (b.IsColourAt(r.A.Col + dc, r.A.Row + dr))
                    {
                        failure = PowerUpFailure.None;
                        return true;
                    }
            failure = PowerUpFailure.NothingToClear;
            return false;
        }

        public void Apply(PuzzleSession s, PowerUpRequest r, MoveOutcome o)
        {
            for (int dr = -Radius; dr <= Radius; dr++)
                for (int dc = -Radius; dc <= Radius; dc++)
                {
                    var p = new GridPos(r.A.Col + dc, r.A.Row + dr);
                    if (s.Board.IsColourAt(p.Col, p.Row)) s.RemoveTile(p, o);
                }
        }

        public void Preview(PuzzleSession s, PowerUpRequest r, List<GridPos> cells)
        {
            cells.Clear();
            for (int dr = -Radius; dr <= Radius; dr++)
                for (int dc = -Radius; dc <= Radius; dc++)
                    if (s.Board.IsColourAt(r.A.Col + dc, r.A.Row + dr)) cells.Add(new GridPos(r.A.Col + dc, r.A.Row + dr));
        }
    }

    /// <summary>Swaps two orthogonally adjacent colour tiles of different colours. Nothing is cleared.</summary>
    public sealed class SwapEffect : IPowerUpEffect
    {
        public PowerUpId Id => PowerUpId.Swap;
        public bool ChangesBoard => true;

        public bool Validate(PuzzleSession s, PowerUpRequest r, out PowerUpFailure failure)
        {
            if (!PowerUpEffects.IsColour(s, r.A) || !PowerUpEffects.IsColour(s, r.B))
            {
                failure = PowerUpFailure.InvalidTarget;
                return false;
            }
            if (!r.A.IsAdjacentTo(r.B))
            {
                failure = PowerUpFailure.NotAdjacent;
                return false;
            }
            if (s.Board[r.A].Colour == s.Board[r.B].Colour)
            {
                failure = PowerUpFailure.SameColour;
                return false;
            }
            failure = PowerUpFailure.None;
            return true;
        }

        public void Apply(PuzzleSession s, PowerUpRequest r, MoveOutcome o) => s.SwapTiles(r.A, r.B, o);

        public void Preview(PuzzleSession s, PowerUpRequest r, List<GridPos> cells)
        {
            cells.Clear();
            if (!PowerUpEffects.IsColour(s, r.A)) return;
            int colour = s.Board[r.A].Colour;
            for (int d = 0; d < 4; d++)
            {
                var n = new GridPos(r.A.Col + Board.DCol[d], r.A.Row + Board.DRow[d]);
                if (PowerUpEffects.IsColour(s, n) && s.Board[n].Colour != colour) cells.Add(n);
            }
        }
    }

    /// <summary>Repaints one colour tile with another colour currently in play.</summary>
    public sealed class ColourShiftEffect : IPowerUpEffect
    {
        public PowerUpId Id => PowerUpId.ColourShift;
        public bool ChangesBoard => true;

        public bool Validate(PuzzleSession s, PowerUpRequest r, out PowerUpFailure failure)
        {
            if (!PowerUpEffects.IsColour(s, r.A) || r.Colour < 0 || r.Colour >= s.ColourCount)
            {
                failure = PowerUpFailure.InvalidTarget;
                return false;
            }
            if (s.Board[r.A].Colour == r.Colour)
            {
                failure = PowerUpFailure.SameColour;
                return false;
            }
            failure = PowerUpFailure.None;
            return true;
        }

        public void Apply(PuzzleSession s, PowerUpRequest r, MoveOutcome o) => s.RecolourTile(r.A, r.Colour, o);

        public void Preview(PuzzleSession s, PowerUpRequest r, List<GridPos> cells)
        {
            cells.Clear();
            if (PowerUpEffects.IsColour(s, r.A)) cells.Add(r.A);
        }
    }

    /// <summary>Removes a single colour tile or a single stone barrier. Voids cannot be broken.</summary>
    public sealed class TileBreakerEffect : IPowerUpEffect
    {
        public PowerUpId Id => PowerUpId.TileBreaker;
        public bool ChangesBoard => true;

        public bool Validate(PuzzleSession s, PowerUpRequest r, out PowerUpFailure failure)
        {
            var b = s.Board;
            if (!b.InBounds(r.A) || (b[r.A].Type != CellType.Colour && b[r.A].Type != CellType.Stone))
            {
                failure = PowerUpFailure.InvalidTarget;
                return false;
            }
            failure = PowerUpFailure.None;
            return true;
        }

        public void Apply(PuzzleSession s, PowerUpRequest r, MoveOutcome o) => s.RemoveTile(r.A, o);

        public void Preview(PuzzleSession s, PowerUpRequest r, List<GridPos> cells)
        {
            cells.Clear();
            var b = s.Board;
            if (b.InBounds(r.A) && (b[r.A].Type == CellType.Colour || b[r.A].Type == CellType.Stone)) cells.Add(r.A);
        }
    }

    /// <summary>Adds moves to a move-limited level. Default amount is 3.</summary>
    public sealed class ExtraMovesEffect : IPowerUpEffect
    {
        public const int DefaultAmount = 3;
        public PowerUpId Id => PowerUpId.ExtraMoves;
        public bool ChangesBoard => false;

        public bool Validate(PuzzleSession s, PowerUpRequest r, out PowerUpFailure failure)
        {
            if (!s.HasMoveLimit || s.Mode != GameMode.Campaign)
            {
                failure = PowerUpFailure.NoMoveLimit;
                return false;
            }
            failure = PowerUpFailure.None;
            return true;
        }

        public void Apply(PuzzleSession s, PowerUpRequest r, MoveOutcome o) => s.GrantMoves(r.Amount > 0 ? r.Amount : DefaultAmount, o);

        public void Preview(PuzzleSession s, PowerUpRequest r, List<GridPos> cells) => cells.Clear();
    }

    /// <summary>Removes every tile of the target tile's colour.</summary>
    public sealed class ColourBurstEffect : IPowerUpEffect
    {
        public PowerUpId Id => PowerUpId.ColourBurst;
        public bool ChangesBoard => true;

        public bool Validate(PuzzleSession s, PowerUpRequest r, out PowerUpFailure failure)
        {
            if (!PowerUpEffects.IsColour(s, r.A))
            {
                failure = PowerUpFailure.InvalidTarget;
                return false;
            }
            failure = PowerUpFailure.None;
            return true;
        }

        public void Apply(PuzzleSession s, PowerUpRequest r, MoveOutcome o)
        {
            var b = s.Board;
            int colour = b[r.A].Colour;
            o.GroupColour = colour;
            for (int row = 0; row < b.Height; row++)
                for (int col = 0; col < b.Width; col++)
                {
                    var t = b[col, row];
                    if (t.Type == CellType.Colour && t.Colour == colour) s.RemoveTile(new GridPos(col, row), o);
                }
        }

        public void Preview(PuzzleSession s, PowerUpRequest r, List<GridPos> cells)
        {
            cells.Clear();
            if (!PowerUpEffects.IsColour(s, r.A)) return;
            var b = s.Board;
            int colour = b[r.A].Colour;
            for (int row = 0; row < b.Height; row++)
                for (int col = 0; col < b.Width; col++)
                    if (b[col, row].Type == CellType.Colour && b[col, row].Colour == colour) cells.Add(new GridPos(col, row));
        }
    }
}
