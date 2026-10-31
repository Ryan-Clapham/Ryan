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
        // Obstacles already hit during the current action: each loses at most one step per action (MECHANICS §2.1).
        readonly HashSet<int> hitThisAction = new HashSet<int>();
        int actionObstaclePoints;
        // Special tiles hit during the current action, waiting to fire in hit order; each fires once per action.
        readonly List<GridPos> specialQueue = new List<GridPos>();
        readonly HashSet<int> firedThisAction = new HashSet<int>();
        readonly List<GridPos> effectCells = new List<GridPos>();
        int actionSpecialPoints;
        int nextWave;
        bool mossRemovedThisAction;
        int startingMoss;
        readonly List<MossGrowth> mossCandidates = new List<MossGrowth>();
        readonly HashSet<GridPos> mossSeen = new HashSet<GridPos>();
        Rng recoveryRng;
        Rng mossRng;
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
        /// <summary>Progress towards the level's goals (campaign completion; tracked in every mode).</summary>
        public GoalTracker Goals { get; private set; }
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
                int stars = StarRating.Compute(FinalScore, Level.Target, Level.Star2, Level.Star3);
                // A won campaign level (every goal met) always earns at least one star, even when it had no score goal.
                return Mode == GameMode.Campaign ? Math.Max(1, stars) : stars;
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
            if (!ActionPolicy.ModeAllowsCampaignMechanics(Mode) &&
                (Level.Layout.HasObstacles || Level.Layout.HasPieces || Level.Specials != SpecialTier.None || (Level.Goals != null && Level.Goals.Length > 0)))
                throw new InvalidOperationException(Mode + " levels cannot contain obstacles, treasure, special tiles or campaign goals");
            var rng = new Rng(setup.BoardSeed);
            source = new TileSource(rng.Fork(1), Level.Colours, Level.ColourWeights, Level.AssistPermille);
            recoveryRng = rng.Fork(2);
            mossRng = rng.Fork(3);
            Board = BoardGenerator.Generate(Level.Layout, source);
            Goals = new GoalTracker(Level.EffectiveGoals(), Board);
            startingMoss = Board.CountObstacle(ObstacleKind.Moss);
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

        /// <summary>True when the cell holds a special tile (tapping it fires it).</summary>
        public bool IsSpecialAt(GridPos p) => Board != null && Board.InBounds(p) && Board[p].Type == CellType.Special;

        /// <summary>Tapping p would fire a prism (alone or with a neighbour), so the player picks a colour first.</summary>
        public bool NeedsColourChoice(GridPos p)
        {
            if (!IsSpecialAt(p)) return false;
            if (Board[p].Special == SpecialKind.Prism) return true;
            GridPos partner;
            return SpecialEffects.FindPartner(Board, p, out partner) && Board[partner].Special == SpecialKind.Prism;
        }

        /// <summary>
        /// The cells a tap on the special at p reaches directly (chain reactions not included), for previews and
        /// bots. colour is the prism's colour (-1 = the most common). Returns false if p is not a special tile.
        /// </summary>
        public bool PreviewSpecial(GridPos p, int colour, List<GridPos> cells, out GridPos partner, out bool combo)
        {
            cells.Clear();
            partner = default;
            combo = false;
            if (!IsSpecialAt(p)) return false;
            var t = Board[p];
            combo = SpecialEffects.FindPartner(Board, p, out partner);
            var other = combo ? Board[partner].Special : SpecialKind.None;
            if (colour < 0) colour = SpecialEffects.MostCommonColour(Board);
            AreaFor(p, t.Special, combo, partner, other, colour, cells);
            return true;
        }

        void AreaFor(GridPos p, SpecialKind kind, bool combo, GridPos partnerPos, SpecialKind partner, int colour, List<GridPos> cells)
        {
            if (!combo)
            {
                SpecialEffects.Single(Board, p, kind, colour, cells);
                return;
            }
            bool onePrism = (kind == SpecialKind.Prism) != (partner == SpecialKind.Prism);
            if (!onePrism)
            {
                SpecialEffects.Combined(Board, p, kind, partner, cells);
                return;
            }
            // Prism + another special: the chosen colour everywhere, then the other special from where it sat.
            SpecialEffects.Single(Board, p, SpecialKind.Prism, colour, cells);
            if (kind == SpecialKind.Prism) SpecialEffects.Single(Board, partnerPos, partner, colour, cells);
            else SpecialEffects.Single(Board, p, kind, colour, cells);
        }

        public int PointsForGroup(int size) => Scoring.GroupPoints(size);

        /// <summary>
        /// Clears the group containing p. Returns false (and changes nothing) if the move is not legal.
        /// Free tiles in the group are removed; chained tiles stay and lose a link (docs/MECHANICS.md §2.2).
        /// </summary>
        public bool TryPlay(GridPos p, MoveOutcome o) => TryPlay(p, o, -1);

        /// <summary>
        /// Plays a tap at p: clears its group, or fires the special tile there (docs/MECHANICS.md §2.3). prismColour
        /// is the colour a prism clears (the player picks it); -1 picks the most common colour.
        /// </summary>
        public bool TryPlay(GridPos p, MoveOutcome o, int prismColour)
        {
            o.Reset();
            if (!AcceptsMoves || Board == null || !Board.InBounds(p)) return false;
            if (Board[p].Type == CellType.Special) return FireTappedSpecial(p, o, prismColour);
            int size = finder.FindGroup(Board, p, groupBuffer);
            if (size < 2) return false;

            BeginAction();
            int colour = Board[p].Colour;
            o.Kind = OutcomeKind.Group;
            o.Origin = p;
            o.GroupSize = size;
            o.GroupColour = colour;
            for (int i = 0; i < groupBuffer.Count; i++)
            {
                var pos = groupBuffer[i];
                var t = Board[pos];
                if (TileRules.StaysWhenItsGroupClears(t))
                {
                    HitObstacle(pos, HitCause.GroupClear, o);
                    continue;
                }
                if (TileRules.HasObstacle(t)) RemoveObstacleWithTile(pos, t, HitCause.GroupClear, o);
                o.Cleared.Add(new ClearedTile { TileId = t.Id, Pos = pos, Colour = t.Colour });
                Board[pos] = default;
            }
            CreateSpecial(p, size, o);

            int points = Scoring.GroupPoints(size);
            Score += points;
            o.ScoreGained = points;
            o.Praise = Scoring.PraiseFor(size);
            MovesMade++;
            GroupsCleared++;
            TilesCleared += o.Cleared.Count;
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

            Resolve(o, true, true);
            return true;
        }

        /// <summary>A big group earns a special tile in the cell the player tapped (or the nearest cleared cell).</summary>
        void CreateSpecial(GridPos tapped, int size, MoveOutcome o)
        {
            if (Level.Specials == SpecialTier.None) return;
            int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            for (int i = 0; i < groupBuffer.Count; i++)
            {
                var g = groupBuffer[i];
                minC = Math.Min(minC, g.Col);
                maxC = Math.Max(maxC, g.Col);
                minR = Math.Min(minR, g.Row);
                maxR = Math.Max(maxR, g.Row);
            }
            var kind = Scoring.specials.KindFor(size, Level.Specials, maxC - minC > maxR - minR);
            if (kind == SpecialKind.None) return;
            GridPos at = tapped;
            if (Board[tapped].Type != CellType.Empty)
            {
                // The tapped tile stayed (it was chained): use the nearest cleared cell, lowest row then column on a tie.
                int best = int.MaxValue;
                bool found = false;
                for (int i = 0; i < groupBuffer.Count; i++)
                {
                    var g = groupBuffer[i];
                    if (Board[g].Type != CellType.Empty) continue;
                    int d = Math.Abs(g.Col - tapped.Col) + Math.Abs(g.Row - tapped.Row);
                    if (d < best || (d == best && (g.Row < at.Row || (g.Row == at.Row && g.Col < at.Col))))
                    {
                        best = d;
                        at = g;
                        found = true;
                    }
                }
                if (!found) return;
            }
            var tile = Board.NewSpecial(kind);
            Board[at] = tile;
            o.HasCreated = true;
            o.Created = new SpecialCreated { TileId = tile.Id, Pos = at, Kind = kind };
        }

        /// <summary>A tap on a special tile: it fires (with a neighbouring special, if any) and costs a move.</summary>
        bool FireTappedSpecial(GridPos p, MoveOutcome o, int prismColour)
        {
            BeginAction();
            o.Kind = OutcomeKind.Special;
            o.Origin = p;
            MovesMade++;
            if (HasMoveLimit) MovesLeft--;
            FireSpecial(p, true, prismColour, o);
            RunSpecialQueue(o);
            Resolve(o, true, true);
            return true;
        }

        void FireSpecial(GridPos p, bool tapped, int chosenColour, MoveOutcome o)
        {
            var t = Board[p];
            if (t.Type != CellType.Special) return;
            firedThisAction.Add(t.Id);
            GridPos partnerPos = default;
            bool combo = tapped && SpecialEffects.FindPartner(Board, p, out partnerPos);
            var partner = combo ? Board[partnerPos] : default;
            if (combo) firedThisAction.Add(partner.Id);
            bool prism = t.Special == SpecialKind.Prism || (combo && partner.Special == SpecialKind.Prism);
            int colour = -1;
            if (prism) colour = chosenColour >= 0 && chosenColour < TileColours.Max ? chosenColour : SpecialEffects.MostCommonColour(Board);
            int wave = nextWave++;
            Board[p] = default;
            if (combo) Board[partnerPos] = default;
            o.SpecialsFired.Add(new SpecialFired
            {
                TileId = t.Id, Pos = p, Kind = t.Special, ComboWith = combo ? partner.Special : SpecialKind.None,
                ComboPos = partnerPos, Colour = colour, Wave = wave,
            });
            if (tapped && prism) o.GroupColour = colour;
            effectCells.Clear();
            AreaFor(p, t.Special, combo, partnerPos, combo ? partner.Special : SpecialKind.None, colour, effectCells);
            int rate = combo ? Scoring.specials.comboPointsPerTile : Scoring.specials.pointsPerTile;
            int before = o.Cleared.Count;
            // Copy: hitting cells can queue more specials but never changes this list.
            for (int i = 0; i < effectCells.Count; i++) HitCell(effectCells[i], o);
            for (int i = before; i < o.Cleared.Count; i++)
            {
                var c = o.Cleared[i];
                c.Wave = wave;
                o.Cleared[i] = c;
                if (c.WasStone) continue;
                actionSpecialPoints += rate;
                TilesCleared++;
            }
        }

        void QueueSpecial(GridPos p)
        {
            var t = Board[p];
            if (t.Type != CellType.Special || firedThisAction.Contains(t.Id)) return;
            for (int i = 0; i < specialQueue.Count; i++)
                if (specialQueue[i] == p) return;
            specialQueue.Add(p);
        }

        /// <summary>Fires the specials hit so far, in the order they were hit; any they hit join the end of the queue.</summary>
        void RunSpecialQueue(MoveOutcome o)
        {
            for (int i = 0; i < specialQueue.Count; i++) FireSpecial(specialQueue[i], false, -1, o);
            specialQueue.Clear();
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
            BeginAction();
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
            if (specialQueue.Count > 0)
            {
                nextWave = 1;
                RunSpecialQueue(o);
            }

            Resolve(o, effect.ChangesBoard, false);
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

        /// <summary>
        /// An area effect reaching a cell (Blast, Colour Burst; specials later): a free colour tile is removed, an
        /// obstacle loses one step, anything else is untouched (docs/MECHANICS.md §2, §6). Returns true if it did something.
        /// </summary>
        public bool HitCell(GridPos p, MoveOutcome o)
        {
            if (!Board.InBounds(p)) return false;
            var t = Board[p];
            if (t.Type == CellType.Special)
            {
                QueueSpecial(p);
                return true;
            }
            if (TileRules.AreaHitRemovesTile(t))
            {
                RemoveObstacleWithTile(p, t, HitCause.Area, o);
                RemoveTile(p, o);
                return true;
            }
            if (TileRules.HasObstacle(t)) return HitObstacle(p, HitCause.Area, o);
            if (!TileRules.IsFree(t)) return false;
            RemoveTile(p, o);
            return true;
        }

        /// <summary>Tile Breaker: removes a free colour tile, a mossy tile or a stone; strips every layer or link from ice
        /// and chains (the tile stays); fires a special tile.</summary>
        public bool StrongHit(GridPos p, MoveOutcome o)
        {
            if (!Board.InBounds(p)) return false;
            var t = Board[p];
            if (t.Type == CellType.Special)
            {
                QueueSpecial(p);
                return true;
            }
            if (TileRules.AreaHitRemovesTile(t))
            {
                RemoveObstacleWithTile(p, t, HitCause.Strong, o);
                RemoveTile(p, o);
                return true;
            }
            if (TileRules.HasObstacle(t)) return HitObstacle(p, HitCause.Strong, o);
            if (!TileRules.IsFree(t) && t.Type != CellType.Stone) return false;
            RemoveTile(p, o);
            return true;
        }

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
            if (!TileRules.CanRecolour(t) || t.Colour == colour) return;
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

        void BeginAction()
        {
            hitThisAction.Clear();
            actionObstaclePoints = 0;
            actionSpecialPoints = 0;
            specialQueue.Clear();
            firedThisAction.Clear();
            nextWave = 0;
            mossRemovedThisAction = false;
        }

        /// <summary>An obstacle that leaves with its tile (moss): recorded as a hit that frees it, and scored.</summary>
        void RemoveObstacleWithTile(GridPos p, Tile t, HitCause cause, MoveOutcome o)
        {
            if (!hitThisAction.Add(t.Id)) return;
            o.ObstacleHits.Add(new ObstacleHit
            {
                TileId = t.Id, Pos = p, Kind = t.Obstacle, Colour = t.Colour, StrengthBefore = t.Strength, StrengthAfter = 0, Cause = cause,
            });
            actionObstaclePoints += Scoring.ObstacleStepPoints(t.Obstacle) * Math.Max(1, t.Strength);
            if (t.Obstacle == ObstacleKind.Moss) mossRemovedThisAction = true;
        }

        /// <summary>
        /// After a move that removed no moss, moss grows onto one free tile next to it, chosen with the session's own
        /// seeded generator; it stops at the level's cap (docs/MECHANICS.md §2.5).
        /// </summary>
        void SpreadMoss(MoveOutcome o)
        {
            if (mossRemovedThisAction || startingMoss == 0) return;
            int moss = Board.CountObstacle(ObstacleKind.Moss);
            if (moss == 0) return;
            int cap = Level.MossCap > 0 ? Level.MossCap : startingMoss * 2;
            if (moss >= cap) return;
            mossCandidates.Clear();
            mossSeen.Clear();
            for (int i = 0; i < Board.CellCount; i++)
            {
                var t = Board.GetByIndex(i);
                if (t.Type != CellType.Colour || t.Obstacle != ObstacleKind.Moss) continue;
                var from = Board.PosOf(i);
                for (int d = 0; d < 4; d++)
                {
                    var n = new GridPos(from.Col + Board.DCol[d], from.Row + Board.DRow[d]);
                    if (!Board.InBounds(n) || !TileRules.CanBeOvergrown(Board[n]) || !mossSeen.Add(n)) continue;
                    mossCandidates.Add(new MossGrowth { TileId = Board[n].Id, Pos = n, From = from });
                }
            }
            if (mossCandidates.Count == 0) return;
            var g = mossCandidates[mossRng.Next(mossCandidates.Count)];
            var tile = Board[g.Pos];
            tile.Obstacle = ObstacleKind.Moss;
            tile.Strength = 1;
            Board[g.Pos] = tile;
            o.MossGrowth.Add(g);
        }

        /// <summary>
        /// Takes strength off the obstacle at p: one step, or all of it for a strong hit. At most one step per
        /// obstacle per action (a strong hit is a whole action on its own). Scores the steps removed.
        /// </summary>
        bool HitObstacle(GridPos p, HitCause cause, MoveOutcome o)
        {
            var t = Board[p];
            if (!TileRules.HasObstacle(t) || !hitThisAction.Add(t.Id)) return false;
            int before = t.Strength;
            int after = cause == HitCause.Strong ? 0 : Math.Max(0, before - 1);
            o.ObstacleHits.Add(new ObstacleHit
            {
                TileId = t.Id, Pos = p, Kind = t.Obstacle, Colour = t.Colour, StrengthBefore = before, StrengthAfter = after, Cause = cause,
            });
            actionObstaclePoints += Scoring.ObstacleStepPoints(t.Obstacle) * (before - after);
            t.Strength = after;
            if (after <= 0) t.Obstacle = ObstacleKind.None;
            Board[p] = t;
            return true;
        }

        /// <summary>Every obstacle next to a removed tile that reacts to adjacent clears loses one step (§2.1).</summary>
        void ApplyAdjacentHits(MoveOutcome o)
        {
            for (int i = 0; i < o.Cleared.Count; i++)
            {
                var c = o.Cleared[i].Pos;
                for (int d = 0; d < 4; d++)
                {
                    var n = new GridPos(c.Col + Board.DCol[d], c.Row + Board.DRow[d]);
                    if (Board.InBounds(n) && TileRules.HitByAdjacentClear(Board[n])) HitObstacle(n, HitCause.AdjacentClear, o);
                }
            }
        }

        /// <summary>
        /// The shared tail of every action (docs/MECHANICS.md §3.1): obstacle reactions, obstacle points, gravity
        /// and refill, goal progress, no-move recovery and the end check.
        /// </summary>
        void Resolve(MoveOutcome o, bool boardChanged, bool costsMove)
        {
            if (boardChanged) ApplyAdjacentHits(o);
            int extra = actionObstaclePoints + actionSpecialPoints;
            actionObstaclePoints = 0;
            actionSpecialPoints = 0;
            if (extra > 0)
            {
                Score += extra;
                o.ScoreGained += extra;
            }
            if (boardChanged)
            {
                Gravity.Apply(Board, source, o.Falls, o.Spawns, true, o.Treasures);
                if (o.Treasures.Count > 0)
                {
                    int points = o.Treasures.Count * Scoring.treasurePoints;
                    Score += points;
                    o.ScoreGained += points;
                }
                if (costsMove) SpreadMoss(o);
            }
            if (Goals != null) o.GoalsChanged = Goals.Record(o, Score, Board);
            if (boardChanged) Recover(o);
            EvaluateEnd(o);
        }

        void Recover(MoveOutcome o)
        {
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
                        if (Goals.AllComplete)
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
