using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIToolkitLobbyController : MonoBehaviour
{
    private const int MenuBuildIndex = 0;
    private const int SceneTemplateBuildIndex = 2;
    private const int SecondMapBuildIndex = 3;

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private RoomList roomList;

    private readonly List<RoomList.RoomEntryData> roomEntries = new List<RoomList.RoomEntryData>();

    private VisualElement root;
    private VisualElement drawerScrim;
    private VisualElement createDrawer;

    private Label connectionStatusLabel;
    private Label roomCountBadge;
    private Label emptyRoomsLabel;
    private Label createMessageBadge;
    private Label createMessageLabel;
    private Label mapOneTitleLabel;
    private Label mapOneDescriptionLabel;
    private Label mapTwoTitleLabel;
    private Label mapTwoDescriptionLabel;

    private Button openDrawerButton;
    private Button backToMenuButton;
    private Button closeDrawerButton;
    private Button cancelCreateButton;
    private Button createRoomButton;
    private Button selectMapOneButton;
    private Button selectMapTwoButton;

    private TextField createRoomNameField;
    private ListView roomListView;

    private bool callbacksRegistered;
    private int selectedSceneIndex = SceneTemplateBuildIndex;
    private int lastRoomCount = -1;

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
        SelectMap(SceneTemplateBuildIndex);
        CloseDrawer();
        HandleConnectionStatusChanged(roomList.CurrentStatus);
        HandleRoomEntriesChanged(roomList.CurrentRoomEntries);
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
        roomEntries.Clear();
        lastRoomCount = -1;
        root = null;
    }

    private void QueryElements()
    {
        drawerScrim = root.Q("drawer-scrim");
        createDrawer = root.Q("create-drawer");

        connectionStatusLabel = root.Q<Label>("lbl-connection-status");
        roomCountBadge = root.Q<Label>("lbl-room-count-badge");
        emptyRoomsLabel = root.Q<Label>("lbl-empty-rooms");
        createMessageBadge = root.Q<Label>("create-message-badge");
        createMessageLabel = root.Q<Label>("create-message");
        mapOneTitleLabel = root.Q<Label>("lbl-map-1-title");
        mapOneDescriptionLabel = root.Q<Label>("lbl-map-1-desc");
        mapTwoTitleLabel = root.Q<Label>("lbl-map-2-title");
        mapTwoDescriptionLabel = root.Q<Label>("lbl-map-2-desc");

        openDrawerButton = root.Q<Button>("btn-open-drawer");
        backToMenuButton = root.Q<Button>("btn-back-menu");
        closeDrawerButton = root.Q<Button>("btn-close-drawer");
        cancelCreateButton = root.Q<Button>("btn-cancel-create");
        createRoomButton = root.Q<Button>("btn-create-room");
        selectMapOneButton = root.Q<Button>("btn-select-map-1");
        selectMapTwoButton = root.Q<Button>("btn-select-map-2");

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
        roomListView.reorderable = false;
        roomListView.itemsSource = roomEntries;
        roomListView.makeItem = MakeRoomItem;
        roomListView.bindItem = BindRoomItem;
    }

    private VisualElement MakeRoomItem()
    {
        var rowShell = new VisualElement();
        rowShell.AddToClassList("room-row-shell");

        var joinButton = new Button();
        joinButton.name = "room-row-button";
        joinButton.AddToClassList("room-row-button");
        joinButton.clicked += () =>
        {
            if (joinButton.userData is RoomList.RoomEntryData roomEntry)
            {
                roomList.JoinRoomByName(roomEntry.RoomName, roomEntry.SceneIndex);
            }
        };

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

        var joinLabel = new Label("JOIN");
        joinLabel.AddToClassList("room-row-cta");

        sideColumn.Add(roomCountLabel);
        sideColumn.Add(joinLabel);

        joinButton.Add(mainColumn);
        joinButton.Add(sideColumn);
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

        openDrawerButton.clicked += OnOpenDrawerClicked;
        backToMenuButton.clicked += OnBackToMenuClicked;
        closeDrawerButton.clicked += OnCloseDrawerClicked;
        cancelCreateButton.clicked += OnCloseDrawerClicked;
        createRoomButton.clicked += OnCreateRoomClicked;
        selectMapOneButton.clicked += OnSelectMapOneClicked;
        selectMapTwoButton.clicked += OnSelectMapTwoClicked;

        createRoomNameField.RegisterValueChangedCallback(OnRoomNameChanged);
        drawerScrim.RegisterCallback<PointerDownEvent>(OnDrawerScrimPointerDown);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

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

        openDrawerButton.clicked -= OnOpenDrawerClicked;
        backToMenuButton.clicked -= OnBackToMenuClicked;
        closeDrawerButton.clicked -= OnCloseDrawerClicked;
        cancelCreateButton.clicked -= OnCloseDrawerClicked;
        createRoomButton.clicked -= OnCreateRoomClicked;
        selectMapOneButton.clicked -= OnSelectMapOneClicked;
        selectMapTwoButton.clicked -= OnSelectMapTwoClicked;

        createRoomNameField.UnregisterValueChangedCallback(OnRoomNameChanged);
        drawerScrim.UnregisterCallback<PointerDownEvent>(OnDrawerScrimPointerDown);
        root.UnregisterCallback<KeyDownEvent>(OnKeyDown);

        roomList.RoomEntriesChanged -= HandleRoomEntriesChanged;
        roomList.ConnectionStatusChanged -= HandleConnectionStatusChanged;

        callbacksRegistered = false;
    }

    private void OnOpenDrawerClicked()
    {
        OpenDrawer();
    }

    private void OnBackToMenuClicked()
    {
        StartCoroutine(ReturnToMenuFlow());
    }

    private void OnCloseDrawerClicked()
    {
        CloseDrawer();
    }

    private void OnCreateRoomClicked()
    {
        TryCreateRoom();
    }

    private void OnSelectMapOneClicked()
    {
        SelectMap(SceneTemplateBuildIndex);
    }

    private void OnSelectMapTwoClicked()
    {
        SelectMap(SecondMapBuildIndex);
    }

    private void OnRoomNameChanged(ChangeEvent<string> evt)
    {
        roomList.ChangeRoomToCreateName(evt.newValue);

        if (!string.IsNullOrWhiteSpace(evt.newValue))
        {
            ClearCreateMessage();
        }
    }

    private void OnDrawerScrimPointerDown(PointerDownEvent evt)
    {
        CloseDrawer();
        evt.StopPropagation();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (!IsDrawerOpen())
        {
            return;
        }

        if (evt.keyCode == KeyCode.Escape)
        {
            CloseDrawer();
            evt.StopPropagation();
        }
    }

    private void OpenDrawer()
    {
        drawerScrim.RemoveFromClassList("hidden");
        createDrawer.RemoveFromClassList("hidden");
        ClearCreateMessage();
        SyncRoomNameField();
        createRoomNameField.schedule.Execute(() => createRoomNameField.Focus());
    }

    private void CloseDrawer()
    {
        drawerScrim.AddToClassList("hidden");
        createDrawer.AddToClassList("hidden");
        ClearCreateMessage();
    }

    private bool IsDrawerOpen()
    {
        return createDrawer != null && !createDrawer.ClassListContains("hidden");
    }

    private System.Collections.IEnumerator ReturnToMenuFlow()
    {
        CloseDrawer();
        HandleConnectionStatusChanged("Returning to menu...");

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            yield return new WaitUntil(() => !PhotonNetwork.IsConnected);
        }

        SceneManager.LoadScene(MenuBuildIndex);
    }

    private void TryCreateRoom()
    {
        var trimmedRoomName = createRoomNameField.value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedRoomName))
        {
            SetCreateMessage("Enter a room name.");
            createRoomNameField.Focus();
            return;
        }

        ClearCreateMessage();
        roomList.ChangeRoomToCreateName(trimmedRoomName);
        roomList.CreateRoomByIndex(selectedSceneIndex);
    }

    private void SelectMap(int buildIndex)
    {
        selectedSceneIndex = buildIndex;
        selectMapOneButton.EnableInClassList("selected", buildIndex == SceneTemplateBuildIndex);
        selectMapTwoButton.EnableInClassList("selected", buildIndex == SecondMapBuildIndex);
    }

    private void SyncRoomNameField()
    {
        createRoomNameField.SetValueWithoutNotify(roomList.CurrentRoomNameToCreate);
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
            if (lastRoomCount != roomEntries.Count)
            {
                roomListView.Rebuild();
            }
            else
            {
                roomListView.RefreshItems();
            }
        }

        lastRoomCount = roomEntries.Count;

        var hasRooms = roomEntries.Count > 0;
        emptyRoomsLabel.EnableInClassList("hidden", hasRooms);
        roomCountBadge.text = roomEntries.Count == 1 ? "1 ROOM" : $"{roomEntries.Count} ROOMS";
    }

    private void HandleConnectionStatusChanged(string status)
    {
        var resolvedStatus = string.IsNullOrWhiteSpace(status) ? "Lobby idle." : status.Trim();
        connectionStatusLabel.text = GetCompactStatusLabel(resolvedStatus);

        var isLobbyOnline = resolvedStatus.StartsWith("Lobby feed online", StringComparison.OrdinalIgnoreCase);
        openDrawerButton.SetEnabled(isLobbyOnline);
        createRoomButton.SetEnabled(isLobbyOnline);
    }

    private void UpdateMapLabels()
    {
        SetMapLabel(mapOneTitleLabel, mapOneDescriptionLabel, SceneTemplateBuildIndex);
        SetMapLabel(mapTwoTitleLabel, mapTwoDescriptionLabel, SecondMapBuildIndex);
    }

    private static void SetMapLabel(Label titleLabel, Label descriptionLabel, int buildIndex)
    {
        var sceneName = GetSceneDisplayName(buildIndex);
        titleLabel.text = sceneName;
        descriptionLabel.text = $"Build scene {buildIndex}";
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

    private static string GetCompactStatusLabel(string status)
    {
        if (status.StartsWith("Lobby feed online", StringComparison.OrdinalIgnoreCase))
        {
            return "ONLINE";
        }

        if (status.StartsWith("Loading room", StringComparison.OrdinalIgnoreCase))
        {
            return "LOADING";
        }

        if (status.StartsWith("Disconnected", StringComparison.OrdinalIgnoreCase))
        {
            return "DISCONNECTED";
        }

        if (status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Connecting", StringComparison.OrdinalIgnoreCase))
        {
            return "CONNECTING";
        }

        if (status.StartsWith("Resetting", StringComparison.OrdinalIgnoreCase))
        {
            return "RESETTING";
        }

        return "LOBBY";
    }

    private void SetCreateMessage(string message)
    {
        createMessageBadge.text = "ERR";
        createMessageLabel.text = message;
        createMessageLabel.parent.RemoveFromClassList("hidden");
    }

    private void ClearCreateMessage()
    {
        createMessageLabel.text = string.Empty;
        createMessageLabel.parent.AddToClassList("hidden");
    }
}
