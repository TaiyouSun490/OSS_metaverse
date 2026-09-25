using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Voice/Disabled Voice Provider", fileName = "NullVoiceProvider")]
    public sealed class NullVoiceProvider : VoiceProvider
    {
        public override bool IsCapturing => false;
        public override bool InputMuted { get; set; }

        public override Task InitializeAsync(GameObject host, NetworkProvider network, SafetyService safety, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public override Task StartCaptureAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Tick(float unscaledDeltaTime) { }
        public override void Shutdown() { }
    }
}
