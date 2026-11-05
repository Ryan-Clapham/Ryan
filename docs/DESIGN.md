# Colour Cascade: Game Design

This document records how the game works and why. Every number below is a default in
`ColourCascadeConfig.asset` and can be changed without touching code.

## 1. Pillars and look

* **Instantly readable:** one rule (tap a touching group of two or more), with clear feedback.
* **Satisfying:** big groups pay off disproportionately and feel big (sound pitch rises, praise text,
  shockwave rings, screen shake for huge groups).
* **Fair and calm:** no timers except the opt-in Weekly Rush, free reshuffles, no lives, no pay-to-win.
* **Visual identity:** a deep night-violet world (`#120D33` to `#2C1B70` with soft pink and blue glows) so six
  glossy "candy" tiles glow. Coral (`#FF6A4D`) is the primary action colour and gold is reserved for rewards.
  Rounded surfaces, soft shadows and the Fredoka typeface throughout. All art is generated from vector
  shapes in code (`ArtBook.cs`), so it stays crisp at any resolution and needs no imported images.

## 2. Core rules

| Rule | Behaviour |
|---|---|
| Groups | Orthogonally connected tiles of one colour. Diagonals never connect. Minimum size 2. |
| Clearing | The whole group is removed and scores `size² × 10`. A single tile does nothing and costs nothing. |
| Gravity | Tiles fall straight down to fill gaps. |
| Barriers | **Stones** never move, block falling and block connections. **Holes** (voids) are missing cells that shape a board. Both split columns into **sections** that fall and refill independently. A section under a stone refills from just beneath the stone. |
| Refill | Every empty cell is refilled. Early levels use an "assist" chance that copies a neighbouring colour, so friendly groups form more often. |
| Dead board | After every move the engine checks for a legal move. If none exists it reshuffles the existing tiles (stones stay put): up to 12 random shuffles, then a constructive arrangement that always succeeds when success is possible. Score, moves and progress are untouched. If no arrangement can work (practically impossible with real layouts), the run ends as **Board Locked**, which is not counted as a loss. |

Coordinates are **columns × rows**, column 0 on the left, row 0 at the bottom.

### Scoring

| Group | 2 | 3 | 4 | 5 | 6 | 8 | 12 |
|---|---|---|---|---|---|---|---|
| Points | 40 | 90 | 160 | 250 | 360 | 640 | 1,440 |

Praise appears at 5 ("Nice!"), 8 ("Great!"), 12 ("Amazing!") and 16+ ("Colour Cascade!").
Tiles removed by power-ups score a flat 20 each (they are not "groups").

## 3. Campaign

* **Board sizes:** levels 1–3 are 6 × 6, levels 4–6 are 7 × 7, levels 7–120 are 8 × 9. From level 121 sizes and
  shapes vary (§3.5).
* **Levels 1–10 are hand-made** to teach one idea at a time:

| Level | Teaches |
|---|---|
| 1 | Fixed teaching board, three colours, hand pointer on a big group |
| 2 | Bigger groups score more; press-and-hold preview |
| 3 | A fourth colour; target, moves and stars explained |
| 4 | Bigger board (7 × 7) |
| 5 | Tip pointing at the Symbols button |
| 7 | Full-size board (8 × 9) |
| 8 | **Stone barriers** (pillars) with an explanation |
| 9 | Shelves: sections refill on their own |
| 10 | First milestone; **Blast** is introduced with 2 free uses |

* **Generated levels (11 onwards, unlimited):** each level is built from its number with a fixed seed, so it
  never changes between plays or devices. Every third level from 15 adds a fifth colour, all levels from 31
  use five, and from level 81 every fourth level uses six. Stone patterns (pillars, centre block, shelves, walls, steps, posts, scatter) appear from
  level 8 and grow over time; shaped boards with holes appear from level 25. Every fifth level is an open
  "breather" board. Some levels have a dominant or a scarce colour. From level 121 the progression of §3.5 takes over.
* **Targets are calibrated, not guessed (levels 1–20):** when a level is first needed, a reference bot (always
  takes the biggest group) plays it 16 times. The target is a percentage of its median score. These levels are
  unchanged by the campaign mechanics (a test checks them against the pre-mechanics build).
* **From level 21, every level is sized from simulated players** (details in 3.1).
* **Passing and stars:** reaching the target (or meeting every goal) **passes** the level and earns ★, but play
  goes on (and when the moves run out, special tiles still on the board go off before the result: MECHANICS.md §4.4): the moves left raise the score towards ★★ and ★★★, and the stars light up on the game screen as they
  are earned. The player can tap **Finish** at any time after passing (unused moves score nothing).
  Out of moves after passing = won with the stars of the final score; out of moves before = failed, whatever the
  score (docs/MECHANICS.md §4.3). Levels 1–20: ★★ 125% and ★★★ 150% of the target. From 21: see 3.2.
* **Losing:** running out of moves shows "Out of Moves": watch an advert for +10 moves, spend 100 coins for +5, or
  give up (then Try Again, Map or Home). Nothing is ever spent automatically (docs/MONETISATION.md §2.1).
* **Regions:** 20 levels each, cycling through six themes (Sunrise Meadow, Coral Lagoon, Crystal Caverns,
  Ember Canyon, Frost Peaks, Starlight Nebula). After six regions the names continue as "Sunrise Meadow II" etc.
* **Milestones:** every 10th level is a gem-shaped node with a coin bonus; every 20th finishes a region.

### 3.1 Campaign mechanics (from level 21)

The rules of each mechanic are in docs/MECHANICS.md. This section is about where they appear and how levels are
sized.

* **Introductions, one at a time:** ice 21, collect-a-colour goals 25, thick ice 28, rockets 31, bombs 41,
  special combos 47, prisms 51, chains 61, treasure 71, moss 101 (config: campaign → mechanics). Each has a
  hand-made level with a tutorial card and a pointer, then three practice levels focused on it, then it mixes with
  the others. Special tiles are earned from their introduction on: rockets from 31, bombs from 41, prisms from 51.
  Introductions and power-up introductions show their lesson on **every** attempt, replays included.
* **Bands:** Learn 1–20, Develop 21–60, Combine 61–120, Advanced 121–200, Expert 201+. Each band has a move range
  (Develop 16–30, Combine 20–35, Advanced 24–40, Expert 28–45; treasure levels may go 5 over) and a share of
  spare room (20%, 18%, 15%, 11%; lowered from 30/25/20/13% after a player found the 40s easy).
* **Ten-level rhythm:** levels ending 1–3 are generous (+10% moves), 4–5 medium, 6 a breather (+15%, or a plain
  score level), 7–9 harder (−5%), 0 a milestone, often combining two mechanics.
