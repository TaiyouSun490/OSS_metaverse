using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Taiyo.Metaverse
{
    public sealed class RemoteContentService : MonoBehaviour
    {
        [SerializeField] private List<string> remoteCatalogUrls = new List<string>();
        [SerializeField] private bool autoLoadCatalogs = true;

        private readonly List<AsyncOperationHandle> catalogHandles = new List<AsyncOperationHandle>();
        private AsyncOperationHandle<SceneInstance>? currentWorld;

        public bool CatalogsLoaded { get; private set; }

        private async void Start()
        {
            if (autoLoadCatalogs)
            {
                try { await LoadCatalogsAsync(destroyCancellationToken); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        public async Task LoadCatalogsAsync(CancellationToken cancellationToken)
        {
            foreach (var url in remoteCatalogUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(url))
                    continue;
                var handle = Addressables.LoadContentCatalogAsync(url, false);
                await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw handle.OperationException ?? new InvalidOperationException($"Could not load catalog: {url}");
                catalogHandles.Add(handle);
            }
            CatalogsLoaded = true;
        }

        public async Task<GameObject> InstantiateAvatarAsync(string address, Transform parent, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.InstantiateAsync(address, parent, false);
            await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw handle.OperationException ?? new InvalidOperationException($"Could not load avatar '{address}'.");
            return handle.Result;
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance != null)
                Addressables.ReleaseInstance(instance);
        }

        public async Task LoadWorldAsync(string address, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("A world Addressables address is required.", nameof(address));

            if (currentWorld.HasValue && currentWorld.Value.IsValid())
            {
                var unload = Addressables.UnloadSceneAsync(currentWorld.Value, true);
                await unload.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Additive, true);
            await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw handle.OperationException ?? new InvalidOperationException($"Could not load world '{address}'.");
            currentWorld = handle;
        }

        private void OnDestroy()
        {
            foreach (var handle in catalogHandles)
                if (handle.IsValid()) Addressables.Release(handle);
            catalogHandles.Clear();
        }
    }
}
