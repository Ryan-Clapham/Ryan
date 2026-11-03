# Colour Cascade: Mechanics Specification

The exact rules for every tile type, goal and interaction in the campaign. Code and tests follow this document;
when a rule changes, this document changes in the same commit.

Sources: *Colour Cascade — Campaign Progression & Mechanics Plan*, the progression overhaul brief and the
phase 2 (new tile mechanics) brief. Where a rule was changed after simulation, the reason is given.

## 1. Words used here

* **Free colour tile:** a colour tile with no obstacle on it.
* **Obstacle:** a layer on a colour tile: **ice**, **chain** or **moss**. An obstacle has a **strength**
  (ice layers, chain links; moss is always 1).
* **Piece:** a board item that is not a colour tile: a **special tile** (rocket, bomb, prism) or **treasure**.
* **Action:** one player input that resolves as a whole: clearing a group, firing a special tile, or using a
  power-up. Group clears and special firings **cost a move**; power-ups do not.
* **Removed:** a tile leaves the board (it was cleared). Damaging an obstacle is not removing the tile.
* **Hit:** an obstacle loses one step of strength. **Strong hit:** it loses all of it.
* **Section:** a vertical run of cells in one column between gravity barriers (see 3.2).

## 2. Tiles

| Tile | Joins groups? | Falls? | Holds up tiles above? | Reshuffled? | Removed by |
|---|---|---|---|---|---|
| Free colour tile | Yes, its colour | Yes | No | Yes | Its group, area effects, power-ups |
| Stone | No | No | Yes, until broken | No | Rockets, bombs (and their combinations), Tile Breaker (2.6) |
| Void | No | No | Yes (permanent) | No | Never |
| Frozen (ice) | No | No | Yes, until freed | No | Never directly: ice is broken off, then it is a free tile |
| Chained | Yes, its own colour | No | Yes, until freed | No | Never directly: links are broken off, then it is a free tile |
| Mossy | Yes, its own colour | Yes | No | No | Its group, area effects, Tile Breaker (the moss goes with it) |
| Special (rocket, bomb, prism) | No (tapped alone) | Yes | No | No | Firing (tapped, or hit by another effect) |
| Treasure | No | Yes | No | No | Collection at the bottom of its section |

### 2.1 Ice (frozen tiles)

* A colour tile under 1 or 2 layers of ice. Thick ice (2 layers) has a second frost rim and a **2** badge.
* It cannot be tapped, does not join groups, cannot be swapped, recoloured or reshuffled.
* **Adjacent clear:** when an action removes at least one tile orthogonally next to a frozen tile (a colour
  tile, or a stone removed by Tile Breaker), that frozen tile loses **one** layer. Several cleared neighbours in
  the same action still remove only one layer.