* **Recipes:** a generated level picks one mechanic from those taught so far, and often a second ("free 3 frozen
  tiles and collect 10 blue"): about 45% of levels 29–30, 55% of 31–40, 65% to 120, 70% to 200, nearly every
  milestone; from level 51 some levels have three goals. Partners include special-tile goals from 31 ("make 2
  rockets", "set off 3 specials", "fire a combination") and a score to reach from 35 (MECHANICS.md §4.2). The
  practice levels after each special-tile introduction ask the player to make or set off that special next to a
  goal they know (bombs, prisms and combinations on four-colour boards; some start with one on the board). Amounts
  are set by band (fewer of each when combined). Obstacles are placed with mirror symmetry away from the top row;
  treasure only in open columns; moss in separate patches away from stones and edges. A level whose board would
  need more moves than its band allows is scaled down (80%, 65%, then 50%, never below 4 ice, 3 chains, 3 moss or
  10 tiles to collect) and tried again. Goal levels use at most five colours (four with chains, bombs, prisms or
  combinations).
* **Mixed obstacles (2.1):** each pairing of obstacles first appears on a showcase level once both halves have been
  taught and practised, one pairing at a time: 65 ice + chains, 68 chains on a stone board, 75 treasure + chains,
  79 treasure under ice, 105 moss on a stone board, 108 moss + ice, 112 moss + chains, 115 treasure + moss; then
  three together: 125 ice + chains on stones, 135 treasure + chains + ice, 145 moss + ice + chains, 155 moss +
  chains + treasure. After the chains practice, a mixed level's second goal is twice as likely to be another
  obstacle as a colour, special or score goal, so most obstacle levels from 121 mix two or more kinds (44 of 63,
  stones counted). Treasure never shares a board with stones (it needs open columns). Every showcase level was
  simulated: the Planner passes 83–100% of them.
* **Moves** = what a casual player needs (median of 10 Casual-bot games with plenty of moves) × (1 + the band's
  spare room + the rhythm), within the band's range. Levels with a special-tile goal are sized from the Planner
  bot instead (its 75th-percentile game, +30%; +50% for combinations), since a casual player never aims for big
  groups; treasure levels from the Casual bot's 75th-percentile game, since treasure depends a lot on how tiles
  fall. Built-in introductions keep their hand-set moves unless calibration says more are needed.
* **Stars:** see 3.2. Score levels from 21 on take their target from Casual-bot scores at a percentile set by band
  and rhythm.
* **Same on every device:** generation and calibration use fixed seeds and integer arithmetic only (generation
  time: 3.2).

**Simulation results** (docs/campaign-simulation.csv, update 2.1; 12 Casual and 12 Planner games per level on
fresh boards, every move played, specials left at the end going off; "Casual" picks groups at random weighted by
size, roughly a relaxed beginner who never aims for specials; "Planner" is goal-aware with one move of lookahead):

| Band | Levels | Casual pass | Planner pass | Planner ★★ / ★★★ | Casual ★★ / ★★★ | Average moves |
|---|---|---|---|---|---|---|
| Learn | 1–20 | 83% | 100% | 100% / 99% | 68% / 52% | 17.5 |
| Develop | 21–60 | 68% | 94% | 78% / 39% | 17% / 4% | 23.9 |
| Combine | 61–120 | 64% | 94% | 78% / 49% | 17% / 5% | 29.2 |
| Advanced | 121–200 | 64% | 94% | 77% / 41% | 15% / 3% | 34.1 |

These are the figures with 5 × 5 bombs, connected groups of specials, the end-of-level groups and no spare-move
bonus (update 2.1 clarifications). The first 2.1 build (4 × 4 bombs, pairs only, spare-move bonus): Develop 67% /
94%, Combine 65% / 94%, Advanced 64% / 93%, with Planner ★★★ 37% / 43% / 37%. Before 2.1 (specials at 20 a tile, 3 × 3 bombs, no stone breaking or finale): Develop 62% / 94%, Combine 60% / 94%,
Advanced 56% / 92%. Stronger specials make levels a little easier to pass; stars did not get easier, because their
thresholds are recalibrated from the same bot games with the new scoring (3.2). Before the play-on update: Develop
76% / 96%, Combine 66% / 95%, Advanced 57% / 89%.

Levels 21–200: 58 with one goal, 81 with two, 13 with three, 28 score levels; 27 have a special-tile goal and 11 a
score to reach as well as another goal (3.3).

Pass rates by goal kind (levels 21–200; a level counts under every kind of goal it has):

| Goal kind | Levels | Casual pass | Planner pass | Planner ★★★ |
|---|---|---|---|---|
| Score only | 28 | 67% | 98% | 35% |
| Chains | 56 | 66% | 96% | 50% |
| Collect a colour | 30 | 66% | 96% | 38% |
| Moss | 26 | 61% | 93% | 49% |
| Ice | 70 | 60% | 92% | 42% |
| Special tiles (make, fire, combine) | 27 | 60% | 91% | 42% |
| Treasure | 39 | 70% | 90% | 48% |

**Flagged levels (43 of 200):** the report flags a level as *too hard* when the Casual pass rate is below the band's
floor (Learn 75%, Develop 55%, Combine 40%, Advanced 30%, introductions 70%), *too easy* above its ceiling,
*unfair?* when even the Planner passes under 80%, *goals trivial* when the Planner meets them by move 2, and
*3 stars rare* when it never gets three. "Too hard" is expected on special-tile levels (the Casual bot never builds
specials). 13 levels are *unfair?*, 11 of them at exactly 9 of 12 Planner games (75%); the clearly hard ones are
99 (treasure under ice with prisms, 58%) and 151 (one treasure on a 45-move prism level, 50%). Level 128's moss goal
is still trivial (its board only fits two patches), and 106 and 130 rarely reach three stars. The full list is in the CSV; tune a level by adding an
override in the config.

Changes made because of the simulation: specials at 6/9/12 tiles instead of 7/10/14; moss falls with its tile,
is scraped off by a neighbouring clear and never grows two moves in a row; treasure only in open interior
columns; fewer chains, and four colours on chain levels until 121; score levels from 21 calibrated against the
Casual bot; gentler introductions for treasure, chains, prisms and colour goals.

### 3.2 Stars and difficulty (play on after passing)

* **What stars measure:** ★ passing (every goal, the target included); ★★ and ★★★ the final score, with play going
  on after the pass. A player who passes in one move with 17 to spare keeps playing those 17 moves for stars. (This
  is what went wrong on level 47: the level used to end at the pass, 1,160 points + 17 × 70 spare-move bonus =
  2,350, under its two-star 2,500, so one star. The bonus has since been removed altogether.)
* **Calibration (every level from 21):** bots play the level to its last move, as a player chasing stars would.
  ★★ = halfway between a casual player's median final score (Casual bot, 16 games) and the 35th percentile of a
  thoughtful player's (Planner bot, 8 games); ★★★ = the Planner's 60th percentile, never more than 1.45 × ★★.
  Medians, not extremes, so one lucky calibration game can't put three stars out of reach. Goal levels show a
  target of ¾ of ★★; levels with a score goal show that score.
* **No spare-move bonus (2.1):** unused moves score nothing. Stars depend only on the mandatory goals (★) and the
  final score against the two thresholds (★★, ★★★); finishing early just stops the level.
* **"...and reach a score":** the 20th percentile of the final scores of Casual games that meet the other goals.
* **No one-move levels:** the combinations introduction (47) asks for a combination and 14 frozen tiles, 4 of them
  out of the starting rockets' reach; thick-ice practice always has thick ice; moss comes in separate patches;
  scaled-down goals keep a working minimum. A test checks that the Planner needs more than two moves to pass every
  level 21–60, and that its best of a few games reaches ★★★ on most of them and gets within 80% on all.
* **Level generation time** rose from about 6 ms to about 17 ms on average (max about 100 ms) in the .NET harness,
  because calibration now plays whole games. Levels are built when first needed (level card, game start) and then
  cached; on a phone this may show as a short pause the first time a level card opens. Not measured on a device.
* **After the 2.1 scoring changes:** nothing was raised by a fixed percentage. Every level's target (score levels),
  two- and three-star scores come from bot games played with the real rules, so they followed the new special
  scoring, 5 × 5 bombs, groups of specials, stone breaking, the end-of-level specials and the removal of the
  spare-move bonus automatically: the Planner still reaches ★★ in 77–78% of games and ★★★ in 39–49%. A level's realistic scoring potential (board
  size, moves, how often big groups and specials form, obstacle density, goals, combinations) is what those games
  measure. Power-ups are left out on purpose, so stars never depend on purchases. Levels 1–20 have no specials and
  are unchanged.
* **Spare-move bonus: removed (2.1 clarifications).** It used to pay each unused move on Finish (half a
  thoughtful player's average move). Now nothing is paid for unused moves: the calculation, its config
  (leftoverBonusPermille, spareMoveBonusPermille), the level's bonus per move and the result screen's "Spare moves"
  line are gone, and star calibration plays every move. Final score = score. Saves are unaffected (the bonus was
  never stored separately; best scores already saved stay as they are).

### 3.3 Level types (2.1 review)

* **Objectives only** (141 of levels 21–200): pass by completing every goal; no score is required. Score still
  decides ★★ and ★★★. Most obstacle and special-tile levels are this type.
* **Score** (28): pass by reaching the target. Kept as breathers (every level ending in 6, and a share of the rest)
  and where the board itself is the challenge (stones, shapes).
* **Combined** (11): goals **and** a score to reach ("collect 2 treasures and score 3,400"), from level 35 and more
  often later (the score partner's weight rises from 12 to 18 at 61 and 30 at 121). The score is set so that most
  casual games that meet the other goals also reach it.
* The level card says which: its goals (with "Score X" listed as a goal when it is required) and what each star needs.

### 3.4 Bomb size: the 5 × 5 test (2.1 clarifications)

The bomb was made 5 × 5, centred on the bomb (cut off at the board's edges), and measured before deciding whether
to keep it. The figures come from a one-off harness program (not committed) using the same calibration bots on the
real level specs, with every other 2.1 rule the same. Share of the board one bomb reaches, averaged over every cell
it could sit on (best cell in brackets):

| Board | 3 × 3 | 4 × 4 | 5 × 5 |
|---|---|---|---|
| 6 × 6 | 20% | 44% | 44% (69%) |
| 7 × 7 | 15% | 33% | 35% (51%) |
| 8 × 9 | 11% | 22% | 26% (35%) |
| 9 × 10 | 9% | 18% | 21% (28%) |

Campaign levels 41–100 (every bomb level and the first prism levels; stars recalibrated for each size, Planner and
Casual bots, about 1.3 bombs fired per game):

| Bomb | Planner pass | Planner ★★★ | Casual pass | Tiles per single bomb | Points per single bomb |
|---|---|---|---|---|---|
| 3 × 3 | 92% | 45% | 63% | 5.8 | 292 |
| 4 × 4 | 93% | 46% | 65% | 13.1 | 657 |
| 5 × 5 | 92% | 46% | 65% | 16.9 | 847 |

On a 6 × 6 board (20 moves, Planner) a 5 × 5 bomb clears 34% of the board each time, but only 0.7 form per game
and specials supply about 30% of the points. **Decision: keep 5 × 5.** It does not change pass or star rates
(stars are recalibrated from games with it), it reaches clearly more than a rocket's single line, and no campaign
level with bombs is smaller than 8 × 9, where it reaches a quarter of the board. On boards of 6 × 6 or smaller it is
strong (a centred bomb reaches over two thirds of the board), so a future small-board mode with bombs should test a
smaller size first (config: scoring → specials → bombSize; odd sizes are centred, even sizes keep the old
slide-inwards square).

### 3.5 The progression from level 121 and board shapes (update 3.2)

**Why it changed.** An audit of levels 200–400 (the generator before 3.2) found that later levels asked for no more
than level 201 did, and often less:

* **One difficulty band from level 201 on**, so levels 201 and 500 had the same parameters, and the last new mixture
  of obstacles arrived at level 155.
* **Content shrank to fit a move cap.** Moves were sized from a casual (nearly random) bot; whenever it would need
  more than 45, the level's goals were cut to 80%, 65%, then 50%, and two or three kinds of goal were cut to 65% or
  50% of a single one. So 94–100% of treasure levels had a single treasure, chains averaged 3 and moss 3.
* **Moves were sized for a random player.** 41–48% of levels in 201–500 had 44–50 moves, while a thoughtful player
  (Planner bot) passed them in about 11–30.
* **One board.** Every level from 7 to 500 was 8 × 9, with seven stone patterns and three small cut-outs whose
  intrusion stopped growing at level 53: 15–19 distinct board geometries per 100 levels.
* **What difficulty there was came from luck.** 65 of levels 121–500 failed a thoughtful player more than one game
  in five (two-link chains in edge columns, treasure stalls), while 18–27% were score-only.

Levels 1–120 are unchanged (a test pins their level files to the build before 3.2). From level 121
(`CampaignConfig.progressionFromLevel`; 0 switches the progression off and gives the old generator back) levels are
built by `CampaignProgression`, `BoardShapes` and `CampaignPlacement`:

**Difficulty is several things, not one number.** Each level gets an **intensity** (5–100): a base rising from 38 at
level 121 to 75 by level 450; the ten-level rhythm (levels ending 1–3 easier, 6 a **breather**, 7–9 harder, 0 a
milestone); the first levels of a region a little easier and its finale harder; and a little noise. The intensity
then sets, together:

| Factor | How it grows |
|---|---|
| Kinds of goal | One on gentle levels; two from the middle (72%–96% of levels as intensity rises); two or three on the hardest (three up to 70% of the time). Breathers are score levels or one light goal (60% amounts). |
| Combinations | The second goal is usually another obstacle (mixed boards); a third is often about special tiles or a score. Pairs that matter to each other are favoured (treasure with ice or chains, chains with moss). |
| Amounts | For a single goal about 7–22 ice, 4–12 chains, 4–10 moss seeds, 18–48 of a colour, 1–4 treasure, scaled to the board's size and shape; a little less of each when goals are combined (80%, 65% for three), never down to a token amount. |
| Obstacle strength | Thick ice 30% → 60% of the ice; chains of 2 links from intensity 30, 3 from 65 (never 3 in an edge column). |
| Dependencies | From intensity 40, treasure combined with ice or chains starts on a frozen or chained tile, which holds it up until freed. |
| Treasure distance | Starts 2–4 rows above its floor on gentle levels, up to 4–8 on the hardest; one per section on split boards. |
| Specials | Ice is usually laid in whole rows when a level asks for rockets and in blocks when it asks for bombs or any special; combination goals only on unsplit boards. |
| Colours | Five as a rule; four for long chains, big specials, lots of treasure and boards whose smallest section is under 40 cells; six now and then on later open levels. |
| Board shape | Introduced one family at a time (below); the slower shapes get fewer obstacles (Islands 60%, Three sections 70%, Passage, Hollow and stone boards 85%). |
| Moves | What a thoughtful player needs in three of four of the games it passes, plus 50% (gentle) down to 20% (hardest) spare, more for breathers, new boards and treasure: at least 4 and at most 14 more than needed (more if the fairness check needs them), within 19–40 moves (121–160) up to 22–46 (from 281); treasure adds 3. |

**Every level is checked before it is used.** The generator places the goals (with `CampaignPlacement`, which never
puts an obstacle in a dead-end pocket, gives every chained tile room for a neighbour of its colour and starts treasure
only where the last tile under it can be matched on both sides), sizes the moves and then plays it: a thoughtful
player must pass 10 of 12 games (11 for breathers and new boards; 9 with treasure) **and** 9 of 12 more on other
boards. A level that fails is placed again, given more moves within its band, then made 80%, 65% or 50% as big; a
level needing more than 30% over its budget (12 + intensity / 4 moves of thoughtful play) is made smaller first; one
cleared in a handful of moves is made bigger. If no version passes, the same goals are tried on a plain board, and failing
that the gentlest version is kept with the band's most moves.

**Board shapes.** All are made of cells the engine already has (MECHANICS.md §3.2): open cells, stones (rockets, bombs
and Tile Breaker break them) and gaps (no cell). Every shape is at most 10 × 11, so cells stay touch-sized on every
supported screen, every separate section has at least 12 cells, and no open cell is a dead end.

| From level | Family | What makes it different |
|---|---|---|
| 121 | Varied rectangles | 7 × 9 to 9 × 10; and the stone patterns as before |
| 131 | Hollow board | A hole (or two windows) in the middle: groups go round it; the columns under it fill from beneath it |
| 151 | Tall board | 6 or 7 wide, 10 or 11 tall |
| 161 | Wide board | 9 or 10 wide, 7 or 8 tall |
| 171 | Twin towers | Two sections side by side; each falls and fills on its own; one may be shorter |
| 191 | Upper and lower | Two halves split by a gap; the lower one fills from beneath the gap; treasure lands on the gap |
| 211 | Break the wall | Two halves split by a wall of stones, down the middle or across; a rocket or bomb opens it and tiles pour through |
| 231 | Narrow passage | Two halves joined by a one- or two-cell opening (in the middle, at a side, or a bridge near the floor) |
| 251 | Lopsided board | An L, steps, or an uneven floor where treasure lands on the steps |
| 271 | Three sections | Three floors; two towers over a wide section; a wide section over two towers |
| 291 | Islands | Four separate sections of 16–25 cells |
| 141 | Cut corners (3.3) | A rectangle with its corners cut away (square, stepped or a single cut): the corner columns are short |
| 311 | T-shaped board (3.3) | A wide top over a narrow stem, or upside down: the cells under the arms are short columns that fill on their own |
| 331 | Cross (3.3) | Four arms round a middle, with three-wide or four-wide arms |
| 351 | Diamond (3.3) | Every column a different height (a diamond, or an octagon) |

Each family's first level is its **introduction**: one familiar goal, gently sized, with "New board: ..." on the level
card. **Since 3.3 it comes back about 4 and 13 levels later** (a "return": a little gentler, intensity −10; moved on a
level or two if that level is a breather, another board's introduction or return, or one of the obstacle showcase
levels, which keep their own boards), and comes up more often for twenty levels; the most demanding shapes grow more
common after 300. (With sixteen families a random draw alone left the T, the cross and the diamond unseen for 40
levels after their introductions.) An introduction or a return keeps its board: if its goals can't be made fair
there, the goal becomes frozen tiles (which play well on every shape) rather than the board becoming a rectangle;
before this, the diamond's introduction asked for treasure dug down its three-wide bottom, failed the check and fell
back to a plain board. Each ten levels follow a plan, so a board never repeats on the next level, a family uses its
next variant each time it appears, and every main mechanic appears twice in every ten levels (never twice in a row).
The level card names the board ("Twin towers · Ice + Chains").

**Shapes are data (3.3).** Every templated shape is a few rows of text in `BoardShapeLibrary` ('.' a cell, 'x' a gap,
'#' a stone), the notation of level files; rectangles, tall, wide and stone boards are generated. More shapes can be
added without code in the config (campaign → shapes → extraShapes: a family and its rows); each is checked when the
game starts (fair: sections of 12+ cells, no dead ends, at most 10 × 11) and an unfair one is ignored. A config shape
joins its family's variants wherever the family is used: campaign levels, advanced Daily and Weekly challenges and
Endless runs. A new family needs only an enum value, its level in the schedule, a name and its rows. Gravity,
matching, refills, obstacles, treasure, specials, power-ups and goals need nothing per shape: they already work on
gaps and stones (MECHANICS.md §3.2).

**The board's panel follows its shape (3.3).** Before 3.3 a shaped board sat on the same rectangle as a plain one, so
a split board's gap showed as an empty strip inside the panel and holes as dark squares. Now a board with gaps gets a
panel built from its cells (`BoardSilhouette`: a distance transform of the cell mask, grown a little past the cells
with rounded outer corners, a faint outline and a soft shadow, all in the game's colours): each section of a split
board has its own panel with the background showing between them, holes and cut corners are really open, and the
panel is rebuilt whenever the cell size changes (screen size or orientation). Plain and stone boards keep the
original rectangle. The art preview renders every shape this way (`Sim -- art`: mock_shape_*.png).

**Specials and power-ups on shaped boards** act on their real area, as everywhere: a rocket's line crosses gaps
(nothing there to clear) and breaks the stones in it; a bomb's 5 × 5 square reaches both sides of a gap; a prism and
Colour Burst clear their colour in every section; Blast clears only cells that exist; nothing is ever swapped into a
gap.

**Results** (the same tools before and after: `Sim -- campaign-shape 1 500 100 [legacy]`, and `Sim -- campaign 121 500
20 <csv> [legacy]`, 20 games per level per bot on boards the generator never saw):

| Levels | Goals per level | Kinds of objective | Obstacles and treasure | Treasure levels with one | Moves (44+) | Board geometries | Shaped boards |
|---|---|---|---|---|---|---|---|
| 101–200 before / after | 1.66 / 1.72 | 1.36 / 1.52 | 3.8 / 5.8 | 96% / 84% | 32.6 (19%) / 29.8 (2%) | 15 / 35 | 9% / 21% |
| 201–300 | 1.78 / 1.90 | 1.39 / 1.65 | 3.9 / 7.0 | 94% / 62% | 38.1 (48%) / 32.9 (16%) | 17 / 43 | 9% / 43% |
| 301–400 | 1.78 / 2.21 | 1.38 / 1.75 | 4.5 / 7.7 | 100% / 47% | 37.4 (41%) / 35.4 (34%) | 19 / 38 | 16% / 64% |
| 401–500 | 1.65 / 2.40 | 1.23 / 1.88 | 3.3 / 8.2 | 100% / 42% | 36.2 (43%) / 37.2 (33%) | 15 / 42 | 13% / 53% |

| Tier (levels) | Thoughtful player passes, before / after | Casual player passes, before / after |
|---|---|---|
| Advanced (121–160) | 94% / 89% | 65% / 44% |
| Skilled (161–220) | 91% / 92% | 59% / 48% |
| Expert (221–280) | 87% / 89% | 52% / 38% |
| Master (281–350) | 87% / 89% | 52% / 43% |
| Grandmaster (351–450) | 87% / 90% | 52% / 41% |
| Legend (451–500) | 86% / 88% | 45% / 39% |

Later levels ask for more and a casual player passes fewer of them, while a thoughtful player passes as many or more,
so the extra difficulty is planning, not luck. Levels a thoughtful player failed more than one game in five fell from
65 to 56. Score-only levels fell from 17–27% to 4–7%. Levels 323–329, from the playtest that prompted the update:

| Level | Before 3.2 | After |
|---|---|---|
| 323 | 8 × 9, 50 moves: one treasure | Lopsided 9 × 10, 24 moves: 6 ice and 3 chains |
| 324 | 8 × 9, 44 moves: 3 moss, 3 chains | Three floors 7 × 11, 22 moves: treasure and 10 of a colour |
| 325 | 8 × 9, 50 moves: one treasure and a score | Twin towers 10 × 9, 22 moves: 4 moss and a score |
| 326 | 8 × 9, 20 moves: a score (breather) | 9 × 8, 22 moves: 7 ice (breather) |
| 327 | 8 × 9, 22 moves: a score | Three sections 9 × 10, 22 moves: 3 moss and 18 of a colour |
| 328 | 8 × 9, 50 moves: one treasure and 3 moss | Wide 10 × 7, 37 moves: a treasure and a score |
| 329 | 8 × 9, 20 moves: a score | Narrow passage 9 × 10, 46 moves: 5 chains, a rocket and 10,000 points |

Other examples: level 350 went from 7 ice and 18 of a colour to 16 ice, setting off 4 specials and 12,000 points on a
tall board; level 400 (a region finale) from a 2,000-point score level to 2 treasures and 17 ice on a lopsided board.

**Update 3.3** added the four new families, the returns and the keep-the-board rule, so levels from 135 on changed
(levels 1–134 are unchanged; 1–120 are pinned by a test). What every level contains (`Sim -- campaign-shape 1 500 100`):

| Levels | Shaped boards, 3.2 / 3.3 | Board geometries, 3.2 / 3.3 | Families seen |
|---|---|---|---|
| 101–200 | 21% / 30% | 35 / 37 | Hollow, tall, wide, twin towers, upper and lower, cut corners |
| 201–300 | 43% / 63% | 43 / 47 | all twelve of 3.2, plus cut corners |
| 301–400 | 64% / 66% | 38 / 47 | all sixteen (T 7 levels, cross 4, diamond 3 in 311–400) |
| 401–500 | 53% / 69% | 42 / 46 | all sixteen |

Goals, kinds of objective, obstacles and moves per range are as in 3.2 (within ±0.1 goals and ±1 move per level).

**Limits.** The fairness check uses the Planner bot, which looks one move ahead. On treasure levels it sometimes stalls
with the last tile under a treasure unmatched (a player can change the tiles beside it, or use Tile Breaker), so
treasure levels are allowed one more stalled game in twelve and average 85% for it (other goals 87–92%). Levels are
built the first time they are needed: about 50–90 ms each in the harness (up to about 300 ms), several times that on a
phone, so the game builds the current level while the art loads and the next level while one is played, on a
background thread (`CampaignCatalog.Prewarm`).

## 4. Endless

* A **fresh random board every run** (8 × 9). No timer.
* Start with **30 moves**. Each group costs one move; groups of 6+ give +1 move, 9+ give +2 and 13+ give +3; each
  treasure brought down gives +3; breathers give +5.
* **The stage journey (2.1; reordered in 3.3):** every run walks through the game's mechanics, one new thing per
  stage, so the mode feels like a journey rather than the same board with a bigger number (config: endless →
  journey; rules in MECHANICS.md §9):

| Stage | Score | What it brings |
|---|---|---|
| 1 | 0 | Warm-up: ordinary matching, 4 colours |
| 2 | 1,200 | **Rockets** (3.3): big groups make them from now on; one is placed to try |
| 3 | 2,800 | Stones appear (they hold tiles up; rockets and bombs break them) |
| 4 | 4,800 | Frozen tiles (a fifth colour arrives) |
| 5 | 7,200 | Breather: +5 moves, nothing new |
| 6 | 10,000 | Chains |
| 7 | 13,500 | Bombs (one is placed) |
| 8 | 17,500 | Treasure |
| 9 | 22,000 | Breather |
| 10 | 27,000 | Moss |
| 11 | 32,500 | Prisms (one is placed) |
| 12+ | 38,500, then +6,000 a stage | Mixes of everything met so far: 2 kinds at a time, 3 from 16, 4 from 20, each denser (+8% a stage, up to double); a breather every fourth stage (15, 19, 23...); a sixth colour from 12 |

* **Why players saw no specials (fixed in 3.3).** Before 3.3 rockets and bombs arrived together at stage 6. Most runs
  barely reach it, and a config asset saved before update 2.1 (as the project's was) kept the old stage scores (26,000
  for stage 6, with six colours from stage 6), so stage 6 was out of reach and specials never appeared at all. Now
  rockets come at stage 2 and bombs at 7, and an old asset is brought up to date: stage scores, stage colours and the
  journey move to the current values only where they still equal an old default, so values a designer changed are
  kept (`EndlessTuning.Upgrade`). The editor does this to the asset (and saves it) when the project opens, logging
  what it moved; the game also applies it to its in-memory copy when it loads the config, so a build made from an
  asset that was never upgraded still plays the 3.3 journey. Measured with the same 30 runs per
  bot (`Sim -- modes` and a comparison run):

| Setup | Casual: runs with a special / per run | Mixed: runs with a special / per run |
|---|---|---|
| Asset saved before 2.1, 3.2 journey | 0 / 30, 0.0 | 0 / 30, 0.0 |
| 3.2 defaults | 4 / 30, 0.2 | 9 / 30, 0.7 |
| 3.3 | 30 / 30, 4.6 | 30 / 30, 7.0 (Planner 18.2) |

* **Board shapes (3.3).** Once the player's best run has reached stage 6 (endless → shapedRunsFromBestStage), about
  two runs in three start on a shape instead of the 8 × 9 rectangle: tall, wide, hollow or cut corners; from best
  stage 9 also twin towers, upper and lower, lopsided and a stone wall; from 12 also a narrow passage, T, cross and
  diamond. So progression adds new boards as well as obstacles, mixes and denser waves, not just bigger numbers. The
  shape comes from the run's seed (named on the run's opening banner, e.g. "Twin towers · 30 moves"); every stage wave places its
  stones, obstacles and specials only on the shape's cells, by the same rules as on the rectangle.
* Each new stage is announced on a banner ("Stage 6 · New: chains"; "Stage 12 · Mix: frozen tiles and moss") and a
  mechanic seen for the first time outside the campaign gets its tutorial card. Mixing stages pick their mechanics,
  and every wave its cells, from the run's own seed, so runs differ. Waves never cover more than a third of the
  board, so later stages are harder (denser, more combined) without becoming unwinnable.
* The run ends when moves run out (specials left on the board go off first). Bests are kept for score, biggest
  group, moves and stage; the Endless level (one per stage reached in any run) is kept as a lifetime figure and no
  longer gates anything. The mode card shows the best stage and the next new thing; the result shows the stage
  reached (or a new best) and what the next introduction is.
* Coins: one-time milestone rewards the first time any run reaches 2,500 / 5,000 / 10,000 / 20,000 / 40,000
  points (5/10/15/25/40 since 3.1). Endless never grants campaign rewards and never unlocks levels. Stage scores,
  milestones and records are unchanged by 3.3.
* **Simulation** (`Sim -- modes`, 30 runs per bot): median stage 5 (Casual bot), 6 (Mixed), 9 (Planner); the best
  10% of Planner runs reach 11 (best 13), into the mixing stages; runs last about 44–66 moves with 11–16 obstacles,
  stones, treasures and specials added. Before 2.1 runs stopped around stage 4–5 under the old stage scores (2,000 /
  5,000 / 10,000 / 17,000 / 26,000 / 38,000) and mechanics only arrived after many runs (Endless levels 5–40).

## 5. Daily Challenge

* One puzzle per **UTC calendar day**, built from the date (`daily:yyyy-MM-dd`) with a deterministic
  generator, so every player and every reopen on that date gets the same board **and** the same refills for
  the same moves.
* **20 moves, no timer, every move is played** (it is a score attack, unlike the campaign). Completing means
  reaching the target. Stars at the target, ★★ and ★★★ thresholds calibrated against a reference bot.
* First completion each day pays **10 coins**; three stars pays **+5** once (update 3.1; 15 / +5 before it). Replays
  are unlimited and update the best score but never pay twice.
* No power-ups and no shop during a run, so the challenge is identical for everyone.
* **Tile mechanics:** each date has a plan of one mechanic, or a pair known to play fairly (chains only alone). A
  player gets the parts of it they have completed in the campaign, so everyone with the same progress plays the same
  puzzle, and a new player plays the original daily. Obstacles add goals (shown in the goals panel, with the target
  as the first goal); the day is complete when the target and every goal are met. Days with goals have a target 12%
  lower. Simulation (60 days): Casual-bot success 47% on plain days, 50–56% with mechanics; Planner 90–100%.
* **Advanced Daily Challenge (update 3.3).** From campaign level 121 (daily → advancedFromLevel; 0 turns it off) the
  day's puzzle is advanced, built with the campaign's own systems (rules in MECHANICS.md §9):
  * **A board shape the player has played**, drawn from the date: any family whose first campaign level is behind
    them, mostly shaped (plain and stone boards are drawn about half as often as the others; the slowest shapes,
    three sections and islands, less often too).
  * **The target plus two objectives that go together**: the day's planned obstacle and a partner the campaign pairs
    with it (another obstacle, a special-tile goal, or treasure held up by ice or chains on harder days), sized by
    the campaign's rules for the board's size and shape, at an intensity that rises with the shapes met (about 38 at
    level 121 to 64 once every shape is met, a little either way by the date). Colours follow the campaign's rule
    (four with multi-link chains, big specials, lots of treasure or small sections).
  * **Moves from a thoughtful player**: what the Planner bot needs in three games of four, plus room, within 20–30
    (daily → advancedMaxMoves). Too long for 30 moves: a smaller version. The target is 68% of a Mixed bot's score
    (the goal-day easing), two and three stars at 105% and 125%, as before.
  * **A fairness check**: a thoughtful player completes the target and both objectives in 9 of 12 games with varied
    refills, and again in 8 of 12 more (one fewer with treasure); each size is placed twice before a smaller one is
    tried. If nothing passes, the day falls back to the original daily, so nobody is ever given an unfair puzzle.
  * Everyone with the same unlocks and the same shapes met gets the same puzzle and refills; records, best scores,
    replays and the 10 + 5 coin rewards (once per day) are keyed by the date as before. A player below level 121 gets
    exactly the original daily (pinned by a fingerprint test). Built in the background when the game starts and
    whenever the home screen opens (about 0.06 s, at most 0.15 s, in the harness), so opening it never waits.
  * Simulation (`Sim -- advanced`, 30 days at each of levels 150 / 250 / 400, 6 games per bot per day with varied
    refills): 28 / 30 / 29 of 30 days advanced (the rest fell back), 2.9–3.0 goals and 24–26 moves a day, on 12–23
    different board geometries; success Casual 34% / 33% / 36%, Mixed 44% / 37% / 34%, thoughtful (Planner) 82% / 83% /
    83%, against 50% and 90% on the original days: harder, and the difficulty is planning rather than luck.
* **Limitation:** the date comes from the device clock. A player who changes their clock can view other days'
  puzzles. Mitigation: rewards are refused for any date earlier than the latest one the player has opened.
  Moving the clock forward is not detected. All time reads go through `IClock`, so a server time source can
  replace it later without touching the rules.

## 6. Weekly Rush (the only timed mode)

* A **60-second** score attack on the week's board. Weeks follow **ISO-8601 in UTC** (Monday 00:00 UTC to the
  next Monday; key `yyyy-Www`, e.g. `2026-W43`).
* A 3-2-1-GO countdown, then the timer runs. Unlimited moves; play stops instantly at zero.
* **Pausing** (button, Android Back or leaving the app) stops the timer and **hides the board**, so pausing
  can't be used to plan. Animations run about 35% faster than elsewhere so the clock feels fair.
* Medal tiers: **Bronze 2,500 (+10), Silver 5,000 (+15), Gold 8,000 (+25)**, each paid once per week (unchanged by
  update 3.1).
  Bots score about 5,000–6,200 in 40 moves and 7,900–9,700 in 60, so Gold rewards fast, good play.
* No power-ups, no shop during the run.
* **Tile mechanics:** each week has a theme (frozen, rocket, chain, bomb, treasure, prism or moss week) and up to two
  more of the player's campaign-unlocked mechanics, so weekly boards combine more than daily ones. Special-tile weeks
  start with a few of that special on the board. The medal scores are scaled by how much the mechanics change a
  45-move bot score, and rewards use the medal scores of the board played. Simulation (16 weeks): with everything
  unlocked the medal scores were 1.42× the plain ones on average (1.19× before 5 × 5 bombs and groups of
  specials), and the bot earned bronze, silver and gold at close to the plain boards' rates (100% / 97% / 48%
  against 100% / 98% / 42%). It never had a spare-move bonus.
* **Specials at the end (fixed in update 3.3).** Before 3.3 the run ended the instant the clock reached zero: the
  end-of-level finale was written for runs of moves, and the timer path skipped it, so specials still on the board
  were lost. Now, when the time is up with specials on the board, play stops (no taps, the clock stays at zero),
  "Time's up! Your specials go off" is shown, they go off exactly as in the other modes (connected ones together,
  each once, chain reactions included), their points appear as "Specials at the end", and only then does the run end
  (reason: time's up). The week's best and the medals use the full score. If the screen is left while they wait, the
  result still sets them off and records it. Tested on plain and advanced Weekly boards (MECHANICS.md §4.4).
* **Advanced Weekly Rush (update 3.3).** From campaign level 121 (rush → advancedFromLevel) the week's board is
  harder than the Daily's:
  * **Split and irregular shapes** the player has met (hollow, cut corners, twin towers, upper and lower, a stone
    wall, a narrow passage, lopsided, T, cross, diamond, three sections, islands; stone boards until the first is
    met), drawn from the week.
  * **The theme plus up to two more mechanics**, placed by the campaign's rules, and **up to three objectives** (free
    the ice, break the chains, clear the moss, bring down treasure; on specials weeks, set off that special, with a
    few of them on the board at the start).
  * **Objectives add points, they never end the run**: each pays rush → objectiveBonus (1,000) once, the first time
    it is complete ("Objective complete! +1,000"; "Objectives" on the result). They are sized so a thoughtful player
    completes them all within a typical run (45 moves) in 6 games of 8, trying a bigger version first. The goals
    panel shows them, with the medals under the score.
  * **Medals stay score-based**, whether or not the objectives are finished: the medal scores move with the board,
    bonuses included, so they are earned at about the plain board's rates; the range is 0.7× to 4× (shaped boards
    with specials and bonuses score far more than a plain one: in a first trial at the old 1.8× cap, the average bot
    took gold in 83–88% of runs). Rewards stay once per medal per week, keyed by the week.
  * A player below level 121 keeps exactly the original board (pinned by a fingerprint test).
  * Simulation (`Sim -- advanced`, 12 weeks at each of levels 150 / 250 / 400, 8 runs of 45 moves per bot): about
    2.6 objectives a week; the Mixed bot earns bronze 100%, silver 91–98%, gold 38–40% and completes 62–69% of the
    objectives; a thoughtful player completes 87–90% and earns gold 75–100%.

## 7. Power-ups

Bought with coins in the Shop (or from the in-game bar) and stored in an inventory. Using one never costs a
move. A use is only consumed if the effect actually happens; an invalid target costs nothing.

**Five power-ups since 3.0.1.** The sixth, **Extra Moves** (+3 moves for 200 coins, from level 50), was retired:
Out of Moves now offers +10 moves for an advert or +5 for 100 coins, so it was redundant and poor value.
* It is in no list, bar, shop page or introduction, even if an older config asset still lists it.
* Level 50 no longer introduces a power-up; levels themselves are unchanged.
* A player who still owned Extra Moves gets 200 coins back for each, once, when the save loads, with a message and a
  ledger line. Unused free uses lapse.
* Saves that mention it load as before (`RetiredPowerUps`).

| Power-up | Price (three-pack, 3.1) | Unlocks | Free uses on its level | How it works |
|---|---|---|---|---|
| Blast | 100 (270) | Level 10 | 2 | Tap any tile: every colour tile in the 3 × 3 area is cleared (clipped at the edges). Stones and holes are not affected (only specials and Tile Breaker break stones). Must clear at least one tile. |
| Tile Breaker | 125 (340) | Level 20 | 2 | Tap one tile **or one stone** to break it (a stone scores 100). Rockets and bombs break stones too (2.1). Holes can't be broken. |
| Swap | 75 (200) | Level 30 | 2 | Tap a tile, then a neighbouring tile of a different colour; they swap places. Nothing is cleared, so the board stays valid. |
| Colour Shift | 150 (400) | Level 40 | 2 | Tap a tile, then pick any other colour in play from the palette. |
| Colour Burst | 250 (675) | Level 60 | 1 | Tap a tile: every tile of that colour is removed. |

* **Introduction (2.1):** every attempt at a power-up's unlock level (first play, retry or replay, also after
  the game is closed and reopened) shows its card ("New Power-up!", or "Power-up practice" on a replay), points at
  its button and **lends** its free uses for that attempt: they are spent before the player's own and are never
  saved. On the **first win** of that level, the lent uses left over move into the normal inventory, once ever
  (the result says "Kept 1 free Colour Burst"); replays lend them again but keep nothing, so replaying can't
  stockpile power-ups (and replays pay no new coins). The loan ends with the run: the next level's bar shows only
  power-ups the player owns (a bug let unusable "Free" badges linger). A level that ran out of moves keeps its
  loan until the player leaves, in case they continue from Out of Moves. Players whose older version already
  granted the free trial uses at the start of the level keep those and get nothing more. Future power-ups with an
  unlock level get the same treatment automatically.
