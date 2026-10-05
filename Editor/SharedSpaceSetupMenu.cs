using UnityEditor;
using UnityEngine;

namespace Taiyo.Metaverse.Editor
{
    public static class SharedSpaceSetupMenu
    {
        [MenuItem("GameObject/Taiyo Metaverse/Add Shared Space", false, 20)]
        public static void Add()
        {
            var runtime = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<MetaverseRuntime>() : null;
            if (!runtime) { Debug.LogError("Select a GameObject with MetaverseRuntime first."); return; }
            var space = runtime.GetComponent<SharedSpaceSession>() ?? Undo.AddComponent<SharedSpaceSession>(runtime.gameObject);
            Undo.RecordObject(space, "Configure shared space"); space.runtime = runtime;
            if (!space.separateOrigin)
            {
                var origin = new GameObject("Local Virtual Origin"); Undo.RegisterCreatedObjectUndo(origin, "Create virtual origin");
                origin.transform.SetParent(runtime.transform, false); space.separateOrigin = origin.transform;
            }
            var serialized = new SerializedObject(runtime);
            serialized.FindProperty("sharedSpace").objectReferenceValue = space; serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(space); Selection.activeGameObject = space.gameObject;
        }
    }
}