* **Area hits** (Blast, a bomb, a rocket's line, a prism or Colour Burst of its colour) remove one layer.
* **Tile Breaker** removes all its layers at once (the tile stays, now free).
* **One step per action:** apart from Tile Breaker, a frozen tile loses at most one layer per action, however
  many effects reach it.
* When its last layer goes, it becomes a free colour tile in the same cell and falls in the same action if there
  is space below.
* While frozen it is a gravity barrier: tiles above rest on it, and the section below it refills from beneath it.

### 2.2 Chains (chained tiles)

* A colour tile locked by 1 to 3 links, shown by one, two or three crossing chains with a padlock, and a number
  badge from two links up.
* It joins groups of its own colour, so it counts towards a group's size (and its points), and tapping it selects
  its group like any tile.
* **Its group clears:** the free tiles of the group are removed; the chained tile **stays** and loses one link.
  A group made only of chained tiles is a legal move: nothing leaves the board, every tile in it loses a link.
* **Adjacent clears do nothing** to chains.
* Area hits remove one link; Tile Breaker removes all links. One step per action, as for ice.
* It cannot be swapped or reshuffled. **Colour Shift can recolour it** (the chain stays): this changes which
  colour frees it.
* With its last link gone it becomes a free colour tile in place and falls if there is space.
* While chained it is a gravity barrier, like ice.

### 2.3 Special tiles: rockets, bombs and prisms

* **Earned by big groups.** Clearing a group of
  * **6 to 8** tiles creates a **rocket**,
  * **9 to 11** creates a **bomb**,
  * **12 or more** creates a **prism**,

  as long as the level has taught that special (see 7): before bombs are taught, a group of 9+ still makes a
  rocket; before prisms, a group of 12+ makes a bomb. The sizes are in the config (scoring → specials). They were
  7/10/14 in the first draft; simulation showed specials were then too rare to rescue awkward boards (treasure in
  particular), so they were lowered.
* **Where it appears:** in the cell the player tapped, after the group is removed and before tiles fall. If the
  tapped tile stayed (it was chained), the nearest cleared cell of the group is used (lowest row, then column, on
  a tie). Only group clears create specials (not power-ups and not other specials). Creating one costs nothing
  extra: the group's move is the only move spent.
* **Rocket direction:** if the group spans more columns than rows, it is a **row** rocket (clears its row);
  otherwise a **column** rocket. The arrows on the tile show which.
* **Firing:** tapping a special fires it and **costs one move**. A press-and-hold shows what it will reach.
  * Rocket: every cell in its row or column is hit.
  * Bomb: every cell of the **5 × 5 square centred on the bomb** is hit (config: scoring → specials → bombSize; it
    was 3 × 3, then 4 × 4 in early 2.1 builds). Near an edge or a corner the square is simply cut off: cells off
    the board are never touched, so a bomb reaches 25 cells in the middle, 15 on an edge and 9 in a corner of an
    8 × 9 board. The effect on screen lights exactly the cells that were hit. (DESIGN.md §3.4 has the size study.)
  * Prism: the player picks a colour (a swatch, or by tapping any tile of that colour); every tile of that colour
    is hit. Only colours on the board are offered.
* **What a hit does:** a free colour tile or mossy tile is removed (with its moss); ice and chains lose one step;
  a **stone is broken** (rockets, bombs and their combinations; a prism reaches colours, not stones); voids and
  treasure are unaffected; another special is set off.
* **Chain reactions:** specials set off by a hit join a queue and fire in the order they were hit, each after the
  previous one finishes. Each special fires at most once per action. A prism set off this way (nobody can choose)
  takes the colour with the most tiles on the board (lowest colour index on a tie).
* **Groups of specials (combinations, 2.1):** tapping a special fires **every special connected to it** (through
  orthogonally adjacent specials: a line, an L, a snake or a block of any size) as **one combination**. Each member
  fires once and is used up; the effect depends only on what the group contains, so the rules are the same for a
  pair and for a group of five. Counting R rockets, B bombs and P prisms in the group:

  | Group | What it reaches |
  |---|---|
  | One special | Its own effect (row, column, 5 × 5, a colour) |
  | Bombs only (B ≥ 2) | A square of 5 + 2 × (B − 1) centred on the tapped bomb: 7 × 7 for two, 9 × 9 for three |
  | Rockets only (R ≥ 2) | A cross through the tapped tile: its row and its column, each 1 + 2 × (R − 2) lines wide (1 for two rockets, 3 for three, 5 for four) |
  | One rocket with bombs | A band in the rocket's direction (rows for a row rocket, columns for a column rocket), 1 + 2 × B lines wide, centred on the tapped tile: 3 lines with one bomb, 5 with two |
  | Several rockets with bombs | A cross 1 + 2 × B + 2 × (R − 2) lines wide (3 for two rockets and a bomb) |
  | One prism with others | The chosen colour everywhere, **and** the rockets and bombs fire together as above, from the tapped tile (or, when the tapped one is the prism, from the first rocket or bomb next in the group) |
  | Two or more prisms | Every colour tile on the board, plus whatever the group's rockets and bombs reach (they still break stones) |

  Two specials therefore behave as before (rocket + rocket a cross, rocket + bomb three lines, prism + rocket or
  bomb the colour and then that special, prism + prism everything), except bomb + bomb, which is 7 × 7 now that a
  bomb is 5 × 5. Every cell is hit once even where parts of the effect overlap. A special the blast reaches that is
  **not** in the group is set off afterwards **alone** (as a chain reaction: only a tap, or the end of the level,
  forms a combination); every special fires at most once per action, so chains always end. Power-ups that reach
  specials set them off alone too: the six inventory power-ups are never combined with anything.
* A group of tiles never contains a special (specials do not join tile groups); specials meet only through
  combinations and chain reactions.
* Specials fall like tiles, are never reshuffled, and **always count as a legal move**.
* **Points (2.1):** 50 for each colour tile a special actually removes, 75 for each removed by a combination of
  any size (config: scoring → specials). Nothing else: holes, empty cells and untouched treasure score nothing, so a rocket on an
  8-wide row scores 7 × 50 = 350 and one on a 6-wide row 250. Ice, chains, moss and stones it hits score their own
  points (section 5) on top. Every tile is scored once, by whichever special removed it: in a chain reaction each
  special scores only the tiles still there when it fires, and a special set off by another scores at the single
  rate even if the first was a combination. Members that are themselves specials score nothing as tiles. 50 is a tile's share of the 6-tile group that makes a rocket (it was
  20, so a rocket across a row was worth a group of 4). A combination's extra 25 a tile is shown as its own
  "combo bonus" line.
