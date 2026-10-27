using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Guards the Unity project files themselves: every package the scripts depend on must be declared in
    /// Packages/manifest.json, otherwise Unity opens the project with compile errors (or silently leaves the
    /// game out of the build).
    /// </summary>
    public class ProjectSetupTests
    {
        static readonly Dictionary<string, string> PackageOfReference = new Dictionary<string, string>
        {
            { "UnityEngine.UI", "com.unity.ugui" },
            { "UnityEngine.TestRunner", "com.unity.test-framework" },
            { "UnityEditor.TestRunner", "com.unity.test-framework" },
        };

        // Built-in modules the runtime scripts use (Unity disables modules a manifest doesn't list).
        static readonly string[] RequiredModules = { "com.unity.modules.ui", "com.unity.modules.audio", "com.unity.modules.androidjni" };

        [Test]
        public void ManifestDeclaresEveryPackageTheAssembliesNeed()
        {
            string root = ProjectRoot();
            var declared = DeclaredPackages(root);
            foreach (var path in Directory.GetFiles(Path.Combine(root, "Assets", "ColourCascade"), "*.asmdef", SearchOption.AllDirectories))
            {
                var asmdef = (Dictionary<string, object>)Json.Parse(File.ReadAllText(path));
                string name = Path.GetFileName(path);
                foreach (string reference in (JsonRead.Arr(asmdef, "references") ?? new List<object>()).Cast<string>())
                {
                    string package;
                    if (PackageOfReference.TryGetValue(reference, out package))
                        Assert.That(declared, Has.Member(package), name + " references " + reference);
                }

                // A define constraint fed by a version define excludes the assembly unless that package is installed.
                var constraints = (JsonRead.Arr(asmdef, "defineConstraints") ?? new List<object>()).Cast<string>().ToList();
                foreach (Dictionary<string, object> versionDefine in JsonRead.Arr(asmdef, "versionDefines") ?? new List<object>())
                {
                    if (constraints.Contains((string)versionDefine["define"]))
                        Assert.That(declared, Has.Member((string)versionDefine["name"]), name + " only compiles with this package");
                }
            }
        }

        [Test]
        public void ManifestEnablesTheModulesTheGameUses()
        {
            var declared = DeclaredPackages(ProjectRoot());
            foreach (var module in RequiredModules) Assert.That(declared, Has.Member(module));
        }

        static HashSet<string> DeclaredPackages(string root)
        {
            var manifest = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(root, "Packages", "manifest.json")));
            return new HashSet<string>(JsonRead.Obj(manifest, "dependencies").Keys);
        }

        /// <summary>The Unity project folder: the working directory in Unity, a parent of the build output in .NET.</summary>
        static string ProjectRoot()
        {
            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "ColourCascade"))) return dir.FullName;
                string nested = Path.Combine(dir.FullName, "ColourCascade");
                if (Directory.Exists(Path.Combine(nested, "Assets", "ColourCascade"))) return nested;
            }
            Assert.Fail("Unity project folder not found from " + Directory.GetCurrentDirectory());
            return null;
        }
    }
}
