using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIToolkitGameplayUIController : MonoBehaviour
{
    public struct LeaderboardEntryData
    {
        public int Rank;
        public string Name;
        public int Score;
        public string Kd;
    }

    private struct KillFeedEntryData
    {
        public string Killer;
        public string Victim;
        public bool IsEnvironmentKill;
        public float ExpireTime;
    }

    private const int MaxChatMessages = 15;
    private const int MaxKillFeedEntries = 5;
    private const float KillFeedLifetimeSeconds = 4f;
    private const string GameplaySettingsInitializedKey = "UITKGameplaySettingsInitialized";

    public static UIToolkitGameplayUIController Instance { get; private set; }
    public static bool IsGameplayInputBlocked { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private readonly List<LeaderboardEntryData> leaderboardEntries = new List<LeaderboardEntryData>();
    private readonly List<LeaderboardEntryData> resultEntries = new List<LeaderboardEntryData>();
    private readonly List<string> chatMessages = new List<string>();
    private readonly List<KillFeedEntryData> killFeedEntries = new List<KillFeedEntryData>();

    private VisualElement root;
    private VisualElement hudLayer;
    private VisualElement healthFill;
    private VisualElement ammoFill;
    private VisualElement connectingOverlay;
    private VisualElement waitingOverlay;
    private VisualElement respawnOverlay;
    private VisualElement leaderboardOverlay;
    private VisualElement pauseOverlay;
    private VisualElement pauseSettingsPanel;
    private VisualElement resultsOverlay;
    private VisualElement killFeedRoot;
    private ScrollView chatLogView;
    private TextField chatInputField;
    private VisualElement chatInputRow;
    private ListView leaderboardList;
    private ListView resultsList;

    private Label sessionRoomLabel;
    private Label sessionMapLabel;
    private Label sessionStateLabel;
    private Label matchTimerLabel;
    private Label healthValueLabel;
    private Label ammoValueLabel;
    private Label ammoReserveLabel;
    private Label connectingTitleLabel;
    private Label connectingBodyLabel;
    private Label waitingTitleLabel;
    private Label waitingBodyLabel;
    private Label respawnCountdownLabel;
    private Label chatHintLabel;
    private Label emptyLeaderboardLabel;
    private Label emptyResultsLabel;
    private Label resultsTitleLabel;
    private Label resultsWinnerLabel;
    private Label resultsReasonLabel;
    private Label resultsRewardLabel;
    private Label resultsCountdownLabel;

    private Button pauseResumeButton;
    private Button pauseSettingsButton;
    private Button pauseLeaveButton;
    private Button resultsLeaveButton;
    private Toggle hudToggle;
    private Toggle tooltipsToggle;
    private Toggle invertToggle;
    private Slider musicSlider;
    private Slider sensXSlider;
    private Slider sensYSlider;
    private Slider smoothSlider;

    private int lastLeaderboardCount = -1;
    private int lastResultsCount = -1;
    private bool settingsCallbacksRegistered;
    private bool pauseSettingsVisible;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Gameplay UI Toolkit controller requires a UIDocument.");
            return;
        }

        Instance = this;
        root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("Gameplay UIDocument rootVisualElement is not ready.");
            return;
        }

        QueryElements();
        ConfigureListView(leaderboardList, leaderboardEntries, ref lastLeaderboardCount);
        ConfigureListView(resultsList, resultEntries, ref lastResultsCount);
        RegisterButtons();
        RegisterSettingsCallbacks();
        EnsureGameplaySettingsDefaults();
        LoadSettingsIntoUI();
        ApplyHudVisibilitySetting();

        SetSessionInfo(PlayerPrefs.GetString("RoomNameToJoin", "AUTO ROOM"), GetCurrentSceneLabel());
        SetSessionState("CONNECTING");
        SetMatchTimerText("WAIT");
        SetLocalHealth(100, 100);
        SetAmmo(0, 0, 0);
        SetConnectingVisible(false);
        SetWaitingVisible(false);
        HideRespawn();
        SetLeaderboardVisible(false);
        ShowPauseMenu(false);
        ShowResults(false);
        CloseChat(true);
        RenderChatMessages();
        RenderKillFeed();
        SetGameplayInputBlocked(false);
    }

    private void OnDisable()
    {
        UnregisterSettingsCallbacks();

        if (Instance == this)
            Instance = null;

        IsGameplayInputBlocked = false;
        root = null;
        lastLeaderboardCount = -1;
        lastResultsCount = -1;
    }

    private void Update()
    {
        if (root == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) &&
            !GameChat.IsPlayerChatting() &&
            !IsResultsVisible() &&
            RoomManager.instance != null)
        {
            RoomManager.instance.TogglePauseMenu();
        }

        if (PruneExpiredKillFeed())
            RenderKillFeed();
    }

    public void SetGameplayInputBlocked(bool blocked)
    {
        IsGameplayInputBlocked = blocked;

        if (blocked)
            SetLeaderboardVisible(false);
    }

    public void SetSessionInfo(string roomName, string mapName)
    {
        if (sessionRoomLabel != null)
            sessionRoomLabel.text = string.IsNullOrWhiteSpace(roomName) ? "AUTO ROOM" : roomName.ToUpperInvariant();

        if (sessionMapLabel != null)
            sessionMapLabel.text = string.IsNullOrWhiteSpace(mapName) ? GetCurrentSceneLabel().ToUpperInvariant() : mapName.ToUpperInvariant();
    }

    public void SetSessionState(string state)
    {
        if (sessionStateLabel != null)
            sessionStateLabel.text = string.IsNullOrWhiteSpace(state) ? "LIVE" : state.Trim().ToUpperInvariant();
    }

    public void SetMatchTimer(float remainingSeconds)
    {
        if (matchTimerLabel == null)
            return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        matchTimerLabel.text = $"{minutes:00}:{seconds:00}";
    }

    public void SetMatchTimerText(string timerText)
    {
        if (matchTimerLabel == null)
            return;

        matchTimerLabel.text = string.IsNullOrWhiteSpace(timerText) ? "--:--" : timerText.Trim().ToUpperInvariant();
    }

    public void SetLocalHealth(int currentHealth, int maxHealth)
    {
        if (healthValueLabel != null)
            healthValueLabel.text = Mathf.Max(0, currentHealth).ToString();

        SetFillPercent(healthFill, maxHealth <= 0 ? 0f : (float)currentHealth / maxHealth);
    }

    public void SetAmmo(int currentAmmo, int reserveMags, int magSize)
    {
        if (ammoValueLabel != null)
            ammoValueLabel.text = $"{Mathf.Max(0, currentAmmo)}/{Mathf.Max(0, magSize)}";

        if (ammoReserveLabel != null)
            ammoReserveLabel.text = $"MAG x{Mathf.Max(0, reserveMags)}";

        SetFillPercent(ammoFill, magSize <= 0 ? 0f : (float)currentAmmo / magSize);
    }

    public void SetConnectingVisible(bool visible, string title = null, string body = null)
    {
        if (connectingOverlay == null)
            return;

        connectingOverlay.EnableInClassList("hidden", !visible);

        if (!string.IsNullOrWhiteSpace(title) && connectingTitleLabel != null)
            connectingTitleLabel.text = title;

        if (!string.IsNullOrWhiteSpace(body) && connectingBodyLabel != null)
            connectingBodyLabel.text = body;
    }

    public void SetWaitingVisible(bool visible, string title = null, string body = null)
    {
        if (waitingOverlay == null)
            return;

        waitingOverlay.EnableInClassList("hidden", !visible);

        if (!string.IsNullOrWhiteSpace(title) && waitingTitleLabel != null)
            waitingTitleLabel.text = title;

        if (!string.IsNullOrWhiteSpace(body) && waitingBodyLabel != null)
            waitingBodyLabel.text = body;
    }

    public void ShowRespawnCountdown(int secondsRemaining)
    {
        if (respawnCountdownLabel != null)
            respawnCountdownLabel.text = $"Respawn in: {Mathf.Max(0, secondsRemaining)}";

        if (respawnOverlay != null)
            respawnOverlay.RemoveFromClassList("hidden");
    }

    public void HideRespawn()
    {
        if (respawnOverlay != null)
            respawnOverlay.AddToClassList("hidden");
    }

    public void AppendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        chatMessages.Add(message);
        while (chatMessages.Count > MaxChatMessages)
            chatMessages.RemoveAt(0);

        RenderChatMessages();
    }

    public void OpenChat()
    {
        if (chatInputRow != null)
            chatInputRow.RemoveFromClassList("hidden");

        if (chatHintLabel != null)
            chatHintLabel.text = "ENTER TO SEND";

        if (chatInputField != null)
        {
            chatInputField.SetValueWithoutNotify(string.Empty);
            chatInputField.schedule.Execute(() => chatInputField.Focus());
        }
    }

    public void CloseChat(bool clearInput)
    {
        if (chatInputRow != null)
            chatInputRow.AddToClassList("hidden");

        if (chatHintLabel != null)
            chatHintLabel.text = "PRESS Y TO CHAT";

        if (chatInputField != null)
        {
            if (clearInput)
                chatInputField.SetValueWithoutNotify(string.Empty);

            chatInputField.Blur();
        }
    }

    public string GetChatInputValue()
    {
        return chatInputField?.value ?? string.Empty;
    }

    public void ClearChatInput()
    {
        if (chatInputField != null)
            chatInputField.SetValueWithoutNotify(string.Empty);
    }

    public void PushKillFeedEntry(string killerName, string victimName, bool isEnvironmentKill)
    {
        killFeedEntries.Insert(0, new KillFeedEntryData
        {
            Killer = killerName,
            Victim = victimName,
            IsEnvironmentKill = isEnvironmentKill,
            ExpireTime = Time.unscaledTime + KillFeedLifetimeSeconds
        });

        while (killFeedEntries.Count > MaxKillFeedEntries)
            killFeedEntries.RemoveAt(killFeedEntries.Count - 1);

        RenderKillFeed();
    }

    public void SetLeaderboardEntries(IReadOnlyList<LeaderboardEntryData> entries)
    {
        leaderboardEntries.Clear();
        if (entries != null)
            leaderboardEntries.AddRange(entries);

        RefreshListView(leaderboardList, leaderboardEntries, ref lastLeaderboardCount);

        if (emptyLeaderboardLabel != null)
            emptyLeaderboardLabel.EnableInClassList("hidden", leaderboardEntries.Count > 0);
    }

    public void SetResultEntries(IReadOnlyList<LeaderboardEntryData> entries)
    {
        resultEntries.Clear();
        if (entries != null)
            resultEntries.AddRange(entries);

        RefreshListView(resultsList, resultEntries, ref lastResultsCount);

        if (emptyResultsLabel != null)
            emptyResultsLabel.EnableInClassList("hidden", resultEntries.Count > 0);
    }

    public void SetLeaderboardVisible(bool visible)
    {
        if (leaderboardOverlay != null)
            leaderboardOverlay.EnableInClassList("hidden", !visible);
    }

    public void ShowPauseMenu(bool visible)
    {
        if (pauseOverlay != null)
            pauseOverlay.EnableInClassList("hidden", !visible);

        if (!visible)
        {
            pauseSettingsVisible = false;
            pauseSettingsPanel?.AddToClassList("hidden");
        }
    }

    public void ShowResults(bool visible, bool isVictory = false, string winnerName = "", string winReason = "", int reward = 0)
    {
        if (resultsOverlay != null)
            resultsOverlay.EnableInClassList("hidden", !visible);

        if (!visible)
            return;

        if (resultsTitleLabel != null)
            resultsTitleLabel.text = isVictory ? "VICTORY" : "DEFEAT";

        if (resultsWinnerLabel != null)
            resultsWinnerLabel.text = string.IsNullOrWhiteSpace(winnerName)
                ? "Winner: Unnamed"
                : $"Winner: {winnerName}";

        if (resultsReasonLabel != null)
            resultsReasonLabel.text = string.IsNullOrWhiteSpace(winReason)
                ? "Reason: Match Finished"
                : $"Reason: {winReason}";

        if (resultsRewardLabel != null)
            resultsRewardLabel.text = reward > 0 ? $"Coins Earned: +{reward}" : "Coins Earned: 0";
    }

    public void SetResultsCountdown(float remainingSeconds)
    {
        if (resultsCountdownLabel == null)
            return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        resultsCountdownLabel.text = $"Leaving room in {totalSeconds}s";
    }

    private void QueryElements()
    {
        hudLayer = root.Q("hud-layer");
        healthFill = root.Q("health-fill");
        ammoFill = root.Q("ammo-fill");
        connectingOverlay = root.Q("connecting-overlay");
        waitingOverlay = root.Q("waiting-overlay");
        respawnOverlay = root.Q("respawn-overlay");
        leaderboardOverlay = root.Q("leaderboard-overlay");
        pauseOverlay = root.Q("pause-overlay");
        pauseSettingsPanel = root.Q("pause-settings-panel");
        resultsOverlay = root.Q("results-overlay");
        killFeedRoot = root.Q("kill-feed-root");
        chatLogView = root.Q<ScrollView>("chat-log-view");
        chatInputField = root.Q<TextField>("chat-input-field");
        chatInputRow = root.Q("chat-input-row");
        leaderboardList = root.Q<ListView>("leaderboard-list");
        resultsList = root.Q<ListView>("results-list");

        sessionRoomLabel = root.Q<Label>("lbl-session-room");
        sessionMapLabel = root.Q<Label>("lbl-session-map");
        sessionStateLabel = root.Q<Label>("lbl-session-state");
        matchTimerLabel = root.Q<Label>("lbl-match-timer");
        healthValueLabel = root.Q<Label>("lbl-health-value");
        ammoValueLabel = root.Q<Label>("lbl-ammo-value");
        ammoReserveLabel = root.Q<Label>("lbl-ammo-reserve");
        connectingTitleLabel = root.Q<Label>("lbl-connecting-title");
        connectingBodyLabel = root.Q<Label>("lbl-connecting-body");
        waitingTitleLabel = root.Q<Label>("lbl-waiting-title");
        waitingBodyLabel = root.Q<Label>("lbl-waiting-body");
        respawnCountdownLabel = root.Q<Label>("lbl-respawn-countdown");
        chatHintLabel = root.Q<Label>("lbl-chat-hint");
        emptyLeaderboardLabel = root.Q<Label>("lbl-empty-leaderboard");
        emptyResultsLabel = root.Q<Label>("lbl-empty-results");
        resultsTitleLabel = root.Q<Label>("lbl-results-title");
        resultsWinnerLabel = root.Q<Label>("lbl-results-winner");
        resultsReasonLabel = root.Q<Label>("lbl-results-reason");
        resultsRewardLabel = root.Q<Label>("lbl-results-reward");
        resultsCountdownLabel = root.Q<Label>("lbl-results-countdown");

        pauseResumeButton = root.Q<Button>("btn-pause-resume");
        pauseSettingsButton = root.Q<Button>("btn-pause-settings");
        pauseLeaveButton = root.Q<Button>("btn-pause-leave");
        resultsLeaveButton = root.Q<Button>("btn-results-leave");
        hudToggle = root.Q<Toggle>("toggle-hud");
        tooltipsToggle = root.Q<Toggle>("toggle-tooltips");
        invertToggle = root.Q<Toggle>("toggle-invert");
        musicSlider = root.Q<Slider>("slider-music");
        sensXSlider = root.Q<Slider>("slider-sens-x");
        sensYSlider = root.Q<Slider>("slider-sens-y");
        smoothSlider = root.Q<Slider>("slider-smooth");
    }

    private void RegisterButtons()
    {
        if (pauseResumeButton != null)
            pauseResumeButton.clicked += HandlePauseResumeClicked;

        if (pauseSettingsButton != null)
            pauseSettingsButton.clicked += HandlePauseSettingsClicked;

        if (pauseLeaveButton != null)
            pauseLeaveButton.clicked += HandlePauseLeaveClicked;

        if (resultsLeaveButton != null)
            resultsLeaveButton.clicked += HandleResultsLeaveClicked;
    }

    private void RegisterSettingsCallbacks()
    {
        if (settingsCallbacksRegistered)
            return;

        hudToggle?.RegisterValueChangedCallback(OnHudToggleChanged);
        tooltipsToggle?.RegisterValueChangedCallback(OnTooltipsToggleChanged);
        invertToggle?.RegisterValueChangedCallback(OnInvertToggleChanged);
        musicSlider?.RegisterValueChangedCallback(OnMusicSliderChanged);
        sensXSlider?.RegisterValueChangedCallback(OnSensXSliderChanged);
        sensYSlider?.RegisterValueChangedCallback(OnSensYSliderChanged);
        smoothSlider?.RegisterValueChangedCallback(OnSmoothSliderChanged);
        settingsCallbacksRegistered = true;
    }

    private void UnregisterSettingsCallbacks()
    {
        if (!settingsCallbacksRegistered)
            return;

        hudToggle?.UnregisterValueChangedCallback(OnHudToggleChanged);
        tooltipsToggle?.UnregisterValueChangedCallback(OnTooltipsToggleChanged);
        invertToggle?.UnregisterValueChangedCallback(OnInvertToggleChanged);
        musicSlider?.UnregisterValueChangedCallback(OnMusicSliderChanged);
        sensXSlider?.UnregisterValueChangedCallback(OnSensXSliderChanged);
        sensYSlider?.UnregisterValueChangedCallback(OnSensYSliderChanged);
        smoothSlider?.UnregisterValueChangedCallback(OnSmoothSliderChanged);
        settingsCallbacksRegistered = false;
    }

    private void LoadSettingsIntoUI()
    {
        hudToggle?.SetValueWithoutNotify(PlayerPrefs.GetInt("ShowHUD", 1) == 1);
        tooltipsToggle?.SetValueWithoutNotify(PlayerPrefs.GetInt("ToolTips", 1) == 1);
        invertToggle?.SetValueWithoutNotify(PlayerPrefs.GetInt("MouseInvert", 0) == 1);
        musicSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1f));
        sensXSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSensX", 2f));
        sensYSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSensY", 2f));
        smoothSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSmooth", 3f));
        ApplyRuntimeSettingsFromPrefs();
    }

    private static void EnsureGameplaySettingsDefaults()
    {
        if (PlayerPrefs.GetInt(GameplaySettingsInitializedKey, 0) == 1)
            return;

        PlayerPrefs.SetInt("ShowHUD", 1);
        PlayerPrefs.SetInt("ToolTips", 1);
        PlayerPrefs.SetInt(GameplaySettingsInitializedKey, 1);
        PlayerPrefs.Save();
    }

    private void HandlePauseResumeClicked()
    {
        RoomManager.instance?.SetPauseMenuVisible(false);
    }

    private void HandlePauseSettingsClicked()
    {
        pauseSettingsVisible = !pauseSettingsVisible;

        if (pauseSettingsPanel != null)
            pauseSettingsPanel.EnableInClassList("hidden", !pauseSettingsVisible);
    }

    private void HandlePauseLeaveClicked()
    {
        RoomManager.instance?.LeaveRoomToLobby(true);
    }

    private void HandleResultsLeaveClicked()
    {
        RoomManager.instance?.LeaveRoomToLobby(true);
    }

    private void OnHudToggleChanged(ChangeEvent<bool> evt)
    {
        PlayerPrefs.SetInt("ShowHUD", evt.newValue ? 1 : 0);
        ApplyHudVisibilitySetting();
    }

    private void OnTooltipsToggleChanged(ChangeEvent<bool> evt)
    {
        PlayerPrefs.SetInt("ToolTips", evt.newValue ? 1 : 0);
    }

    private void OnInvertToggleChanged(ChangeEvent<bool> evt)
    {
        PlayerPrefs.SetInt("MouseInvert", evt.newValue ? 1 : 0);
        ApplyRuntimeSettingsFromPrefs();
    }

    private void OnMusicSliderChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MusicVolume", evt.newValue);
        ApplyRuntimeSettingsFromPrefs();
    }

    private void OnSensXSliderChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MouseSensX", evt.newValue);
        ApplyRuntimeSettingsFromPrefs();
    }

    private void OnSensYSliderChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MouseSensY", evt.newValue);
        ApplyRuntimeSettingsFromPrefs();
    }

    private void OnSmoothSliderChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MouseSmooth", evt.newValue);
        ApplyRuntimeSettingsFromPrefs();
    }

    private void ApplyRuntimeSettingsFromPrefs()
    {
        PlayerPrefs.Save();

        MouseLook.instance?.ApplyRuntimeSettingsFromPrefs();

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out AudioSource audioSource))
            audioSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
    }

    private void ApplyHudVisibilitySetting()
    {
        bool showHud = PlayerPrefs.GetInt("ShowHUD", 1) == 1;
        if (hudLayer != null)
            hudLayer.EnableInClassList("hud-hidden", !showHud);
    }

    private void RenderChatMessages()
    {
        if (chatLogView == null)
            return;

        chatLogView.contentContainer.Clear();

        foreach (string message in chatMessages)
        {
            var label = new Label(message)
            {
                enableRichText = true
            };
            label.AddToClassList("gameplay-chat-message");
            chatLogView.Add(label);
        }

        if (chatLogView.contentContainer.childCount > 0)
        {
            chatLogView.schedule.Execute(() =>
                chatLogView.ScrollTo(chatLogView.contentContainer[chatLogView.contentContainer.childCount - 1]));
        }
    }

    private void RenderKillFeed()
    {
        if (killFeedRoot == null)
            return;

        killFeedRoot.Clear();

        foreach (KillFeedEntryData entry in killFeedEntries)
        {
            string killText = entry.IsEnvironmentKill
                ? $"ENVIRONMENT eliminated {entry.Victim}"
                : $"{entry.Killer} eliminated {entry.Victim}";

            var row = new Label(killText);
            row.AddToClassList("kill-feed-entry");
            killFeedRoot.Add(row);
        }
    }

    private bool PruneExpiredKillFeed()
    {
        bool removedAny = false;

        for (int index = killFeedEntries.Count - 1; index >= 0; index--)
        {
            if (killFeedEntries[index].ExpireTime > Time.unscaledTime)
                continue;

            killFeedEntries.RemoveAt(index);
            removedAny = true;
        }

        return removedAny;
    }

    private void ConfigureListView(ListView listView, List<LeaderboardEntryData> source, ref int lastCountCache)
    {
        if (listView == null)
            return;

        listView.selectionType = SelectionType.None;
        listView.showBorder = false;
        listView.reorderable = false;
        listView.itemsSource = source;
        listView.makeItem = MakeLeaderboardRow;
        listView.bindItem = (element, index) => BindLeaderboardRow(element, index, source);
        lastCountCache = -1;
    }

    private void RefreshListView(ListView listView, List<LeaderboardEntryData> source, ref int lastCountCache)
    {
        if (listView == null)
            return;

        listView.itemsSource = source;

        if (lastCountCache != source.Count)
            listView.Rebuild();
        else
            listView.RefreshItems();

        lastCountCache = source.Count;
    }

    private VisualElement MakeLeaderboardRow()
    {
        var rowShell = new VisualElement();
        rowShell.AddToClassList("gameplay-leaderboard-row-shell");

        var row = new VisualElement();
        row.AddToClassList("gameplay-leaderboard-row");

        var rank = new Label { name = "leaderboard-rank" };
        rank.AddToClassList("gameplay-leaderboard-rank");

        var name = new Label { name = "leaderboard-name" };
        name.AddToClassList("gameplay-leaderboard-name");

        var score = new Label { name = "leaderboard-score" };
        score.AddToClassList("gameplay-leaderboard-score");

        var kd = new Label { name = "leaderboard-kd" };
        kd.AddToClassList("gameplay-leaderboard-kd");

        row.Add(rank);
        row.Add(name);
        row.Add(score);
        row.Add(kd);
        rowShell.Add(row);
        return rowShell;
    }

    private void BindLeaderboardRow(VisualElement element, int index, List<LeaderboardEntryData> source)
    {
        if (index < 0 || source == null || index >= source.Count)
            return;

        LeaderboardEntryData entry = source[index];

        element.Q<Label>("leaderboard-rank").text = entry.Rank.ToString();
        element.Q<Label>("leaderboard-name").text = entry.Name;
        element.Q<Label>("leaderboard-score").text = entry.Score.ToString();
        element.Q<Label>("leaderboard-kd").text = entry.Kd;
    }

    private bool IsResultsVisible()
    {
        return resultsOverlay != null && !resultsOverlay.ClassListContains("hidden");
    }

    private static void SetFillPercent(VisualElement fillElement, float percent)
    {
        if (fillElement == null)
            return;

        fillElement.style.width = Length.Percent(Mathf.Clamp01(percent) * 100f);
    }

    private static string GetCurrentSceneLabel()
    {
        return SceneManager.GetActiveScene().name.Replace('_', ' ');
    }
}