* **Where they work:** Campaign and Endless only. Daily Challenge and Weekly
  Rush block every power-up and every purchase. This is decided in one place (`ActionPolicy`) and enforced
  by the engine and services as well as the UI.
* **Cancelling:** tap the power-up again, press Cancel or use Back. Nothing is consumed until it applies.

## 8. Coins and economy

Coins are earned by playing (below), by claiming achievements (docs/ACHIEVEMENTS.md), and since update 3.0 by watching
adverts and buying coin packs (docs/MONETISATION.md: 20 coins per Coins-tab advert, three coin adverts a day in all;
packs of 500 / 1,200 / 2,750 / 6,000). Every play reward has a one-time **claim key** stored in the save, so repeated
taps, reloads and replays can never pay twice; advert rewards are granted once per advert the SDK confirms, and
purchases once per store transaction.

| Source (update 3.1) | Coins | Key |
|---|---|---|
| First clear of a campaign level | 3 (levels 1–25), +1 every 25 levels, max 8 (from level 126) | `c.first.N` |
| First, second and third star of a level, each the first time it's earned | 2 / 3 / 5 (10 for all three) | `c.star.N.S` |
| Milestone (every 10th level) / region finale (every 20th) | 20 / 40 | `c.mile.N` |
| Daily completion / three stars | 10 / +5, once per date | `d.DATE.done` / `d.DATE.3star` |
| Weekly Rush tiers | 10 / 15 / 25, once per week | `w.WEEK.tN` |
| Endless milestones (2,500 / 5,000 / 10,000 / 20,000 / 40,000) | 5 / 10 / 15 / 25 / 40, once ever | `e.mile.SCORE` |
| Achievements (67) | 1,810 coins and 59 power-ups in all | `ach.ID` |

