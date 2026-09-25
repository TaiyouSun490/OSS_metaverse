using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Taiyo.Metaverse.Editor
{
    public sealed class MetaverseContentPublisherWindow : EditorWindow
    {
        private ContentPublishProfile profile;
        private CancellationTokenSource cancellation;
        private bool publishing;
        private string status = "Select a publish profile.";
        private float progress;

        [MenuItem("Tools/Taiyo Metaverse/Content Publisher")]
        public static void Open() => GetWindow<MetaverseContentPublisherWindow>("Metaverse Publisher");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Addressables Content Publisher", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build Addressables, then copy the catalog and bundles to a folder or upload each file with HTTP PUT. " +
                "Secrets are read only from the configured environment variable.",
                MessageType.Info);
            profile = (ContentPublishProfile)EditorGUILayout.ObjectField("Publish Profile", profile, typeof(ContentPublishProfile), false);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(profile == null || publishing))
            {
                if (GUILayout.Button("Build and Publish", GUILayout.Height(32f)))
                    BeginPublish();
            }
            using (new EditorGUI.DisabledScope(!publishing))
            {
                if (GUILayout.Button("Cancel"))
                    cancellation?.Cancel();
            }

            var rect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.ProgressBar(rect, progress, status);
        }

        private async void BeginPublish()
        {
            publishing = true;
            progress = 0f;
            status = "Building…";
            cancellation = new CancellationTokenSource();
            try
            {
                var reporter = new Progress<float>(value =>
                {
                    progress = value;
                    status = $"Publishing… {value:P0}";
                    Repaint();
                });
                var result = await MetaverseContentPublisher.PublishAsync(profile, reporter, cancellation.Token);
                progress = 1f;
                status = result.DryRun
                    ? $"Dry run: {result.FileCount} files, {EditorUtility.FormatBytes(result.ByteCount)}"
                    : $"Published {result.FileCount} files, {EditorUtility.FormatBytes(result.ByteCount)}";
            }
            catch (OperationCanceledException)
            {
                status = "Cancelled.";
            }
            catch (Exception exception)
            {
                status = "Failed: " + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                publishing = false;
                cancellation.Dispose();
                cancellation = null;
                Repaint();
            }
        }

        private void OnDisable() => cancellation?.Cancel();
    }
}
