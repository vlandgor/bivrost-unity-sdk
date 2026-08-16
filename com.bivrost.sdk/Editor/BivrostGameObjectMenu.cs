#if UNITY_EDITOR
using Bivrost;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    internal static class BivrostGameObjectMenu
    {
        [MenuItem("GameObject/Bivrost/Bivrost Manager", false, 10)]
        private static void CreateBivrostManager(MenuCommand menuCommand)
        {
            var existing = Object.FindAnyObjectByType<BivrostManager>();
            if (existing != null)
            {
                Debug.LogWarning("[BIVROST] A BivrostManager already exists in the scene — selecting it instead of creating a duplicate.");
                Selection.activeObject = existing.gameObject;
                return;
            }

            // Always create as a scene root — BivrostManager calls DontDestroyOnLoad on itself
            // in Awake(), which only works correctly on root GameObjects. Deliberately not
            // parenting under whatever's selected in the Hierarchy, unlike a typical "Create Empty".
            var go = new GameObject("BivrostManager");
            go.AddComponent<BivrostManager>();

            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
    }
}
#endif