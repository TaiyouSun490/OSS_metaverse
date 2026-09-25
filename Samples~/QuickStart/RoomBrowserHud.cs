using System;
using System.Collections.Generic;
using UnityEngine;

namespace Taiyo.Metaverse.Samples
{
    public sealed class RoomBrowserHud : MonoBehaviour
    {
        [SerializeField] private MetaverseRuntime runtime;
        private IReadOnlyList<RoomInfo> rooms = Array.Empty<RoomInfo>();
        private string roomId = "lobby";
        private string accessToken = string.Empty;
        private bool privateRoom;
        private string status = string.Empty;
        private bool busy;

        private void Start()
        {
            if (runtime == null)
                runtime = FindObjectOfType<MetaverseRuntime>();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 210, 360, Mathf.Min(460, Screen.height - 226)), GUI.skin.box);
            GUILayout.Label("Rooms");
            GUILayout.Label(status);
            GUILayout.Label("Room ID");
            roomId = GUILayout.TextField(roomId);
            privateRoom = GUILayout.Toggle(privateRoom, "Private room");
            if (privateRoom)
            {
                GUILayout.Label("Invite / access token");
                accessToken = GUILayout.PasswordField(accessToken, '*');
            }

            GUI.enabled = runtime != null && !busy;
            if (GUILayout.Button("Create or join"))
                Join(true);
            if (GUILayout.Button("Join existing"))
                Join(false);
            if (GUILayout.Button("Refresh public rooms"))
                RefreshRooms();
            GUI.enabled = true;

            foreach (var room in rooms)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{room.Id}  {room.PlayerCount}/{room.Capacity}");
                if (!busy && GUILayout.Button("Join", GUILayout.Width(64)))
                {
                    roomId = room.Id;
                    privateRoom = false;
                    Join(false);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private async void Join(bool createIfMissing)
        {
            busy = true;
            try
            {
                if (runtime.State == ConnectionState.Joined)
                    await runtime.LeaveAsync(destroyCancellationToken);
                await runtime.JoinAsync(new RoomRequest
                {
                    roomId = roomId,
                    visibility = privateRoom ? RoomVisibility.Private : RoomVisibility.Public,
                    createIfMissing = createIfMissing,
                    accessToken = privateRoom ? accessToken : string.Empty
                }, destroyCancellationToken);
                status = $"Joined {roomId}";
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
            }
        }

        private async void RefreshRooms()
        {
            busy = true;
            try
            {
                rooms = await runtime.DiscoverPublicRoomsAsync(destroyCancellationToken);
                status = $"Found {rooms.Count} public room(s)";
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
            }
        }
    }
}
