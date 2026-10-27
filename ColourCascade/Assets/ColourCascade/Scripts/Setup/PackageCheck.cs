using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ColourCascade.EditorTools
{
    /// <summary>
    /// Installs the Unity packages the game needs when a project is missing them, for example when an editor
    /// generated its own package manifest without Unity UI. The game's assemblies only compile once Unity UI is
    /// present (see their Define Constraints), so this assembly depends on nothing and always compiles: it adds
    /// what is missing and Unity then recompiles the game. When everything is installed it does nothing.
    /// </summary>
    [InitializeOnLoad]
    static class PackageCheck
    {
        const string AttemptedKey = "ColourCascade.PackageCheck.Attempted.";
        static readonly Queue<string> pending = new Queue<string>();
        static AddRequest request;
        static string current;

        static PackageCheck()
        {
#if !COLOURCASCADE_UGUI
            Require("com.unity.ugui");
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER && !COLOURCASCADE_INPUTSYSTEM
            // The project only accepts input through the Input System, which isn't installed: without it nothing
            // can receive taps (the game supports either input backend).
            Require("com.unity.inputsystem");
#endif
            if (pending.Count > 0) EditorApplication.delayCall += InstallNext;
        }

        static void Require(string package)
        {
            // Once per editor session per package, so a failure (e.g. offline) can't loop on every recompile.
            if (!SessionState.GetBool(AttemptedKey + package, false)) pending.Enqueue(package);
        }

        static void InstallNext()
        {
            if (request != null || pending.Count == 0) return;
            current = pending.Dequeue();
            SessionState.SetBool(AttemptedKey + current, true);
            Debug.Log("[Colour Cascade] Installing the missing package '" + current + "'...");
            request = Client.Add(current);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success)
                Debug.Log("[Colour Cascade] Installed " + request.Result.packageId + ". Unity will now recompile the game.");
            else
                Debug.LogError("[Colour Cascade] Could not install '" + current + "' automatically" +
                               (request.Error != null ? " (" + request.Error.message + ")" : "") +
                               ". Open Window > Package Manager, press + > Install package by name and enter " + current + ".");
            request = null;
            InstallNext();
        }
    }
}
