namespace ColourCascade.Core
{
    /// <summary>
    /// The one place that answers rule questions about a tile: can it join a group, does it fall, does it hold
    /// up the tiles above, may it be reshuffled, swapped or recoloured (docs/MECHANICS.md §2). Group search,
    /// gravity, recovery, power-ups and bots all ask here instead of checking cell types themselves.
    /// </summary>
    public static class TileRules
    {
        /// <summary>A colour tile with no obstacle on it.</summary>
        public static bool IsFree(Tile t) => t.Type == CellType.Colour && t.Obstacle == ObstacleKind.None;

        /// <summary>A colour tile under an obstacle.</summary>
        public static bool HasObstacle(Tile t) => t.Type == CellType.Colour && t.Obstacle != ObstacleKind.None;

        /// <summary>Counts towards groups of its colour (free tiles, and obstacles whose definition allows it).</summary>
        public static bool JoinsGroups(Tile t)
        {
            if (t.Type != CellType.Colour) return false;
            if (t.Obstacle == ObstacleKind.None) return true;
            var d = Obstacles.Get(t.Obstacle);
            return d != null && d.JoinsGroups;
        }

        /// <summary>Holds up the tiles above it and splits its column into gravity sections.</summary>
        public static bool IsGravityBarrier(Tile t)
        {
            if (t.Type == CellType.Stone || t.Type == CellType.Void) return true;
            if (t.Type != CellType.Colour || t.Obstacle == ObstacleKind.None) return false;
            var d = Obstacles.Get(t.Obstacle);
            return d != null && d.HoldsInPlace;
        }

        /// <summary>Moves down into empty cells below it during gravity: colour tiles not held in place, specials and treasure.</summary>
        public static bool Falls(Tile t)
        {
            if (t.Type == CellType.Special || t.Type == CellType.Treasure) return true;
            return t.Type == CellType.Colour && !IsGravityBarrier(t);
        }

        public static bool IsSpecial(Tile t) => t.Type == CellType.Special;

        public static bool IsTreasure(Tile t) => t.Type == CellType.Treasure;

        /// <summary>Tapping it is a move: a colour tile that joins groups (if its group has 2+) or a special tile.</summary>
        public static bool IsTappable(Tile t) => JoinsGroups(t) || IsSpecial(t);

        /// <summary>A gravity floor that never goes away: the bottom edge (checked by the caller), a stone or a void.
        /// Treasure is collected when it comes to rest on one.</summary>
        public static bool IsPermanentFloor(Tile t) => t.Type == CellType.Stone || t.Type == CellType.Void;

        /// <summary>An area hit removes the tile with its obstacle (moss) instead of taking one step off the obstacle.</summary>
        public static bool AreaHitRemovesTile(Tile t)
        {
            if (!HasObstacle(t)) return false;
            var d = Obstacles.Get(t.Obstacle);
            return d != null && d.AreaHitRemovesTile;
        }

        /// <summary>Can be covered by spreading moss: a free colour tile.</summary>
        public static bool CanBeOvergrown(Tile t) => IsFree(t);

        /// <summary>May be moved by no-move recovery. Everything else keeps its cell.</summary>
        public static bool CanShuffle(Tile t) => IsFree(t);

        /// <summary>May be moved by the Swap power-up: free colour tiles and special tiles.</summary>
        public static bool CanSwap(Tile t) => IsFree(t) || IsSpecial(t);

        /// <summary>May be repainted by Colour Shift.</summary>
        public static bool CanRecolour(Tile t)
        {
            if (t.Type != CellType.Colour) return false;
            if (t.Obstacle == ObstacleKind.None) return true;
            var d = Obstacles.Get(t.Obstacle);
            return d != null && d.CanBeRecoloured;
        }

        /// <summary>When a group containing it clears, the tile stays and loses one step of its obstacle.</summary>
        public static bool StaysWhenItsGroupClears(Tile t)
        {
            if (!HasObstacle(t)) return false;
            var d = Obstacles.Get(t.Obstacle);
            return d != null && d.HitWhenItsGroupClears;
        }

        /// <summary>Loses one step of its obstacle when an adjacent tile is removed.</summary>
        public static bool HitByAdjacentClear(Tile t)
        {
            if (!HasObstacle(t)) return false;
            var d = Obstacles.Get(t.Obstacle);
            return d != null && d.HitByAdjacentClear;
        }
    }
}
