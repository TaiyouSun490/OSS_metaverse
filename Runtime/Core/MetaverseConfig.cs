using UnityEngine;

namespace Taiyo.Metaverse
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Configuration", fileName = "MetaverseConfig")]
    public sealed class MetaverseConfig : ScriptableObject
    {
        [Header("Providers")]
        [SerializeField] private AccountProvider accountProvider;
        [SerializeField] private NetworkProvider networkProvider;
        [SerializeField] private VoiceProvider voiceProvider;

        [Header("Local user")]
        [SerializeField] private LocalUserProfile localUser = new LocalUserProfile();
        [SerializeField] private RoomRequest defaultRoom = new RoomRequest();

        [Header("Replication")]
        [SerializeField, Range(1f, 60f)] private float poseSendRate = 20f;
        [SerializeField, Range(256, 65535)] private int maximumPayloadBytes = 16384;
        [SerializeField, Range(0f, 1f)] private float remotePoseSmoothing = 0.15f;

        public AccountProvider AccountProvider => accountProvider;
        public NetworkProvider NetworkProvider => networkProvider;
        public VoiceProvider VoiceProvider => voiceProvider;
        public LocalUserProfile LocalUser => localUser;
        public RoomRequest DefaultRoom => defaultRoom;
        public float PoseSendRate => poseSendRate;
        public int MaximumPayloadBytes => maximumPayloadBytes;
        public float RemotePoseSmoothing => remotePoseSmoothing;

#if UNITY_EDITOR
        public void EditorAssignProviders(NetworkProvider network, VoiceProvider voice, AccountProvider account = null)
        {
            accountProvider = account;
            networkProvider = network;
            voiceProvider = voice;
        }
#endif
    }
}