**First clear**, exactly: `min(3 + (level − 1) / 25, 8)` with whole-number division (`EconomyConfig.FirstClearReward`),
so 3 coins for levels 1–25, 4 for 26–50, 5 for 51–75, 6 for 76–100, 7 for 101–125 and 8 from level 126 on. The
old cap of 15 (and the 10–30 of version 1) is gone; levels 1–500 pay 3,625 in all.

**Stars** pay by which star it is (`EconomyConfig.starRewards`): the first star of a level 2, the second 3, the
third 5. Each is paid once per level, ever: a level first won with one star pays 2, and replaying it for three stars
later pays the 3 + 5 still owed, never the 2 again. A level with all three stars has paid 10, so stars reward
playing well where first clears reward progress.

**Region milestones stay at 20 and region finales at 40.** They arrive once every 10 levels, so they are 3 coins a
level on average; they were already cut in half by version 2, and they are the one reward that marks a
region's end on the map. Cutting them further would take away the biggest moment of the campaign for very little
change in the total (they are about a fifth of campaign coins). The cuts were made where the audit found the excess:
achievement coins (22,265 → 1,810), the Coins-tab advert (50 × 5 → 20 × 3, shared) and late first clears.

**Three-packs (3.1):** each power-up can also be bought three at a time, about 10% cheaper (Swap 200, Blast 270,
Tile Breaker 340, Colour Shift 400, Colour Burst 675; `PowerUpDefinition.packPrice`, 0 = the built-in price,
negative = no pack, never more than three singles). The single prices are unchanged. The pack takes its price once
and adds exactly three; it is refused like a single one when the player can't afford it, before its unlock level
and during challenges. The ledger line reads "Bought Blast ×3".

