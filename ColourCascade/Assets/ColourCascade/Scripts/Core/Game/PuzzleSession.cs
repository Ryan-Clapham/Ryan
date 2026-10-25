using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    public sealed class SessionSetup
    {
        public GameMode Mode;
        public LevelSpec Level;
        public ScoringRules Scoring = new ScoringRules();
        /// <summary>Required for Endless (bonus moves and stages).</summary>
        public EndlessConfig Endless;
        /// <summary>Seed for the board and refills. Fixed for daily/weekly, fresh per attempt otherwise.</summary>
        public ulong BoardSeed;
    }

    public enum PowerUpFailure
    {
        None = 0,
        UnknownPowerUp,
        NotPlaying,
        NotAllowedInMode,
        Locked,
        NoneOwned,
        InvalidTarget,
        NotAdjacent,
        SameColour,
        NothingToClear,
        NoMoveLimit,
    }

    /// <summary>
    /// One play-through of one board, in any mode. Owns the board and all rule decisions
    /// (validity, scoring, gravity, refill, recovery, moves, timer, win/lose). Has no knowledge of
    /// UI, saving or coins, so it can be unit-tested and simulated.
    /// </summary>
    public sealed class PuzzleSession
    {
        readonly SessionSetup setup;
        readonly GroupFinder finder = new GroupFinder();
        readonly List<GridPos> groupBuffer = new List<GridPos>();
        Rng recoveryRng;
        TileSource source;

        public PuzzleSession(SessionSetup setup)
        {
            if (setup == null || setup.Level == null) throw new ArgumentException("Session needs a level");
            if (setup.Mode == GameMode.Endless && setup.Endless == null) throw new ArgumentException("Endless session needs EndlessConfig");
            if (setup.Scoring == null) setup.Scoring = new ScoringRules();
            this.setup = setup;
        }

        public Board Board { get; private set; }
        public GameMode Mode => setup.Mode;
        public LevelSpec Level => setup.Level;
        public ScoringRules Scoring => setup.Scoring;
        public SessionState State { get; private set; }
        public EndReason EndReason { get; private set; }

        public int Score { get; private set; }
        public int MovesLeft { get; private set; }
        public int MovesMade { get; private set; }
        public int GroupsCleared { get; private set; }
        public int TilesCleared { get; private set; }
        public int BiggestGroup { get; private set; }
        public int PowerUpsUsed { get; private set; }
        public int Shuffles { get; private set; }
        public int Stage { get; private set; }
        /// <summary>Campaign bonus for unused moves, set when the target is reached.</summary>
        public int LeftoverBonus { get; private set; }
        public double TimeLeft { get; private set; }
        public bool TimerRunning { get; private set; }
        public bool Paused { get; private set; }

        public bool HasMoveLimit => Level.Moves > 0;
        public bool IsTimed => Level.TimeLimitSeconds > 0;
        public int Target => Level.Target;
        public int FinalScore => Score + LeftoverBonus;
        public int ColourCount => source != null ? source.ColourCount : Level.Colours;
        public bool IsOver => State != SessionState.Playing && State != SessionState.Ready;

        /// <summary>Stars for the current final score (campaign only counts them once won).</summary>
        public int Stars
        {
            get
            {
                if (Mode == GameMode.Campaign && State != SessionState.Won) return 0;
                return StarRating.Compute(FinalScore, Level.Target, Level.Star2, Level.Star3);
            }
        }

        /// <summary>Whether the run counts as a success (level won / daily target met).</summary>
        public bool Succeeded
        {
            get
            {
                switch (Mode)
                {
                    case GameMode.Campaign: return State == SessionState.Won;
                    case GameMode.Daily: return IsOver && State != SessionState.Stuck && FinalScore >= Level.Target;
                    default: return IsOver && State != SessionState.Stuck;
                }
            }
        }

        /// <summary>True when a valid group or power-up would be accepted right now.</summary>
        public bool AcceptsMoves
        {
            get
            {
                if (State != SessionState.Playing || Paused) return false;
                if (IsTimed && (!TimerRunning || TimeLeft <= 0)) return false;
                return true;
            }
        }

        public void Start()
        {
            if (State != SessionState.Ready) throw new InvalidOperationException("Session already started");
            var rng = new Rng(setup.BoardSeed);
            source = new TileSource(rng.Fork(1), Level.Colours, Level.ColourWeights, Level.AssistPermille);
            recoveryRng = rng.Fork(2);
            Board = BoardGenerator.Generate(Level.Layout, source);
            MovesLeft = Level.Moves;
            TimeLeft = Level.TimeLimitSeconds;
            Stage = 1;
            State = SessionState.Playing;
            if (BoardRecovery.EnsurePlayable(Board, recoveryRng, null) == RecoveryResult.Failed)
            {
                State = SessionState.Stuck;
                EndReason = EndReason.BoardStuck;
            }
        }

        /// <summary>Weekly Rush: starts the countdown once the 3-2-1 intro has finished.</summary>
        public void StartTimer()
        {
            if (IsTimed && State == SessionState.Playing) TimerRunning = true;
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
        }

        /// <summary>Advances the countdown (Weekly Rush only). Returns true on the tick the time ran out.</summary>
        public bool Tick(double deltaSeconds)
        {
            if (!IsTimed || State != SessionState.Playing || Paused || !TimerRunning || deltaSeconds <= 0) return false;
            TimeLeft -= deltaSeconds;
            if (TimeLeft > 0) return false;
            TimeLeft = 0;
            TimerRunning = false;
            State = SessionState.Finished;
            EndReason = EndReason.TimeUp;
            return true;
        }

        /// <summary>Size of the group under a cell without changing anything (for the press-and-hold preview).</summary>
        public int PreviewGroup(GridPos p, List<GridPos> into)
        {
            if (Board == null) return 0;
            return finder.FindGroup(Board, p, into);
        }

        public int PointsForGroup(int size) => Scoring.GroupPoints(size);

        /// <summary>Clears the group containing p. Returns false (and changes nothing) if the move is not legal.</summary>
        public bool TryPlay(GridPos p, MoveOutcome o)
        {
            o.Reset();
            if (!AcceptsMoves) return false;
            int size = finder.FindGroup(Board, p, groupBuffer);
            if (size < 2) return false;

            int colour = Board[p].Colour;
            o.Kind = OutcomeKind.Group;
            o.Origin = p;
            o.GroupSize = size;
            o.GroupColour = colour;
            for (int i = 0; i < groupBuffer.Count; i++)
            {
                var pos = groupBuffer[i];
                var t = Board[pos];
                o.Cleared.Add(new ClearedTile { TileId = t.Id, Pos = pos, Colour = t.Colour });
                Board[pos] = default;
            }

            int points = Scoring.GroupPoints(size);
            Score += points;
            o.ScoreGained = points;
            o.Praise = Scoring.PraiseFor(size);
            MovesMade++;
            GroupsCleared++;
            TilesCleared += size;
            if (size > BiggestGroup) BiggestGroup = size;
            if (HasMoveLimit) MovesLeft--;

            if (Mode == GameMode.Endless)
            {
                int bonus = setup.Endless.BonusMovesFor(size);
                if (bonus > 0)
                {
                    MovesLeft += bonus;
                    o.BonusMoves = bonus;
                }
                UpdateEndlessStage(o);
            }

            Settle(o);
            EvaluateEnd(o);
            return true;
        }

        /// <summary>Validates a power-up target without applying it.</summary>
        public bool CanApplyPowerUp(PowerUpRequest req, out PowerUpFailure failure)
        {
            var effect = PowerUpEffects.Get(req.Id);
            if (effect == null)
            {
                failure = PowerUpFailure.UnknownPowerUp;
                return false;
            }
            if (!ActionPolicy.ModeAllowsPowerUps(Mode))
            {
                failure = PowerUpFailure.NotAllowedInMode;
                return false;
            }
            bool revivingAfterOutOfMoves = req.Id == PowerUpId.ExtraMoves && State == SessionState.Failed && EndReason == EndReason.OutOfMoves;
            if (!AcceptsMoves && !revivingAfterOutOfMoves)
            {
                failure = PowerUpFailure.NotPlaying;
                return false;
            }
            return effect.Validate(this, req, out failure);
        }

        /// <summary>Applies a power-up effect. Ownership/payment is handled by PowerUpService, not here.</summary>
        public bool TryApplyPowerUp(PowerUpRequest req, MoveOutcome o, out PowerUpFailure failure)
        {
            o.Reset();
            if (!CanApplyPowerUp(req, out failure)) return false;
            var effect = PowerUpEffects.Get(req.Id);
            o.Kind = OutcomeKind.PowerUp;
            o.PowerUp = req.Id;
            o.Origin = req.A;
            o.Secondary = req.B;
            effect.Apply(this, req, o);
            PowerUpsUsed++;

            int colourTiles = 0;
            for (int i = 0; i < o.Cleared.Count; i++)
                if (!o.Cleared[i].WasStone) colourTiles++;
            if (colourTiles > 0)
            {
                int points = Scoring.PowerUpPoints(colourTiles);
                Score += points;
                o.ScoreGained = points;
                TilesCleared += colourTiles;
                if (Mode == GameMode.Endless) UpdateEndlessStage(o);
            }

            if (effect.ChangesBoard) Settle(o);
            EvaluateEnd(o);
            failure = PowerUpFailure.None;
            return true;
        }

        /// <summary>Adds moves (Extra Moves). Revives a campaign level that has just run out of moves.</summary>
        public bool AddMoves(int amount)
        {
            if (!HasMoveLimit || amount <= 0) return false;
            if (State == SessionState.Failed && EndReason == EndReason.OutOfMoves)
            {
                State = SessionState.Playing;
                EndReason = EndReason.None;
            }
            if (State != SessionState.Playing) return false;
            MovesLeft += amount;
            return true;
        }

        // ---- Operations used by power-up effects ----

        public void RemoveTile(GridPos p, MoveOutcome o)
        {
            var t = Board[p];
            if (t.Type != CellType.Colour && t.Type != CellType.Stone) return;
            o.Cleared.Add(new ClearedTile { TileId = t.Id, Pos = p, Colour = t.Colour, WasStone = t.Type == CellType.Stone });
            Board[p] = default;
        }

        public void RecolourTile(GridPos p, int colour, MoveOutcome o)
        {
            var t = Board[p];
            if (t.Type != CellType.Colour || t.Colour == colour) return;
            o.Recolours.Add(new Recolour { TileId = t.Id, Pos = p, From = t.Colour, To = colour });
            t.Colour = colour;
            Board[p] = t;
        }

        public void SwapTiles(GridPos a, GridPos b, MoveOutcome o)
        {
            var ta = Board[a];
            var tb = Board[b];
            Board[a] = tb;
            Board[b] = ta;
            o.SwapMoves.Add(new TileMove { TileId = ta.Id, From = a, To = b });
            o.SwapMoves.Add(new TileMove { TileId = tb.Id, From = b, To = a });
        }

        public void GrantMoves(int amount, MoveOutcome o)
        {
            if (AddMoves(amount)) o.MovesAdded += amount;
        }

        // ---- Internals ----

        void Settle(MoveOutcome o)
        {
            Gravity.Apply(Board, source, o.Falls, o.Spawns);
            var result = BoardRecovery.EnsurePlayable(Board, recoveryRng, o.ShuffleMoves);
            if (result == RecoveryResult.Shuffled)
            {
                o.Shuffled = true;
                Shuffles++;
            }
            else if (result == RecoveryResult.Failed)
            {
                o.RecoveryFailed = true;
                State = SessionState.Stuck;
                EndReason = EndReason.BoardStuck;
            }
        }

        void UpdateEndlessStage(MoveOutcome o)
        {
            int stage = setup.Endless.StageForScore(Score);
            if (stage <= Stage) return;
            Stage = stage;
            o.StageUp = true;
            o.NewStage = stage;
            source.Configure(setup.Endless.ColoursForStage(stage), Level.ColourWeights);
        }

        void EvaluateEnd(MoveOutcome o)
        {
            if (State == SessionState.Playing)
            {
                switch (Mode)
                {
                    case GameMode.Campaign:
                        if (Score >= Level.Target)
                        {
                            LeftoverBonus = Math.Max(0, MovesLeft) * Level.LeftoverBonusPerMove;
                            State = SessionState.Won;
                            EndReason = EndReason.TargetReached;
                        }
                        else if (MovesLeft <= 0)
                        {
                            State = SessionState.Failed;
                            EndReason = EndReason.OutOfMoves;
                        }
                        break;
                    case GameMode.Daily:
                    case GameMode.Endless:
                        if (MovesLeft <= 0)
                        {
                            State = SessionState.Finished;
                            EndReason = EndReason.OutOfMoves;
                        }
                        break;
                }
            }
            o.Ended = IsOver;
            o.EndReason = EndReason;
        }
    }
}
