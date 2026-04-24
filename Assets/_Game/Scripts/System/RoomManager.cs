using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public enum MatchPhase
    {
        Waiting = 0,
        Live = 1,
        Finished = 2
    }

    public enum MatchWinReason
    {
        None = 0,
        TimeLimit = 1,
        KillCap = 2
    }

    public const string MatchPhaseKey = "matchPhase";
    public const string MatchStartServerTimestampKey = "matchStartServerTimestamp";
    public const string WinnerActorNumberKey = "winnerActorNumber";
    public const string WinReasonKey = "winReason";
    public const string MatchEndServerTimestampKey = "matchEndServerTimestamp";

    private const byte KillConfirmedEventCode = 31;
    private const string KillsPropertyKey = "Kills";
    private const string DeathsPropertyKey = "Deaths";
    private const string CoinCountKey = "CoinCount";
    private const int MenuSceneBuildIndex = 0;
    private const float MinUpdateInterval = 1.2f;

    public static RoomManager instance;

    [Header("Spawn")]
    public GameObject player;
    public Transform[] spawnPoints;

    [Header("Scene Objects")]
    public GameObject roomCam;
    public GameObject connectingUI;

    [Header("Match Settings")]
    [SerializeField] private float matchDurationSeconds = 600f;
    [SerializeField] private int killCap = 20;
    [SerializeField] private float endCountdownSeconds = 8f;
    [SerializeField] private int winCoinReward = 150;
    [SerializeField] private int loseCoinReward = 50;
    [SerializeField] private int killHealAmount = 35;
    [SerializeField] private float respawnDelaySeconds = 3f;

    private string nickname = "unnamed";
    public string mapName = "Nothing";

    [HideInInspector] public int Kills;
    [HideInInspector] public int Deaths;

    private int pendingKills;
    private int pendingDeaths;
    private float nextPlayFabUpdateTime;
    private bool isUpdatingStats;
    private bool rewardApplied;
    private bool manualLeaveRequested;
    private bool isPauseVisible;
    private bool isLeavingRoom;
    private int awardedCoinAmount;

    private MatchPhase currentMatchPhase = MatchPhase.Waiting;
    private MatchWinReason currentWinReason = MatchWinReason.None;
    private int matchStartServerTimestamp = -1;
    private int matchEndServerTimestamp = -1;
    private int winnerActorNumber = -1;

    private Coroutine respawnCoroutine;
    private GameObject localPlayerObject;

    public bool IsMatchLive => currentMatchPhase == MatchPhase.Live;
    public bool IsMatchFinished => currentMatchPhase == MatchPhase.Finished;

    private void Awake()
    {
        instance = this;
        nickname = string.IsNullOrEmpty(PlayFabLogin.DisplayNameFromPlayFab)
            ? "unnamed"
            : PlayFabLogin.DisplayNameFromPlayFab;
    }

    public override void OnEnable()
    {
        base.OnEnable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
    }

    private void Start()
    {
        UIToolkitGameplayUIController.Instance?.SetSessionInfo(PlayerPrefs.GetString("RoomNameToJoin"), mapName);
        UIToolkitGameplayUIController.Instance?.SetSessionState("CONNECTING");
        UIToolkitGameplayUIController.Instance?.SetMatchTimerText("WAIT");
        UIToolkitGameplayUIController.Instance?.HideRespawn();
        UIToolkitGameplayUIController.Instance?.SetConnectingVisible(true, "Joining Room", "Matchmaking in progress.");
        JoinRoomButtonPressed();
    }

    private void Update()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (currentMatchPhase == MatchPhase.Live)
        {
            float remainingSeconds = GetRemainingMatchSeconds();
            UIToolkitGameplayUIController.Instance?.SetMatchTimer(remainingSeconds);

            if (PhotonNetwork.IsMasterClient && remainingSeconds <= 0f)
            {
                TryFinishMatch(MatchWinReason.TimeLimit, GetSortedPlayers().FirstOrDefault());
            }
        }
        else if (currentMatchPhase == MatchPhase.Finished)
        {
            float remainingLeaveSeconds = GetRemainingLeaveSeconds();
            UIToolkitGameplayUIController.Instance?.SetResultsCountdown(remainingLeaveSeconds);

            if (remainingLeaveSeconds <= 0f && PhotonNetwork.InRoom)
            {
                LeaveRoomToLobby(false);
            }
        }
        else
        {
            UIToolkitGameplayUIController.Instance?.SetMatchTimerText("WAIT");
        }
    }

    public void JoinRoomButtonPressed()
    {
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
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        isLeavingRoom = false;
        ResetLocalMatchState();

        if (roomCam != null)
            roomCam.SetActive(false);

        if (connectingUI != null)
            connectingUI.SetActive(false);

        PhotonNetwork.LocalPlayer.NickName = nickname;

        UIToolkitGameplayUIController.Instance?.SetSessionInfo(PhotonNetwork.CurrentRoom?.Name, mapName);
        UIToolkitGameplayUIController.Instance?.SetConnectingVisible(false);

        if (PhotonNetwork.IsMasterClient)
            EnsureWaitingRoomState();

        RefreshMatchStateFromRoom();
        SpawnPlayer();

        if (PhotonNetwork.IsMasterClient)
            TryStartMatchIfMaster();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        if (PhotonNetwork.IsMasterClient)
            TryStartMatchIfMaster();

        RefreshWaitingPresentation();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        RefreshWaitingPresentation();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!PhotonNetwork.IsMasterClient || currentMatchPhase != MatchPhase.Live || changedProps == null)
            return;

        if (!changedProps.TryGetValue(KillsPropertyKey, out object killsValue))
            return;

        if (TryGetIntValue(killsValue, out int parsedKills) && parsedKills >= killCap)
        {
            TryFinishMatch(MatchWinReason.KillCap, targetPlayer);
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);
        RefreshMatchStateFromRoom();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);

        if (PhotonNetwork.IsMasterClient && currentMatchPhase == MatchPhase.Waiting)
            TryStartMatchIfMaster();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        isLeavingRoom = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        SceneManager.LoadScene(MenuSceneBuildIndex);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null || photonEvent.Code != KillConfirmedEventCode)
            return;

        if (!(photonEvent.CustomData is object[] payload) || payload.Length < 5)
            return;

        int killerActorNumber = Convert.ToInt32(payload[0]);
        int victimActorNumber = Convert.ToInt32(payload[1]);
        string killerName = payload[2] as string ?? "Unknown";
        string victimName = payload[3] as string ?? "Unknown";
        bool isEnvironmentKill = Convert.ToBoolean(payload[4]);

        HandleKillConfirmed(killerActorNumber, victimActorNumber, killerName, victimName, isEnvironmentKill);
    }

    public void SpawnPlayer()
    {
        if (PhotonNetwork.CurrentRoom == null || currentMatchPhase == MatchPhase.Finished || spawnPoints == null || spawnPoints.Length == 0)
            return;

        ResetLocalSelectedWeapon();

        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        GameObject spawnedPlayer = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        spawnedPlayer.GetComponent<PlayerSetup>().IsLocalPlayer();
        spawnedPlayer.GetComponent<Health>().isLocalPlayer = true;

        WeaponSwitcher weaponSwitcher = spawnedPlayer.GetComponentInChildren<WeaponSwitcher>(true);
        if (weaponSwitcher != null)
            weaponSwitcher.InitializeSelectedWeapon(0);

        spawnedPlayer.GetComponent<PhotonView>().RPC("SetNickName", RpcTarget.AllBuffered, nickname);
        RegisterLocalPlayer(spawnedPlayer);
        ApplyGameplayInputLockState();
    }

    public void RegisterLocalPlayer(GameObject localPlayer)
    {
        localPlayerObject = localPlayer;
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

    public void NotifyLocalPlayerDeath(int killerViewId, Transform killerTransform, bool isEnvironmentKill)
    {
        if (PhotonNetwork.LocalPlayer == null)
            return;

        int killerActorNumber = -1;
        string killerName = "ENVIRONMENT";

        if (!isEnvironmentKill)
        {
            PhotonView killerView = PhotonView.Find(killerViewId);
            if (killerView != null)
            {
                killerActorNumber = killerView.OwnerActorNr;
                killerName = GetPlayerDisplayName(killerView.Owner);
            }
        }

        string victimName = GetPlayerDisplayName(PhotonNetwork.LocalPlayer);
        RaiseKillConfirmedEvent(killerActorNumber, PhotonNetwork.LocalPlayer.ActorNumber, killerName, victimName, isEnvironmentKill);

        if (Spectator.Instance != null && killerTransform != null)
            Spectator.Instance.Activate(killerTransform);

        localPlayerObject = null;

        if (currentMatchPhase == MatchPhase.Live)
            StartRespawn(respawnDelaySeconds);
    }

    public void StartRespawn(float delay)
    {
        if (currentMatchPhase != MatchPhase.Live)
            return;

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        respawnCoroutine = StartCoroutine(RespawnDelay(delay));
    }

    public void TogglePauseMenu()
    {
        if (currentMatchPhase == MatchPhase.Finished)
            return;

        SetPauseMenuVisible(!isPauseVisible);
    }

    public void SetPauseMenuVisible(bool visible)
    {
        if (isPauseVisible == visible)
            return;

        isPauseVisible = visible;

        if (visible)
        {
            GameChat.Instance?.ForceCloseChat();
            UIToolkitGameplayUIController.Instance?.ShowPauseMenu(true);
        }
        else
        {
            UIToolkitGameplayUIController.Instance?.ShowPauseMenu(false);
        }

        ApplyGameplayInputLockState();
    }

    public void LeaveRoomToLobby(bool fromPauseMenu)
    {
        manualLeaveRequested |= fromPauseMenu;
        isLeavingRoom = true;

        if (isPauseVisible)
            isPauseVisible = false;

        GameChat.Instance?.ForceCloseChat();
        UIToolkitGameplayUIController.Instance?.ShowPauseMenu(false);
        UIToolkitGameplayUIController.Instance?.SetGameplayInputBlocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!PhotonNetwork.InRoom)
        {
            isLeavingRoom = false;
            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();

            SceneManager.LoadScene(MenuSceneBuildIndex);
            return;
        }

        PhotonNetwork.LeaveRoom();
    }

    public List<Player> GetSortedPlayers()
    {
        return PhotonNetwork.PlayerList
            .OrderByDescending(playerEntry => playerEntry.GetScore())
            .ThenByDescending(playerEntry => GetPlayerKills(playerEntry))
            .ThenBy(playerEntry => GetPlayerDeaths(playerEntry))
            .ThenBy(playerEntry => playerEntry.ActorNumber)
            .ToList();
    }

    public List<UIToolkitGameplayUIController.LeaderboardEntryData> BuildLeaderboardEntries()
    {
        List<Player> sortedPlayers = GetSortedPlayers();
        var entries = new List<UIToolkitGameplayUIController.LeaderboardEntryData>(sortedPlayers.Count);

        for (int index = 0; index < sortedPlayers.Count; index++)
        {
            Player playerEntry = sortedPlayers[index];
            entries.Add(new UIToolkitGameplayUIController.LeaderboardEntryData
            {
                Rank = index + 1,
                Name = GetPlayerDisplayName(playerEntry),
                Score = playerEntry.GetScore(),
                Kd = $"{GetPlayerKills(playerEntry)}/{GetPlayerDeaths(playerEntry)}"
            });
        }

        return entries;
    }

    private void EnsureWaitingRoomState()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props != null && props.ContainsKey(MatchPhaseKey))
            return;

        Hashtable waitingState = new Hashtable
        {
            { MatchPhaseKey, (int)MatchPhase.Waiting },
            { WinnerActorNumberKey, -1 },
            { WinReasonKey, (int)MatchWinReason.None },
            { MatchStartServerTimestampKey, -1 },
            { MatchEndServerTimestampKey, -1 }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(waitingState);
        PhotonNetwork.CurrentRoom.IsOpen = true;
        PhotonNetwork.CurrentRoom.IsVisible = true;
    }

    private void RefreshMatchStateFromRoom()
    {
        MatchPhase previousPhase = currentMatchPhase;

        currentMatchPhase = ReadMatchPhase();
        currentWinReason = ReadWinReason();
        matchStartServerTimestamp = ReadRoomInt(MatchStartServerTimestampKey, -1);
        matchEndServerTimestamp = ReadRoomInt(MatchEndServerTimestampKey, -1);
        winnerActorNumber = ReadRoomInt(WinnerActorNumberKey, -1);

        RefreshWaitingPresentation();

        switch (currentMatchPhase)
        {
            case MatchPhase.Waiting:
                UIToolkitGameplayUIController.Instance?.SetSessionState("WAITING");
                UIToolkitGameplayUIController.Instance?.SetMatchTimerText("WAIT");
                UIToolkitGameplayUIController.Instance?.ShowResults(false);
                CancelRespawnFlow();
                break;

            case MatchPhase.Live:
                UIToolkitGameplayUIController.Instance?.SetSessionState("LIVE");
                UIToolkitGameplayUIController.Instance?.ShowResults(false);
                rewardApplied = false;
                break;

            case MatchPhase.Finished:
                UIToolkitGameplayUIController.Instance?.SetSessionState("FINISHED");
                CancelRespawnFlow();
                ApplyMatchResultIfNeeded();
                break;
        }

        if (previousPhase != currentMatchPhase)
            ApplyGameplayInputLockState();
    }

    private void RefreshWaitingPresentation()
    {
        if (currentMatchPhase != MatchPhase.Waiting)
        {
            UIToolkitGameplayUIController.Instance?.SetWaitingVisible(false);
            return;
        }

        int missingPlayers = Mathf.Max(0, 2 - PhotonNetwork.CurrentRoom.PlayerCount);
        string waitingBody = missingPlayers <= 0
            ? "Starting match..."
            : $"Timer starts when 2 players join. Waiting for {missingPlayers} more player{(missingPlayers > 1 ? "s" : string.Empty)}.";

        UIToolkitGameplayUIController.Instance?.SetWaitingVisible(true, "Waiting For Players", waitingBody);
    }

    private void TryStartMatchIfMaster()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || currentMatchPhase != MatchPhase.Waiting)
            return;

        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        Hashtable liveState = new Hashtable
        {
            { MatchPhaseKey, (int)MatchPhase.Live },
            { MatchStartServerTimestampKey, PhotonNetwork.ServerTimestamp },
            { MatchEndServerTimestampKey, -1 },
            { WinnerActorNumberKey, -1 },
            { WinReasonKey, (int)MatchWinReason.None }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(liveState);
    }

    private void TryFinishMatch(MatchWinReason reason, Player winner)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || currentMatchPhase == MatchPhase.Finished)
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        Hashtable finishState = new Hashtable
        {
            { MatchPhaseKey, (int)MatchPhase.Finished },
            { WinnerActorNumberKey, winner != null ? winner.ActorNumber : -1 },
            { WinReasonKey, (int)reason },
            { MatchEndServerTimestampKey, PhotonNetwork.ServerTimestamp }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(finishState);
    }

    private void HandleKillConfirmed(int killerActorNumber, int victimActorNumber, string killerName, string victimName, bool isEnvironmentKill)
    {
        if (currentMatchPhase != MatchPhase.Live)
            return;

        if (PhotonNetwork.LocalPlayer != null)
        {
            if (killerActorNumber > 0 && killerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                AddKill(1);
                PhotonNetwork.LocalPlayer.AddScore(100);
                ApplyKillReward();
            }

            if (victimActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                AddDeath(1);
            }
        }

        UIToolkitGameplayUIController.Instance?.PushKillFeedEntry(killerName, victimName, isEnvironmentKill);
    }

    private void RaiseKillConfirmedEvent(int killerActorNumber, int victimActorNumber, string killerName, string victimName, bool isEnvironmentKill)
    {
        object[] payload =
        {
            killerActorNumber,
            victimActorNumber,
            killerName,
            victimName,
            isEnvironmentKill
        };

        PhotonNetwork.RaiseEvent(
            KillConfirmedEventCode,
            payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);
    }

    private void ApplyKillReward()
    {
        if (localPlayerObject == null)
            return;

        Health localHealth = localPlayerObject.GetComponent<Health>();
        if (localHealth != null)
            localHealth.RestoreHealth(killHealAmount);

        foreach (Weapon weapon in localPlayerObject.GetComponentsInChildren<Weapon>(true))
        {
            if (!weapon.gameObject.activeInHierarchy)
                continue;

            weapon.RefillCurrentMagazine();
            break;
        }
    }

    private void ResetLocalMatchState()
    {
        Kills = 0;
        Deaths = 0;
        pendingKills = 0;
        pendingDeaths = 0;
        rewardApplied = false;
        manualLeaveRequested = false;
        isPauseVisible = false;
        awardedCoinAmount = 0;
        localPlayerObject = null;
        PhotonNetwork.LocalPlayer.SetScore(0);

        Hashtable stats = new Hashtable
        {
            { KillsPropertyKey, 0 },
            { DeathsPropertyKey, 0 },
            { PlayerSetup.SelectedWeaponPropertyKey, 0 }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(stats);
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

    private void UpdatePhotonHashes()
    {
        if (PhotonNetwork.LocalPlayer == null)
            return;

        Hashtable hash = new Hashtable
        {
            { KillsPropertyKey, Kills },
            { DeathsPropertyKey, Deaths }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
    }

    private void TryUpdatePlayFab()
    {
        if (Time.time >= nextPlayFabUpdateTime && !isUpdatingStats)
            UpdatePlayFabStats();
    }

    private void UpdatePlayFabStats()
    {
        if (pendingKills == 0 && pendingDeaths == 0)
            return;

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
        pendingKills = 0;
        pendingDeaths = 0;
        isUpdatingStats = false;
        nextPlayFabUpdateTime = Time.time + MinUpdateInterval;
    }

    private void OnPlayFabStatsUpdateError(PlayFabError error)
    {
        isUpdatingStats = false;

        bool isConflict = error.Error == PlayFabErrorCode.StatisticUpdateInProgress ||
                          error.HttpStatus == "409" ||
                          (error.ErrorMessage != null && error.ErrorMessage.ToLower().Contains("conflict"));

        if (isConflict)
        {
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

    private IEnumerator RespawnDelay(float delay)
    {
        if (RespawnUI.Instance != null)
            RespawnUI.Instance.Show(delay);

        UIToolkitGameplayUIController.Instance?.SetSessionState("RESPAWNING");

        yield return new WaitForSeconds(delay);
        respawnCoroutine = null;

        if (currentMatchPhase != MatchPhase.Live)
            yield break;

        if (Spectator.Instance != null)
            Spectator.Instance.gameObject.SetActive(false);

        SpawnPlayer();
        UIToolkitGameplayUIController.Instance?.SetSessionState("LIVE");
    }

    private void CancelRespawnFlow()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        if (RespawnUI.Instance != null)
            RespawnUI.Instance.StopAllCoroutines();

        UIToolkitGameplayUIController.Instance?.HideRespawn();
    }

    private void ApplyMatchResultIfNeeded()
    {
        GameChat.Instance?.ForceCloseChat();

        List<UIToolkitGameplayUIController.LeaderboardEntryData> finalEntries = BuildLeaderboardEntries();
        UIToolkitGameplayUIController.Instance?.SetLeaderboardEntries(finalEntries);
        UIToolkitGameplayUIController.Instance?.SetResultEntries(finalEntries);

        bool isVictory = PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.ActorNumber == winnerActorNumber;
        int reward = rewardApplied ? awardedCoinAmount : (isVictory ? winCoinReward : loseCoinReward);

        if (!rewardApplied && !manualLeaveRequested)
        {
            int currentCoins = PlayerPrefs.GetInt(CoinCountKey, 1000);
            PlayerPrefs.SetInt(CoinCountKey, currentCoins + reward);
            PlayerPrefs.Save();
            awardedCoinAmount = reward;
            rewardApplied = true;
        }

        string winnerName = GetPlayerDisplayName(PhotonNetwork.CurrentRoom?.GetPlayer(winnerActorNumber));
        UIToolkitGameplayUIController.Instance?.ShowResults(
            true,
            isVictory,
            winnerName,
            currentWinReason == MatchWinReason.KillCap ? "KILL CAP" : "TIME LIMIT",
            reward);

        ApplyGameplayInputLockState();
    }

    private void ApplyGameplayInputLockState()
    {
        bool blockInput = isLeavingRoom || isPauseVisible || currentMatchPhase == MatchPhase.Finished;
        UIToolkitGameplayUIController.Instance?.SetGameplayInputBlocked(blockInput);

        Cursor.lockState = blockInput ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = blockInput;
    }

    private MatchPhase ReadMatchPhase()
    {
        return (MatchPhase)ReadRoomInt(MatchPhaseKey, (int)MatchPhase.Waiting);
    }

    private MatchWinReason ReadWinReason()
    {
        return (MatchWinReason)ReadRoomInt(WinReasonKey, (int)MatchWinReason.None);
    }

    private int ReadRoomInt(string key, int fallback)
    {
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
            return fallback;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
            return fallback;

        return TryGetIntValue(value, out int parsedValue) ? parsedValue : fallback;
    }

    private float GetRemainingMatchSeconds()
    {
        if (matchStartServerTimestamp < 0)
            return matchDurationSeconds;

        int elapsedMilliseconds = PhotonNetwork.ServerTimestamp - matchStartServerTimestamp;
        return Mathf.Max(0f, matchDurationSeconds - elapsedMilliseconds / 1000f);
    }

    private float GetRemainingLeaveSeconds()
    {
        if (matchEndServerTimestamp < 0)
            return endCountdownSeconds;

        int elapsedMilliseconds = PhotonNetwork.ServerTimestamp - matchEndServerTimestamp;
        return Mathf.Max(0f, endCountdownSeconds - elapsedMilliseconds / 1000f);
    }

    private int GetPlayerKills(Player playerEntry)
    {
        return TryGetPlayerStat(playerEntry, KillsPropertyKey);
    }

    private int GetPlayerDeaths(Player playerEntry)
    {
        return TryGetPlayerStat(playerEntry, DeathsPropertyKey);
    }

    private static int TryGetPlayerStat(Player playerEntry, string key)
    {
        if (playerEntry == null || playerEntry.CustomProperties == null || !playerEntry.CustomProperties.TryGetValue(key, out object value))
            return 0;

        return TryGetIntValue(value, out int parsedValue) ? parsedValue : 0;
    }

    private static bool TryGetIntValue(object value, out int parsedValue)
    {
        switch (value)
        {
            case byte byteValue:
                parsedValue = byteValue;
                return true;
            case short shortValue:
                parsedValue = shortValue;
                return true;
            case int intValue:
                parsedValue = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                parsedValue = (int)longValue;
                return true;
            case float floatValue:
                parsedValue = Mathf.RoundToInt(floatValue);
                return true;
            case double doubleValue:
                parsedValue = Mathf.RoundToInt((float)doubleValue);
                return true;
            default:
                parsedValue = 0;
                return false;
        }
    }

    private static string GetPlayerDisplayName(Player playerEntry)
    {
        return playerEntry == null || string.IsNullOrWhiteSpace(playerEntry.NickName)
            ? "Unnamed"
            : playerEntry.NickName;
    }
}
