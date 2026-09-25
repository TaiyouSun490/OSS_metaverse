using UnityEditor;
using UnityEngine;

namespace Taiyo.Metaverse.Editor
{
    public enum ContentPublishTarget
    {
        LocalFolder,
        HttpPut
    }

    [CreateAssetMenu(menuName = "Taiyo Metaverse/Content Publish Profile", fileName = "ContentPublishProfile")]
    public sealed class ContentPublishProfile : ScriptableObject
    {
        [Tooltip("Addressables build output. [BuildTarget] is replaced at publish time.")]
        public string sourceDirectory = "ServerData/[BuildTarget]";
        public ContentPublishTarget target = ContentPublishTarget.LocalFolder;
        [Tooltip("Destination folder for LocalFolder, or base URL for HttpPut.")]
        public string destination = "PublishedContent/[BuildTarget]";
        [Tooltip("For HTTP publishing, the bearer token is read from this environment variable and is never serialized.")]
        public string bearerTokenEnvironmentVariable = "TAIYO_METAVERSE_UPLOAD_TOKEN";
        public bool buildAddressablesBeforeUpload = true;
        public bool dryRun;

        public string Resolve(string value)
        {
            return (value ?? string.Empty).Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
        }
    }
}
