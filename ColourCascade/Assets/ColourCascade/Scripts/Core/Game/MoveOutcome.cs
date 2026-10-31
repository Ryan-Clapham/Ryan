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
        /// <summary>A special tile was tapped (costs a move).</summary>
        Special = 3,
    }

    public struct ClearedTile
    {
        public int TileId;
        public GridPos Pos;
        public int Colour;
        public bool WasStone;
        /// <summary>0 for the action's own clear; 1, 2 ... for tiles removed by specials it set off, in firing order
        /// (lets the view stagger chain reactions).</summary>
        public int Wave;
    }

    /// <summary>A special tile created by a big group (it appears in the cleared area before tiles fall).</summary>
    public struct SpecialCreated
    {
        public int TileId;
        public GridPos Pos;
        public SpecialKind Kind;
    }

    /// <summary>A special tile that fired during an action. Listed in firing order.</summary>
    public struct SpecialFired
    {
        public int TileId;
        public GridPos Pos;
        public SpecialKind Kind;
        /// <summary>The special it fired together with (tapped next to it), or None.</summary>
        public SpecialKind ComboWith;
        public GridPos ComboPos;
        /// <summary>Colour a prism cleared; -1 otherwise.</summary>
        public int Colour;
        /// <summary>Matches ClearedTile.Wave for the tiles it removed.</summary>
        public int Wave;
    }

    /// <summary>Moss that grew onto a neighbouring tile after a move that cleared none.</summary>
    public struct MossGrowth
    {
        public int TileId;
        public GridPos Pos;
        /// <summary>The mossy tile it grew from.</summary>
        public GridPos From;
    }

    /// <summary>Why an obstacle lost strength (docs/MECHANICS.md §2).</summary>
    public enum HitCause
    {
        /// <summary>A tile next to it was removed.</summary>
        AdjacentClear = 1,
        /// <summary>A group it belongs to was cleared (chains).</summary>
        GroupClear = 2,
        /// <summary>An area effect reached it (Blast, Colour Burst, specials).</summary>
        Area = 3,
        /// <summary>Tile Breaker: all of its strength at once.</summary>
        Strong = 4,
    }

    /// <summary>An obstacle that lost strength during an action. The tile stays in its cell.</summary>
    public struct ObstacleHit
    {
        public int TileId;
        public GridPos Pos;
        public ObstacleKind Kind;
        public int Colour;
        public int StrengthBefore;
        public int StrengthAfter;
        public HitCause Cause;
        /// <summary>The obstacle is gone: the tile is now a free colour tile.</summary>
        public bool Freed => StrengthAfter <= 0;
    }

    public struct Recolour
    {
        public int TileId;
        public GridPos Pos;
        public int From;
        public int To;
    }

    /// <summary>
    /// Everything that happened as the result of one player action, in order (docs/MECHANICS.md §3.1):
    /// Cleared/Recolours/SwapMoves, ObstacleHits, Created and SpecialsFired (waves), then Falls + Treasures + Spawns
    /// (gravity, collection and refill), then MossGrowth, then ShuffleMoves (dead-board recovery).
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
        /// <summary>Some goal's progress changed during this action.</summary>
        public bool GoalsChanged;
        /// <summary>A group clear earned a special tile (see Created).</summary>
        public bool HasCreated;
        public SpecialCreated Created;

        public readonly List<ClearedTile> Cleared = new List<ClearedTile>();
        public readonly List<ObstacleHit> ObstacleHits = new List<ObstacleHit>();
        public readonly List<Recolour> Recolours = new List<Recolour>();
        public readonly List<TileMove> SwapMoves = new List<TileMove>();
        public readonly List<TileMove> Falls = new List<TileMove>();
        public readonly List<TileSpawn> Spawns = new List<TileSpawn>();
        public readonly List<TileMove> ShuffleMoves = new List<TileMove>();
        public readonly List<SpecialFired> SpecialsFired = new List<SpecialFired>();
        public readonly List<CollectedTreasure> Treasures = new List<CollectedTreasure>();
        public readonly List<MossGrowth> MossGrowth = new List<MossGrowth>();

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
            GoalsChanged = false;
            HasCreated = false;
            Created = default;
            SpecialsFired.Clear();
            Treasures.Clear();
            MossGrowth.Clear();
            Cleared.Clear();
            ObstacleHits.Clear();
            Recolours.Clear();
            SwapMoves.Clear();
            Falls.Clear();
            Spawns.Clear();
            ShuffleMoves.Clear();
        }
    }
}
