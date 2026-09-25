using System;
using UnityEngine;

namespace Taiyo.Metaverse.Samples
{
    public sealed class QuickStartHud : MonoBehaviour
    {
        [SerializeField] private MetaverseRuntime runtime;
        [SerializeField] private string worldAddress = string.Empty;
        private string status = "Starting…";

        private void OnEnable()
        {
            if (runtime == null) runtime = FindObjectOfType<MetaverseRuntime>();
            if (runtime == null) return;
            runtime.ConnectionStateChanged += OnStateChanged;
            runtime.Error += OnError;
            status = runtime.State.ToString();
        }

        private void OnDisable()
        {
            if (runtime == null) return;
            runtime.ConnectionStateChanged -= OnStateChanged;
            runtime.Error -= OnError;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 330, 180), GUI.skin.box);
            GUILayout.Label("Taiyo Metaverse Quick Start");
            GUILayout.Label("State: " + status);
            GUILayout.Label("Remote peers: " + (runtime == null ? 0 : runtime.RemotePeers.Count));
            GUILayout.Label("World Addressables address:");
            worldAddress = GUILayout.TextField(worldAddress);
            if (runtime != null && GUILayout.Button("Load world") && !string.IsNullOrWhiteSpace(worldAddress))
                LoadWorld();
            GUILayout.EndArea();
        }

        private async void LoadWorld()
        {
            try { await runtime.LoadWorldAsync(worldAddress); }
            catch (Exception exception) { OnError(exception); }
        }

        private void OnStateChanged(ConnectionState value) => status = value.ToString();
        private void OnError(Exception exception) => status = exception.Message;
    }
}