**Out of Moves** is unchanged: +5 moves for 100 coins, or +10 for an advert, at the same price every time.

### Reward tuning history

| Version | When | What changed |
|---|---|---|
| 1 | Before the first playtest | First clear 10–30, 2 per star, milestones 50 / 100, Daily 30 / +10, Rush 20 / 30 / 50, Endless 20–100. About 230 coins a day. |
| 2 | After the first playtest | First clear 5–15, 1 per star, milestones 20 / 40, Daily 15 / +5, Rush 10 / 15 / 25, Endless 10–60. About 100 coins a day from play, but achievements (22,265 coins, 3.0) and adverts (up to 250 a day) let balances climb into the thousands. |
| 3 | Update 3.1 | First clear 3–8, stars 2 / 3 / 5, Daily 10 / +5, Endless 5–40, achievements 1,810 coins + 59 power-ups, Coins-tab advert 20 coins, three coin adverts a day shared, three-packs. |

Existing config assets are moved to the new values automatically when the project opens (`EconomyTuning`,
`AchievementRewardTuning`; the asset's `rewardTuningVersion` records how far it has been moved). A value is moved
only if it still has an older version's default, so anything someone edited by hand is kept. Everything stays
tunable in the config asset.

**Existing players (3.1):** nothing in a save is reset or changed by the update. Coin balances, progress, stars,
achievements, inventory, No Ads, Endless records and purchases load exactly as they were, so a player with 4,000
coins still has 4,000. Rewards already claimed are never paid again, and nobody is paid the difference between
old and new values; achievements unlocked but not yet claimed pay the 3.1 reward when claimed. The 3.0.1 Extra
Moves refund is unaffected (paid once, `pu.retired.N`).

### Simulation (update 3.1)

The game code was run day by day for three player profiles, with the same boards under the old (version 2) and new
(version 3) rewards, so the only difference is the economy: `Sim -- economy-trace` writes every credit, and
`tools/economy_model.py` adds spending (owned power-ups first, then buying one, with the not-enough-coins advert when
it covers the gap) on top. A power-up is wanted on some levels that took three or more tries and on every 10th–20th
level. Casual: 2 new levels a day, the Daily 4 days a week, Endless once a week, the Mixed bot, no adverts. Engaged:
4 levels a day, the Daily 5 days, Endless twice a week, the Mixed bot, one Coins-tab advert a day. Dedicated: 10
levels a day, every Daily, Endless daily, the Planner bot, every advert allowed, three-packs in 3.1.

| Balance after spending | Level 25 | 100 | 200 | 326 | 500 |
|---|---|---|---|---|---|
| Casual, before 3.1 | 1,254 | 3,767 | 7,920 | 12,511 | 18,717 |
| Casual, 3.1 | 694 | 1,254 | 1,924 | 2,503 | 2,556 |
| Engaged, before 3.1 | 1,195 | 5,478 | 10,560 | 16,421 | 22,587 |
| Engaged, 3.1 | 638 | 1,505 | 2,382 | 2,413 | 2,305 |
| Dedicated, before 3.1 | 2,146 | 7,675 | 13,897 | 22,424 | 36,862 |
| Dedicated, 3.1 | 869 | 1,912 | 3,584 | 6,010 | 9,306 |

| By level 500 | Earned in all | From achievements | From adverts | Play income a day (levels 101–326) | Achievement power-ups |
|---|---|---|---|---|---|
| Casual, before / 3.1 | 22,492 / 10,581 | 10,565 / 1,160 | 0 / 0 | 49 / 38 | 62 / 27 |
| Engaged, before / 3.1 | 27,862 / 12,080 | 10,915 / 1,160 | 6,250 / 2,500 | 89 / 69 | 65 / 28 |
| Dedicated, before / 3.1 | 37,312 / 13,596 | 13,965 / 1,185 | 12,500 / 3,000 | 223 / 189 | 81 / 37 |

* **Casual and Engaged players** can buy every power-up they want at every point, and their balance levels off at
  about 2,000–2,500 instead of growing without end: income and spending meet. Every power-up and every pack was
  affordable on 100% of days after it unlocked.
* **Dedicated players** still build up coins (about 9,300 by level 500) because a skilled player needs few power-ups.
  This is where a future coin sink belongs (below), not a further cut to rewards.
* **Struggling players** (the casual trace, wanting help on every level that took three tries and every 10th
  level: 156 power-ups in 500 levels) can't pay for all of it from play alone: 41 of 156 (26%) unpaid by level 500,
  about 5,100 coins short, and a Colour Burst affordable on only 32% of days. One Coins-tab advert a day halves the
  gap (17 unpaid); three a day closes it; wanting help on every second such level instead leaves 4 of 103 unpaid.
  Levels can still be won without power-ups (the Mixed bot won 515 of 520 within 12 tries using none).
  This is the economy's tightest point: watch it in real play (time spent below 100 coins, not-enough-coins offers
  declined) before changing anything.
