using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    /// <summary>
    /// Dependency-free microphone voice for prototypes and LAN testing.
    /// It sends raw 16-bit mono PCM; use an Opus/Vivox/Photon Voice provider in production.
    /// </summary>
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Voice/PCM Voice Provider", fileName = "PcmVoiceProvider")]
    public sealed class PcmVoiceProvider : VoiceProvider
    {
        [SerializeField, Range(8000, 48000)] private int sampleRate = 24000;
        [SerializeField, Range(10, 50)] private int packetRate = 25;
        [SerializeField, Range(0f, 2f)] private float outputVolume = 1f;
        [SerializeField] private string microphoneDevice = string.Empty;

        private readonly Dictionary<PeerId, StreamingSpeaker> speakers = new Dictionary<PeerId, StreamingSpeaker>();
        private NetworkProvider network;
        private SafetyService safety;
        private GameObject host;
        private AudioClip recording;
        private int lastSamplePosition;
        private float captureTimer;

        public override bool IsCapturing => recording != null;
        public override bool InputMuted { get; set; }

        public override Task InitializeAsync(
            GameObject hostObject,
            NetworkProvider networkProvider,
            SafetyService safetyService,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            host = hostObject;
            network = networkProvider;
            safety = safetyService;
            network.MessageReceived += OnMessage;
            network.PeerLeft += OnPeerLeft;
            return Task.CompletedTask;
        }

        public override Task StartCaptureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recording != null || Microphone.devices.Length == 0)
                return Task.CompletedTask;

            var device = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
            recording = Microphone.Start(device, true, 1, sampleRate);
            lastSamplePosition = 0;
            captureTimer = 0f;
            return Task.CompletedTask;
        }

        public override void Tick(float unscaledDeltaTime)
        {
            if (recording == null || InputMuted || network == null || network.State != ConnectionState.Joined)
                return;

            captureTimer += unscaledDeltaTime;
            if (captureTimer < 1f / packetRate)
                return;
            captureTimer = 0f;

            var device = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
            var position = Microphone.GetPosition(device);
            if (position < 0 || position == lastSamplePosition)
                return;

            var available = position >= lastSamplePosition
                ? position - lastSamplePosition
                : recording.samples - lastSamplePosition + position;
            var maximum = Mathf.Max(1, sampleRate / packetRate * 2);
            available = Mathf.Min(available, maximum);

            var samples = new float[available];
            var firstCount = Mathf.Min(available, recording.samples - lastSamplePosition);
            var first = new float[firstCount];
            recording.GetData(first, lastSamplePosition);
            Array.Copy(first, 0, samples, 0, firstCount);
            if (firstCount < available)
            {
                var second = new float[available - firstCount];
                recording.GetData(second, 0);
                Array.Copy(second, 0, samples, firstCount, second.Length);
            }

            lastSamplePosition = (lastSamplePosition + available) % recording.samples;
            var bytes = EncodePcm16(samples);
            network.Send(MetaverseChannels.Voice, new ArraySegment<byte>(bytes), DeliveryMode.Unreliable);
        }

        public override void Shutdown()
        {
            if (network != null)
            {
                network.MessageReceived -= OnMessage;
                network.PeerLeft -= OnPeerLeft;
            }

            if (recording != null)
            {
                var device = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
                Microphone.End(device);
                recording = null;
            }

            foreach (var speaker in speakers.Values)
                speaker.Dispose();
            speakers.Clear();
        }

        private void OnMessage(NetworkMessage message)
        {
            if (message.Channel != MetaverseChannels.Voice || safety.IsMuted(message.Sender) || safety.IsBlocked(message.Sender))
                return;
            if (!speakers.TryGetValue(message.Sender, out var speaker))
            {
                speaker = new StreamingSpeaker(host.transform, message.Sender, sampleRate, outputVolume);
                speakers.Add(message.Sender, speaker);
            }
            speaker.Enqueue(DecodePcm16(message.Payload));
        }

        private void OnPeerLeft(PeerInfo peer)
        {
            if (!speakers.TryGetValue(peer.Id, out var speaker))
                return;
            speaker.Dispose();
            speakers.Remove(peer.Id);
        }

        private static byte[] EncodePcm16(float[] samples)
        {
            var result = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++)
            {
                var value = (short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                result[i * 2] = (byte)value;
                result[i * 2 + 1] = (byte)(value >> 8);
            }
            return result;
        }

        private static float[] DecodePcm16(ArraySegment<byte> payload)
        {
            var samples = new float[payload.Count / 2];
            for (var i = 0; i < samples.Length; i++)
            {
                var offset = payload.Offset + i * 2;
                var value = (short)(payload.Array[offset] | payload.Array[offset + 1] << 8);
                samples[i] = value / 32768f;
            }
            return samples;
        }

        private sealed class StreamingSpeaker : IDisposable
        {
            private readonly object gate = new object();
            private readonly Queue<float> samples = new Queue<float>();
            private readonly GameObject gameObject;

            public StreamingSpeaker(Transform parent, PeerId peer, int rate, float volume)
            {
                gameObject = new GameObject($"Voice-{peer}");
                gameObject.transform.SetParent(parent, false);
                var source = gameObject.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.volume = volume;
                source.loop = true;
                source.clip = AudioClip.Create($"Voice-{peer}", rate, 1, rate, true, FillBuffer);
                source.Play();
            }

            public void Enqueue(float[] values)
            {
                lock (gate)
                {
                    foreach (var value in values)
                        samples.Enqueue(value);
                    while (samples.Count > 48000)
                        samples.Dequeue();
                }
            }

            public void Dispose()
            {
                if (gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }

            private void FillBuffer(float[] buffer)
            {
                lock (gate)
                {
                    for (var i = 0; i < buffer.Length; i++)
                        buffer[i] = samples.Count > 0 ? samples.Dequeue() : 0f;
                }
            }
        }
    }
}