* **Exceptional combinations:** two prisms clear every colour tile (up to 70 on an 8 × 9 board: 5,250 points), but
  each obstacle still takes only one step, so thick ice and chains of two or more links remain: a full-board clear
  is a big score, never a pass by itself, and three stars stay calibrated against whole games (DESIGN.md §3.2).

### 2.4 Treasure

* A piece placed by the level design (refills never create treasure).
* It falls with gravity inside its section like any tile.
* **Collected** when, after gravity, it rests on the bottom edge of the board or on a stone or void. Resting on a
  frozen or chained tile, or on any tile, does **not** count: it must reach a floor that never goes away.
* A collected treasure leaves the board (500 points); the tiles above close the gap before the refill.
* It cannot be tapped, swapped, recoloured, reshuffled or hit by Tile Breaker, and area effects pass over it.
* Generated levels place treasure only in columns that are open all the way to the bottom edge with open columns
  on both sides (and never in the edge columns): simulation showed treasure above stone "cups" or in pockets could
  get stuck beyond what even the Planner bot could fix.
* A layout may not place treasure where it would be collected before the first move.

### 2.5 Moss

* A layer that grows on colour tiles (strength 1), drawn as moss creeping in from the edges so the colour still
  shows.
* **Clearing a mossy tile removes the moss:** mossy tiles join groups of their own colour and leave with them.
  Area hits (Blast, rockets, bombs, prisms, Colour Burst of its colour) and Tile Breaker also remove the tile and
  its moss.
* **A clear right next to a mossy tile scrapes the moss off** (the tile stays, now free). This was added after
  simulation: with only the first rule, moss that spread into a corner could become impossible to clear.
* Mossy tiles **fall like any tile** (moss grows on the tile; it does not pin it). They cannot be swapped,
  recoloured or reshuffled.
* **Growth:** after an action that **costs a move** and removes **no** moss, if moss is on the board, it grows onto
  exactly one free colour tile orthogonally next to existing moss, chosen with the session's seeded generator (so
  it is repeatable). Limits:
  * it never grows on two moves in a row (after growing, the next move without a moss clear does nothing),
  * it never grows past the level's cap (generated levels: the starting moss plus two),
  * it only grows onto free tiles (not ice, chains, specials or treasure),
  * power-ups never make it grow (they cost no move).
* Because mossy tiles still match their colour and a neighbouring clear removes moss, moss can never take every
  move away or make a level impossible.

