using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Networking;

namespace Taiyo.Metaverse.Editor
{
    public readonly struct PublishResult
    {
        public readonly int FileCount;
        public readonly long ByteCount;
        public readonly bool DryRun;

        public PublishResult(int fileCount, long byteCount, bool dryRun)
        {
            FileCount = fileCount;
            ByteCount = byteCount;
            DryRun = dryRun;
        }
    }

    public static class MetaverseContentPublisher
    {
        public static async Task<PublishResult> PublishAsync(
            ContentPublishProfile profile,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (profile.buildAddressablesBeforeUpload)
            {
                AddressableAssetSettings.BuildPlayerContent(out var buildResult);
                if (!string.IsNullOrWhiteSpace(buildResult.Error))
                    throw new InvalidOperationException("Addressables build failed: " + buildResult.Error);
            }

            var source = Path.GetFullPath(profile.Resolve(profile.sourceDirectory));
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException($"Addressables output does not exist: {source}");

            var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var totalBytes = files.Sum(path => new FileInfo(path).Length);
            if (profile.dryRun)
                return new PublishResult(files.Length, totalBytes, true);

            switch (profile.target)
            {
                case ContentPublishTarget.LocalFolder:
                    PublishToFolder(profile, source, files, progress, cancellationToken);
                    break;
                case ContentPublishTarget.HttpPut:
                    await PublishHttpAsync(profile, source, files, progress, cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new PublishResult(files.Length, totalBytes, false);
        }

        private static void PublishToFolder(
            ContentPublishProfile profile,
            string source,
            IReadOnlyList<string> files,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var destination = Path.GetFullPath(profile.Resolve(profile.destination));
            if (string.Equals(source.TrimEnd(Path.DirectorySeparatorChar), destination.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Source and destination folders must be different.");

            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = GetRelativePath(source, files[i]);
                var target = Path.Combine(destination, relative);
                var directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.Copy(files[i], target, true);
                progress?.Report((i + 1f) / files.Count);
            }
        }

        private static async Task PublishHttpAsync(
            ContentPublishProfile profile,
            string source,
            IReadOnlyList<string> files,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var baseUrl = profile.Resolve(profile.destination).TrimEnd('/');
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("HTTP destination must be an absolute http(s) URL.");

            var token = string.IsNullOrWhiteSpace(profile.bearerTokenEnvironmentVariable)
                ? null
                : Environment.GetEnvironmentVariable(profile.bearerTokenEnvironmentVariable);

            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = GetRelativePath(source, files[i]).Replace('\\', '/');
                var url = baseUrl + "/" + string.Join("/", relative.Split('/').Select(Uri.EscapeDataString));
                var request = UnityWebRequest.Put(url, File.ReadAllBytes(files[i]));
                try
                {
                    request.SetRequestHeader("Content-Type", "application/octet-stream");
                    if (!string.IsNullOrEmpty(token))
                        request.SetRequestHeader("Authorization", "Bearer " + token);
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new InvalidOperationException($"Upload failed ({request.responseCode}) {url}: {request.error}");
                }
                finally
                {
                    request.Dispose();
                }
                progress?.Report((i + 1f) / files.Count);
            }
        }

        private static string GetRelativePath(string root, string path)
        {
            var rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(new Uri(path)).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
