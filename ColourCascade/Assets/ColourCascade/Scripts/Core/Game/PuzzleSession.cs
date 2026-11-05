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
        readonly List<GridPos> clusterBuffer = new List<GridPos>();
        int actionSpecialPoints;
        // The extra a combination pays on top of the single-special rate, and stones destroyed, this action.
        int actionComboPoints;
        int actionStonePoints;
        // True while a special's blast is landing: specials (unlike power-ups and groups) break stones.
        bool specialHitting;
        // The end-of-level finale is running (specials left on the board going off; docs/MECHANICS.md §4.4).
        bool inFinale;
        bool finaleDone;
        int nextWave;
        // Wave of the special firing right now (0 outside special effects); stamped on obstacle hits.
        int hitWave;
        bool mossRemovedThisAction;
        // Moss never grows on two moves in a row (docs/MECHANICS.md §2.5).
        bool mossGrewLastMove;
        int startingMoss;
        int mossCap;
        // Endless: a new stage was reached this action; its wave (docs/DESIGN.md §4) arrives once tiles have fallen.
        bool pendingWave;
        // Endless: the first stage whose wave has not arrived yet.
        int waveFromStage = 2;
        // The special tiles big groups make: the level's, or in Endless the journey's so far (it only goes up).
        SpecialTier specialTier;
        Rng waveRng;
        readonly List<GridPos> waveCells = new List<GridPos>();
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

        // Gameplay tallies for this run (lifetime statistics and achievements; GameContext records them). They count
        // what actually happened on the board, whatever caused it (groups, specials, power-ups, the end-of-level finale).
        /// <summary>Stones broken (by rockets, bombs or Tile Breaker).</summary>
        public int StonesBroken { get; private set; }
        /// <summary>Layers of ice removed (a thick tile stripped by Tile Breaker counts both).</summary>
        public int IceLayersRemoved { get; private set; }
        /// <summary>Chained tiles set free (their last link removed).</summary>
        public int ChainsReleased { get; private set; }
        /// <summary>Mossy tiles cleared (the moss went with the tile or was scraped off).</summary>
        public int MossRemoved { get; private set; }
        public int TreasuresCollected { get; private set; }
        /// <summary>Most colour tiles removed by one tap (a group, or a special or group of specials and what they set off).</summary>
        public int MostTilesInOneMove { get; private set; }
        /// <summary>Most specials fired together as one combination by one tap.</summary>
        public int BiggestCombo { get; private set; }
        readonly int[] powerUpUses = new int[8];

        /// <summary>Uses of one power-up this run.</summary>
        public int PowerUpUses(PowerUpId id) => (int)id >= 0 && (int)id < powerUpUses.Length ? powerUpUses[(int)id] : 0;
        /// <summary>
        /// Campaign: every goal (the target score included, when the level has one) has been met at some point this
        /// run. The level is passed from then on, but it is not over: the remaining moves can still raise the score
        /// and the stars, until they run out or the player finishes (docs/MECHANICS.md §4.3).
        /// </summary>
        public bool Passed { get; private set; }
        /// <summary>Moves made when the level was passed, or -1.</summary>
        public int PassedAtMove { get; private set; } = -1;
        /// <summary>
        /// The moves have run out (or the player chose Finish) with special tiles still on the board: they go off
        /// before the result is decided. No moves are accepted until RunFinale has run (docs/MECHANICS.md §4.4).
        /// </summary>
        public bool FinalePending { get; private set; }
        /// <summary>Where the score came from (docs/MECHANICS.md §5); Breakdown.Total is always Score.</summary>
        public ScoreBreakdown Breakdown { get; } = new ScoreBreakdown();
        /// <summary>What the specials left on the board scored when the level ended, line by line (its Total is
        /// Breakdown.Finale), for the summary shown after they go off (docs/MECHANICS.md §4.4).</summary>
        public ScoreBreakdown FinaleBreakdown { get; } = new ScoreBreakdown();
        /// <summary>Progress towards the level's goals (campaign completion; tracked in every mode).</summary>
        public GoalTracker Goals { get; private set; }
        public double TimeLeft { get; private set; }
        public bool TimerRunning { get; private set; }
        public bool Paused { get; private set; }

        public bool HasMoveLimit => Level.Moves > 0;
        /// <summary>The special tiles big groups make right now (Endless: they arrive with its stages).</summary>
        public SpecialTier Specials => specialTier;
        public bool IsTimed => Level.TimeLimitSeconds > 0;
        public int Target => Level.Target;
        /// <summary>The score the run ends with (there is no longer a bonus for unused moves: 2.1).</summary>
        public int FinalScore => Score;
        public int ColourCount => source != null ? source.ColourCount : Level.Colours;
        public bool IsOver => State != SessionState.Playing && State != SessionState.Ready;

        /// <summary>Stars for the current final score (campaign only counts them once won).</summary>
        public int Stars
        {
            get
            {
                if (Mode == GameMode.Campaign) return State == SessionState.Won ? CampaignStars(FinalScore) : 0;
                // Weekly Rush objectives only add points (update 3.3): its medals stay score-based.
                if (Mode != GameMode.WeeklyRush && !GoalsMet) return 0;
                return StarRating.Compute(FinalScore, Level.Target, Level.Star2, Level.Star3);
            }
        }

        /// <summary>
        /// Campaign, during play: the stars the level has earned so far (0 until it is cleared). Only ever goes up,
        /// since the score never drops; when the level ends the stars are these (specials left on the board may add more).
        /// </summary>
        public int LiveStars => Mode == GameMode.Campaign && (Passed || State == SessionState.Won) ? CampaignStars(State == SessionState.Won ? FinalScore : Score) : 0;

        /// <summary>Campaign: the stars Finish would give right now, before any specials on the board go off, or 0.</summary>
        public int StarsIfFinishedNow => CanFinish ? CampaignStars(Score) : 0;

        /// <summary>A cleared campaign level always earns at least one star, even when it has no score goal.</summary>
        int CampaignStars(int score) => Math.Max(1, StarRating.Compute(score, Level.Target, Level.Star2, Level.Star3));

        /// <summary>Campaign: the level is cleared and still in play, so the player may end it now (Finish).</summary>
        public bool CanFinish => Mode == GameMode.Campaign && Passed && State == SessionState.Playing;

        /// <summary>A level's own goals are all met (true when it has none: score-only levels and modes). Campaign: once
        /// cleared, it stays met (moss growing back later does not undo a pass).</summary>
        public bool GoalsMet => Passed || Level.Goals == null || Level.Goals.Length == 0 || (Goals != null && Goals.AllComplete);

        /// <summary>Whether the run counts as a success (level won / daily target met).</summary>
        public bool Succeeded
        {
            get
            {
                switch (Mode)
                {
                    case GameMode.Campaign: return State == SessionState.Won;
                    case GameMode.Daily: return IsOver && State != SessionState.Stuck && FinalScore >= Level.Target && GoalsMet;
                    default: return IsOver && State != SessionState.Stuck;
                }
            }
        }

        /// <summary>True when a valid group or power-up would be accepted right now.</summary>
        public bool AcceptsMoves
        {
            get
            {
                if (State != SessionState.Playing || Paused || FinalePending) return false;
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
            mossRng = rng.Fork(3);
            waveRng = rng.Fork(4);
            Board = BoardGenerator.Generate(Level.Layout, source);
            specialTier = Mode == GameMode.Endless && setup.Endless.journey != null ? EndlessJourney.SpecialsAt(1, setup.Endless.journey) : Level.Specials;
            Goals = new GoalTracker(Level.EffectiveGoals(), Board);
            startingMoss = Board.CountObstacle(ObstacleKind.Moss);
            mossCap = Level.MossCap > 0 ? Level.MossCap : startingMoss + 2;
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

        /// <summary>
        /// Advances the countdown (Weekly Rush only). Returns true on the tick the time ran out. If special tiles are still
        /// on the board then (update 3.3), FinalePending is set and the run ends once RunFinale has set them off, exactly as
        /// when a level's moves run out; otherwise the run is finished at once.
        /// </summary>
        public bool Tick(double deltaSeconds)
        {
            if (!IsTimed || State != SessionState.Playing || Paused || !TimerRunning || deltaSeconds <= 0) return false;
            TimeLeft -= deltaSeconds;
            if (TimeLeft > 0) return false;
            TimeLeft = 0;
            TimerRunning = false;
            if (!finaleDone && Board.CountType(CellType.Special) > 0)
            {
                FinalePending = true;
                return true;
            }
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
            SpecialEffects.FindCluster(Board, p, clusterBuffer);
            for (int i = 0; i < clusterBuffer.Count; i++)
                if (Board[clusterBuffer[i]].Special == SpecialKind.Prism) return true;
            return false;
        }

        /// <summary>The specials that would fire together with a tap at p (p first), for previews and bots.</summary>
        public void SpecialGroupAt(GridPos p, List<GridPos> members) => SpecialEffects.FindCluster(Board, p, members);

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
            SpecialEffects.FindCluster(Board, p, clusterBuffer);
            combo = clusterBuffer.Count > 1;
            if (combo) partner = Strongest(clusterBuffer, 1);
            if (colour < 0) colour = SpecialEffects.MostCommonColour(Board);
            SpecialEffects.ClusterArea(Board, clusterBuffer, colour, Scoring.specials, cells);
            return true;
        }

        /// <summary>The strongest special in members from index 'from' on (prism, bomb, rocket; the first on a tie).</summary>
        GridPos Strongest(List<GridPos> members, int from)
        {
            int best = -1;
            GridPos at = members[Math.Min(from, members.Count - 1)];
            for (int i = from; i < members.Count; i++)
            {
                int rank = SpecialEffects.Rank(Board[members[i]].Special);
                if (rank > best)
                {
                    best = rank;
                    at = members[i];
                }
            }
            return at;
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
            Breakdown.Groups += points;
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
            if (specialTier == SpecialTier.None) return;
            int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            for (int i = 0; i < groupBuffer.Count; i++)
            {
                var g = groupBuffer[i];
                minC = Math.Min(minC, g.Col);
                maxC = Math.Max(maxC, g.Col);
                minR = Math.Min(minR, g.Row);
                maxR = Math.Max(maxR, g.Row);
            }
            var kind = Scoring.specials.KindFor(size, specialTier, maxC - minC > maxR - minR);
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

        /// <summary>
        /// Fires the special at p. With asGroup (a tap, or the end of the level), every special connected to it fires
        /// together as one combination (docs/MECHANICS.md §2.3); otherwise (set off by another blast) it fires alone.
        /// Each special fires at most once per action; every cell it reaches is recorded for the view.
        /// </summary>
        void FireSpecial(GridPos p, bool asGroup, int chosenColour, MoveOutcome o)
        {
            var t = Board[p];
            if (t.Type != CellType.Special) return;
            var members = new List<GridPos>();
            if (asGroup) SpecialEffects.FindCluster(Board, p, members);
            else members.Add(p);
            bool prism = false;
            foreach (var m in members)
            {
                firedThisAction.Add(Board[m].Id);
                prism |= Board[m].Special == SpecialKind.Prism;
            }
            bool combo = members.Count > 1;
            int colour = -1;
            if (prism) colour = chosenColour >= 0 && chosenColour < TileColours.Max ? chosenColour : SpecialEffects.MostCommonColour(Board);
            int wave = nextWave++;
            // The area is worked out with the group still on the board (it depends on what each member is).
            effectCells.Clear();
            SpecialEffects.ClusterArea(Board, members, colour, Scoring.specials, effectCells);
            var lead = new SpecialFired { TileId = t.Id, Pos = p, Kind = t.Special, Colour = colour, Wave = wave, ComboSize = members.Count };
            if (combo)
            {
                var partnerPos = Strongest(members, 1);
                lead.ComboWith = Board[partnerPos].Special;
                lead.ComboPos = partnerPos;
                lead.ComboTileId = Board[partnerPos].Id;
                for (int i = 1; i < members.Count; i++)
                {
                    var m = Board[members[i]];
                    o.ComboMembers.Add(new ComboMember { TileId = m.Id, Pos = members[i], Kind = m.Special, Wave = wave });
                }
            }
            o.SpecialsFired.Add(lead);
            foreach (var m in members) Board[m] = default;
            if (o.Kind == OutcomeKind.Special && prism && p == o.Origin) o.GroupColour = colour;
            for (int i = 0; i < effectCells.Count; i++) o.EffectCells.Add(new EffectCell { Pos = effectCells[i], Wave = wave });
            // Scored on what the blast actually removes: each colour tile at the special rate (a combination pays
            // more, counted separately as its bonus); stones, obstacles and treasure score their own points. A tile
            // already removed earlier in the action is not there to be removed (or scored) again.
            int rate = Scoring.specials.pointsPerTile;
            int comboExtra = combo ? Math.Max(0, Scoring.specials.comboPointsPerTile - rate) : 0;
            int before = o.Cleared.Count;
            hitWave = wave;
            specialHitting = true;
            // Copy: hitting cells can queue more specials but never changes this list.
            for (int i = 0; i < effectCells.Count; i++) HitCell(effectCells[i], o);
            specialHitting = false;
            hitWave = 0;
            for (int i = before; i < o.Cleared.Count; i++)
            {
                var c = o.Cleared[i];
                c.Wave = wave;
                o.Cleared[i] = c;
                if (c.WasStone) continue;
                actionSpecialPoints += rate;
                actionComboPoints += comboExtra;
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
            if (!AcceptsMoves)
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
            if ((int)req.Id >= 0 && (int)req.Id < powerUpUses.Length) powerUpUses[(int)req.Id]++;

            int colourTiles = 0;
            for (int i = 0; i < o.Cleared.Count; i++)
                if (!o.Cleared[i].WasStone) colourTiles++;
            if (colourTiles > 0)
            {
                int points = Scoring.PowerUpPoints(colourTiles);
                Score += points;
                Breakdown.PowerUps += points;
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

        /// <summary>
        /// Campaign: ends a passed level now. Specials still on the board go off first (the same groups and rules as
        /// when the moves run out); unused moves score nothing. Returns false if the level is not passed or not in play.
        /// </summary>
        public bool Finish(MoveOutcome o = null)
        {
            if (!CanFinish) return false;
            o = o ?? new MoveOutcome();
            o.Reset();
            // Specials still on the board go off first (their points count). Unused moves are worth nothing.
            if (Mode == GameMode.Campaign && !finaleDone && Board.CountType(CellType.Special) > 0) FireFinale(o);
            State = SessionState.Won;
            EndReason = EndReason.FinishedEarly;
            o.Ended = true;
            o.EndReason = EndReason;
            return true;
        }

        /// <summary>
        /// The end of the level (docs/MECHANICS.md §4.4): the moves have run out and special tiles are still on the
        /// board. Every one of them goes off (no move is charged), chain reactions resolve, their points and goal
        /// progress count, and only then is the result decided (a goal met now still passes the level). Returns false
        /// if there was nothing to run.
        /// </summary>
        public bool RunFinale(MoveOutcome o)
        {
            o.Reset();
            if (!FinalePending) return false;
            FinalePending = false;
            FireFinale(o);
            // Weekly Rush: the time ran out; the run ends once the specials have gone off.
            if (IsTimed && TimeLeft <= 0 && State == SessionState.Playing)
            {
                State = SessionState.Finished;
                EndReason = EndReason.TimeUp;
                o.Ended = true;
                o.EndReason = EndReason;
            }
            return true;
        }

        void FireFinale(MoveOutcome o)
        {
            finaleDone = true;
            BeginAction();
            o.Kind = OutcomeKind.Finale;
            inFinale = true;
            // Top to bottom, left to right: each connected group of specials fires as one combination, led by its
            // top-left member (exactly as if that one were tapped; a prism takes the most common colour), and anything
            // it reaches goes off in turn before the next group.
            for (int r = Board.Height - 1; r >= 0; r--)
                for (int c = 0; c < Board.Width; c++)
                {
                    var p = new GridPos(c, r);
                    if (Board[p].Type != CellType.Special || firedThisAction.Contains(Board[p].Id)) continue;
                    FireSpecial(p, true, -1, o);
                    RunSpecialQueue(o);
                }
            Resolve(o, true, false);
            inFinale = false;
            // A stage reached by the finale's points is recorded (the result shows it), but its wave never arrives: the
            // run is over.
            pendingWave = false;
        }

        /// <summary>Adds moves (the Out of Moves continues). Revives a campaign level that has just run out of moves.</summary>
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
            // More moves: specials made from now on go off again if these run out too.
            finaleDone = false;
            FinalePending = false;
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
            if (t.Type == CellType.Stone && specialHitting)
            {
                // Rockets and bombs break stones in their path (docs/MECHANICS.md §2.6); power-up areas don't.
                BreakStone(p, o);
                return true;
            }
            if (!TileRules.IsFree(t)) return false;
            RemoveTile(p, o);
            return true;
        }

        void BreakStone(GridPos p, MoveOutcome o)
        {
            RemoveTile(p, o);
            actionStonePoints += Scoring.stonePoints;
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
            if (t.Type == CellType.Stone)
            {
                BreakStone(p, o);
                return true;
            }
            if (!TileRules.IsFree(t)) return false;
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
            actionComboPoints = 0;
            actionStonePoints = 0;
            specialHitting = false;
            specialQueue.Clear();
            firedThisAction.Clear();
            nextWave = 0;
            hitWave = 0;
            mossRemovedThisAction = false;
        }

        /// <summary>An obstacle that leaves with its tile (moss): recorded as a hit that frees it, and scored.</summary>
        void RemoveObstacleWithTile(GridPos p, Tile t, HitCause cause, MoveOutcome o)
        {
            if (!hitThisAction.Add(t.Id)) return;
            o.ObstacleHits.Add(new ObstacleHit
            {
                TileId = t.Id, Pos = p, Kind = t.Obstacle, Colour = t.Colour, StrengthBefore = t.Strength, StrengthAfter = 0, Cause = cause, Wave = hitWave,
            });
            actionObstaclePoints += Scoring.ObstacleStepPoints(t.Obstacle) * Math.Max(1, t.Strength);
            if (t.Obstacle == ObstacleKind.Moss) mossRemovedThisAction = true;
        }

        /// <summary>
        /// After a move that removed no moss, moss grows onto one free tile next to it, chosen with the session's own
        /// seeded generator. It rests on the move after it grew, and stops at the level's cap (docs/MECHANICS.md §2.5).
        /// </summary>
        void SpreadMoss(MoveOutcome o)
        {
            bool rested = mossGrewLastMove;
            mossGrewLastMove = false;
            if (mossRemovedThisAction || rested) return;
            int moss = Board.CountObstacle(ObstacleKind.Moss);
            if (moss == 0 || moss >= mossCap) return;
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
            mossGrewLastMove = true;
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
                TileId = t.Id, Pos = p, Kind = t.Obstacle, Colour = t.Colour, StrengthBefore = before, StrengthAfter = after, Cause = cause, Wave = hitWave,
            });
            actionObstaclePoints += Scoring.ObstacleStepPoints(t.Obstacle) * (before - after);
            if (t.Obstacle == ObstacleKind.Moss && after <= 0) mossRemovedThisAction = true;
            t.Strength = after;
            if (after <= 0) t.Obstacle = ObstacleKind.None;
            Board[p] = t;
            return true;
        }

        /// <summary>Every obstacle next to a removed tile that reacts to adjacent clears (ice, moss) loses one step (§2.1, §2.5).</summary>
        void ApplyAdjacentHits(MoveOutcome o)
        {
            for (int i = 0; i < o.Cleared.Count; i++)
            {
                var c = o.Cleared[i].Pos;
                hitWave = o.Cleared[i].Wave;
                for (int d = 0; d < 4; d++)
                {
                    var n = new GridPos(c.Col + Board.DCol[d], c.Row + Board.DRow[d]);
                    if (Board.InBounds(n) && TileRules.HitByAdjacentClear(Board[n])) HitObstacle(n, HitCause.AdjacentClear, o);
                }
            }
            hitWave = 0;
        }

        /// <summary>
        /// The shared tail of every action (docs/MECHANICS.md §3.1): obstacle reactions, obstacle points, gravity
        /// and refill, goal progress, no-move recovery and the end check.
        /// </summary>
        void Resolve(MoveOutcome o, bool boardChanged, bool costsMove)
        {
            if (boardChanged) ApplyAdjacentHits(o);
            int extra = actionObstaclePoints + actionSpecialPoints + actionComboPoints + actionStonePoints;
            if (inFinale)
            {
                Breakdown.Finale += extra;
                FinaleBreakdown.Obstacles += actionObstaclePoints;
                FinaleBreakdown.Specials += actionSpecialPoints;
                FinaleBreakdown.ComboBonus += actionComboPoints;
                FinaleBreakdown.Stones += actionStonePoints;
            }
            else
            {
                Breakdown.Obstacles += actionObstaclePoints;
                Breakdown.Specials += actionSpecialPoints;
                Breakdown.ComboBonus += actionComboPoints;
                Breakdown.Stones += actionStonePoints;
            }
            actionObstaclePoints = 0;
            actionSpecialPoints = 0;
            actionComboPoints = 0;
            actionStonePoints = 0;
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
                    if (inFinale)
                    {
                        Breakdown.Finale += points;
                        FinaleBreakdown.Treasure += points;
                    }
                    else Breakdown.Treasure += points;
                    o.ScoreGained += points;
                    // No bonus moves once the run is ending: the finale is the end of an Endless run (update 3.1). It used to
                    // pay them, so the run carried on with its finale spent, and specials left later never went off.
                    if (Mode == GameMode.Endless && HasMoveLimit && setup.Endless.treasureBonusMoves > 0 && !inFinale)
                    {
                        int bonus = o.Treasures.Count * setup.Endless.treasureBonusMoves;
                        MovesLeft += bonus;
                        o.BonusMoves += bonus;
                    }
                }
                if (costsMove) SpreadMoss(o);
                if (pendingWave && !inFinale) ApplyEndlessWave(o);
            }
            pendingWave = false;
            if (inFinale && Mode == GameMode.Endless) UpdateEndlessStage(o);
            if (Goals != null) o.GoalsChanged = Goals.Record(o, Score, Board);
            AwardObjectives(o);
            Tally(o);
            // No reshuffle once the level is ending: nobody plays the board again.
            if (boardChanged && !inFinale) Recover(o);
            EvaluateEnd(o);
        }

        bool[] objectiveAwarded;

        /// <summary>
        /// Weekly Rush objectives (update 3.3): the first time each one is complete, its bonus is added to the score once
        /// (it shows on the result as "Objectives"). The run still ends only when the time is up.
        /// </summary>
        void AwardObjectives(MoveOutcome o)
        {
            if (Mode != GameMode.WeeklyRush || Goals == null || Level.Goals == null || Level.Goals.Length == 0 || ObjectiveBonusPoints <= 0) return;
            if (objectiveAwarded == null) objectiveAwarded = new bool[Goals.Count];
            for (int i = 0; i < Goals.Count && i < objectiveAwarded.Length; i++)
            {
                if (objectiveAwarded[i] || !Goals.IsComplete(i)) continue;
                objectiveAwarded[i] = true;
                Score += ObjectiveBonusPoints;
                Breakdown.Objectives += ObjectiveBonusPoints;
                o.ScoreGained += ObjectiveBonusPoints;
                o.ObjectiveBonus += ObjectiveBonusPoints;
            }
        }

        /// <summary>Points each completed Weekly Rush objective adds (the level's own value, else none).</summary>
        int ObjectiveBonusPoints => Level.ObjectiveBonus;

        /// <summary>Weekly Rush objectives completed so far.</summary>
        public int ObjectivesDone
        {
            get
            {
                if (objectiveAwarded == null) return 0;
                int n = 0;
                foreach (var a in objectiveAwarded) if (a) n++;
                return n;
            }
        }

        /// <summary>Adds one action's results to the run's gameplay tallies.</summary>
        void Tally(MoveOutcome o)
        {
            int colourTiles = 0;
            foreach (var c in o.Cleared)
            {
                if (c.WasStone) StonesBroken++;
                else colourTiles++;
            }
            foreach (var h in o.ObstacleHits)
            {
                switch (h.Kind)
                {
                    case ObstacleKind.Ice: IceLayersRemoved += Math.Max(0, h.StrengthBefore - h.StrengthAfter); break;
                    case ObstacleKind.Chain: if (h.Freed) ChainsReleased++; break;
                    case ObstacleKind.Moss: if (h.Freed) MossRemoved++; break;
                }
            }
            TreasuresCollected += o.Treasures.Count;
            // "One move" is a tap on the board: a group, or a special tile (power-ups and the finale are not moves).
            if (o.Kind == OutcomeKind.Group || o.Kind == OutcomeKind.Special)
            {
                if (colourTiles > MostTilesInOneMove) MostTilesInOneMove = colourTiles;
                if (o.Kind == OutcomeKind.Special)
                    foreach (var f in o.SpecialsFired)
                        if (f.ComboSize > BiggestCombo) BiggestCombo = f.ComboSize;
            }
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

        /// <summary>
        /// Endless (docs/DESIGN.md §4): each stage reached brings its step of the journey, once tiles have fallen: a new
        /// mechanic, a breather with bonus moves, or a mix of what came before (which ones, and where they land, from
        /// the run's own seeded generator). If one action jumps several stages, each arrives in turn.
        /// </summary>
        void ApplyEndlessWave(MoveOutcome o)
        {
            var j = setup.Endless.journey;
            if (j == null) return;
            for (; waveFromStage <= Stage; waveFromStage++) ApplyStage(EndlessJourney.For(waveFromStage, j), o);
        }

        void ApplyStage(EndlessStagePlan plan, MoveOutcome o)
        {
            if ((int)plan.Specials > (int)specialTier) specialTier = plan.Specials;
            switch (plan.Step)
            {
                case EndlessStep.Breather:
                    if (HasMoveLimit && plan.BonusMoves > 0)
                    {
                        MovesLeft += plan.BonusMoves;
                        o.BonusMoves += plan.BonusMoves;
                    }
                    break;
                case EndlessStep.Introduce:
                    // A new special tile arrives with one on the board, so the player can try it at once.
                    if (plan.Mechanics.Has(MechanicSet.Rockets)) PlaceSpecial(SpecialKind.RocketRow, o);
                    else if (plan.Mechanics.Has(MechanicSet.Bombs)) PlaceSpecial(SpecialKind.Bomb, o);
                    else if (plan.Mechanics.Has(MechanicSet.Prisms)) PlaceSpecial(SpecialKind.Prism, o);
                    else AddWave(plan.Mechanics, 100, plan.Stage, false, o);
                    break;
                case EndlessStep.Mix:
                    {
                        var pool = new List<MechanicSet>();
                        foreach (var m in new[] { MechanicSet.Stones, MechanicSet.Ice, MechanicSet.Chains, MechanicSet.Treasure, MechanicSet.Moss })
                            if (plan.Mechanics.Has(m)) pool.Add(m);
                        for (int k = 0; k < plan.Kinds && pool.Count > 0; k++)
                        {
                            int i = waveRng.Next(pool.Count);
                            AddWave(pool[i], plan.DensityPercent, plan.Stage, plan.Mechanics.Has(MechanicSet.ThickIce), o);
                            pool.RemoveAt(i);
                        }
                        break;
                    }
            }
        }

        /// <summary>One mechanic's share of a stage wave; percent scales the introduction amount. Never past a third of
        /// the board covered, so a run never chokes.</summary>
        void AddWave(MechanicSet m, int percent, int stage, bool thick, MoveOutcome o)
        {
            int Scale(int v) => Math.Max(1, v * percent / 100);
            int room = Math.Max(0, Board.CellCount / 3 - CountObstacles());
            switch (m)
            {
                case MechanicSet.Stones:
                    PlaceStones(Math.Min(room, Math.Min(Scale(3), Board.Width - Board.CountType(CellType.Stone))), o);
                    break;
                case MechanicSet.Ice:
                    foreach (var p in PickFreeCells(Math.Min(room, Scale(5)), 1, Board.Height - 2, false))
                        AddObstacle(p, ObstacleKind.Ice, thick && waveRng.ChancePermille(300) ? 2 : 1, o);
                    break;
                case MechanicSet.Chains:
                    {
                        int maxLinks = Math.Min(3, 1 + Math.Max(0, stage - setup.Endless.journey.mixFrom) / 4);
                        foreach (var p in PickFreeCells(Math.Min(room, Scale(4)), 1, Board.Height - 2, false))
                            AddObstacle(p, ObstacleKind.Chain, 1 + waveRng.Next(maxLinks), o);
                        break;
                    }
                case MechanicSet.Moss:
                    {
                        int seeds = Board.CountObstacle(ObstacleKind.Moss) == 0 ? 2 : 1;
                        foreach (var p in PickFreeCells(Math.Min(room, seeds), 1, Board.Height / 2, true))
                            AddObstacle(p, ObstacleKind.Moss, 1, o);
                        mossCap = Math.Max(mossCap, Board.CountObstacle(ObstacleKind.Moss) + 2);
                        break;
                    }
                case MechanicSet.Treasure:
                    PlaceTreasure(percent >= 150 ? 2 : 1, o);
                    break;
            }
        }

        int CountObstacles()
        {
            int n = 0;
            for (int i = 0; i < Board.CellCount; i++)
            {
                var t = Board.GetByIndex(i);
                if (TileRules.HasObstacle(t) || t.Type == CellType.Stone || t.Type == CellType.Treasure) n++;
            }
            return n;
        }

        /// <summary>
        /// Stones appear on free tiles in the middle rows, one per column at most and never under treasure, so every
        /// section above and below still refills and treasure still has a way down.
        /// </summary>
        void PlaceStones(int n, MoveOutcome o)
        {
            waveCells.Clear();
            for (int r = 2; r <= Board.Height - 3; r++)
                for (int c = 0; c < Board.Width; c++)
                    if (TileRules.IsFree(Board[c, r]) && !ColumnHasStoneOrTreasure(c)) waveCells.Add(new GridPos(c, r));
            for (int k = 0; k < n && waveCells.Count > 0; k++)
            {
                int i = waveRng.Next(waveCells.Count);
                var p = waveCells[i];
                waveCells.RemoveAll(q => q.Col == p.Col);
                int replaced = Board[p].Id;
                var stone = Board.NewStone();
                Board[p] = stone;
                o.Added.Add(new TileChange { TileId = stone.Id, Pos = p, IsStone = true, ReplacedTileId = replaced });
            }
        }

        bool ColumnHasStoneOrTreasure(int c)
        {
            for (int r = 0; r < Board.Height; r++)
            {
                var t = Board[c, r].Type;
                if (t == CellType.Stone || t == CellType.Treasure) return true;
            }
            return false;
        }

        /// <summary>The first rocket (or prism) of a run, placed on a free tile so the player can try it at once.</summary>
        void PlaceSpecial(SpecialKind kind, MoveOutcome o)
        {
            var cells = PickFreeCells(1, 1, Board.Height - 3, true);
            if (cells.Count == 0) return;
            var p = cells[0];
            int replaced = Board[p].Id;
            var tile = Board.NewSpecial(kind);
            Board[p] = tile;
            o.Added.Add(new TileChange { TileId = tile.Id, Pos = p, Special = kind, ReplacedTileId = replaced });
        }

        /// <summary>Up to n free tiles in rows [fromRow, toRow], chosen at random; interior keeps them off the edges.</summary>
        List<GridPos> PickFreeCells(int n, int fromRow, int toRow, bool interior)
        {
            waveCells.Clear();
            for (int r = Math.Max(0, fromRow); r <= Math.Min(Board.Height - 1, toRow); r++)
                for (int c = interior ? 1 : 0; c < (interior ? Board.Width - 1 : Board.Width); c++)
                    if (TileRules.IsFree(Board[c, r])) waveCells.Add(new GridPos(c, r));
            var picked = new List<GridPos>();
            while (picked.Count < n && waveCells.Count > 0)
            {
                int i = waveRng.Next(waveCells.Count);
                picked.Add(waveCells[i]);
                waveCells.RemoveAt(i);
            }
            return picked;
        }

        void AddObstacle(GridPos p, ObstacleKind kind, int strength, MoveOutcome o)
        {
            var t = Board[p];
            t.Obstacle = kind;
            t.Strength = strength;
            Board[p] = t;
            o.Added.Add(new TileChange { TileId = t.Id, Pos = p, Obstacle = kind, Strength = strength });
        }

        /// <summary>Treasure goes in place of a free tile a few rows up, in a column with a fair path to the floor.</summary>
        void PlaceTreasure(int n, MoveOutcome o)
        {
            waveCells.Clear();
            int top = Board.Height - 3, bottom = Math.Max(2, Board.Height - 6);
            for (int r = bottom; r <= top; r++)
                for (int c = 1; c < Board.Width - 1; c++)
                    if (TileRules.IsFree(Board[c, r]) && FairTreasureColumn(c, r)) waveCells.Add(new GridPos(c, r));
            for (int k = 0; k < n && waveCells.Count > 0; k++)
            {
                int i = waveRng.Next(waveCells.Count);
                var p = waveCells[i];
                waveCells.RemoveAll(q => q.Col == p.Col);
                int replaced = Board[p].Id;
                var tile = Board.NewTreasure();
                Board[p] = tile;
                o.Added.Add(new TileChange { TileId = tile.Id, Pos = p, IsTreasure = true, ReplacedTileId = replaced });
            }
        }

        bool FairTreasureColumn(int c, int r)
        {
            for (int y = r - 1; y >= 0; y--)
            {
                if (!Board.IsOpenCell(c, y) || TileRules.IsGravityBarrier(Board[c, y]) || Board[c, y].Type == CellType.Treasure) return false;
                if (!Board.IsOpenCell(c - 1, y) || !Board.IsOpenCell(c + 1, y)) return false;
            }
            return true;
        }

        void UpdateEndlessStage(MoveOutcome o)
        {
            int stage = setup.Endless.StageForScore(Score);
            if (stage <= Stage) return;
            Stage = stage;
            o.StageUp = true;
            o.NewStage = stage;
            if (setup.Endless.journey != null) pendingWave = true;
            source.Configure(setup.Endless.ColoursForStage(stage), Level.ColourWeights);
        }

        /// <summary>
        /// Campaign (docs/MECHANICS.md §4.3): completing every goal clears the level, but play goes on with the moves
        /// that are left. When they run out the level is won if it was cleared and failed if any goal is still
        /// unfinished, however high the score. A cleared level whose board has no moves left ends as won.
        /// </summary>
        void EvaluateEnd(MoveOutcome o)
        {
            if (Mode == GameMode.Campaign && !Passed && (State == SessionState.Playing || State == SessionState.Stuck) && Goals.AllComplete)
            {
                Passed = true;
                PassedAtMove = MovesMade;
                o.JustPassed = true;
            }
            if (Mode == GameMode.Campaign && Passed && State == SessionState.Stuck)
            {
                State = SessionState.Won;
                EndReason = EndReason.TargetReached;
            }
            // Moves gone with specials left on the board: they go off before the result (RunFinale). Not in the timed
            // Weekly Rush, and only once per run of moves.
            if (State == SessionState.Playing && !inFinale && !finaleDone && HasMoveLimit && MovesLeft <= 0 && Mode != GameMode.WeeklyRush &&
                Board.CountType(CellType.Special) > 0)
            {
                FinalePending = true;
                o.FinalePending = true;
                o.Ended = false;
                o.EndReason = EndReason.None;
                return;
            }
            if (State == SessionState.Playing)
            {
                switch (Mode)
                {
                    case GameMode.Campaign:
                        if (MovesLeft <= 0)
                        {
                            State = Passed ? SessionState.Won : SessionState.Failed;
                            EndReason = Passed ? EndReason.TargetReached : EndReason.OutOfMoves;
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