* **Coin packs** are never needed by the Casual, Engaged or Dedicated profiles; only the struggling one without
  adverts would need about one 1,200-coin pack every two months to buy everything it wanted.

The short repository report (`dotnet run --project Sim -c Release -- economy`) compares 30 days of 5 levels a day
under versions 1, 2 and 3: about 225, 100 and 80 coins a day of play, so a Blast takes about 1.3 days of play and a
Colour Burst about 3 (its boards are freshly random each run, so the figures move a little between runs).

**Future task: cosmetics.** Board themes, tile skins or map decorations bought with coins would give skilled players
something to save for without making power-ups dearer. Not built in 3.1. The economy already supports it: the shop's
tabs are data (`ShopTab`), every debit goes through `Wallet` with a reason, and a one-time purchase can use a claim
key like the rewards do. Price them against the dedicated player's surplus (thousands of coins by level 300–500).

Every balance change goes through `Wallet`, the single source of truth, and is written to a 200-entry ledger
(time, amount, resulting balance, reason) for diagnosing economy bugs. Developer builds can show it in Settings.

## 9. Shop

* Reached from the home screen (Shop button and the **+** on the coin counter) and from the in-game bar.
* **Power-ups tab:** icon, name, description, owned/free count, price, and under it the **×3 three-pack** at its own
  price (update 3.1). Locked items show their unlock level; an unaffordable one opens the buy screen, which explains
  the shortfall and offers an advert for the missing coins (at most 50); the player then confirms the purchase
  themselves (update 3.0). An unaffordable pack says how many more coins three would need.
