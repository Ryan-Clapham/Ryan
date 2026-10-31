# Colour Cascade: Mechanics Specification

The exact rules for every tile type, goal and interaction in the campaign progression overhaul. Code and tests
follow this document; when a rule changes, this document changes in the same commit.

Sources: *Colour Cascade — Campaign Progression & Mechanics Plan* and the implementation brief. Where the brief
and the plan differ, the brief wins (noted where it matters, e.g. moss).

## 1. Words used here

* **Free colour tile:** a colour tile with no obstacle on it.
* **Obstacle:** a layer on a colour tile: **ice**, **chain** or **moss**. An obstacle has a **strength**
  (ice layers, chain links; moss is always 1).
* **Action:** one player input that resolves as a whole: clearing a group, activating a special tile, or
  using a power-up. Group clears and special activations **cost a move**; power-ups do not.
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
| Mossy (phase 6) | Yes, its own colour | No | Yes | No | Its group (removes tile and moss), area effects |
| Special (phase 4) | No (tapped alone) | Yes | No | No | Activation (its own or a hit by another effect) |
| Treasure (phase 5) | No | Yes | No | No | Collection at the bottom of its section |

### 2.1 Ice (frozen tiles)

* A colour tile under 1 or 2 layers of ice (thick ice, 2 layers, arrives later in the campaign).
* It cannot be tapped, does not join groups, cannot be swapped, recoloured or reshuffled.
* **Adjacent clear:** when an action removes at least one tile orthogonally next to a frozen tile, that frozen
  tile loses **one** layer. Several cleared neighbours in the same action still remove only one layer.
