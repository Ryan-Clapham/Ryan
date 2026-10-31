using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Immutable;
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
    /// 3. Fields Unity would serialize (public or [SerializeField] on components, ScriptableObjects and [Serializable]
    ///    types) whose type is one of the game's own types without [Serializable] (Unity warning UAC1001).
    /// 4. Sound without a listener: the game's scene is empty and everything is built in code, so if the code
    ///    plays audio it must also create an AudioListener, or the game is silent.
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
            CheckSerialization(Path.GetFullPath(paths.RuntimeDll), problems);
            CheckAudioListener(Path.GetFullPath(paths.RuntimeDll), problems);
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

        // ------------------------------------------------------------------ Audio listener

        static void CheckAudioListener(string dll, List<string> problems)
        {
            using var stream = File.OpenRead(dll);
            using var pe = new PEReader(stream);
            var md = pe.GetMetadataReader();
            var used = new HashSet<string>();
            foreach (var h in md.TypeReferences)
            {
                var r = md.GetTypeReference(h);
                if (md.GetString(r.Namespace) == "UnityEngine") used.Add(md.GetString(r.Name));
            }
            if (used.Contains("AudioSource") && !used.Contains("AudioListener"))
                problems.Add("The game creates AudioSources but never an AudioListener; the scene has none either, so nothing would be heard.");
            Console.WriteLine("Checked that audio has a listener");
        }

        // ------------------------------------------------------------------ Serialized fields

        sealed class TypeFacts
        {
            public bool Serializable, Enum, UnityObject, Interface;
        }

        /// <summary>Resolves field types to "Namespace.Name" keys; List&lt;T&gt; and T[] resolve to T.</summary>
        sealed class TypeNames : ISignatureTypeProvider<string, object>
        {
            public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "";
            public string GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle handle, byte rawTypeKind) => FullName(md, handle);
            public string GetTypeFromReference(MetadataReader md, TypeReferenceHandle handle, byte rawTypeKind) => FullName(md, handle);
            public string GetTypeFromSpecification(MetadataReader md, object context, TypeSpecificationHandle handle, byte rawTypeKind) => "";
            public string GetSZArrayType(string elementType) => elementType;
            public string GetArrayType(string elementType, ArrayShape shape) => "";
            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
                genericType == "System.Collections.Generic.List`1" ? typeArguments[0] : "";
            public string GetByReferenceType(string elementType) => "";
            public string GetPointerType(string elementType) => "";
            public string GetPinnedType(string elementType) => "";
            public string GetFunctionPointerType(MethodSignature<string> signature) => "";
            public string GetGenericMethodParameter(object context, int index) => "";
            public string GetGenericTypeParameter(object context, int index) => "";
            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        }

        static string FullName(MetadataReader md, TypeDefinitionHandle handle)
        {
            var t = md.GetTypeDefinition(handle);
            var outer = t.GetDeclaringType();
            string name = md.GetString(t.Name);
            return outer.IsNil ? Join(md.GetString(t.Namespace), name) : FullName(md, outer) + "+" + name;
        }

        static string FullName(MetadataReader md, TypeReferenceHandle handle)
        {
            var r = md.GetTypeReference(handle);
            string name = md.GetString(r.Name);
            return r.ResolutionScope.Kind == HandleKind.TypeReference
                ? FullName(md, (TypeReferenceHandle)r.ResolutionScope) + "+" + name
                : Join(md.GetString(r.Namespace), name);
        }

        static string Join(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;

        static string BaseName(MetadataReader md, EntityHandle b) =>
            b.IsNil ? "" : b.Kind == HandleKind.TypeDefinition ? FullName(md, (TypeDefinitionHandle)b)
            : b.Kind == HandleKind.TypeReference ? FullName(md, (TypeReferenceHandle)b) : "";

        static void CheckSerialization(string runtimeDll, List<string> problems)
        {
            string dir = Path.GetDirectoryName(runtimeDll);
            var readers = new List<(PEReader pe, MetadataReader md)>();
            foreach (var name in new[] { "ColourCascade.Runtime.dll", "ColourCascade.Core.dll", "ColourCascade.Procedural.dll" })
            {
                var pe = new PEReader(File.OpenRead(Path.Combine(dir, name)));
                readers.Add((pe, pe.GetMetadataReader()));
            }

            // Facts about every type the game defines, by full name.
            var facts = new Dictionary<string, TypeFacts>();
            var bases = new Dictionary<string, string>();
            foreach (var (_, md) in readers)
                foreach (var h in md.TypeDefinitions)
                {
                    var t = md.GetTypeDefinition(h);
                    string full = FullName(md, h);
                    string baseName = BaseName(md, t.BaseType);
                    bases[full] = baseName;
                    facts[full] = new TypeFacts
                    {
                        Serializable = (t.Attributes & TypeAttributes.Serializable) != 0,
                        Enum = baseName == "System.Enum",
                        Interface = (t.Attributes & TypeAttributes.Interface) != 0,
                    };
                }
            foreach (var kv in facts)
            {
                string b = bases[kv.Key];
                for (int depth = 0; depth < 32 && b.Length > 0; depth++)
                {
                    if (b.StartsWith("UnityEngine.", StringComparison.Ordinal)) { kv.Value.UnityObject = true; break; }
                    if (!bases.TryGetValue(b, out b)) break;
                }
            }

            var provider = new TypeNames();
            int checkedFields = 0;
            foreach (var (_, md) in readers)
            {
                foreach (var h in md.TypeDefinitions)
                {
                    var owner = md.GetTypeDefinition(h);
                    string ownerName = FullName(md, h);
                    var ownerFacts = facts[ownerName];
                    if (!ownerFacts.UnityObject && !ownerFacts.Serializable) continue;
                    foreach (var fh in owner.GetFields())
                    {
                        var f = md.GetFieldDefinition(fh);
                        var a = f.Attributes;
                        if ((a & (FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.InitOnly | FieldAttributes.NotSerialized)) != 0) continue;
                        bool isPublic = (a & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
                        if (!isPublic && !HasAttribute(md, f.GetCustomAttributes(), "SerializeField")) continue;
                        checkedFields++;
                        string fieldType = f.DecodeSignature(provider, null);
                        if (!facts.TryGetValue(fieldType, out var tf) || tf.Serializable || tf.Enum || tf.UnityObject || tf.Interface) continue;
                        problems.Add(ownerName + "." + md.GetString(f.Name) + " is saved by Unity, but its type " + fieldType +
                            " isn't [Serializable] (Unity warning UAC1001). Mark the field [System.NonSerialized] or the type [System.Serializable].");
                    }
                }
            }
            foreach (var (pe, _) in readers) pe.Dispose();
            Console.WriteLine("Checked " + checkedFields + " serialized fields for types Unity can't save");
        }

        static bool HasAttribute(MetadataReader md, CustomAttributeHandleCollection attributes, string name)
        {
            foreach (var h in attributes)
            {
                var ctor = md.GetCustomAttribute(h).Constructor;
                EntityHandle type = default;
                if (ctor.Kind == HandleKind.MemberReference) type = md.GetMemberReference((MemberReferenceHandle)ctor).Parent;
                else if (ctor.Kind == HandleKind.MethodDefinition) type = md.GetMethodDefinition((MethodDefinitionHandle)ctor).GetDeclaringType();
                string full = BaseName(md, type);
                if (full.EndsWith("." + name, StringComparison.Ordinal) || full.EndsWith("." + name + "Attribute", StringComparison.Ordinal)) return true;
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
