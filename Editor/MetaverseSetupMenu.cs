using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Taiyo.Metaverse.Editor
{
    public static class MetaverseSetupMenu
    {
        private const string SettingsFolder = "Assets/Metaverse/Settings";

        [MenuItem("Tools/Taiyo Metaverse/Create Bootstrap")]
        public static void CreateBootstrap()
        {
            EnsureFolder("Assets", "Metaverse");
            EnsureFolder("Assets/Metaverse", "Settings");

            var network = LoadOrCreate<LoopbackNetworkProvider>($"{SettingsFolder}/LoopbackNetworkProvider.asset");
            var voice = LoadOrCreate<PcmVoiceProvider>($"{SettingsFolder}/PcmVoiceProvider.asset");
            var account = LoadOrCreate<LocalAccountProvider>($"{SettingsFolder}/LocalAccountProvider.asset");
            var config = LoadOrCreate<MetaverseConfig>($"{SettingsFolder}/MetaverseConfig.asset");
            config.EditorAssignProviders(network, voice, account);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var root = new GameObject("Metaverse Runtime");
            Undo.RegisterCreatedObjectUndo(root, "Create Metaverse Bootstrap");
            var content = Undo.AddComponent<RemoteContentService>(root);
            var runtime = Undo.AddComponent<MetaverseRuntime>(root);
            var serialized = new SerializedObject(runtime);
            serialized.FindProperty("configuration").objectReferenceValue = config;
            serialized.FindProperty("contentService").objectReferenceValue = content;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Created Metaverse Runtime. Assign a TrackedPoseSource, then press Play.", root);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var combined = Path.Combine(parent, child).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(combined))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
