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

* **Board sizes:** levels 1–3 are 6 × 6, levels 4–6 are 7 × 7, level 7 onwards is 8 × 9.
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
  "breather" board. Some levels have a dominant or a scarce colour.
* **Targets are calibrated, not guessed (levels 1–20):** when a level is first needed, a reference bot (always
  takes the biggest group) plays it 16 times. The target is a percentage of its median score. These levels are
  unchanged by the campaign mechanics (a test checks them against the pre-mechanics build).
* **From level 21, every level is sized from simulated players** (details in 3.1).
* **Passing and stars:** reaching the target (or meeting every goal) **passes** the level and earns ★, but play
  goes on (and when the moves run out, special tiles still on the board go off before the result: MECHANICS.md §4.4): the moves left raise the score towards ★★ and ★★★, and the stars light up on the game screen as they
  are earned. The player can tap **Finish** at any time after passing (unused moves score nothing).
  Out of moves after passing = won with the stars of the final score; out of moves before = failed, whatever the
  score (docs/MECHANICS.md §4.3). Levels 1–20: ★★ 125% and ★★★ 150% of the target. From 21: see 3.2.
* **Losing:** running out of moves shows "Out of Moves" with Try Again. If Extra Moves is unlocked, the
  player may choose to use one they own, or explicitly buy one for 200 coins. Nothing is ever spent automatically.
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

## 4. Endless

* A **fresh random board every run** (8 × 9). No timer.
* Start with **30 moves**. Each group costs one move; groups of 6+ give +1 move, 9+ give +2 and 13+ give +3; each
  treasure brought down gives +3; breathers give +5.
* **The stage journey (2.1):** every run walks through the game's mechanics, one new thing per stage, so the mode
  feels like a journey rather than the same board with a bigger number (config: endless → journey; rules in
  MECHANICS.md §9):

| Stage | Score | What it brings |
|---|---|---|
| 1 | 0 | Warm-up: ordinary matching, 4 colours |
| 2 | 1,200 | Stones appear (they hold tiles up; rockets and bombs break them later) |
| 3 | 2,800 | Frozen tiles |
| 4 | 4,800 | Breather: +5 moves, nothing new (a fifth colour arrives) |
| 5 | 7,200 | Chains |
| 6 | 10,000 | Rockets and bombs: big groups make them from now on; one rocket is placed to try |
| 7 | 13,500 | Treasure |
| 8 | 17,500 | Breather |
| 9 | 22,000 | Moss |
| 10 | 27,000 | Prisms (one is placed) |
| 11+ | 32,500, then +6,000 a stage | Mixes of everything met so far: 2 kinds at a time, 3 from 15, 4 from 19, each denser (+8% a stage, up to double); a breather every fourth stage (14, 18, 22...); a sixth colour from 12 |

* Each new stage is announced on a banner ("Stage 5 · New: chains"; "Stage 11 · Mix: frozen tiles and moss") and a
  mechanic seen for the first time outside the campaign gets its tutorial card. Mixing stages pick their mechanics,
  and every wave its cells, from the run's own seed, so runs differ. Waves never cover more than a third of the
  board, so later stages are harder (denser, more combined) without becoming unwinnable.
* The run ends when moves run out (specials left on the board go off first). Bests are kept for score, biggest
  group, moves and stage; the Endless level (one per stage reached in any run) is kept as a lifetime figure and no
  longer gates anything. The mode card shows the best stage and the next new thing; the result shows the stage
  reached (or a new best) and what the next introduction is.
* Coins: one-time milestone rewards the first time any run reaches 2,500 / 5,000 / 10,000 / 20,000 / 40,000
  points (20/30/50/75/100). Endless never grants campaign rewards and never unlocks levels.
* **Simulation** (`Sim -- modes`, 30 runs per bot): median stage 5 (Casual bot), 6 (Mixed), 8 (Planner); the best
  10% of Planner runs reach 11 (best 12), into the mixing stages; runs last about 45–60 moves with 12–14 obstacles, stones,
  treasures and specials added. Before 2.1 runs stopped around stage 4–5 under the old stage scores (2,000 / 5,000 /
  10,000 / 17,000 / 26,000 / 38,000) and mechanics only arrived after many runs (Endless levels 5–40).

## 5. Daily Challenge

* One puzzle per **UTC calendar day**, built from the date (`daily:yyyy-MM-dd`) with a deterministic
  generator, so every player and every reopen on that date gets the same board **and** the same refills for
  the same moves.
