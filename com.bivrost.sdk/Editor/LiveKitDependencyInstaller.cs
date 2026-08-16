#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Editor
{
    internal static class LiveKitDependencyInstaller
    {
        private const string RegistryName = "package.openupm.com";
        private const string RegistryUrl = "https://package.openupm.com";
        private const string Scope = "io.livekit";
        private const string ExtensionPackageId = "com.bivrost.sdk.livekit";
        private const string ExtensionGitUrl = "https://github.com/vlandgor/bivrost-unity-sdk.git?path=/com.bivrost.sdk.livekit";

        private static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        internal static void Install(bool force = true)
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

                if (!manifest.Contains("\"" + ExtensionPackageId + "\""))
                {
                    manifest = AddExtensionDependency(manifest);
                    changed = true;
                }

                if (changed)
                {
                    File.WriteAllText(ManifestPath, manifest);
                    Debug.Log("[Bivrost] Installing realtime module (LiveKit)... Unity will reload after resolving.");
                    Client.Resolve();
                }
                else if (force)
                {
                    Debug.Log("[Bivrost] Realtime module already installed. Nothing to do.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Bivrost] Could not auto-install the realtime module (" +
                    e.Message + "). Add these to Packages/manifest.json manually:\n" +
                    "  scopedRegistries: { name: \"" + RegistryName + "\", url: \"" + RegistryUrl +
                    "\", scopes: [\"" + Scope + "\"] }\n" +
                    "  dependencies: \"" + ExtensionPackageId + "\": \"" + ExtensionGitUrl + "\"");
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
                int arrayStart = manifest.IndexOf('[', scopedRegistriesIndex);
                return manifest.Insert(arrayStart + 1, "\n" + registryObject);
            }

            string registryBlock =
                "  \"scopedRegistries\": [\n" + registryObject.TrimEnd(',', '\n') + "\n  ],\n";
            int insertAt = manifest.IndexOf('{') + 1;
            return manifest.Insert(insertAt, "\n" + registryBlock);
        }

        private static string AddExtensionDependency(string manifest)
        {
            string entry = "    \"" + ExtensionPackageId + "\": \"" + ExtensionGitUrl + "\",\n";
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