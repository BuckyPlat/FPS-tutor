using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;

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

    [HideInInspector]
    public int Kills = 0;
    [HideInInspector]
    public int Deaths = 0;

    // === HỆ THỐNG CHỐNG 409 CONFLICT ===
    private int pendingKills = 0;
    private int pendingDeaths = 0;
    private float nextPlayFabUpdateTime = 0f;
    private const float MIN_UPDATE_INTERVAL = 1.2f;     // Chỉ update PlayFab tối đa 1 lần mỗi 1.2 giây
    private bool isUpdatingStats = false;

    void Awake()
    {
        instance = this;

        nickname = string.IsNullOrEmpty(PlayFabLogin.DisplayNameFromPlayFab)
            ? "unnamed"
            : PlayFabLogin.DisplayNameFromPlayFab;
    }

    void Start()
    {
        JoinRoomButtonPressed();
    }

    public void JoinRoomButtonPressed()
    {
        Debug.Log("Connecting....");

        RoomOptions ro = new RoomOptions
        {
            CustomRoomProperties = new Hashtable()
            {
                { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
                { "mapName", mapName }
            },
            CustomRoomPropertiesForLobby = new[] { "mapSceneIndex", "mapName" }
        };

        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin"), ro, null);
        connectingUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        roomCam.SetActive(false);
        if (connectingUI != null) connectingUI.SetActive(false);

        Debug.Log("We're connected and in a room!!!");
        SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        _player.GetComponent<PlayerSetup>().IsLocalPlayer();
        _player.GetComponent<Health>().isLocalPlayer = true;

        _player.GetComponent<PhotonView>().RPC("SetNickName", RpcTarget.AllBuffered, nickname);
        PhotonNetwork.LocalPlayer.NickName = nickname;
    }

    // Hàm mới - Nên gọi từ Weapon hoặc nơi khác
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
        catch { }
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
        nextPlayFabUpdateTime = Time.time + MIN_UPDATE_INTERVAL;
    }

    private void OnPlayFabStatsUpdateError(PlayFabError error)
    {
        isUpdatingStats = false;
        Debug.LogError("PlayFab stats update error: " + error.GenerateErrorReport());

        // Xử lý lỗi 409 Conflict
        bool isConflict = (error.Error == PlayFabErrorCode.StatisticUpdateInProgress) ||
                          (error.HttpStatus == "409") ||
                          (error.ErrorMessage != null && error.ErrorMessage.ToLower().Contains("conflict"));

        if (isConflict)
        {
            Debug.LogWarning("PlayFab 409 Conflict - Retry sau 0.8 giây...");
            Invoke(nameof(RetryPlayFabUpdate), 0.8f);
        }
        else
        {
            nextPlayFabUpdateTime = Time.time + MIN_UPDATE_INTERVAL;
        }
    }

    private void RetryPlayFabUpdate()
    {
        if (pendingKills > 0 || pendingDeaths > 0)
            UpdatePlayFabStats();
    }

    public void StartRespawn(float delay)
    {
        StartCoroutine(RespawnDelay(delay));
    }

    IEnumerator RespawnDelay(float delay)
    {
        if (RespawnUI.Instance != null)
            RespawnUI.Instance.Show(delay);

        yield return new WaitForSeconds(delay);

        if (Spectator.Instance != null)
            Spectator.Instance.gameObject.SetActive(false);

        SpawnPlayer();
        AddDeath(1);                    // Dùng hàm mới
    }
}