using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIToolkitLobbyController : MonoBehaviour
{
    private const int SceneTemplateBuildIndex = 2;
    private const int SecondMapBuildIndex = 3;

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private RoomList roomList;

    private VisualElement root;
    private VisualElement browserScreen;
    private VisualElement createScreen;
    private VisualElement createMessageRow;

    private Label topStatusLabel;
    private Label topStatusDescription;
    private Label sidebarStatusChip;
    private Label sidebarStatusCopy;
    private Label sidebarRoomCount;
    private Label roomCountBadge;
    private Label emptyRoomsLabel;
    private Label createMessageBadge;
    private Label createMessageLabel;
    private Label mapOneTitleLabel;
    private Label mapOneDescriptionLabel;
    private Label mapTwoTitleLabel;
    private Label mapTwoDescriptionLabel;

    private Button browseNavButton;
    private Button createNavButton;
    private Button openCreateButton;
    private Button backToBrowserButton;
    private Button createMapOneButton;
    private Button createMapTwoButton;

    private TextField createRoomNameField;
    private ListView roomListView;

    private readonly List<RoomList.RoomEntryData> roomEntries = new List<RoomList.RoomEntryData>();
    private bool callbacksRegistered;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (roomList == null)
        {
            roomList = FindFirstObjectByType<RoomList>();
        }

        if (uiDocument == null || roomList == null)
        {
            Debug.LogError("Lobby UI Toolkit controller requires both UIDocument and RoomList.");
            return;
        }

        root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("Lobby UIDocument rootVisualElement is not ready.");
            return;
        }

        QueryElements();
        ConfigureRoomListView();
        UpdateMapLabels();
        RegisterCallbacks();
        SyncRoomNameField();
        ShowBrowserScreen();
        HandleConnectionStatusChanged(roomList.CurrentStatus);
        HandleRoomEntriesChanged(roomList.CurrentRoomEntries);
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
        roomEntries.Clear();
        root = null;
    }

    private void QueryElements()
    {
        browserScreen = root.Q("room-browser-screen");
        createScreen = root.Q("room-create-screen");
        createMessageRow = root.Q("create-message-row");

        topStatusLabel = root.Q<Label>("lbl-lobby-status");
        topStatusDescription = root.Q<Label>("lbl-lobby-status-desc");
        sidebarStatusChip = root.Q<Label>("sidebar-status-chip");
        sidebarStatusCopy = root.Q<Label>("sidebar-status-copy");
        sidebarRoomCount = root.Q<Label>("sidebar-room-count");
        roomCountBadge = root.Q<Label>("lbl-room-count-badge");
        emptyRoomsLabel = root.Q<Label>("lbl-empty-rooms");
        createMessageBadge = root.Q<Label>("create-message-badge");
        createMessageLabel = root.Q<Label>("create-message");
        mapOneTitleLabel = root.Q<Label>("lbl-map-1-title");
        mapOneDescriptionLabel = root.Q<Label>("lbl-map-1-desc");
        mapTwoTitleLabel = root.Q<Label>("lbl-map-2-title");
        mapTwoDescriptionLabel = root.Q<Label>("lbl-map-2-desc");

        browseNavButton = root.Q<Button>("nav-browse");
        createNavButton = root.Q<Button>("nav-create");
        openCreateButton = root.Q<Button>("btn-open-create-room");
        backToBrowserButton = root.Q<Button>("btn-back-to-browser");
        createMapOneButton = root.Q<Button>("btn-create-map-1");
        createMapTwoButton = root.Q<Button>("btn-create-map-2");

        createRoomNameField = root.Q<TextField>("create-room-name");
        roomListView = root.Q<ListView>("room-list-view");
    }

    private void ConfigureRoomListView()
    {
        if (roomListView == null)
        {
            return;
        }

        roomListView.selectionType = SelectionType.None;
        roomListView.showBorder = false;
        roomListView.itemsSource = roomEntries;
        roomListView.makeItem = MakeRoomItem;
        roomListView.bindItem = BindRoomItem;
        roomListView.reorderable = false;
    }

    private VisualElement MakeRoomItem()
    {
        var rowShell = new VisualElement();
        rowShell.AddToClassList("room-row-shell");

        var joinButton = new Button();
        joinButton.name = "room-row-button";
        joinButton.AddToClassList("room-row-button");

        var mainColumn = new VisualElement();
        mainColumn.AddToClassList("room-row-main");

        var roomNameLabel = new Label();
        roomNameLabel.name = "room-row-name";
        roomNameLabel.AddToClassList("room-row-name");

        var roomMapLabel = new Label();
        roomMapLabel.name = "room-row-map";
        roomMapLabel.AddToClassList("room-row-map");

        mainColumn.Add(roomNameLabel);
        mainColumn.Add(roomMapLabel);

        var sideColumn = new VisualElement();
        sideColumn.AddToClassList("room-row-side");

        var roomCountLabel = new Label();
        roomCountLabel.name = "room-row-count";
        roomCountLabel.AddToClassList("room-row-count");

        var joinLabel = new Label("Join");
        joinLabel.AddToClassList("room-row-cta");

        sideColumn.Add(roomCountLabel);
        sideColumn.Add(joinLabel);

        joinButton.Add(mainColumn);
        joinButton.Add(sideColumn);
        joinButton.clicked += () =>
        {
            if (joinButton.userData is RoomList.RoomEntryData roomEntry)
            {
                roomList.JoinRoomByName(roomEntry.RoomName, roomEntry.SceneIndex);
            }
        };

        rowShell.Add(joinButton);
        return rowShell;
    }

    private void BindRoomItem(VisualElement element, int index)
    {
        if (index < 0 || index >= roomEntries.Count)
        {
            return;
        }

        var roomEntry = roomEntries[index];
        var joinButton = element.Q<Button>("room-row-button");
        var roomNameLabel = element.Q<Label>("room-row-name");
        var roomMapLabel = element.Q<Label>("room-row-map");
        var roomCountLabel = element.Q<Label>("room-row-count");

        if (joinButton != null)
        {
            joinButton.userData = roomEntry;
            joinButton.tooltip = $"Join {roomEntry.RoomName}";
        }

        if (roomNameLabel != null)
        {
            roomNameLabel.text = roomEntry.RoomName;
        }

        if (roomMapLabel != null)
        {
            roomMapLabel.text = roomEntry.MapLabel;
        }

        if (roomCountLabel != null)
        {
            roomCountLabel.text = $"{roomEntry.PlayerCount}/10";
        }
    }

    private void RegisterCallbacks()
    {
        if (callbacksRegistered || roomList == null)
        {
            return;
        }

        browseNavButton?.RegisterCallback<ClickEvent>(OnBrowseClicked);
        createNavButton?.RegisterCallback<ClickEvent>(OnCreateClicked);
        openCreateButton?.RegisterCallback<ClickEvent>(OnCreateClicked);
        backToBrowserButton?.RegisterCallback<ClickEvent>(OnBrowseClicked);
        createMapOneButton?.RegisterCallback<ClickEvent>(OnCreateSceneTemplateClicked);
        createMapTwoButton?.RegisterCallback<ClickEvent>(OnCreateSecondMapClicked);
        createRoomNameField?.RegisterValueChangedCallback(OnRoomNameChanged);

        roomList.RoomEntriesChanged += HandleRoomEntriesChanged;
        roomList.ConnectionStatusChanged += HandleConnectionStatusChanged;

        callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!callbacksRegistered || roomList == null)
        {
            return;
        }

        browseNavButton?.UnregisterCallback<ClickEvent>(OnBrowseClicked);
        createNavButton?.UnregisterCallback<ClickEvent>(OnCreateClicked);
        openCreateButton?.UnregisterCallback<ClickEvent>(OnCreateClicked);
        backToBrowserButton?.UnregisterCallback<ClickEvent>(OnBrowseClicked);
        createMapOneButton?.UnregisterCallback<ClickEvent>(OnCreateSceneTemplateClicked);
        createMapTwoButton?.UnregisterCallback<ClickEvent>(OnCreateSecondMapClicked);
        createRoomNameField?.UnregisterValueChangedCallback(OnRoomNameChanged);

        roomList.RoomEntriesChanged -= HandleRoomEntriesChanged;
        roomList.ConnectionStatusChanged -= HandleConnectionStatusChanged;

        callbacksRegistered = false;
    }

    private void OnBrowseClicked(ClickEvent evt)
    {
        ShowBrowserScreen();
    }

    private void OnCreateClicked(ClickEvent evt)
    {
        ShowCreateScreen();
    }

    private void OnCreateSceneTemplateClicked(ClickEvent evt)
    {
        TryCreateRoom(SceneTemplateBuildIndex);
    }

    private void OnCreateSecondMapClicked(ClickEvent evt)
    {
        TryCreateRoom(SecondMapBuildIndex);
    }

    private void OnRoomNameChanged(ChangeEvent<string> evt)
    {
        roomList.ChangeRoomToCreateName(evt.newValue);

        if (!string.IsNullOrWhiteSpace(evt.newValue))
        {
            ClearCreateMessage();
        }
    }

    private void TryCreateRoom(int sceneIndex)
    {
        if (createRoomNameField == null)
        {
            return;
        }

        var trimmedRoomName = createRoomNameField.value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedRoomName))
        {
            SetCreateMessage("Enter a room name before selecting a battleground.");
            createRoomNameField.Focus();
            return;
        }

        roomList.ChangeRoomToCreateName(trimmedRoomName);
        roomList.CreateRoomByIndex(sceneIndex);
    }

    private void HandleRoomEntriesChanged(IReadOnlyList<RoomList.RoomEntryData> updatedRooms)
    {
        roomEntries.Clear();
        if (updatedRooms != null)
        {
            roomEntries.AddRange(updatedRooms);
        }

        if (roomListView != null)
        {
            roomListView.itemsSource = roomEntries;
            roomListView.Rebuild();
        }

        var hasRooms = roomEntries.Count > 0;
        emptyRoomsLabel?.EnableInClassList("hidden", hasRooms);
        if (roomCountBadge != null)
        {
            roomCountBadge.text = $"{roomEntries.Count} ROOMS";
        }

        if (sidebarRoomCount != null)
        {
            sidebarRoomCount.text = roomEntries.Count.ToString();
        }

        if (topStatusDescription != null)
        {
            topStatusDescription.text = hasRooms
                ? $"{roomEntries.Count} deployable room(s) available."
                : "No deployable rooms detected yet.";
        }
    }

    private void HandleConnectionStatusChanged(string status)
    {
        var resolvedStatus = string.IsNullOrWhiteSpace(status) ? "Lobby idle." : status.Trim();
        var compactStatus = resolvedStatus.ToUpperInvariant();

        if (topStatusLabel != null)
        {
            topStatusLabel.text = compactStatus;
        }

        if (sidebarStatusChip != null)
        {
            sidebarStatusChip.text = compactStatus;
        }

        if (sidebarStatusCopy != null)
        {
            sidebarStatusCopy.text = resolvedStatus;
        }
    }

    private void ShowBrowserScreen()
    {
        ShowScreen(browserScreen, createScreen);
        SetNavState(true);
        ClearCreateMessage();
    }

    private void ShowCreateScreen()
    {
        ShowScreen(createScreen, browserScreen);
        SetNavState(false);
        SyncRoomNameField();
    }

    private void ShowScreen(VisualElement screenToShow, VisualElement screenToHide)
    {
        if (screenToShow != null)
        {
            screenToShow.RemoveFromClassList("hidden");
            screenToShow.AddToClassList("motion-active");
        }

        if (screenToHide != null)
        {
            screenToHide.RemoveFromClassList("motion-active");
            screenToHide.AddToClassList("hidden");
        }
    }

    private void SetNavState(bool browsing)
    {
        browseNavButton?.EnableInClassList("active", browsing);
        createNavButton?.EnableInClassList("active", !browsing);
    }

    private void SyncRoomNameField()
    {
        if (createRoomNameField == null || roomList == null)
        {
            return;
        }

        createRoomNameField.SetValueWithoutNotify(roomList.CurrentRoomNameToCreate);
    }

    private void UpdateMapLabels()
    {
        SetMapLabel(mapOneTitleLabel, mapOneDescriptionLabel, SceneTemplateBuildIndex);
        SetMapLabel(mapTwoTitleLabel, mapTwoDescriptionLabel, SecondMapBuildIndex);
    }

    private static void SetMapLabel(Label titleLabel, Label descriptionLabel, int buildIndex)
    {
        var sceneName = GetSceneDisplayName(buildIndex);
        if (titleLabel != null)
        {
            titleLabel.text = sceneName;
        }

        if (descriptionLabel != null)
        {
            descriptionLabel.text = $"Load build scene {buildIndex} ({sceneName}) and join or create the selected Photon room.";
        }
    }

    private static string GetSceneDisplayName(int buildIndex)
    {
        var scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return $"Build Scene {buildIndex}";
        }

        return Path.GetFileNameWithoutExtension(scenePath).Replace('_', ' ');
    }

    private void SetCreateMessage(string message)
    {
        if (createMessageRow == null || createMessageLabel == null || createMessageBadge == null)
        {
            return;
        }

        createMessageLabel.text = message;
        createMessageBadge.text = "ERR";
        createMessageRow.RemoveFromClassList("hidden");
    }

    private void ClearCreateMessage()
    {
        if (createMessageRow == null || createMessageLabel == null)
        {
            return;
        }

        createMessageLabel.text = string.Empty;
        createMessageRow.AddToClassList("hidden");
    }
}
