using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomList : MonoBehaviourPunCallbacks
{
    public sealed class RoomEntryData
    {
        public RoomEntryData(string roomName, string mapLabel, int playerCount, int sceneIndex)
        {
            RoomName = roomName;
            MapLabel = mapLabel;
            PlayerCount = playerCount;
            SceneIndex = sceneIndex;
        }

        public string RoomName { get; }
        public string MapLabel { get; }
        public int PlayerCount { get; }
        public int SceneIndex { get; }
    }

    public static RoomList Instance;

    public event Action<IReadOnlyList<RoomEntryData>> RoomEntriesChanged;
    public event Action<string> ConnectionStatusChanged;

    private readonly List<RoomInfo> cachedRoomList = new List<RoomInfo>();
    private readonly List<RoomEntryData> currentRoomEntries = new List<RoomEntryData>();

    private string cachedRoomNameToCreate;

    public IReadOnlyList<RoomEntryData> CurrentRoomEntries => currentRoomEntries;
    public string CurrentRoomNameToCreate => cachedRoomNameToCreate ?? string.Empty;
    public string CurrentStatus { get; private set; } = "Lobby idle.";

    public void ChangeRoomToCreateName(string _roomName)
    {
        cachedRoomNameToCreate = _roomName?.Trim();
    }

    public void CreateRoomByIndex(int sceneIndex)
    {
        if (string.IsNullOrEmpty(cachedRoomNameToCreate))
        {
            Debug.LogWarning("Room name is required to create a room!");
            return;
        }

        JoinRoomByName(cachedRoomNameToCreate, sceneIndex);
    }

    private void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        UpdateConnectionStatus("Resetting previous room state.");

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.Disconnect();
        }

        yield return new WaitUntil(() => !PhotonNetwork.IsConnected);

        UpdateConnectionStatus("Connecting to Photon master server...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();

        UpdateConnectionStatus("Connected to master. Joining lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        UpdateConnectionStatus("Lobby feed online.");
        RaiseRoomEntriesChanged();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        UpdateConnectionStatus($"Disconnected: {cause}");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (var room in roomList)
        {
            var existingIndex = cachedRoomList.FindIndex(existingRoom => existingRoom.Name == room.Name);

            if (room.RemovedFromList || !room.IsVisible || !room.IsOpen)
            {
                if (existingIndex >= 0)
                {
                    cachedRoomList.RemoveAt(existingIndex);
                }

                continue;
            }

            if (existingIndex >= 0)
            {
                cachedRoomList[existingIndex] = room;
            }
            else
            {
                cachedRoomList.Add(room);
            }
        }

        cachedRoomList.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        UpdateConnectionStatus(cachedRoomList.Count > 0
            ? $"Lobby feed online. {cachedRoomList.Count} room(s) available."
            : "Lobby feed online. No active rooms yet.");
        RaiseRoomEntriesChanged();
    }

    private void RaiseRoomEntriesChanged()
    {
        currentRoomEntries.Clear();

        foreach (var room in cachedRoomList)
        {
            var roomMapName = TryGetRoomMapName(room);
            var roomSceneIndex = TryGetRoomSceneIndex(room);
            currentRoomEntries.Add(new RoomEntryData(
                room.Name,
                string.IsNullOrWhiteSpace(roomMapName) ? $"Build Scene {roomSceneIndex}" : roomMapName,
                room.PlayerCount,
                roomSceneIndex));
        }

        RoomEntriesChanged?.Invoke(currentRoomEntries);
    }

    private static string TryGetRoomMapName(RoomInfo room)
    {
        if (room.CustomProperties.TryGetValue("mapName", out var mapNameObject) && mapNameObject is string mapName)
        {
            return mapName;
        }

        return string.Empty;
    }

    private static int TryGetRoomSceneIndex(RoomInfo room)
    {
        if (room.CustomProperties.TryGetValue("mapSceneIndex", out var sceneIndexObject) && sceneIndexObject is int sceneIndex)
        {
            return sceneIndex;
        }

        return 1;
    }

    private void UpdateConnectionStatus(string status)
    {
        CurrentStatus = status;
        ConnectionStatusChanged?.Invoke(status);
    }

    public void JoinRoomByName(string _name, int _sceneIndex)
    {
        PlayerPrefs.SetString("RoomNameToJoin", _name);
        UpdateConnectionStatus($"Loading room '{_name}'...");
        SceneManager.LoadScene(_sceneIndex);
    }
}
