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
| Stone | No | No | Yes (permanent) | No | Tile Breaker only |
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
  * Bomb: every cell in the 3 × 3 square around it is hit.
  * Prism: the player picks a colour (a swatch, or by tapping any tile of that colour); every tile of that colour
    is hit. Only colours on the board are offered.
* **What a hit does:** a free colour tile or mossy tile is removed (with its moss); ice and chains lose one step;
  stones, voids and treasure are unaffected; another special is set off.
* **Chain reactions:** specials set off by a hit join a queue and fire in the order they were hit, each after the
  previous one finishes. Each special fires at most once per action. A prism set off this way (nobody can choose)
  takes the colour with the most tiles on the board (lowest colour index on a tie).
* **Two specials together:** tapping a special that touches another special (orthogonally) fires both as one
  effect, using up both. With several neighbours the strongest is the partner (prism, then bomb, then rocket;
  ties go right, left, up, down):
  * rocket + rocket: the tapped tile's row **and** column,
  * rocket + bomb: three rows (row rocket) or three columns (column rocket), centred on the tapped tile,
  * bomb + bomb: the 5 × 5 square around the tapped tile,
  * prism + rocket or bomb: the chosen colour everywhere, then the other special fires from where it sat,
  * prism + prism: every colour tile on the board.
* A group never contains a special (specials do not join groups), so "a group with several specials" cannot
  happen; several specials can only meet through combinations and chain reactions.
* Specials fall like tiles, are never reshuffled, and **always count as a legal move**.
* Points: 20 for each colour tile a special removes, 40 in a combination (config: scoring → specials).

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

A campaign level has one to three goals and is complete when **all** are met.

| Goal | Counts | Complete when |
|---|---|---|
| Score | The running score (the spare-move bonus is added only after winning) | Score ≥ amount |
| Collect a colour | Colour tiles of that colour **removed** from the board, by any means | Count ≥ amount |
| Free the ice | Frozen tiles whose **last** layer is removed | Count ≥ amount |
| Break the chains | Chained tiles whose **last** link is removed | Count ≥ amount |
| Collect treasure | Treasures collected | Count ≥ amount |
| Clear the moss | Moss on the board (it can grow back) | No moss remains |

* A level with no goals listed has one Score goal equal to its target, so every score level plays as before.
* Stars: completing every goal is one star. Two and three stars come from the level's score thresholds (for goal
  levels, set from the scores the Planner bot finishes with; see DESIGN.md).
* Endless, Daily Challenge, Weekly Rush and Practice keep their own rules and never contain obstacles, pieces,
  special tiles or campaign goals. This is enforced: a session in any of those modes refuses to start with them.

## 5. Points

| Event | Points |
|---|---|
| Group of *n* tiles | n² × 10 (unchanged), chained and mossy tiles included in *n* |
| Tile removed by a power-up | 20 (unchanged) |
| Tile removed by a special | 20; 40 in a combination |
| Ice layer removed | 100 |
| Chain link removed | 150 |
| Moss removed | 100 |
| Treasure collected | 500 |

All values live in the game config's scoring section.

## 6. Power-ups on every tile

| Power-up | Free tile | Stone | Ice | Chain | Moss | Special | Treasure |
|---|---|---|---|---|---|---|---|
| Blast (3 × 3) | Removed | Untouched | −1 layer | −1 link | Removed | Fires | Untouched |
| Tile Breaker (one cell) | Removed | Removed | All layers (tile stays) | All links (tile stays) | Removed | Fires | Not a target |
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
| Ice, chains (phase 1), specials, combos, chain reactions, treasure, moss, all goals | Done in the engine and tested in the .NET harness |
| Campaign schedule, introduction levels, practice levels, generated level recipes | Done and tested; balance simulated (DESIGN.md, campaign-simulation.csv) |
| Art, overlays, strength badges, animations, sounds, goals panel, prism picker, tutorials, how-to-play pages | Written and compile-checked against Unity's assemblies; **not yet seen running in Unity** |
| Level files (JSON) for export and hand-made levels | Done and tested |
