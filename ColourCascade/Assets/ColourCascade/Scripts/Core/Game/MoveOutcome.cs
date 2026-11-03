using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum SessionState
    {
        Ready = 0,
        Playing = 1,
        /// <summary>Campaign level over and passed: every goal was met, then the moves ran out or the player chose to finish.</summary>
        Won = 2,
        /// <summary>Campaign ran out of moves before every goal was met.</summary>
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
        /// <summary>Campaign: the player finished a passed level before using every move (unused moves score nothing).</summary>
        FinishedEarly = 5,
    }

    public enum OutcomeKind
    {
        None = 0,
        Group = 1,
        PowerUp = 2,
        /// <summary>A special tile was tapped (costs a move).</summary>
        Special = 3,
        /// <summary>The end of the level: specials left on the board went off (no move charged; docs/MECHANICS.md §4.4).</summary>
        Finale = 4,
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
        /// <summary>The strongest other special it fired together with (connected to it), or None.</summary>
        public SpecialKind ComboWith;
        public GridPos ComboPos;
        public int ComboTileId;
        /// <summary>How many specials fired together as this one combination (1 = alone). The others are in
        /// MoveOutcome.ComboMembers with the same Wave.</summary>
        public int ComboSize;
        /// <summary>Colour a prism cleared; -1 otherwise.</summary>
        public int Colour;
        /// <summary>Matches ClearedTile.Wave for the tiles it removed.</summary>
        public int Wave;
    }

    /// <summary>A special that fired as part of a combination led by another (the SpecialFired with the same Wave).</summary>
    public struct ComboMember
    {
        public int TileId;
        public GridPos Pos;
        public SpecialKind Kind;
        public int Wave;
    }

    /// <summary>A cell a special's (or combination's) blast reached, whatever was there: what the view flashes.</summary>
    public struct EffectCell
    {
        public GridPos Pos;
        public int Wave;
    }

    /// <summary>
    /// Something a new Endless stage added to the board: an obstacle on an existing tile, or treasure, a stone or a
    /// special tile placed in a tile's cell (that tile, ReplacedTileId, leaves the board).
    /// </summary>
    public struct TileChange
    {
        public int TileId;
        public GridPos Pos;
        public ObstacleKind Obstacle;
        public int Strength;
        public bool IsTreasure;
        /// <summary>The tile in that cell became a stone (Endless stages).</summary>
        public bool IsStone;
        /// <summary>A special tile was placed in that cell (Endless: the first rocket and prism of a run), or None.</summary>
        public SpecialKind Special;
        public int ReplacedTileId;
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
        /// <summary>Same as ClearedTile.Wave: when in the action it happened (for the view's timing).</summary>
        public int Wave;
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
        /// <summary>Campaign: this action completed the level's last goal. The level is now passed and play goes on.</summary>
        public bool JustPassed;
        /// <summary>The moves ran out with specials on the board: call PuzzleSession.RunFinale to set them off.</summary>
        public bool FinalePending;
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
        /// <summary>Specials that fired as part of a combination (besides each combination's lead in SpecialsFired).</summary>
        public readonly List<ComboMember> ComboMembers = new List<ComboMember>();
        /// <summary>Every cell each blast reached (the exact area), by wave.</summary>
        public readonly List<EffectCell> EffectCells = new List<EffectCell>();
        public readonly List<CollectedTreasure> Treasures = new List<CollectedTreasure>();
        public readonly List<MossGrowth> MossGrowth = new List<MossGrowth>();
        /// <summary>Endless: what the stage just reached added to the board (after tiles have fallen).</summary>
        public readonly List<TileChange> Added = new List<TileChange>();

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
            JustPassed = false;
            FinalePending = false;
            HasCreated = false;
            Created = default;
            SpecialsFired.Clear();
            ComboMembers.Clear();
            EffectCells.Clear();
            Treasures.Clear();
            MossGrowth.Clear();
            Added.Clear();
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
