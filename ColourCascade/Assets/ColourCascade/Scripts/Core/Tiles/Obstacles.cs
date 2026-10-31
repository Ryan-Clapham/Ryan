using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>A layer on a colour tile (docs/MECHANICS.md §2). Values are stable: they appear in layouts and tests.</summary>
    public enum ObstacleKind : byte
    {
        None = 0,
        /// <summary>Frozen tile: 1-2 layers, broken off by clears next to it.</summary>
        Ice = 1,
        /// <summary>Chained tile: 1-3 links, broken off when its own colour's group clears.</summary>
        Chain = 2,
    }

    /// <summary>
    /// How one kind of obstacle behaves, as data. Every rule question about obstacles is answered from these
    /// flags (through TileRules), so a new obstacle is a new definition plus its art, not new rule code.
    /// </summary>
    public sealed class ObstacleDefinition
    {
        public ObstacleKind Kind;
        public string Name = "";
        /// <summary>Highest strength (layers or links) a level may give it.</summary>
        public int MaxStrength = 1;
        /// <summary>The tile cannot fall and holds up the tiles above it (a temporary gravity barrier).</summary>
        public bool HoldsInPlace;
        /// <summary>The tile still joins groups of its own colour.</summary>
        public bool JoinsGroups;
        /// <summary>When a group containing the tile clears, the tile stays and loses one step instead of being removed.</summary>
        public bool HitWhenItsGroupClears;
        /// <summary>A clear in an orthogonally adjacent cell takes one step off it.</summary>
        public bool HitByAdjacentClear;
        /// <summary>Colour Shift may repaint the tile (the obstacle stays).</summary>
        public bool CanBeRecoloured;
    }

    public static class Obstacles
    {
        static readonly Dictionary<ObstacleKind, ObstacleDefinition> definitions = new Dictionary<ObstacleKind, ObstacleDefinition>();

        static Obstacles()
        {
            Register(new ObstacleDefinition
            {
                Kind = ObstacleKind.Ice, Name = "Ice", MaxStrength = 2,
                HoldsInPlace = true, JoinsGroups = false, HitWhenItsGroupClears = false, HitByAdjacentClear = true, CanBeRecoloured = false,
            });
            Register(new ObstacleDefinition
            {
                Kind = ObstacleKind.Chain, Name = "Chain", MaxStrength = 3,
                HoldsInPlace = true, JoinsGroups = true, HitWhenItsGroupClears = true, HitByAdjacentClear = false, CanBeRecoloured = true,
            });
        }

        public static void Register(ObstacleDefinition definition)
        {
            definitions[definition.Kind] = definition;
        }

        /// <summary>The definition, or null for ObstacleKind.None and unknown kinds.</summary>
        public static ObstacleDefinition Get(ObstacleKind kind)
        {
            ObstacleDefinition d;
            return kind != ObstacleKind.None && definitions.TryGetValue(kind, out d) ? d : null;
        }
    }
}
