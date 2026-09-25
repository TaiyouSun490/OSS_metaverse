using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public abstract class VoiceProvider : ScriptableObject
    {
        public abstract bool IsCapturing { get; }
        public abstract bool InputMuted { get; set; }
        public abstract Task InitializeAsync(
            GameObject host,
            NetworkProvider network,
            SafetyService safety,
            CancellationToken cancellationToken);
        public abstract Task StartCaptureAsync(CancellationToken cancellationToken);
        public abstract void Tick(float unscaledDeltaTime);
        public abstract void Shutdown();
    }
}