* **20 moves, no timer, every move is played** (it is a score attack, unlike the campaign). Completing means
  reaching the target. Stars at the target, ★★ and ★★★ thresholds calibrated against a reference bot.
* First completion each day pays **30 coins**; three stars pays **+10** once. Replays are unlimited and update
  the best score but never pay twice.
* No power-ups and no shop during a run, so the challenge is identical for everyone.
* **Tile mechanics:** each date has a plan of one mechanic, or a pair known to play fairly (chains only alone). A
  player gets the parts of it they have completed in the campaign, so everyone with the same progress plays the same
  puzzle, and a new player plays the original daily. Obstacles add goals (shown in the goals panel, with the target
  as the first goal); the day is complete when the target and every goal are met. Days with goals have a target 12%
  lower. Simulation (60 days): Casual-bot success 47% on plain days, 50–56% with mechanics; Planner 90–100%.
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
* Medal tiers: **Bronze 2,500 (+20), Silver 5,000 (+30), Gold 8,000 (+50)**, each paid once per week.
  Bots score about 5,000–6,200 in 40 moves and 7,900–9,700 in 60, so Gold rewards fast, good play.
* No power-ups, no shop during the run.
* **Tile mechanics:** each week has a theme (frozen, rocket, chain, bomb, treasure, prism or moss week) and up to two
  more of the player's campaign-unlocked mechanics, so weekly boards combine more than daily ones. Special-tile weeks
  start with a few of that special on the board. The medal scores are scaled by how much the mechanics change a
  45-move bot score, and rewards use the medal scores of the board played. Simulation (16 weeks): with everything
  unlocked the medal scores were 1.42× the plain ones on average (1.19× before 5 × 5 bombs and groups of
  specials), and the bot earned bronze, silver and gold at close to the plain boards' rates (100% / 97% / 48%
  against 100% / 98% / 42%). The Weekly Rush has no end-of-level specials (it is timed) and never had a spare-move
  bonus.

## 7. Power-ups

Bought with coins in the Shop (or from the in-game bar) and stored in an inventory. Using one never costs a
move. A use is only consumed if the effect actually happens; an invalid target costs nothing.

| Power-up | Price | Unlocks | Free uses on its level | How it works |
|---|---|---|---|---|
| Blast | 100 | Level 10 | 2 | Tap any tile: every colour tile in the 3 × 3 area is cleared (clipped at the edges). Stones and holes are not affected (only specials and Tile Breaker break stones). Must clear at least one tile. |
| Tile Breaker | 125 | Level 20 | 2 | Tap one tile **or one stone** to break it (a stone scores 100). Rockets and bombs break stones too (2.1). Holes can't be broken. |
| Swap | 75 | Level 30 | 2 | Tap a tile, then a neighbouring tile of a different colour; they swap places. Nothing is cleared, so the board stays valid. |
| Colour Shift | 150 | Level 40 | 2 | Tap a tile, then pick any other colour in play from the palette. |
| Extra Moves | 200 | Level 50 | 1 | +3 moves. Campaign only (it would bypass Endless's move economy and challenges' fairness). Also offered, as an explicit choice, on the Out of Moves screen. |
| Colour Burst | 250 | Level 60 | 1 | Tap a tile: every tile of that colour is removed. |

* **Introduction (2.1):** every attempt at a power-up's unlock level (first play, retry or replay, also after
  the game is closed and reopened) shows its card ("New Power-up!", or "Power-up practice" on a replay), points at
  its button and **lends** its free uses for that attempt: they are spent before the player's own and are never
  saved. On the **first win** of that level, the lent uses left over move into the normal inventory, once ever
  (the result says "Kept 1 free Colour Burst"); replays lend them again but keep nothing, so replaying can't
  stockpile power-ups (and replays pay no new coins). The loan ends with the run: the next level's bar shows only
  power-ups the player owns (a bug let unusable "Free" badges linger). A level that ran out of moves keeps its
  loan until the player leaves, in case they continue with Extra Moves. Players whose older version already
  granted the free trial uses at the start of the level keep those and get nothing more. Future power-ups with an
  unlock level get the same treatment automatically.
* **Where they work:** Campaign and Endless only (Extra Moves: Campaign only). Daily Challenge and Weekly
  Rush block every power-up and every purchase. This is decided in one place (`ActionPolicy`) and enforced
  by the engine and services as well as the UI.
* **Cancelling:** tap the power-up again, press Cancel or use Back. Nothing is consumed until it applies.

## 8. Coins and economy

Coins are earned only by playing, and every grant has a one-time **claim key** stored in the save, so
repeated taps, reloads and replays can never pay twice.

