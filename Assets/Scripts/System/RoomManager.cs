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

    [Space]
    public GameObject nameUI;           // KEEP FIELD TO MAINTAIN STRUCTURE (EVEN THOUGH PANEL WAS REMOVED)
    public GameObject connectingUI;

    private string nickname = "unnamed";

    public string mapName = "Nothing";

    [HideInInspector]
    public int Kills = 0;
    [HideInInspector]
    public int Deaths = 0;

    void Awake()
    {
        instance = this;

        // ACCESS USERNAME FROM PLAYFAB (DISPLAY NAME) - REMOVED CHARACTER NAMING PANEL
        nickname = string.IsNullOrEmpty(PlayFabLogin.DisplayNameFromPlayFab)
            ? "unnamed"
            : PlayFabLogin.DisplayNameFromPlayFab;
    }

    void Start()
    {
        JoinRoomButtonPressed();
    }

    // KEEP METHOD TO MAINTAIN DEBUG STRUCTURE (NO LONGER USED BECAUSE PANEL WAS REMOVED)
    public void ChangeNickName(string _name)
    {
        nickname = _name;
    }

    public void JoinRoomButtonPressed()
    {
        Debug.Log(message: "Connecting....");

        RoomOptions ro = new RoomOptions();

        ro.CustomRoomProperties = new Hashtable()
        {
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
            { "mapName", mapName }
        };

        ro.CustomRoomPropertiesForLobby = new[]
        {
            "mapSceneIndex",
            "mapName"
        };

        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin"), ro, null);

        // REMOVED NAMING PANEL → NO LONGER HIDING nameUI
        // nameUI.SetActive(false);   // REMOVED
        connectingUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        roomCam.SetActive(false);
        if (connectingUI != null) connectingUI.SetActive(false);

        Debug.Log("We'er connected and in a room!!!");

        SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        _player.GetComponent<PlayerSetup>().IsLocalPlayer();
        _player.GetComponent<Health>().isLocalPlayer = true;

        // USE NICKNAME FROM PLAYFAB (SET IN AWAKE)
        _player.GetComponent<PhotonView>().RPC("SetNickName", RpcTarget.AllBuffered, nickname);
        PhotonNetwork.LocalPlayer.NickName = nickname;
    }

    public void SetHashes()
    {
        try
        {
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash["Kills"] = Kills;
            hash["Deaths"] = Deaths;
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            // SAVE SCORE TO PLAYFAB LEADERBOARD (KILLS / DEATHS)
            UpdatePlayFabStats();
        }
        catch
        {
            //Do nothing
        }
    }

    // NEWLY ADDED: SAVE STATISTICS TO PLAYFAB (KEEP OLD STRUCTURE FOR PHOTON UI)
    private void UpdatePlayFabStats()
    {
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
    }

    private void OnPlayFabStatsUpdateError(PlayFabError error)
    {
        Debug.LogError("PlayFab stats update error: " + error.GenerateErrorReport());
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
        Deaths++;
        SetHashes();
    }
}