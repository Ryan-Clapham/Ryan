using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// A set of tile mechanics, used to say which ones a player has unlocked and which ones a board may use. The rules
    /// of each mechanic live in the engine (TileRules, PuzzleSession, docs/MECHANICS.md) and are the same in every
    /// mode; what differs by mode is only when a mechanic is unlocked and how boards use it.
    /// </summary>
    [Flags]
    public enum MechanicSet
    {
        None = 0,
        Ice = 1,
        ThickIce = 2,
        Rockets = 4,
        Bombs = 8,
        Prisms = 16,
        Chains = 32,
        Treasure = 64,
        Moss = 128,
        /// <summary>Collect-a-colour goals (campaign and Daily Challenge).</summary>
        ColourGoals = 256,
        All = Ice | ThickIce | Rockets | Bombs | Prisms | Chains | Treasure | Moss | ColourGoals,
        /// <summary>Stone blocks added during play (Endless stages). Not a campaign unlock: campaign boards have stones in
        /// their layout from level 8, so this is not part of All.</summary>
        Stones = 512,
    }

    public static class Mechanics
    {
        /// <summary>The obstacle-type mechanics, in the order boards add them.</summary>
        public static readonly MechanicSet[] Obstacles = { MechanicSet.Ice, MechanicSet.Chains, MechanicSet.Treasure, MechanicSet.Moss };

        /// <summary>Everything an Endless stage can add to the board.</summary>
        public const MechanicSet EndlessObstacles = MechanicSet.Stones | MechanicSet.Ice | MechanicSet.ThickIce | MechanicSet.Chains | MechanicSet.Treasure | MechanicSet.Moss;

        public static bool Has(this MechanicSet set, MechanicSet m) => (set & m) == m && m != MechanicSet.None;

        /// <summary>
        /// Mechanics the player has been taught in the campaign: a mechanic counts once its introduction level has
        /// been completed (the next level is unlocked). Daily Challenge and Weekly Rush use only these.
        /// </summary>
        public static MechanicSet FromCampaign(int highestUnlocked, MechanicSchedule s)
        {
            var set = MechanicSet.None;
            if (s == null) return set;
            if (highestUnlocked > s.iceFromLevel) set |= MechanicSet.Ice;
            if (highestUnlocked > s.colourGoalsFromLevel) set |= MechanicSet.ColourGoals;
            if (highestUnlocked > s.thickIceFromLevel) set |= MechanicSet.ThickIce;
            if (highestUnlocked > s.rocketsFromLevel) set |= MechanicSet.Rockets;
            if (highestUnlocked > s.bombsFromLevel) set |= MechanicSet.Bombs;
            if (highestUnlocked > s.prismsFromLevel) set |= MechanicSet.Prisms;
            if (highestUnlocked > s.chainsFromLevel) set |= MechanicSet.Chains;
            if (highestUnlocked > s.treasureFromLevel) set |= MechanicSet.Treasure;
            if (highestUnlocked > s.mossFromLevel) set |= MechanicSet.Moss;
            return set;
        }

        /// <summary>The special tiles big groups create with these mechanics.</summary>
        public static SpecialTier Tier(MechanicSet set)
        {
            if (set.Has(MechanicSet.Prisms)) return SpecialTier.Prisms;
            if (set.Has(MechanicSet.Bombs)) return SpecialTier.Bombs;
            if (set.Has(MechanicSet.Rockets)) return SpecialTier.Rockets;
            return SpecialTier.None;
        }

        /// <summary>The obstacle mechanics in a set, in board order.</summary>
        public static List<MechanicSet> ObstaclesIn(MechanicSet set)
        {
            var list = new List<MechanicSet>();
            foreach (var m in Obstacles) if (set.Has(m)) list.Add(m);
            return list;
        }

        public static string Name(MechanicSet single)
        {
            switch (single)
            {
                case MechanicSet.Ice: return "frozen tiles";
                case MechanicSet.ThickIce: return "thick ice";
                case MechanicSet.Rockets: return "rockets";
                case MechanicSet.Bombs: return "bombs";
                case MechanicSet.Prisms: return "prisms";
                case MechanicSet.Chains: return "chains";
                case MechanicSet.Treasure: return "treasure";
                case MechanicSet.Moss: return "moss";
                case MechanicSet.ColourGoals: return "colour goals";
                case MechanicSet.Stones: return "stones";
                default: return "";
            }
        }

        /// <summary>"ice, chains and moss".</summary>
        public static string Names(MechanicSet set, bool includeSpecials = true)
        {
            var parts = new List<string>();
            foreach (MechanicSet m in new[] { MechanicSet.Stones, MechanicSet.Ice, MechanicSet.ThickIce, MechanicSet.Chains, MechanicSet.Treasure, MechanicSet.Moss, MechanicSet.Rockets, MechanicSet.Bombs, MechanicSet.Prisms })
            {
                if (!set.Has(m)) continue;
                if (!includeSpecials && (m == MechanicSet.Rockets || m == MechanicSet.Bombs || m == MechanicSet.Prisms)) continue;
                if (m == MechanicSet.ThickIce && set.Has(MechanicSet.Ice)) continue;
                parts.Add(Name(m));
            }
            if (parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
        }

        /// <summary>The tutorial that teaches a mechanic (shared by every mode: seen once, anywhere).</summary>
        public static string TutorialKey(MechanicSet single)
        {
            switch (single)
            {
                case MechanicSet.Ice: return TutorialKeys.Ice;
                case MechanicSet.ThickIce: return TutorialKeys.ThickIce;
                case MechanicSet.Rockets: return TutorialKeys.Rockets;
                case MechanicSet.Bombs: return TutorialKeys.Bombs;
                case MechanicSet.Prisms: return TutorialKeys.Prisms;
                case MechanicSet.Chains: return TutorialKeys.Chains;
                case MechanicSet.Treasure: return TutorialKeys.Treasure;
                case MechanicSet.Moss: return TutorialKeys.Moss;
                case MechanicSet.ColourGoals: return TutorialKeys.ColourGoal;
                default: return "";
            }
        }

        /// <summary>Every single mechanic in a set, in teaching order.</summary>
        public static List<MechanicSet> Each(MechanicSet set)
        {
            var list = new List<MechanicSet>();
            foreach (MechanicSet m in new[] { MechanicSet.Ice, MechanicSet.ColourGoals, MechanicSet.ThickIce, MechanicSet.Rockets, MechanicSet.Bombs, MechanicSet.Prisms, MechanicSet.Chains, MechanicSet.Treasure, MechanicSet.Moss })
                if (set.Has(m)) list.Add(m);
            return list;
        }
    }
}
