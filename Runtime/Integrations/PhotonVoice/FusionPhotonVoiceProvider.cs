#if TAIYO_METAVERSE_FUSION && TAIYO_METAVERSE_PHOTON_VOICE
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using Taiyo.Metaverse.FusionAdapter;
using UnityEngine;

namespace Taiyo.Metaverse.PhotonVoice
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Voice/Photon Voice for Fusion", fileName = "FusionPhotonVoiceProvider")]
    public sealed class FusionPhotonVoiceProvider : VoiceProvider
    {
        [Tooltip("Prefab containing Speaker and AudioSource. Use spatialBlend=1 for proximity voice.")]
        [SerializeField] private GameObject speakerPrefab;
        [SerializeField] private bool encrypt = true;
        [SerializeField] private bool voiceDetection = true;
        [SerializeField, Range(0.001f, 0.1f)] private float voiceDetectionThreshold = 0.01f;

        private readonly Dictionary<Speaker, PeerId> speakers = new Dictionary<Speaker, PeerId>();
        private ManagedFusionVoiceClient voiceClient;
        private Recorder recorder;
        private NetworkProvider network;
        private SafetyService safety;
        private bool inputMuted;

        public override bool IsCapturing => recorder != null && recorder.RecordingEnabled;

        public override bool InputMuted
        {
            get => inputMuted;
            set
            {
                inputMuted = value;
                if (recorder != null)
                    recorder.TransmitEnabled = !value;
            }
        }

        public override Task InitializeAsync(
            GameObject host,
            NetworkProvider network,
            SafetyService safetyService,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!(network is FusionNetworkProvider fusionProvider) || fusionProvider.Runner == null)
                throw new InvalidOperationException("Photon Voice for Fusion requires FusionNetworkProvider.");
            if (speakerPrefab == null || speakerPrefab.GetComponentInChildren<Speaker>(true) == null)
                throw new InvalidOperationException("Photon Voice requires a speaker prefab containing Speaker and AudioSource components.");

            safety = safetyService;
            this.network = network;
            var runnerObject = fusionProvider.Runner.gameObject;
            recorder = runnerObject.GetComponent<Recorder>() ?? runnerObject.AddComponent<Recorder>();
            recorder.SourceType = Recorder.InputSourceType.Microphone;
            recorder.RecordWhenJoined = true;
            recorder.RecordingEnabled = false;
            recorder.TransmitEnabled = !inputMuted;
            recorder.Encrypt = encrypt;
            recorder.ReliableMode = false;
            recorder.VoiceDetection = voiceDetection;
            recorder.VoiceDetectionThreshold = voiceDetectionThreshold;
            recorder.UseMicrophoneTypeFallback = true;

            voiceClient = runnerObject.GetComponent<ManagedFusionVoiceClient>() ??
                          runnerObject.AddComponent<ManagedFusionVoiceClient>();
            voiceClient.Configure(recorder, speakerPrefab);
            voiceClient.SpeakerLinked += OnSpeakerLinked;
            safety.Changed += ApplySafety;
            return Task.CompletedTask;
        }

        public override Task StartCaptureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recorder != null)
            {
                recorder.UserData = network != null ? network.LocalPeer.Value : string.Empty;
                recorder.RecordingEnabled = true;
                recorder.TransmitEnabled = !inputMuted;
            }
            return Task.CompletedTask;
        }

        public override void Tick(float unscaledDeltaTime)
        {
            // Remove Unity objects destroyed by Photon Voice after a remote stream closes.
            if (speakers.Count == 0)
                return;
            var removed = ListPool<Speaker>.Get();
            foreach (var pair in speakers)
            {
                if (pair.Key == null)
                    removed.Add(pair.Key);
            }
            foreach (var speaker in removed)
                speakers.Remove(speaker);
            ListPool<Speaker>.Release(removed);
        }

        public override void Shutdown()
        {
            if (safety != null)
                safety.Changed -= ApplySafety;
            if (voiceClient != null)
                voiceClient.SpeakerLinked -= OnSpeakerLinked;
            if (recorder != null)
            {
                recorder.TransmitEnabled = false;
                recorder.RecordingEnabled = false;
            }
            speakers.Clear();
            safety = null;
            network = null;
            voiceClient = null;
            recorder = null;
        }

        private void OnSpeakerLinked(Speaker speaker)
        {
            if (speaker == null || speaker.RemoteVoice == null)
                return;
            var advertisedPeer = speaker.RemoteVoice.Info.UserData as string;
            speakers[speaker] = new PeerId(string.IsNullOrWhiteSpace(advertisedPeer)
                ? speaker.RemoteVoice.PlayerId.ToString()
                : advertisedPeer);
            ApplySafetyTo(speaker, speakers[speaker]);
        }

        private void ApplySafety()
        {
            foreach (var pair in speakers)
            {
                if (pair.Key != null)
                    ApplySafetyTo(pair.Key, pair.Value);
            }
        }

        private void ApplySafetyTo(Speaker speaker, PeerId peer)
        {
            var source = speaker.GetComponentInChildren<AudioSource>(true);
            if (source != null)
                source.mute = safety != null && (safety.IsMuted(peer) || safety.IsBlocked(peer));
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

            public static List<T> Get() => Pool.Count == 0 ? new List<T>() : Pool.Pop();

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }

    internal sealed class ManagedFusionVoiceClient : FusionVoiceClient
    {
        private static readonly FieldInfo UsePrimaryRecorderField =
            typeof(VoiceConnection).GetField("usePrimaryRecorder", BindingFlags.Instance | BindingFlags.NonPublic);

        internal void Configure(Recorder primaryRecorder, GameObject remoteSpeakerPrefab)
        {
            UseFusionAppSettings = true;
            UseFusionAuthValues = true;
            AutoConnectAndJoin = true;
            if (UsePrimaryRecorderField == null)
                throw new MissingFieldException(typeof(VoiceConnection).FullName, "usePrimaryRecorder");
            UsePrimaryRecorderField.SetValue(this, true);
            PrimaryRecorder = primaryRecorder;
            SpeakerPrefab = remoteSpeakerPrefab;
        }
    }
}
#endif
