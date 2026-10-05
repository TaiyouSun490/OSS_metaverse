#if TAIYO_METAVERSE_META_SPATIAL
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse.MetaSpatial
{
    /// <summary>Group sharing via Meta XR Core SDK. Networking is supplied by MetaverseRuntime.</summary>
    public sealed class MetaSharedAnchorProvider : SharedAnchorProvider
    {
        OVRSpatialAnchor current;
        int generation;
        public override bool IsLocalized => current && current.Localized && current.IsTracked;
        public override Pose AnchorPose => IsLocalized
            ? new Pose(current.transform.position, current.transform.rotation) : Pose.identity;

        public override async Task<Guid> CreateAndShareAsync(Guid group, Pose pose, CancellationToken cancellation)
        {
            CheckPlatform(); cancellation.ThrowIfCancellationRequested();
            int revision = generation;
            var go = new GameObject("Metaverse Shared Anchor");
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var created = current = go.AddComponent<OVRSpatialAnchor>();
            if (!await created.WhenCreatedAsync()) throw new InvalidOperationException("Meta anchor creation failed. Check anchor permissions and tracking.");
            Check(revision, cancellation);
            if (!await created.WhenLocalizedAsync()) throw new InvalidOperationException("Meta anchor could not localize.");
            Check(revision, cancellation);
            var saved = await created.SaveAnchorAsync();
            Check(revision, cancellation);
            if (!saved.Success) throw Failure("save", saved.Status.ToString());
            var shared = await OVRSpatialAnchor.ShareAsync(new[] { created }, group);
            Check(revision, cancellation);
            if (!shared.Success) throw Failure("share", shared.Status.ToString());
            while (!IsLocalized) { Check(revision, cancellation); await Task.Yield(); }
            return created.Uuid;
        }
        public override async Task LoadAsync(Guid group, Guid anchor, CancellationToken cancellation)
        {
            CheckPlatform(); cancellation.ThrowIfCancellationRequested();
            int revision = generation;
            var anchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var loaded = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(group, anchors);
            Check(revision, cancellation);
            if (!loaded.Success) throw Failure("load", loaded.Status.ToString());
            foreach (var unbound in anchors)
            {
                if (unbound.Uuid != anchor) continue;
                if (!await unbound.LocalizeAsync(30)) throw new InvalidOperationException("Anchor not recognized. Look around the same room, then retry.");
                Check(revision, cancellation);
                var go = new GameObject("Metaverse Shared Anchor");
                current = go.AddComponent<OVRSpatialAnchor>();
                unbound.BindTo(current);
                if (!await current.WhenLocalizedAsync()) throw new InvalidOperationException("Shared anchor binding failed.");
                Check(revision, cancellation);
                while (!IsLocalized) { Check(revision, cancellation); await Task.Yield(); }
                return;
            }
            throw new InvalidOperationException("Host anchor is not available yet. Retry alignment.");
        }
        void Check(int revision, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (revision != generation || !this) throw new OperationCanceledException();
        }
        static void CheckPlatform()
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            throw new NotSupportedException("Meta shared anchors require a Quest device build. Separate spaces works without Meta services.");
#endif
        }
        static Exception Failure(string operation, string result) => new InvalidOperationException(
            "Meta anchor " + operation + ": " + result + ". Check internet, Enhanced Spatial Services and anchor permissions; then Retry alignment.");
        public override void Release()
        {
            generation++;
            if (current) Destroy(current.gameObject);
            current = null;
        }
        void OnDestroy() => Release();
    }
}
#endif