* **Area hits** (Blast, a bomb, a rocket's line, a prism or Colour Burst of its colour) remove one layer.
* **Tile Breaker** removes all its layers at once (the tile stays, now free).
* **One step per action:** apart from Tile Breaker, a frozen tile loses at most one layer per action, however
  many effects reach it.
* When its last layer goes, it becomes a free colour tile in the same cell. If there is space below it, it falls
  during the same action's gravity step.
* While frozen it is a gravity barrier: tiles above rest on it, and the section below it refills from beneath it,
  exactly like a stone.

### 2.2 Chains (chained tiles)

* A colour tile locked by 1 to 3 chain links.
* It joins groups of its own colour, so it counts towards a group's size (and its points), and tapping it selects
  its group like any tile.
* **Its group clears:** the free tiles of the group are removed; the chained tile **stays** and loses one link.
  A group made only of chained tiles is a legal move: nothing leaves the board, every tile in it loses a link.
* **Adjacent clears do nothing** to chains (this is what makes chains a colour puzzle rather than a second kind
  of ice).
* Area hits remove one link; Tile Breaker removes all links. One step per action, as for ice.
* It cannot be swapped or reshuffled. **Colour Shift can recolour it** (the chain stays): this changes which
  colour frees it.
* With its last link gone it becomes a free colour tile in place and falls if there is space.
* While chained it is a gravity barrier, like ice.

### 2.3 Special tiles (phase 4)

* **Earned, not placed** (except in hand-made teaching levels): clearing a group of
  * **7 to 9** tiles creates a **rocket**,
  * **10 to 13** creates a **bomb**,
  * **14 or more** creates a **prism**,
  in the cell the player tapped, after the group is removed and before gravity. Only group clears create
  specials (not power-ups and not other specials).
* **Rocket direction:** if the group spans more columns than rows, it is a **row** rocket (clears its row);
  otherwise a **column** rocket. The arrow on the tile shows which. Deterministic, so players can plan it.
* **Activation:** tapping a special activates it and costs one move.
  * Rocket: every cell in its row or column is hit.
  * Bomb: every cell in the 3 × 3 square around it is hit.
  * Prism: the player picks a colour; every tile of that colour is hit.
* **What a hit does:** a free colour tile or mossy tile is removed; ice and chains lose one step; stones,
  voids and treasures are unaffected; another special is activated (after the current one finishes).
* **Chain reactions:** specials activated by a hit go into a queue and resolve in the order they were hit.
  Each special activates at most once per action.
* **Combinations** (later in the campaign): tapping a special that is orthogonally next to another special fires
  both as one effect, consuming both:
  * rocket + rocket: its row **and** its column,
  * rocket + bomb: three rows (row rocket) or three columns (column rocket), centred on the tapped tile,
  * bomb + bomb: the 5 × 5 square around the tapped tile,
  * prism + anything: the chosen colour is hit everywhere, then the other special fires normally,
  * prism + prism: every colour tile on the board is hit.
* Specials fall like tiles, never join groups, are never reshuffled, and **always count as a legal move**.
* Points: each tile a special removes is worth the power-up rate (20 points); doubled for combinations.

### 2.4 Treasures (phase 5)

* A non-colour piece placed by the level design (never spawned by refills).
* It falls with gravity inside its section like any tile.
* **Collected** when, after gravity, it rests on the bottom edge of the board or on a stone or void. Resting on a
  frozen, chained or mossy tile does **not** count (those are temporary): it waits until they are freed.
* A collected treasure leaves the board; gravity then continues so the gap closes before refilling.
* It cannot be tapped, swapped, recoloured or reshuffled, and area effects do not affect it.

### 2.5 Moss (phase 6)

* A spreading layer on a colour tile (strength 1). **Mossy tiles join groups of their own colour; clearing the
  group removes the tile and its moss** (as the brief specifies).
* Area hits (Blast, bombs, rockets, prisms, Colour Burst of its colour) remove the mossy tile and its moss.
  Tile Breaker removes it. Adjacent clears do nothing.
* A mossy tile stays in place and is a gravity barrier while it exists.
* It cannot be swapped, recoloured or reshuffled.
* **Spreading:** after any action that **costs a move** and removes **no** moss, if moss remains on the board,
  exactly one free colour tile orthogonally next to existing moss becomes mossy. The choice uses the session's
  seeded random generator, so it is repeatable. Power-ups never cause spreading (they cost no move). If no free
  neighbour exists, nothing spreads.
* Introduced late (around level 125) and used sparingly.

## 3. Move resolution

### 3.1 Order of an action

1. **Primary effect:** the group clear (chained tiles in it lose a link and stay), the special activation, or
   the power-up effect.
2. **Special creation** (group clears only, phase 4).
3. **Chain reactions** of specials hit in steps 1–2 (phase 4).
4. **Obstacle reactions:** every frozen tile next to a removed tile loses one layer, respecting the one-step-per-
   action rule (hits already taken in steps 1–3 count towards it).
5. **Gravity**, then **treasure collection** (repeated until nothing more is collected), then **refill**.
6. **Moss spreading** (phase 6).
7. **Goal progress** is updated from everything that happened.
8. **No-move recovery** (3.3).
9. **End check:** campaign levels are won when every goal is complete, and lost when no moves remain.

### 3.2 Gravity barriers

A section is split by any cell that holds up the tiles above it: stones, voids, and frozen, chained or mossy
tiles. Each section compacts downwards and refills from its own top; a section under a barrier refills from
beneath that barrier.

### 3.3 No legal moves

* After every action the board is checked. A legal move is a group of two or more tiles that join groups
  (free or chained tiles of one colour), or any special tile.
* If none exists, only **free colour tiles** are reshuffled; every other tile keeps its cell. Score, moves,
  goals and power-ups are unchanged. Up to 12 random shuffles are tried, then a constructive arrangement that
  places a matching pair; if even that fails the run ends as **Board Locked**, with no penalty.

## 4. Goals

A campaign level has one to three goals and is complete when **all** are met.

| Goal | Counts | Complete when |
|---|---|---|
| Score | The running score (the spare-move bonus is added only after winning) | Score ≥ amount |
| Collect a colour | Colour tiles of that colour **removed** from the board, by any means | Count ≥ amount |
| Free the ice | Frozen tiles whose **last** layer is removed | Count ≥ amount |
| Break the chains | Chained tiles whose **last** link is removed | Count ≥ amount |
| Collect treasure (phase 5) | Treasures collected | Count ≥ amount |
| Clear the moss (phase 6) | Moss on the board | No moss remains |

* A level with no goals listed has one Score goal equal to its target, so every existing level plays exactly as
  before.
* Endless, Daily Challenge and Weekly Rush keep their own rules and never contain campaign-only tiles.

## 5. Points

| Event | Points |
|---|---|
| Group of *n* tiles | n² × 10 (unchanged), chained tiles included in *n* |
| Tile removed by a power-up | 20 (unchanged) |
| Tile removed by a special | 20; 40 in a combination (phase 4) |
| Ice layer removed | 100 |
| Chain link removed | 150 |
| Treasure collected | 500 (phase 5) |

All values live in the game config's scoring section.

## 6. Power-ups on every tile

| Power-up | Free tile | Stone | Ice | Chain | Moss | Special | Treasure |
|---|---|---|---|---|---|---|---|
| Blast (3 × 3) | Removed | Untouched | −1 layer | −1 link | Removed | Activated | Untouched |
| Tile Breaker (one cell) | Removed | Removed | All layers (tile stays) | All links (tile stays) | Removed | Activated | Not a target |
| Swap (two neighbours) | Allowed | No | No | No | No | Allowed (with a free tile) | No |
| Colour Shift | Allowed | No | No | Allowed (chain stays) | No | No | No |
| Colour Burst (a colour) | Removed | Untouched | −1 layer (that colour) | −1 link (that colour) | Removed (that colour) | Untouched | Untouched |
| Extra Moves | Unchanged | | | | | | |

* Blast is valid when its 3 × 3 area contains at least one colour tile (free or under an obstacle).
* Colour Burst can target any colour tile; the target picks the colour.
* Swap still refuses two tiles of the same colour.
* Power-ups never create specials and never make moss spread.

## 7. Implementation status

| Part | Phase | Status |
|---|---|---|
| Tile behaviour system, obstacles framework, ice and chain rules, power-up interactions for them | 1 | Implemented and tested in the engine; no level contains them yet |
| Goals (score, colour, ice, chains) and goal-based completion | 1 | Implemented and tested; levels still use a score goal only |
| Ordered move resolution (3.1 steps 1, 4, 5, 7, 8, 9) | 1 | Implemented |
| Planner bot | 1 | Implemented |
| Bands, rhythm, slack-based sizing, stars v2, generator | 2 | Planned |
| Ice in levels, colour goals in levels, art, goals panel, introductions | 3 | Planned |
| Specials (rockets, bombs, prisms, combinations) | 4 | Planned |
| Chains in levels, treasures | 5 | Planned |
| Moss, expert recipes, pre-generated levels 1–300, economy | 6 | Planned |
