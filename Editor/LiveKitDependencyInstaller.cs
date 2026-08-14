#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Ensures the LiveKit Unity SDK is resolvable via UPM by patching the
    /// consuming project's Packages/manifest.json with the OpenUPM scoped
    /// registry + a pinned version, if it isn't already configured.
    ///
    /// Keep PackageVersion in sync with the "io.livekit.livekit-sdk" entry
    /// in Bivrost's own package.json.
    /// </summary>
    [InitializeOnLoad]
    internal static class LiveKitDependencyInstaller
    {
        private const string RegistryName = "package.openupm.com";
        private const string RegistryUrl = "https://package.openupm.com";
        private const string Scope = "io.livekit";
        private const string PackageId = "io.livekit.livekit-sdk";
        private const string PackageVersion = "2.0.0";

        private static string ManifestPath =>
            Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        static LiveKitDependencyInstaller()
        {
            // Delay so we don't fight Unity during the initial domain load.
            EditorApplication.delayCall += () => EnsureDependency(force: false);
        }

        [MenuItem("Bivrost/Verify LiveKit Dependency")]
        private static void EnsureDependencyMenuItem() => EnsureDependency(force: true);

        private static void EnsureDependency(bool force)
        {
            try
            {
                if (!File.Exists(ManifestPath))
                {
                    Debug.LogWarning("[Bivrost] Could not find Packages/manifest.json.");
                    return;
                }

                string manifest = File.ReadAllText(ManifestPath);
                bool changed = false;

                if (!manifest.Contains(RegistryUrl))
                {
                    manifest = AddScopedRegistry(manifest);
                    changed = true;
                }

                if (!manifest.Contains("\"" + PackageId + "\""))
                {
                    manifest = AddDependency(manifest);
                    changed = true;
                }

                if (changed)
                {
                    File.WriteAllText(ManifestPath, manifest);
                    Debug.Log("[Bivrost] Added LiveKit SDK (" + PackageVersion +
                              ") + OpenUPM registry to manifest.json. Resolving packages...");
                    Client.Resolve();
                }
                else if (force)
                {
                    Debug.Log("[Bivrost] LiveKit dependency already configured. Nothing to do.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Bivrost] Could not auto-configure the LiveKit dependency (" +
                    e.Message + "). Add it manually to Packages/manifest.json:\n" +
                    "  scopedRegistries: { name: \"" + RegistryName + "\", url: \"" + RegistryUrl +
                    "\", scopes: [\"" + Scope + "\"] }\n" +
                    "  dependencies: \"" + PackageId + "\": \"" + PackageVersion + "\"");
            }
        }

        private static string AddScopedRegistry(string manifest)
        {
            string registryObject =
                "    {\n" +
                "      \"name\": \"" + RegistryName + "\",\n" +
                "      \"url\": \"" + RegistryUrl + "\",\n" +
                "      \"scopes\": [\"" + Scope + "\"]\n" +
                "    },\n";

            int scopedRegistriesIndex = manifest.IndexOf("\"scopedRegistries\"", StringComparison.Ordinal);
            if (scopedRegistriesIndex >= 0)
            {
                // scopedRegistries already exists (other registries present) —
                // insert ours as the first entry in that array.
                int arrayStart = manifest.IndexOf('[', scopedRegistriesIndex);
                return manifest.Insert(arrayStart + 1, "\n" + registryObject);
            }

            // No scopedRegistries key yet — add it as a new top-level entry.
            string registryBlock =
                "  \"scopedRegistries\": [\n" + registryObject.TrimEnd(',', '\n') + "\n  ],\n";
            int insertAt = manifest.IndexOf('{') + 1;
            return manifest.Insert(insertAt, "\n" + registryBlock);
        }

        private static string AddDependency(string manifest)
        {
            string entry = "    \"" + PackageId + "\": \"" + PackageVersion + "\",\n";
            int depsIndex = manifest.IndexOf("\"dependencies\"", StringComparison.Ordinal);
            if (depsIndex < 0)
            {
                throw new Exception("No \"dependencies\" block found in manifest.json.");
            }

            int braceIndex = manifest.IndexOf('{', depsIndex);
            return manifest.Insert(braceIndex + 1, "\n" + entry);
        }
    }
}
#endif