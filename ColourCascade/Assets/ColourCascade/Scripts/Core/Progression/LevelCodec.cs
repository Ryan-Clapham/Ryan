using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Reads and writes a level as JSON: board rows, obstacle rows, goals, moves, colours, specials, stars. Used to
    /// export the campaign for review and to load hand-made levels (as a LevelOverride, which the catalog builds
    /// exactly like a level in the config asset). Format version 1:
    /// <code>{"format":"colour-cascade-level","version":1,"level":21,"moves":18,"colours":4,"layout":[...],
    /// "obstacles":[...],"goals":[{"kind":"ice","amount":6}],"specials":"rockets","target":0,...}</code>
    /// </summary>
    public static class LevelCodec
    {
        public const string Format = "colour-cascade-level";
        public const int Version = 1;

        static readonly string[] GoalNames = { "score", "colour", "ice", "chains", "treasure", "moss" };
        static readonly string[] TierNames = { "none", "rockets", "bombs", "prisms" };

        /// <summary>A playable level as JSON (the board shape as rows, so it can be read and edited by hand).</summary>
        public static string Encode(LevelSpec spec)
        {
            var goals = new List<object>();
            if (spec.Goals != null)
                foreach (var g in spec.Goals) goals.Add(EncodeGoal(g));
            var root = new Dictionary<string, object>
            {
                ["format"] = Format,
                ["version"] = Version,
                ["level"] = spec.Number,
                ["moves"] = spec.Moves,
                ["colours"] = spec.Colours,
                ["assist"] = spec.AssistPermille,
                ["target"] = spec.Target,
                ["star2"] = spec.Star2,
                ["star3"] = spec.Star3,
                ["specials"] = TierNames[Math.Max(0, Math.Min(TierNames.Length - 1, (int)spec.Specials))],
                ["mossCap"] = spec.MossCap,
                ["layout"] = ToList(spec.Layout.ToRows()),
                ["obstacles"] = ToList(spec.Layout.ToObstacleRows()),
                ["goals"] = goals,
                ["tutorial"] = spec.TutorialKey ?? "",
                ["note"] = spec.Note ?? "",
            };
            return Json.Write(root);
        }

        /// <summary>
        /// Reads a level written by Encode (or by hand) as a LevelOverride. Throws FormatException for anything that is
        /// not a valid level: wrong format, rows of the wrong size, unknown characters or goals, or a layout that fails
        /// LevelLayout.Validate.
        /// </summary>
        public static LevelOverride Decode(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Not a JSON object");
            object format;
            if (!root.TryGetValue("format", out format) || (format as string) != Format) throw new FormatException("Not a Colour Cascade level");
            if (JsonRead.Int(root, "version") > Version) throw new FormatException("Level from a newer version of the game");
            var ov = new LevelOverride
            {
                level = JsonRead.Int(root, "level", 1),
                moves = JsonRead.Int(root, "moves"),
                colours = JsonRead.Int(root, "colours"),
                assistPermille = JsonRead.Int(root, "assist", -1),
                target = JsonRead.Int(root, "target"),
                star2 = JsonRead.Int(root, "star2"),
                star3 = JsonRead.Int(root, "star3"),
                layout = Strings(JsonRead.Arr(root, "layout")),
                obstacles = Strings(JsonRead.Arr(root, "obstacles")),
                tutorial = Str(root, "tutorial"),
                note = Str(root, "note"),
                specials = Array.IndexOf(TierNames, Str(root, "specials")),
                mossCap = JsonRead.Int(root, "mossCap"),
            };
            var goals = new List<GoalSpec>();
            var list = JsonRead.Arr(root, "goals");
            if (list != null)
                foreach (var item in list)
                {
                    var g = item as Dictionary<string, object>;
                    if (g == null) throw new FormatException("Bad goal");
                    goals.Add(DecodeGoal(g));
                }
            ov.goals = goals.ToArray();
            if (ov.layout.Length == 0) throw new FormatException("Level has no layout");
            try
            {
                var layout = LevelLayout.Parse(ov.layout);
                if (ov.obstacles.Length > 0) layout.ApplyObstacleRows(ov.obstacles);
                string error;
                if (!layout.Validate(out error)) throw new FormatException(error);
            }
            catch (ArgumentException e)
            {
                throw new FormatException(e.Message);
            }
            return ov;
        }

        static Dictionary<string, object> EncodeGoal(GoalSpec g)
        {
            var d = new Dictionary<string, object> { ["kind"] = GoalNames[(int)g.kind], ["amount"] = g.amount };
            if (g.kind == GoalKind.CollectColour) d["colour"] = TileColours.Names[g.colour].ToLowerInvariant();
            return d;
        }

        static GoalSpec DecodeGoal(Dictionary<string, object> d)
        {
            int kind = Array.IndexOf(GoalNames, Str(d, "kind"));
            if (kind < 0) throw new FormatException("Unknown goal '" + Str(d, "kind") + "'");
            var g = new GoalSpec { kind = (GoalKind)kind, amount = JsonRead.Int(d, "amount") };
            if (g.kind == GoalKind.CollectColour)
            {
                string colour = Str(d, "colour");
                g.colour = -1;
                for (int i = 0; i < TileColours.Names.Length; i++)
                    if (string.Equals(TileColours.Names[i], colour, StringComparison.OrdinalIgnoreCase)) g.colour = i;
                if (g.colour < 0) throw new FormatException("Unknown colour '" + colour + "'");
            }
            if (g.amount < 0 || (g.amount == 0 && g.kind != GoalKind.ClearMoss)) throw new FormatException("Goal amount must be positive");
            return g;
        }

        static List<object> ToList(string[] rows)
        {
            var l = new List<object>(rows.Length);
            foreach (var r in rows) l.Add(r);
            return l;
        }

        static string[] Strings(List<object> list)
        {
            if (list == null) return new string[0];
            var a = new string[list.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = list[i] as string;
                if (a[i] == null) throw new FormatException("Rows must be strings");
            }
            return a;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v is string s ? s : "";
        }
    }
}