* **Coins tab:** balance (updates live), an advert for 20 coins, four coin packs priced by Google Play, then every
  way to earn coins by playing, with shortcuts.
* **Premium tab (3.0):** the permanent No Ads purchase and Restore Purchases.
* Tabs are data (`ShopTab` subclasses), so cosmetics or more premium items can be added as cards or tabs.
  Real-money purchases use Google Play Billing through Unity IAP (docs/MONETISATION.md).

**Adverts (3.0):** rewarded adverts only by the player's choice (Out of Moves: +10 moves; Coins tab; not enough
coins), and an interstitial at most once every 5 completed campaign levels and 5 minutes, only when leaving a
won level's results. Never during play, after a loss or a retry, or with No Ads. Out of Moves also offers +5
moves for 100 coins. Details and every limit: docs/MONETISATION.md.

## 10. Accessibility and settings

* **Symbols on tiles** (the "Symbols button", the eye icon in the game's top bar, also in Settings and Pause):
  every colour gets its own shape: red ♥ heart, yellow ★ star, blue ◎ ring, green ▲ triangle, purple ☾ moon,
  orange ✚ cross. White glyphs with a dark rim stay legible on every tile colour. The shapes were chosen by
  measurement: a test requires every pair to differ by more than 30% of their combined area (Jaccard distance),
  which rejected a circle (too close to the heart) and a square/diamond. The setting is saved, changes the
  board instantly and is shown in the Settings preview.
* Sound effects and music volume, vibration (Android, amplitude-controlled), **Reduced motion** (fewer
  particles, no screen shake, no overshoot, shorter animations, no pulsing).
* Portrait safe-area support, touch targets of at least 96 reference pixels, minimum body text about 27 px.

## 11. Onboarding and How to Play

Teaching is spread across the first levels (Section 3) with hints that never block input and appear once.
**How to Play** (home screen) has six short pages: matching (a real, playable practice board), scoring table,
gravity and stones (a playable board with stones), levels and stars, coins and power-ups, and game modes.
Tutorial hints can be shown again from Settings.

## 12. Screens and navigation

```
Home ─┬─ Play Level N ───────────────▶ Game ──▶ Result ──▶ Next Level / Map / Replay
      ├─ Map ─▶ Level card ─▶ Play ──▶ Game
      ├─ Endless / Daily / Weekly cards ─▶ Mode card ─▶ Play ─▶ Game ─▶ Result ─▶ Play Again / Home
      ├─ Shop (Power-ups | Coins | Premium)
      ├─ How to Play
      ├─ Achievements (trophy, beside the gear; also Settings > Your progress)
      └─ Settings (gear)
```

* Back always returns to the previous screen; Home always returns home; showing a screen that's already in
  the history returns to it rather than stacking duplicates (`NavStack`, unit tested).
* Leaving a game replaces it in the history, so Back from the map after a level goes home, not into a
  finished game.
* **Android Back:** closes the top popup → pauses a game → goes back → on Home asks "Leave Colour Cascade?".

## 12.1 Achievements

67 fixed achievements in seven categories (campaign levels, stars, perfect regions, Endless, skill, board
mastery, challenges), each Easy, Medium, Hard or Very hard. Rewards are coins and power-ups, scaled by difficulty
and claimed by hand. Progress is read from what the game already records, so nothing is kept twice. The full
list, the bot data behind the thresholds, the reward budget, the rules for existing players and the developer
test save are in **docs/ACHIEVEMENTS.md**.

## 13. Decisions made where the brief left room

| Topic | Decision | Why |
|---|---|---|
| Original project | The repository was empty, so nothing could be inspected or reused. The game was built from scratch to the brief. | |
| Symbols button | Implemented as the colour-shape accessibility toggle the brief describes, wired end to end. | No original code existed to infer anything more specific. |
| Campaign end | Passing earns ★ and play goes on; the player may Finish at any time. Unused moves score nothing (the spare-move bonus was removed in 2.1). | Stars reward playing the moves well; there is no reward for stopping. |
| Daily format | Plays all 20 moves (score attack). | Distinguishes it from the campaign and suits leaderboards later. |
| Challenge resets | UTC, ISO weeks. | Everyone shares one puzzle at once; ready for server validation. |
| Power-up order | Blast → Tile Breaker → Swap → Colour Shift → Colour Burst (Extra Moves, once between Colour Shift and Colour Burst, was retired in 3.0.1). | Simple to complex; Tile Breaker arrives once stones are common. |
| Coin packs | None; Coins tab explains how to earn. | Brief forbids fake purchase buttons. |
| Mid-level saving | Runs are not saved mid-level; finished results save immediately. | Simple and exploit-free. A level interrupted by the OS is replayed. |
| Art and audio | Procedurally generated in code. | No asset pipeline to break; resolution independent; previewable outside Unity. |
