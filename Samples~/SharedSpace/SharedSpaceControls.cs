using System;
using System.Threading;
using Taiyo.Metaverse;
using UnityEngine;

/// <summary>Bind these public methods to normal desktop or XR UI buttons.</summary>
public sealed class SharedSpaceControls : MonoBehaviour
{
    public MetaverseRuntime runtime;
    public SharedSpaceSession space;
    public Transform head;
    public string room = "shared-space";
    [Tooltip("Authenticated host peer id, supplied by your transport/session flow. NGO server id is 0.")]
    public string hostPeerId = "0";
    public string Status { get; private set; } = "Choose Host or Join";
    public bool Busy { get; private set; }
    CancellationTokenSource lifetime;

    public void HostSameRoom() => Join(true, SpaceMode.Colocated);
    public void HostSeparateSpaces() => Join(true, SpaceMode.SeparateSpaces);
    public void JoinHost() => Join(false, SpaceMode.SeparateSpaces);
    async void Join(bool host, SpaceMode mode)
    {
        if (Busy) return;
        Busy = true; lifetime?.Dispose(); lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await runtime.JoinAsync(new RoomRequest { roomId = room, host = host }, lifetime.Token);
            if (host) space.BeginHost(mode, head ? new Pose(head.position + head.forward * .5f, Quaternion.Euler(0, head.eulerAngles.y, 0)) : Pose.identity);
            else space.BeginClient(new PeerId(hostPeerId));
            Status = "Connected";
        }
        catch (Exception error) { Status = error.Message; }
        finally { Busy = false; }
    }
    public void RetryAlignment() => space.Retry();
    public void SwitchToSeparateSpaces()
    {
        if (space.IsHost) space.BeginHost(SpaceMode.SeparateSpaces, Pose.identity);
    }
    public async void Disconnect()
    {
        lifetime?.Cancel(); space.End();
        try { await runtime.LeaveAsync(); Status = "Disconnected"; }
        catch (Exception error) { Status = error.Message; }
    }
    void OnDestroy() { lifetime?.Cancel(); lifetime?.Dispose(); }
}
