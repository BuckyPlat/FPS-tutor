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

    public static UIToolkitGameplayUIController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private readonly List<LeaderboardEntryData> leaderboardEntries = new List<LeaderboardEntryData>();
    private readonly List<string> chatMessages = new List<string>();

    private VisualElement root;
    private VisualElement healthFill;
    private VisualElement ammoFill;
    private VisualElement connectingOverlay;
    private VisualElement respawnOverlay;
    private VisualElement leaderboardOverlay;
    private ScrollView chatLogView;
    private TextField chatInputField;
    private VisualElement chatInputRow;
    private ListView leaderboardList;

    private Label sessionRoomLabel;
    private Label sessionMapLabel;
    private Label sessionStateLabel;
    private Label healthValueLabel;
    private Label ammoValueLabel;
    private Label ammoReserveLabel;
    private Label connectingTitleLabel;
    private Label connectingBodyLabel;
    private Label respawnCountdownLabel;
    private Label chatHintLabel;
    private Label emptyLeaderboardLabel;

    private int lastLeaderboardCount = -1;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

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
        ConfigureLeaderboardList();
        SetSessionInfo(PlayerPrefs.GetString("RoomNameToJoin", "AUTO ROOM"), GetCurrentSceneLabel());
        SetSessionState("CONNECTING");
        SetLocalHealth(100, 100);
        SetAmmo(0, 0, 0);
        SetConnectingVisible(false);
        HideRespawn();
        SetLeaderboardVisible(false);
        CloseChat(true);
        RenderChatMessages();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        root = null;
        lastLeaderboardCount = -1;
    }

    private void QueryElements()
    {
        sessionRoomLabel = root.Q<Label>("lbl-session-room");
        sessionMapLabel = root.Q<Label>("lbl-session-map");
        sessionStateLabel = root.Q<Label>("lbl-session-state");
        healthValueLabel = root.Q<Label>("lbl-health-value");
        ammoValueLabel = root.Q<Label>("lbl-ammo-value");
        ammoReserveLabel = root.Q<Label>("lbl-ammo-reserve");
        connectingTitleLabel = root.Q<Label>("lbl-connecting-title");
        connectingBodyLabel = root.Q<Label>("lbl-connecting-body");
        respawnCountdownLabel = root.Q<Label>("lbl-respawn-countdown");
        chatHintLabel = root.Q<Label>("lbl-chat-hint");
        emptyLeaderboardLabel = root.Q<Label>("lbl-empty-leaderboard");

        healthFill = root.Q("health-fill");
        ammoFill = root.Q("ammo-fill");
        connectingOverlay = root.Q("connecting-overlay");
        respawnOverlay = root.Q("respawn-overlay");
        leaderboardOverlay = root.Q("leaderboard-overlay");
        chatLogView = root.Q<ScrollView>("chat-log-view");
        chatInputField = root.Q<TextField>("chat-input-field");
        chatInputRow = root.Q("chat-input-row");
        leaderboardList = root.Q<ListView>("leaderboard-list");
    }

    private void ConfigureLeaderboardList()
    {
        if (leaderboardList == null)
        {
            return;
        }

        leaderboardList.selectionType = SelectionType.None;
        leaderboardList.showBorder = false;
        leaderboardList.reorderable = false;
        leaderboardList.itemsSource = leaderboardEntries;
        leaderboardList.makeItem = MakeLeaderboardRow;
        leaderboardList.bindItem = BindLeaderboardRow;
    }

    private VisualElement MakeLeaderboardRow()
    {
        var rowShell = new VisualElement();
        rowShell.AddToClassList("gameplay-leaderboard-row-shell");

        var row = new VisualElement();
        row.AddToClassList("gameplay-leaderboard-row");

        var rank = new Label();
        rank.name = "leaderboard-rank";
        rank.AddToClassList("gameplay-leaderboard-rank");

        var name = new Label();
        name.name = "leaderboard-name";
        name.AddToClassList("gameplay-leaderboard-name");

        var score = new Label();
        score.name = "leaderboard-score";
        score.AddToClassList("gameplay-leaderboard-score");

        var kd = new Label();
        kd.name = "leaderboard-kd";
        kd.AddToClassList("gameplay-leaderboard-kd");

        row.Add(rank);
        row.Add(name);
        row.Add(score);
        row.Add(kd);
        rowShell.Add(row);
        return rowShell;
    }

    private void BindLeaderboardRow(VisualElement element, int index)
    {
        if (index < 0 || index >= leaderboardEntries.Count)
        {
            return;
        }

        var entry = leaderboardEntries[index];
        element.Q<Label>("leaderboard-rank").text = entry.Rank.ToString();
        element.Q<Label>("leaderboard-name").text = entry.Name;
        element.Q<Label>("leaderboard-score").text = entry.Score.ToString();
        element.Q<Label>("leaderboard-kd").text = entry.Kd;
    }

    public void SetSessionInfo(string roomName, string mapName)
    {
        if (sessionRoomLabel != null)
        {
            sessionRoomLabel.text = string.IsNullOrWhiteSpace(roomName) ? "AUTO ROOM" : roomName.ToUpperInvariant();
        }

        if (sessionMapLabel != null)
        {
            sessionMapLabel.text = string.IsNullOrWhiteSpace(mapName) ? GetCurrentSceneLabel().ToUpperInvariant() : mapName.ToUpperInvariant();
        }
    }

    public void SetSessionState(string state)
    {
        if (sessionStateLabel != null)
        {
            sessionStateLabel.text = string.IsNullOrWhiteSpace(state) ? "LIVE" : state.Trim().ToUpperInvariant();
        }
    }

    public void SetLocalHealth(int currentHealth, int maxHealth)
    {
        if (healthValueLabel != null)
        {
            healthValueLabel.text = Mathf.Max(0, currentHealth).ToString();
        }

        SetFillPercent(healthFill, maxHealth <= 0 ? 0f : (float)currentHealth / maxHealth);
    }

    public void SetAmmo(int currentAmmo, int reserveMags, int magSize)
    {
        if (ammoValueLabel != null)
        {
            ammoValueLabel.text = $"{Mathf.Max(0, currentAmmo)}/{Mathf.Max(0, magSize)}";
        }

        if (ammoReserveLabel != null)
        {
            ammoReserveLabel.text = $"MAG x{Mathf.Max(0, reserveMags)}";
        }

        SetFillPercent(ammoFill, magSize <= 0 ? 0f : (float)currentAmmo / magSize);
    }

    public void SetConnectingVisible(bool visible, string title = null, string body = null)
    {
        if (connectingOverlay == null)
        {
            return;
        }

        connectingOverlay.EnableInClassList("hidden", !visible);

        if (!string.IsNullOrWhiteSpace(title) && connectingTitleLabel != null)
        {
            connectingTitleLabel.text = title;
        }

        if (!string.IsNullOrWhiteSpace(body) && connectingBodyLabel != null)
        {
            connectingBodyLabel.text = body;
        }
    }

    public void ShowRespawnCountdown(int secondsRemaining)
    {
        if (respawnCountdownLabel != null)
        {
            respawnCountdownLabel.text = $"Respawn in: {Mathf.Max(0, secondsRemaining)}";
        }

        if (respawnOverlay != null)
        {
            respawnOverlay.RemoveFromClassList("hidden");
        }
    }

    public void HideRespawn()
    {
        if (respawnOverlay != null)
        {
            respawnOverlay.AddToClassList("hidden");
        }
    }

    public void AppendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        chatMessages.Add(message);
        while (chatMessages.Count > 15)
        {
            chatMessages.RemoveAt(0);
        }

        RenderChatMessages();
    }

    public void OpenChat()
    {
        if (chatInputRow != null)
        {
            chatInputRow.RemoveFromClassList("hidden");
        }

        if (chatHintLabel != null)
        {
            chatHintLabel.text = "ENTER TO SEND";
        }

        if (chatInputField != null)
        {
            chatInputField.SetValueWithoutNotify(string.Empty);
            chatInputField.schedule.Execute(() => chatInputField.Focus());
        }
    }

    public void CloseChat(bool clearInput)
    {
        if (chatInputRow != null)
        {
            chatInputRow.AddToClassList("hidden");
        }

        if (chatHintLabel != null)
        {
            chatHintLabel.text = "PRESS Y TO CHAT";
        }

        if (chatInputField != null)
        {
            if (clearInput)
            {
                chatInputField.SetValueWithoutNotify(string.Empty);
            }

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
        {
            chatInputField.SetValueWithoutNotify(string.Empty);
        }
    }

    public void SetLeaderboardEntries(IReadOnlyList<LeaderboardEntryData> entries)
    {
        leaderboardEntries.Clear();
        if (entries != null)
        {
            leaderboardEntries.AddRange(entries);
        }

        if (leaderboardList != null)
        {
            if (lastLeaderboardCount != leaderboardEntries.Count)
            {
                leaderboardList.Rebuild();
            }
            else
            {
                leaderboardList.RefreshItems();
            }
        }

        lastLeaderboardCount = leaderboardEntries.Count;

        if (emptyLeaderboardLabel != null)
        {
            emptyLeaderboardLabel.EnableInClassList("hidden", leaderboardEntries.Count > 0);
        }
    }

    public void SetLeaderboardVisible(bool visible)
    {
        if (leaderboardOverlay != null)
        {
            leaderboardOverlay.EnableInClassList("hidden", !visible);
        }
    }

    private void RenderChatMessages()
    {
        if (chatLogView == null)
        {
            return;
        }

        chatLogView.contentContainer.Clear();

        foreach (var message in chatMessages)
        {
            var label = new Label(message);
            label.enableRichText = true;
            label.AddToClassList("gameplay-chat-message");
            chatLogView.Add(label);
        }

        if (chatLogView.contentContainer.childCount > 0)
        {
            chatLogView.schedule.Execute(() =>
                chatLogView.ScrollTo(chatLogView.contentContainer[chatLogView.contentContainer.childCount - 1]));
        }
    }

    private static void SetFillPercent(VisualElement fillElement, float percent)
    {
        if (fillElement == null)
        {
            return;
        }

        fillElement.style.width = Length.Percent(Mathf.Clamp01(percent) * 100f);
    }

    private static string GetCurrentSceneLabel()
    {
        return SceneManager.GetActiveScene().name.Replace('_', ' ');
    }
}
