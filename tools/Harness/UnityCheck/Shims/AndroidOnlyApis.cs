// Harness-only shim. The UnityEngine.Modules reference package was produced from a desktop player, so it
// omits mobile-only APIs such as UnityEngine.Handheld. Real Android builds (and the Unity Editor) provide
// them. This file is only compiled by the UnityCheck project for -p:UnityPlatform=Android; it never ships.
namespace UnityEngine
{
    public static class Handheld
    {
        public static void Vibrate() { }
    }
}