### 2.6 Stones (2.1)

* Stones are barriers: tiles above them stay up, and each section below refills on its own.
* **Breaking them:** a rocket breaks every stone in its line, a bomb every stone in its square, and the combinations
  that include a rocket or a bomb do the same over their larger areas. Tile Breaker breaks the stone it targets.
  Nothing else does: tapping groups never touches stones, and neither do Blast, Colour Burst or a prism.
* A broken stone leaves an empty cell: the tiles above fall through it and the column refills from the top, so two
  sections become one. Each stone broken scores 100 (config: scoring → stonePoints) and is shown on the result
  screen as "Stones". Neighbouring frozen tiles count it as a removed neighbour (they lose a layer).

## 3. Move resolution

### 3.1 Order of an action

1. **Primary effect:** the group clear (chained tiles in it lose a link and stay; mossy tiles go with their
   moss), the special firing (with its partner), or the power-up effect.
2. **Special creation** (group clears only).
3. **Chain reactions:** specials hit so far fire in order (each may hit more).
4. **Obstacle reactions:** ice and moss next to a removed tile lose one step, respecting the one-step-per-action
   rule.
5. **Points** for obstacle steps and special clears.
6. **Gravity**, with **treasure collection** as treasure reaches a permanent floor, then **refill**; treasure
   points.
7. **Moss growth** (move-costing actions only).
8. **Goal progress** is updated from everything that happened.
9. **No-move recovery** (3.3).
10. **End check:** campaign levels are won when every goal is complete, and lost when no moves remain.

### 3.2 Gravity barriers

A section is split by any cell that holds up the tiles above it: stones, voids, and frozen or chained tiles.
Each section compacts downwards and refills from its own top; a section under a barrier refills from beneath that
barrier. Specials, treasure and mossy tiles fall within their section.

### 3.3 No legal moves

* After every action the board is checked. A legal move is a group of two or more tiles that join groups
  (free, chained or mossy tiles of one colour), or any special tile.
* If none exists, only **free colour tiles** are reshuffled; ice, chains, mossy tiles, specials and treasure keep
  their cells. Score, moves, goals and power-ups are unchanged. Up to 12 random shuffles are tried, then a
  constructive arrangement that places a matching pair; if even that fails the run ends as **Board Locked**, with
  no penalty.

## 4. Goals

### 4.1 Kinds of goal

A campaign level has one to three goals (objectives). They are tracked independently, update the moment an action
makes progress, and **all** of them must be met to pass the level.

| Goal | Counts | Complete when |
|---|---|---|
| Score | The running score | Score ≥ amount |
| Collect a colour | Colour tiles of that colour **removed** from the board, by any means | Count ≥ amount |
| Free the ice | Frozen tiles whose **last** layer is removed | Count ≥ amount |
| Break the chains | Chained tiles whose **last** link is removed | Count ≥ amount |
| Collect treasure | Treasures collected | Count ≥ amount |
| Clear the moss | Moss on the board (it can grow back) | No moss remains |
| Make specials | Special tiles made from big groups, of the kind asked (rocket, bomb, prism, or any) | Count ≥ amount |
| Set off specials | Special tiles that go off, of the kind asked, by any means: a tap, a chain reaction, either half of a combination, or a power-up reaching it | Count ≥ amount |
| Fire combinations | Two specials fired together (one tapped next to another) | Count ≥ amount |

* A level with no goals listed has one Score goal equal to its target, so every score level plays as before.
* Goals are data (`GoalSpec`: kind, amount, colour, special) and progress is read from what each action did
  (`MoveOutcome`), so a new kind of goal is one more case in `GoalTracker.Record`, not a new system. Level files
  name them `score`, `colour`, `ice`, `chains`, `treasure`, `moss`, `make`, `fire` and `combo`.
* Every mode uses the same rules for every tile (section 9). What differs by mode is when a mechanic is unlocked and
  how boards use it.

