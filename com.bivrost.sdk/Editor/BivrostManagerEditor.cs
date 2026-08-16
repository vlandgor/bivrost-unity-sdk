#if UNITY_EDITOR
using System;
using Bivrost;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(BivrostManager))]
    [CanEditMultipleObjects]
    internal class BivrostManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!IsLiveKitPackageInstalled())
            {
                EditorGUILayout.HelpBox(
                    "Realtime video/audio (LiveKit) isn't installed. PublishCamera, PublishMicrophone and " +
                    "ConnectLiveKit will silently fail until you install it.",
                    MessageType.Warning);

                if (GUILayout.Button("Install Realtime Module (LiveKit)"))
                {
                    LiveKitDependencyInstaller.Install();
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            DrawDefaultInspector();
        }

        // Independent of RealtimeModuleRegistry (which only populates in Play Mode via
        // [RuntimeInitializeOnLoadMethod]). This checks package presence directly, so the
        // Inspector updates the moment the package resolves — no Play Mode required.
        private static bool IsLiveKitPackageInstalled()
        {
            return Type.GetType("Bivrost.LiveKitManager, Bivrost.LiveKit.Runtime") != null;
        }
    }
}
#endif