| Source | Coins | Key |
|---|---|---|
| First clear of a campaign level | 5 (levels 1–10), +1 every 10 levels, max 15 | `c.first.N` |
| Each star the first time it's earned | 1 | `c.star.N.S` |
| Milestone (every 10th level) / region finale (every 20th) | 20 / 40 | `c.mile.N` |
| Daily completion / three stars | 15 / +5, once per date | `d.DATE.done` / `d.DATE.3star` |
| Weekly Rush tiers | 10 / 15 / 25, once per week | `w.WEEK.tN` |
| Endless milestones | 10 / 15 / 25 / 40 / 60, once ever | `e.mile.SCORE` |

**Retuned after the first playtest (reward tuning version 2).** With the original values (first clear 10–30,
2 per star, milestones 50/100, Daily 30/+10, Rush 20/30/50, Endless 20–100) the economy simulation
(`dotnet run --project Sim -c Release -- economy`) showed an active player (5 new levels a day plus the Daily,
a weekly Rush and some Endless) earning about **230 coins a day**, so every power-up cost less than a day of play
and the playtester bought three straight away. The prices stayed as specified; income was cut instead:

| Measured (30 simulated days, 5 levels a day) | Version 1 | Version 2 |
|---|---|---|
| Average income | 231 coins/day | 100 coins/day (60–70 a day after a first-day welcome of about 130) |
| Campaign coins by level 10 / 30 / 60 | 218 / 786 / 1,942 | 97 / 329 / 756 |
| Days of play per Blast (100) / Colour Burst (250) | 0.4 / 1.1 | 1.0 / 2.5 |

Levels 1–10 with two stars each now earn just under one Blast; the two free trial uses cover its introduction.
Existing config assets are moved to the new values automatically when the project opens (`EconomyTuning`);
values someone edited by hand are kept. Everything stays tunable in the config asset.

Every balance change goes through `Wallet`, the single source of truth, and is written to a 200-entry ledger
(time, amount, resulting balance, reason) for diagnosing economy bugs. Developer builds can show it in Settings.

## 9. Shop

* Reached from the home screen (Shop button and the **+** on the coin counter) and from the in-game bar.
* **Power-ups tab:** icon, name, description, owned/free count, price. Locked items show their unlock level;
  unaffordable ones explain the shortfall ("You need 35 more coins").
* **Coins tab:** balance plus every way to earn coins, with shortcuts to play. It states plainly that coin
  packs are not sold in this version: no fake or non-functional purchase buttons.
* Tabs are data (`ShopTab` subclasses), so cosmetics or real coin packs can be added later as another tab.
  Real-money purchases, if added, should use Google Play Billing (Unity IAP) with receipt validation.

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
      ├─ Shop (Power-ups | Coins)
      ├─ How to Play
      └─ Settings (gear)
```

* Back always returns to the previous screen; Home always returns home; showing a screen that's already in
  the history returns to it rather than stacking duplicates (`NavStack`, unit tested).
* Leaving a game replaces it in the history, so Back from the map after a level goes home, not into a
  finished game.
* **Android Back:** closes the top popup → pauses a game → goes back → on Home asks "Leave Colour Cascade?".

## 13. Decisions made where the brief left room

| Topic | Decision | Why |
|---|---|---|
| Original project | The repository was empty, so nothing could be inspected or reused. The game was built from scratch to the brief. | |
| Symbols button | Implemented as the colour-shape accessibility toggle the brief describes, wired end to end. | No original code existed to infer anything more specific. |
| Campaign end | Passing earns ★ and play goes on; the player may Finish at any time. Unused moves score nothing (the spare-move bonus was removed in 2.1). | Stars reward playing the moves well; there is no reward for stopping. |
| Daily format | Plays all 20 moves (score attack). | Distinguishes it from the campaign and suits leaderboards later. |
| Challenge resets | UTC, ISO weeks. | Everyone shares one puzzle at once; ready for server validation. |
| Power-up order | Blast → Tile Breaker → Swap → Colour Shift → Extra Moves → Colour Burst. | Simple to complex; Tile Breaker arrives once stones are common. |
| Extra Moves scope | Campaign only. | Brief: "current eligible campaign level". |
| Coin packs | None; Coins tab explains how to earn. | Brief forbids fake purchase buttons. |
| Mid-level saving | Runs are not saved mid-level; finished results save immediately. | Simple and exploit-free. A level interrupted by the OS is replayed. |
| Art and audio | Procedurally generated in code. | No asset pipeline to break; resolution independent; previewable outside Unity. |
