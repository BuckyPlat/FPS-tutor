using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

    public GameObject player;

    [Space]
    public Transform[] spawnPoints;

    [Space]
    public GameObject roomCam;
    public GameObject connectingUI;

    private string nickname = "unnamed";

    public string mapName = "Nothing";

    [HideInInspector] public int Kills;
    [HideInInspector] public int Deaths;

    private int pendingKills;
    private int pendingDeaths;
    private float nextPlayFabUpdateTime;
    private const float MinUpdateInterval = 1.2f;
    private bool isUpdatingStats;

    void Awake()
    {
        instance = this;

        nickname = string.IsNullOrEmpty(PlayFabLogin.DisplayNameFromPlayFab)
            ? "unnamed"
            : PlayFabLogin.DisplayNameFromPlayFab;
    }

    void Start()
    {
        UIToolkitGameplayUIController.Instance?.SetSessionInfo(PlayerPrefs.GetString("RoomNameToJoin"), mapName);
        UIToolkitGameplayUIController.Instance?.SetSessionState("CONNECTING");
        JoinRoomButtonPressed();
    }

    public void JoinRoomButtonPressed()
    {
        Debug.Log("Connecting....");

        RoomOptions roomOptions = new RoomOptions
        {
            CustomRoomProperties = new Hashtable
            {
                { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
                { "mapName", mapName }
            },
            CustomRoomPropertiesForLobby = new[] { "mapSceneIndex", "mapName" }
        };

        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin"), roomOptions, null);

        if (connectingUI != null)
            connectingUI.SetActive(true);

        UIToolkitGameplayUIController.Instance?.SetConnectingVisible(true, "Joining Room", "Matchmaking in progress.");
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        if (roomCam != null)
            roomCam.SetActive(false);

        if (connectingUI != null)
            connectingUI.SetActive(false);

        UIToolkitGameplayUIController.Instance?.SetSessionInfo(PhotonNetwork.CurrentRoom?.Name, mapName);
        UIToolkitGameplayUIController.Instance?.SetSessionState("LIVE");
        UIToolkitGameplayUIController.Instance?.SetConnectingVisible(false);

        Debug.Log("We're connected and in a room!!!");
        SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        ResetLocalSelectedWeapon();

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        GameObject spawnedPlayer = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        spawnedPlayer.GetComponent<PlayerSetup>().IsLocalPlayer();
        spawnedPlayer.GetComponent<Health>().isLocalPlayer = true;

        WeaponSwitcher weaponSwitcher = spawnedPlayer.GetComponentInChildren<WeaponSwitcher>(true);
        if (weaponSwitcher != null)
            weaponSwitcher.InitializeSelectedWeapon(0);

        spawnedPlayer.GetComponent<PhotonView>().RPC("SetNickName", RpcTarget.AllBuffered, nickname);
        PhotonNetwork.LocalPlayer.NickName = nickname;
    }

    public void AddKill(int amount = 1)
    {
        Kills += amount;
        pendingKills += amount;
        UpdatePhotonHashes();
        TryUpdatePlayFab();
    }

    public void AddDeath(int amount = 1)
    {
        Deaths += amount;
        pendingDeaths += amount;
        UpdatePhotonHashes();
        TryUpdatePlayFab();
    }

    private void UpdatePhotonHashes()
    {
        try
        {
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash["Kills"] = Kills;
            hash["Deaths"] = Deaths;
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
        }
        catch
        {
        }
    }

    private void TryUpdatePlayFab()
    {
        if (Time.time >= nextPlayFabUpdateTime && !isUpdatingStats)
        {
            UpdatePlayFabStats();
        }
    }

    private void UpdatePlayFabStats()
    {
        if (pendingKills == 0 && pendingDeaths == 0) return;

        isUpdatingStats = true;

        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate { StatisticName = "Kills", Value = Kills },
                new StatisticUpdate { StatisticName = "Deaths", Value = Deaths }
            }
        };

        PlayFabClientAPI.UpdatePlayerStatistics(request, OnPlayFabStatsUpdateSuccess, OnPlayFabStatsUpdateError);
    }

    private void OnPlayFabStatsUpdateSuccess(UpdatePlayerStatisticsResult result)
    {
        Debug.Log("PlayFab leaderboard updated successfully!");
        pendingKills = 0;
        pendingDeaths = 0;
        isUpdatingStats = false;
        nextPlayFabUpdateTime = Time.time + MinUpdateInterval;
    }

    private void OnPlayFabStatsUpdateError(PlayFabError error)
    {
        isUpdatingStats = false;
        Debug.LogError("PlayFab stats update error: " + error.GenerateErrorReport());

        bool isConflict = error.Error == PlayFabErrorCode.StatisticUpdateInProgress ||
                          error.HttpStatus == "409" ||
                          (error.ErrorMessage != null && error.ErrorMessage.ToLower().Contains("conflict"));

        if (isConflict)
        {
            Debug.LogWarning("PlayFab 409 Conflict - retry after 0.8 seconds.");
            Invoke(nameof(RetryPlayFabUpdate), 0.8f);
        }
        else
        {
            nextPlayFabUpdateTime = Time.time + MinUpdateInterval;
        }
    }

    private void RetryPlayFabUpdate()
    {
        if (pendingKills > 0 || pendingDeaths > 0)
            UpdatePlayFabStats();
    }

    private void ResetLocalSelectedWeapon()
    {
        if (PhotonNetwork.LocalPlayer == null)
            return;

        Hashtable hash = new Hashtable
        {
            { PlayerSetup.SelectedWeaponPropertyKey, 0 }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
    }

    public void StartRespawn(float delay)
    {
        StartCoroutine(RespawnDelay(delay));
    }

    IEnumerator RespawnDelay(float delay)
    {
        if (RespawnUI.Instance != null)
            RespawnUI.Instance.Show(delay);

        UIToolkitGameplayUIController.Instance?.SetSessionState("RESPAWNING");

        yield return new WaitForSeconds(delay);

        if (Spectator.Instance != null)
            Spectator.Instance.gameObject.SetActive(false);

        SpawnPlayer();
        UIToolkitGameplayUIController.Instance?.SetSessionState("LIVE");
        AddDeath(1);
    }
}
