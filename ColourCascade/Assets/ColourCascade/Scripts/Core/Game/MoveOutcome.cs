using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum SessionState
    {
        Ready = 0,
        Playing = 1,
        /// <summary>Campaign target reached.</summary>
        Won = 2,
        /// <summary>Campaign ran out of moves before the target.</summary>
        Failed = 3,
        /// <summary>Endless, Daily or Weekly Rush run completed.</summary>
        Finished = 4,
        /// <summary>No legal move and recovery was impossible. Ends without penalty.</summary>
        Stuck = 5,
    }

    public enum EndReason
    {
        None = 0,
        TargetReached = 1,
        OutOfMoves = 2,
        TimeUp = 3,
        BoardStuck = 4,
    }

    public enum OutcomeKind
    {
        None = 0,
        Group = 1,
        PowerUp = 2,
    }

    public struct ClearedTile
    {
        public int TileId;
        public GridPos Pos;
        public int Colour;
        public bool WasStone;
    }

    public struct Recolour
    {
        public int TileId;
        public GridPos Pos;
        public int From;
        public int To;
    }

    /// <summary>
    /// Everything that happened as the result of one player action, in order:
    /// Cleared/Recolours/SwapMoves, then Falls + Spawns (gravity and refill), then ShuffleMoves (dead-board recovery).
    /// The view animates from this; the session's board already holds the final state.
    /// The lists are reused between moves.
    /// </summary>
    public sealed class MoveOutcome
    {
        public OutcomeKind Kind;
        public PowerUpId PowerUp;
        public GridPos Origin;
        public GridPos Secondary;
        public int GroupSize;
        public int GroupColour = -1;
        public int ScoreGained;
        public PraiseTier Praise;
        public int BonusMoves;
        public int MovesAdded;
        public bool StageUp;
        public int NewStage;
        public bool Shuffled;
        public bool RecoveryFailed;
        public bool Ended;
        public EndReason EndReason;
        public bool UsedTrial;

        public readonly List<ClearedTile> Cleared = new List<ClearedTile>();
        public readonly List<Recolour> Recolours = new List<Recolour>();
        public readonly List<TileMove> SwapMoves = new List<TileMove>();
        public readonly List<TileMove> Falls = new List<TileMove>();
        public readonly List<TileSpawn> Spawns = new List<TileSpawn>();
        public readonly List<TileMove> ShuffleMoves = new List<TileMove>();

        public void Reset()
        {
            Kind = OutcomeKind.None;
            PowerUp = default;
            Origin = default;
            Secondary = default;
            GroupSize = 0;
            GroupColour = -1;
            ScoreGained = 0;
            Praise = PraiseTier.None;
            BonusMoves = 0;
            MovesAdded = 0;
            StageUp = false;
            NewStage = 0;
            Shuffled = false;
            RecoveryFailed = false;
            Ended = false;
            EndReason = EndReason.None;
            UsedTrial = false;
            Cleared.Clear();
            Recolours.Clear();
            SwapMoves.Clear();
            Falls.Clear();
            Spawns.Clear();
            ShuffleMoves.Clear();
        }
    }
}
