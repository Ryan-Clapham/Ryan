using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace UnityLint
{
    [AttributeUsage(AttributeTargets.Assembly)]
    sealed class PathsAttribute : Attribute
    {
        public readonly string Assets, RuntimeDll;
        public PathsAttribute(string assets, string runtimeDll) { Assets = assets; RuntimeDll = runtimeDll; }
    }

    /// <summary>
    /// Catches mistakes that compile fine but that Unity rejects when the game runs:
    /// 1. A MonoBehaviour/ScriptableObject method named like a Unity message (Start, Update...) with the wrong
    ///    parameters ("Script error: Start() can not take parameters").
    /// 2. Text in code using characters the bundled Fredoka font doesn't contain (shown as boxes or nothing).
    /// </summary>
    static class Program
    {
        // Unity messages and how many parameters Unity allows for each.
        static readonly Dictionary<string, int> Messages = new Dictionary<string, int>
        {
            { "Awake", 0 }, { "Start", 0 }, { "Update", 0 }, { "LateUpdate", 0 }, { "FixedUpdate", 0 },
            { "OnEnable", 0 }, { "OnDisable", 0 }, { "OnDestroy", 0 }, { "Reset", 0 }, { "OnValidate", 0 },
            { "OnGUI", 0 }, { "OnApplicationQuit", 0 }, { "OnApplicationPause", 1 }, { "OnApplicationFocus", 1 },
            { "OnRectTransformDimensionsChange", 0 }, { "OnTransformParentChanged", 0 }, { "OnBeforeTransformParentChanged", 0 },
            { "OnTransformChildrenChanged", 0 }, { "OnCanvasGroupChanged", 0 }, { "OnDidApplyAnimationProperties", 0 },
            { "OnBecameVisible", 0 }, { "OnBecameInvisible", 0 }, { "OnRenderObject", 0 }, { "OnWillRenderObject", 0 },
        };

        static int Main()
        {
            var paths = typeof(Program).Assembly.GetCustomAttribute<PathsAttribute>();
            var problems = new List<string>();
            CheckMessages(Path.GetFullPath(paths.RuntimeDll), problems);
            CheckGlyphs(Path.GetFullPath(paths.Assets), problems);
            foreach (var p in problems) Console.WriteLine("error: " + p);
            Console.WriteLine(problems.Count == 0 ? "UnityLint: no problems found" : "UnityLint: " + problems.Count + " problem(s)");
            return problems.Count == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ Unity messages

        static void CheckMessages(string dll, List<string> problems)
        {
            using var stream = File.OpenRead(dll);
            using var pe = new PEReader(stream);
            var md = pe.GetMetadataReader();
            int checkedTypes = 0;
            foreach (var handle in md.TypeDefinitions)
            {
                var type = md.GetTypeDefinition(handle);
                if (!DerivesFromUnityObject(md, type)) continue;
                checkedTypes++;
                string typeName = md.GetString(type.Name);
                foreach (var mh in type.GetMethods())
                {
                    var method = md.GetMethodDefinition(mh);
                    string name = md.GetString(method.Name);
                    int allowed;
                    if ((method.Attributes & MethodAttributes.Static) != 0 || !Messages.TryGetValue(name, out allowed)) continue;
                    var blob = md.GetBlobReader(method.Signature);
                    var header = blob.ReadSignatureHeader();
                    if (header.IsGeneric) blob.ReadCompressedInteger();
                    int count = blob.ReadCompressedInteger();
                    if (count != allowed)
                        problems.Add(typeName + "." + name + " has " + count + " parameter(s); Unity calls " + name + " as a message, which takes " + allowed + ". Rename the method.");
                }
            }
            Console.WriteLine("Checked Unity message methods on " + checkedTypes + " MonoBehaviour/ScriptableObject types");
        }

        /// <summary>True when the type's base chain reaches a UnityEngine class (MonoBehaviour, Graphic, ScriptableObject...).</summary>
        static bool DerivesFromUnityObject(MetadataReader md, TypeDefinition type)
        {
            for (int depth = 0; depth < 32; depth++)
            {
                var b = type.BaseType;
                if (b.IsNil) return false;
                if (b.Kind == HandleKind.TypeDefinition)
                {
                    type = md.GetTypeDefinition((TypeDefinitionHandle)b);
                    continue;
                }
                if (b.Kind != HandleKind.TypeReference) return false;
                var r = md.GetTypeReference((TypeReferenceHandle)b);
                return md.GetString(r.Namespace).StartsWith("UnityEngine", StringComparison.Ordinal);
            }
            return false;
        }

        // ------------------------------------------------------------------ Font coverage

        static void CheckGlyphs(string assets, List<string> problems)
        {
            var fonts = Directory.GetFiles(Path.Combine(assets, "Resources", "Fonts"), "*.ttf");
            HashSet<int> covered = null;
            foreach (var f in fonts)
            {
                var c = ReadCmap(File.ReadAllBytes(f));
                if (covered == null) covered = c; else covered.IntersectWith(c);
            }
            var missing = new SortedDictionary<int, List<string>>();
            int scanned = 0;
            foreach (var dir in new[] { "Scripts/Runtime", "Scripts/Core" })
            {
                foreach (var file in Directory.GetFiles(Path.Combine(assets, dir), "*.cs", SearchOption.AllDirectories))
                {
                    scanned++;
                    foreach (var (line, ch) in StringLiteralChars(File.ReadAllText(file)))
                    {
                        if (ch < 128 || covered.Contains(ch)) continue;
                        if (!missing.TryGetValue(ch, out var where)) missing[ch] = where = new List<string>();
                        where.Add(Path.GetFileName(file) + ":" + line);
                    }
                }
            }
            foreach (var kv in missing)
                problems.Add("'" + char.ConvertFromUtf32(kv.Key) + "' (U+" + kv.Key.ToString("X4") + ") is not in the Fredoka font but is used in text at " + string.Join(", ", kv.Value.Take(5)));
            Console.WriteLine("Checked text in " + scanned + " scripts against " + fonts.Length + " font(s) (" + covered.Count + " glyphs)");
        }

        /// <summary>Characters inside string and char literals (comments and code are skipped).</summary>
        static IEnumerable<(int line, int ch)> StringLiteralChars(string src)
        {
            int line = 1;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '\n') { line++; continue; }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    i--;
                    continue;
                }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    for (i += 2; i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/'); i++) if (src[i] == '\n') line++;
                    i++;
                    continue;
                }
                if (c != '"' && c != '\'') continue;
                bool verbatim = i > 0 && (src[i - 1] == '@' || (i > 1 && src[i - 1] == '$' && src[i - 2] == '@'));
                for (i++; i < src.Length; i++)
                {
                    char s = src[i];
                    if (s == '\n') line++;
                    if (!verbatim && s == '\\') { i++; continue; }
                    if (s == c)
                    {
                        if (verbatim && i + 1 < src.Length && src[i + 1] == c) { i++; continue; }
                        break;
                    }
                    if (char.IsHighSurrogate(s) && i + 1 < src.Length) { yield return (line, char.ConvertToUtf32(s, src[i + 1])); i++; }
                    else yield return (line, s);
                }
            }
        }

        /// <summary>Code points mapped by a TrueType font's cmap (formats 4 and 12).</summary>
        static HashSet<int> ReadCmap(byte[] d)
        {
            int U16(int o) => (d[o] << 8) | d[o + 1];
            long U32(int o) => ((long)d[o] << 24) | ((long)d[o + 1] << 16) | ((long)d[o + 2] << 8) | d[o + 3];
            var set = new HashSet<int>();
            int tables = U16(4), cmap = -1;
            for (int i = 0; i < tables; i++)
                if (Encoding.ASCII.GetString(d, 12 + 16 * i, 4) == "cmap") cmap = (int)U32(12 + 16 * i + 8);
            if (cmap < 0) return set;
            int subtables = U16(cmap + 2);
            for (int i = 0; i < subtables; i++)
            {
                int st = cmap + (int)U32(cmap + 4 + 8 * i + 4);
                int format = U16(st);
                if (format == 4)
                {
                    int seg = U16(st + 6) / 2;
                    for (int s = 0; s < seg; s++)
                    {
                        int end = U16(st + 14 + 2 * s), start = U16(st + 16 + 2 * seg + 2 * s);
                        for (int cp = start; cp <= end && cp != 0xFFFF; cp++) set.Add(cp);
                    }
                }
                else if (format == 12)
                {
                    long groups = U32(st + 12);
                    for (int g = 0; g < groups; g++)
                        for (long cp = U32(st + 16 + 12 * g); cp <= U32(st + 20 + 12 * g); cp++) set.Add((int)cp);
                }
            }
            return set;
        }
    }
}