### 4.2 Combining goals

Generated levels from 25 on often combine two goals ("free 4 frozen tiles **and** collect 10 blue"), from the
prisms (51) sometimes three. Partners are drawn from what the player has been taught: board goals (ice, colour,
chains, treasure, moss) plus, from the rockets (31), goals about special tiles ("make 2 rockets", "set off 3
specials", "fire a combination") and, from 35, a score to reach as well. A special-tile goal is only ever added
alongside a board goal. Bombs and prisms are never asked for by name in a mixed level (a group of 9 or 12 depends
too much on the board); their practice levels ask for them on four-colour boards, some starting with one on the
board. Every combination is checked by simulation (DESIGN.md §3.2).

### 4.3 Passing a level, playing on, and stars

* The moment every goal is met (the target included, on a score level), the level is **passed**: one star is
  secured and "Level passed!" is shown. The level is **not over**: the remaining moves stay available, and every
  point they add counts towards two and three stars. The stars on the game screen light up as they are earned.
* A passed level ends when the moves run out, when the player taps **Finish**, or when the board has no move left.
  **Unused moves score nothing** (the spare-move bonus was removed in 2.1): Finish is for stopping once the stars
  are where the player wants them. Quitting or restarting a passed level finishes it first, so the pass is never lost.
* Running out of moves with any goal unmet **fails** the level, however high the score. Extra Moves (owned or bought,
  always an explicit choice) revives it with its score and goal progress intact.
* Stars, on the final score (everything scored, including the specials that went off at the end): ★ pass the level
  (every mandatory goal); ★★ and ★★★ reach the level's two score thresholds. A passed level always earns at least one star. The level card lists all three requirements
  before play; the result screen ticks each one against the actual result and says what the next star needed.
* Daily Challenge, Weekly Rush and Endless are unchanged (Daily already plays every move).

### 4.4 Special tiles left when the moves run out (2.1)

* If the last move leaves special tiles on the board (campaign, Daily Challenge and Endless; not the timed Weekly
  Rush), they all go off before the result is decided. They go group by group, top row first: each **connected
  group fires as one combination** (the same rules as a tap, led by its top-left member; a prism takes the most
  common colour), a lone special fires alone, and chain reactions resolve before the next group. No move is
  charged and no inventory power-up is ever used. Their points and their goal progress count: a goal they complete
  still passes the level.
* **Finish** on a passed level does exactly the same before the result.
* **Showing the points:** when they have gone off, a card on the board lists what they scored, taken from the
  scoring itself: tiles cleared, combination bonus, ice/chains/moss, stones broken, treasure, and the total. The
  result screen shows the same figures as "Specials at the end", with its lines underneath.
* Order of the level-ending sequence: last move (or Finish) → specials left on the board go off → final score (the
  score: nothing is added for moves) → stars (and pass or fail, if goals remain). Recording a result twice returns
  the same result.
* Extra Moves after a failed finish starts a fresh run of moves: specials made from then on go off again if those
  run out too.

## 5. Points

| Event | Points |
|---|---|
| Event | Points | Result-screen line |
|---|---|---|
| Group of *n* tiles | n² × 10 (unchanged), chained and mossy tiles included in *n* | Groups |
| Colour tile removed by a special | 50 (was 20) | Special tiles |
| ... in a combination of two specials | 75 (was 40): the extra 25 | Combo bonus |
| Colour tile removed by a power-up | 20 (unchanged) | Power-ups |
| Ice layer removed | 100 | Ice, chains, moss |
| Chain link removed | 150 | Ice, chains, moss |
| Moss removed | 100 | Ice, chains, moss |
| Stone broken (2.6) | 100 (new) | Stones |
| Treasure collected | 500 | Treasure |
| Everything specials left on the board score when the level ends (4.4) | as above | Specials at the end |
| Unused moves | Nothing (the spare-move bonus was removed in 2.1) | — |

All values live in the game config's scoring section. Every point is counted once, in one line: the lines always
add up to the score, which is the final score. Nothing scores a fixed amount
regardless of its effect.

**Power-ups (audit, 2.1).** Power-ups score less per tile than specials on purpose (20, the share of a tile in the
smallest group), so that stars come from play rather than purchases; what they do to obstacles scores the usual
obstacle points, and any special they set off scores as a special.

| Power-up | What scores | Goal progress |
|---|---|---|
| Blast (3 × 3) | 20 per colour tile removed; obstacle steps; specials it sets off at their rate | Every tile and obstacle it removes or frees |
| Tile Breaker | 20 for a colour tile, 100 for a stone, every ice layer or chain link it strips | As above |
| Swap | Nothing (nothing is removed) | None |
| Colour Shift | Nothing | None |
| Extra Moves | Nothing (+3 moves) | None |
| Colour Burst | 20 per tile of the colour; obstacle steps | Collect that colour; ice and chains of that colour take a step |

## 6. Power-ups on every tile

| Power-up | Free tile | Stone | Ice | Chain | Moss | Special | Treasure |
|---|---|---|---|---|---|---|---|
| Blast (3 × 3) | Removed | Untouched | −1 layer | −1 link | Removed | Fires | Untouched |
| Tile Breaker (one cell) | Removed | Broken (100 points) | All layers (tile stays) | All links (tile stays) | Removed | Fires | Not a target |
| Swap (two neighbours) | Allowed | No | No | No | No | Allowed (not with another special) | No |
| Colour Shift | Allowed | No | No | Allowed (chain stays) | No | No | No |
| Colour Burst (a colour) | Removed | Untouched | −1 layer (that colour) | −1 link (that colour) | Removed (that colour) | Untouched | Untouched |
| Extra Moves | Unchanged | | | | | | |

* Blast is valid when its 3 × 3 area contains a colour tile (free or under an obstacle) or a special tile.
* Colour Burst can target any colour tile (not a special or treasure); the target picks the colour.
* Swap still refuses two tiles of the same colour (and so two specials).
* Specials set off by a power-up fire after it, in the same action; they cost no move. Power-ups never create
  specials and never make moss grow.

## 7. Where each mechanic is introduced

Each mechanic has a hand-made introduction level with a tutorial card (what it is and how it works) and a pointer
at the first one on the board, then three practice levels that focus on it, then it mixes with the earlier ones.
No introduction teaches two new things. The levels are set in the config (campaign → mechanics):

| Level | New | Introduction level |
|---|---|---|
| 21 | Frozen tiles | Six single-layer frozen tiles; goal: free them |
| 25 | Collect a colour (goal type) | Collect 18 blue tiles |
| 28 | Thick ice | Two thick and four thin frozen tiles |
| 31 | Rockets | A rocket ready on a line of ice, and a block of 8 matching tiles to make another |
| 41 | Bombs | A bomb in a ring of ice, and a block of 10 matching tiles |
| 47 | Special combos | Two rockets side by side on a cross of ice |
| 51 | Prisms | A prism, a block of 14 matching tiles, and a colour to collect |
| 61 | Chains | Six chained tiles, each starting next to a tile of its own colour |
| 71 | Treasure | Two treasures in open columns |
| 101 | Moss | Two mossy tiles, each next to a tile of its own colour |

A hand-made level in the config with the same number replaces the built-in introduction.

## 8. Implementation status

| Part | Status |
|---|---|
| Ice, chains, specials, combos, chain reactions, treasure, moss, all goals | Done in the engine and tested in the .NET harness |
| The same mechanics in Daily Challenge, Weekly Rush and Endless (section 9) | Done and tested; simulated (DESIGN.md §4–6) |
| Campaign schedule, introduction levels, practice levels, generated level recipes | Done and tested; balance simulated (DESIGN.md, campaign-simulation.csv) |
| Art, overlays, strength badges, animations, sounds, goals panel, prism picker, tutorials, how-to-play pages, star checklist | Written and compile-checked against Unity's assemblies; **not yet seen running in Unity** |
| Level files (JSON) for export and hand-made levels | Done and tested |

## 9. Mechanics in every mode

The seven mechanics are one shared system: the engine applies the rules above in every mode (there is no
per-mode copy of any rule). Each mode only decides which mechanics a board may use, when they unlock, and how
difficulty is controlled. Power-up rules per mode are unchanged (Daily Challenge and Weekly Rush stay power-up
free), as are rewards and records.

| | Campaign | Daily Challenge | Weekly Rush | Endless |
|---|---|---|---|---|
| Unlocked by | Its introduction levels (section 7) | Campaign: a mechanic counts once its introduction level is completed | Campaign, as for Daily | The stages of each run (below) |
| Boards | Hand-made introductions, then recipes by band | The day's plan from the date | A weekly theme plus more | The stage journey (below) |
| Goals | 1–3 per level | The target plus the obstacles' goals | None (scored) | None (scored) |
| Special tiles | From their introductions | Whatever the player has unlocked | Whatever the player has unlocked | Rockets and bombs from stage 6, prisms from 10 |

**Daily Challenge.** The day's plan comes from the date alone: one mechanic, or a pair that plays fairly on a
20-move board (ice with treasure, moss or a colour goal; treasure with moss or a colour goal; moss with a colour
goal). Chains only ever appear alone. A player gets the parts of the plan they have unlocked: everyone with the same
unlocks plays exactly the same board, goals, moves and target, and the board underneath the mechanics is the day's
board for everyone. Obstacles bring goals; the day is complete when the target **and** every goal are met by the
last move, and stars need the goals too. Days with goals have a slightly lower target (config: daily →
goalTargetEasingPermille). Records and rewards stay keyed by the date. If the day's obstacles would be too many for a
casual player in 20 moves, they are scaled down (70%, then 50%).

**Weekly Rush.** Each week has a theme (frozen, rocket, chain, bomb, treasure, prism or moss week); special-tile
themes start the board with a few of that special. If the player has not unlocked the theme, their most recent
obstacle stands in. Up to two more of the player's obstacles join it, so weekly boards combine more than daily ones
(never treasure with chains). Rush is scored only: obstacles bring points, not goals. The medal scores move with the
board: a bot plays 45 moves on the board with and without the mechanics, and the medal scores are multiplied by the
ratio (0.7× to 1.8×), so a medal takes the same effort. Rewards use the medal scores of the board that was played.

**Endless (2.1): the stage journey.** Every run starts with a clean board and walks through the game's mechanics, one
new thing per stage (config: endless → journey; DESIGN.md §4): 1 warm-up, 2 stones, 3 frozen tiles, 4 a breather
(+5 moves), 5 chains, 6 rockets and bombs (big groups make them from here; one rocket is placed to try), 7 treasure,
8 a breather, 9 moss, 10 prisms (one is placed), then from 11 every stage mixes what came before, two kinds at
first, three from 15 and four from 19, each more densely (amounts grow 8% a stage, up to double), with a breather
every fourth stage (14, 18, 22...). Mixing stages draw their mechanics, and every wave its cells, from the run's own
seed, so no two runs are alike. Waves never cover more than a third of the board; stones appear in the middle rows,
at most one per column and never under treasure. Each treasure brought down gives 3 bonus moves. Stage scores rise
with the stage: 1,200, 2,800, 4,800, 7,200, 10,000, 13,500, 17,500, 22,000, 27,000, 32,500, then +6,000 each.

**Endless level.** Still saved with the player's progress: it starts at 1 and goes up by one for every stage reached in
any run. It no longer unlocks anything (the journey replaced the old Endless milestones); the mode card and the result
show the best stage and what the next stages bring. The first time a mechanic appears outside the campaign without
having been taught, its tutorial card is shown (tutorials are shared by every mode; Weekly Rush never shows one, as its
clock is running).
