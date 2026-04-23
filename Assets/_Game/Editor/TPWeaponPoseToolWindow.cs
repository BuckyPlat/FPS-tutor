using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TPWeaponPoseToolWindow : EditorWindow
{
    private const string PlayerPrefabPath = "Assets/Resources/Player.prefab";
    private const string HolderName = "TP WeaponHolder";

    [SerializeField] private int currentWeaponIndex;
    [SerializeField] private bool editHolder = true;
    [SerializeField] private int selectedLivePlayerIndex;

    private PrefabStage currentPrefabStage;
    private Transform prefabHolderTransform;
    private readonly List<Transform> prefabWeaponTransforms = new List<Transform>();
    private readonly Dictionary<int, bool> prefabDefaultActiveStates = new Dictionary<int, bool>();
    private readonly Dictionary<int, TransformPose> prefabDefaultWeaponPoses = new Dictionary<int, TransformPose>();
    private TransformPose prefabDefaultHolderPose;
    private bool prefabDefaultsCaptured;
    private bool previewMutatedStage;
    private bool prefabPoseChangesMade;
    private bool stageWasDirtyAtCapture;

    private readonly List<PlayerSetup> livePlayers = new List<PlayerSetup>();
    private readonly List<string> livePlayerLabels = new List<string>();
    private PlayerSetup boundLivePlayer;
    private string boundLiveAssetPath;
    private bool liveBindingLost;
    private Transform liveHolderTransform;
    private readonly List<Transform> liveWeaponTransforms = new List<Transform>();
    private readonly Dictionary<int, TransformPose> liveDefaultWeaponPoses = new Dictionary<int, TransformPose>();
    private readonly HashSet<Transform> liveModifiedTargets = new HashSet<Transform>();
    private TransformPose liveDefaultHolderPose;
    private bool liveDefaultsCaptured;

    [MenuItem("Tools/FPS Tutor/TP Weapon Pose Tool")]
    public static void ShowWindow()
    {
        GetWindow<TPWeaponPoseToolWindow>("TP Weapon Pose");
    }

    private void OnEnable()
    {
        PrefabStage.prefabSaving += OnPrefabSaving;
        PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        RefreshContext();
    }

    private void OnDisable()
    {
        PrefabStage.prefabSaving -= OnPrefabSaving;
        PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        RestorePreviewState();
        ClearLiveBinding(false);
        livePlayers.Clear();
        livePlayerLabels.Clear();
    }

    private void OnInspectorUpdate()
    {
        RefreshContext();
        Repaint();
    }

    private void OnGUI()
    {
        RefreshContext();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Remote TP Weapon Pose Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this only for the enemy/world weapon branch under TP WeaponHolder. " +
            "Select Holder to move the shared anchor, or select a weapon to fine-tune that gun.",
            MessageType.Info);

        if (EditorApplication.isPlaying)
        {
            DrawLiveModeUI();
            return;
        }

        DrawPrefabModeUI();
    }

    private void DrawPrefabModeUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Player Prefab", GUILayout.Height(24f)))
                OpenPlayerPrefab();

            using (new EditorGUI.DisabledScope(!HasValidPrefabStage()))
            {
                if (GUILayout.Button("Restore Preview State", GUILayout.Height(24f)))
                    RestorePreviewState();
            }
        }

        if (!HasValidPrefabStage())
        {
            EditorGUILayout.HelpBox(
                "Editing: None\nOpen Assets/Resources/Player.prefab in Prefab Mode to edit the remote TP weapon pose.",
                MessageType.Warning);
            return;
        }

        DrawPrefabStageStatus();
        EditorGUILayout.Space();

        DrawWeaponSelectionPanel("Preview Weapon", true);
        EditorGUILayout.Space();
        DrawTransformPanel();
        EditorGUILayout.Space();
        DrawUtilitiesPanel();
    }

    private void DrawLiveModeUI()
    {
        DrawLivePlayersPanel();
        EditorGUILayout.Space();

        if (!HasValidLiveContext())
        {
            string message = liveBindingLost
                ? "Editing: None\nThe bound remote player is gone or respawned. Refresh the list and bind again."
                : "Editing: None\nJoin a gameplay room, make sure at least one remote player is alive, then bind that player here.";
            EditorGUILayout.HelpBox(message, liveBindingLost ? MessageType.Warning : MessageType.Info);
            return;
        }

        DrawLiveStatus();
        EditorGUILayout.Space();

        DrawWeaponSelectionPanel("Weapon Selection", false);
        EditorGUILayout.Space();
        DrawTransformPanel();
        EditorGUILayout.Space();
        DrawUtilitiesPanel();
        EditorGUILayout.Space();
        DrawLiveApplyPanel();
    }

    private void DrawLivePlayersPanel()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Live Players", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Players"))
                    RefreshLivePlayers();

                using (new EditorGUI.DisabledScope(!CanBindAnyLivePlayer()))
                {
                    if (GUILayout.Button("Bind Selected Player"))
                        BindSelectedLivePlayer();
                }
            }

            if (livePlayers.Count == 0)
            {
                EditorGUILayout.HelpBox("No remote players were found in the current Play Mode session.", MessageType.Warning);
                return;
            }

            selectedLivePlayerIndex = EditorGUILayout.Popup(
                "Remote Player",
                Mathf.Clamp(selectedLivePlayerIndex, 0, Mathf.Max(livePlayers.Count - 1, 0)),
                livePlayerLabels.ToArray());

            EditorGUILayout.HelpBox(
                "Bind Selected Player uses the current Hierarchy selection first. " +
                "If no remote player is selected there, it uses the player chosen in the dropdown.",
                MessageType.None);
        }
    }

    private void DrawLiveStatus()
    {
        string targetName = GetCurrentTarget() != null ? GetCurrentTarget().name : "None";
        string activeWeaponName = GetLiveActiveWeaponName();
        EditorGUILayout.HelpBox(
            $"Editing: Live Remote Player\n" +
            $"Player: {GetBoundLivePlayerLabel()}\n" +
            $"Asset: {boundLiveAssetPath}\n" +
            $"Mode: {GetEditModeLabel()}\n" +
            $"Target: {targetName}\n" +
            $"Active Runtime Weapon: {activeWeaponName}",
            MessageType.None);
    }

    private void DrawLiveApplyPanel()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Live Apply", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Live edits change the running remote player immediately. " +
                "Use Apply To Prefab to bake the current target back into Assets/Resources/Player.prefab.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanApplyCurrentLiveTarget()))
                {
                    if (GUILayout.Button("Apply To Prefab"))
                        ApplyCurrentLiveTargetToPrefab();
                }

                using (new EditorGUI.DisabledScope(!CanRevertCurrentLiveTarget()))
                {
                    if (GUILayout.Button("Revert Live Target"))
                        RevertCurrentLiveTarget();
                }
            }
        }
    }

    private void DrawPrefabStageStatus()
    {
        string targetName = GetCurrentTarget() != null ? GetCurrentTarget().name : "None";
        EditorGUILayout.HelpBox(
            $"Editing: Prefab Asset\n" +
            $"Prefab Stage: {currentPrefabStage.assetPath}\n" +
            $"Mode: {GetEditModeLabel()}\n" +
            $"Target: {targetName}\n" +
            $"Preview: {GetCurrentWeaponName()}",
            MessageType.None);
    }

    private void DrawWeaponSelectionPanel(string title, bool allowPreviewControls)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(GetWeaponTransforms().Count <= 1))
                {
                    if (GUILayout.Button("Prev Weapon"))
                        StepWeapon(-1);
                }

                EditorGUILayout.LabelField(GetCurrentWeaponName(), EditorStyles.centeredGreyMiniLabel);

                using (new EditorGUI.DisabledScope(GetWeaponTransforms().Count <= 1))
                {
                    if (GUILayout.Button("Next Weapon"))
                        StepWeapon(1);
                }
            }

            DrawWeaponSelectionButtons();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Holder"))
                    SelectHolder();

                using (new EditorGUI.DisabledScope(!HasCurrentWeapon()))
                {
                    if (GUILayout.Button(allowPreviewControls ? "Select Preview Weapon" : "Select Current Weapon"))
                        SelectCurrentWeapon();
                }
            }
        }
    }

    private void DrawWeaponSelectionButtons()
    {
        List<Transform> weaponTransforms = GetWeaponTransforms();
        if (weaponTransforms.Count == 0)
        {
            EditorGUILayout.HelpBox("No direct child weapons were found under TP WeaponHolder.", MessageType.Warning);
            return;
        }

        int columns = Mathf.Clamp(weaponTransforms.Count, 1, 4);
        int rows = Mathf.CeilToInt(weaponTransforms.Count / (float)columns);
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int column = 0; column < columns && index < weaponTransforms.Count; column++, index++)
                {
                    bool selected = !editHolder && index == currentWeaponIndex;
                    GUIStyle style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;

                    if (GUILayout.Button(weaponTransforms[index].name, style))
                        SelectWeapon(index);
                }
            }
        }
    }

    private void DrawTransformPanel()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);

            Transform target = GetCurrentTarget();
            if (target == null)
            {
                EditorGUILayout.HelpBox("No valid target is selected.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Editing", target.name);
            if (EditorApplication.isPlaying && !target.gameObject.activeInHierarchy)
            {
                EditorGUILayout.HelpBox(
                    "This weapon is inactive in the running enemy right now. " +
                    "You can still type numbers here, but Scene handles may not be visible until that weapon becomes active.",
                    MessageType.Info);
            }

            DrawTransformFields(target);
        }
    }

    private void DrawUtilitiesPanel()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(GetCurrentTarget() == null))
                {
                    if (GUILayout.Button("Frame Selected"))
                        SceneView.FrameLastActiveSceneView();

                    if (GUILayout.Button("Copy Current Transform"))
                        CopyCurrentTransform();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(GetHolderTransform() == null))
                {
                    if (GUILayout.Button("Reset Holder"))
                        ResetHolderPose();
                }

                using (new EditorGUI.DisabledScope(!HasCurrentWeapon()))
                {
                    if (GUILayout.Button("Reset Current Weapon"))
                        ResetCurrentWeaponPose();
                }
            }
        }
    }

    private void DrawTransformFields(Transform target)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 localPosition = EditorGUILayout.Vector3Field("Local Position", target.localPosition);
        Vector3 localEulerAngles = EditorGUILayout.Vector3Field("Local Rotation", target.localEulerAngles);
        Vector3 localScale = EditorGUILayout.Vector3Field("Local Scale", target.localScale);

        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(target, $"Adjust {target.name} pose");
        target.localPosition = localPosition;
        target.localEulerAngles = localEulerAngles;
        target.localScale = localScale;
        MarkTargetDirty(target);
    }

    private void OpenPlayerPrefab()
    {
        PrefabStageUtility.OpenPrefab(PlayerPrefabPath);
        Focus();
    }

    private void SelectHolder()
    {
        Transform holderTransform = GetHolderTransform();
        if (holderTransform == null)
            return;

        editHolder = true;
        EnsurePreviewVisible();
        SelectTarget(holderTransform);
    }

    private void SelectCurrentWeapon()
    {
        if (!HasCurrentWeapon())
            return;

        editHolder = false;
        EnsurePreviewVisible();
        SelectTarget(GetWeaponTransforms()[currentWeaponIndex]);
    }

    private void SelectWeapon(int index)
    {
        if (!HasCurrentWeapon(index))
            return;

        currentWeaponIndex = index;
        editHolder = false;
        EnsurePreviewVisible();
        SelectTarget(GetWeaponTransforms()[currentWeaponIndex]);
    }

    private void StepWeapon(int delta)
    {
        List<Transform> weaponTransforms = GetWeaponTransforms();
        if (weaponTransforms.Count == 0)
            return;

        currentWeaponIndex = WrapIndex(currentWeaponIndex + delta, weaponTransforms.Count);
        EnsurePreviewVisible();

        if (editHolder)
            SelectTarget(GetHolderTransform());
        else
            SelectTarget(weaponTransforms[currentWeaponIndex]);
    }

    private void SelectTarget(Transform target)
    {
        if (target == null)
            return;

        Selection.activeTransform = target;
        EditorGUIUtility.PingObject(target);
    }

    private void CopyCurrentTransform()
    {
        Transform target = GetCurrentTarget();
        if (target == null)
            return;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(target.name);
        builder.Append("Local Position: ");
        AppendVector(builder, target.localPosition);
        builder.AppendLine();
        builder.Append("Local Rotation: ");
        AppendVector(builder, target.localEulerAngles);
        builder.AppendLine();
        builder.Append("Local Scale: ");
        AppendVector(builder, target.localScale);

        EditorGUIUtility.systemCopyBuffer = builder.ToString();
        Debug.Log($"Copied pose for {target.name} to clipboard.");
    }

    private void ResetHolderPose()
    {
        Transform holderTransform = GetHolderTransform();
        if (holderTransform == null)
            return;

        if (EditorApplication.isPlaying)
        {
            if (!liveDefaultsCaptured)
                return;

            ApplyPose(holderTransform, liveDefaultHolderPose, "Reset TP WeaponHolder pose");
            return;
        }

        if (!prefabDefaultsCaptured)
            return;

        ApplyPose(holderTransform, prefabDefaultHolderPose, "Reset TP WeaponHolder pose");
    }

    private void ResetCurrentWeaponPose()
    {
        if (!HasCurrentWeapon())
            return;

        Transform weaponTransform = GetWeaponTransforms()[currentWeaponIndex];
        TransformPose pose;

        if (EditorApplication.isPlaying)
        {
            if (!liveDefaultWeaponPoses.TryGetValue(currentWeaponIndex, out pose))
                return;
        }
        else
        {
            if (!prefabDefaultWeaponPoses.TryGetValue(currentWeaponIndex, out pose))
                return;
        }

        ApplyPose(weaponTransform, pose, $"Reset {weaponTransform.name} pose");
    }

    private void ApplyPose(Transform target, TransformPose pose, string undoName)
    {
        Undo.RecordObject(target, undoName);
        target.localPosition = pose.LocalPosition;
        target.localEulerAngles = pose.LocalEulerAngles;
        target.localScale = pose.LocalScale;
        MarkTargetDirty(target);
    }

    private void ApplyCurrentLiveTargetToPrefab()
    {
        Transform target = GetCurrentTarget();
        if (!CanApplyCurrentLiveTarget() || target == null)
            return;

        if (!TryApplyLiveTargetToPrefab(target))
            return;

        UpdateLiveDefaultPose(target);
        liveModifiedTargets.Remove(target);

        Debug.Log($"Applied live TP weapon pose from {target.name} to {PlayerPrefabPath}.");
    }

    private void RevertCurrentLiveTarget()
    {
        Transform target = GetCurrentTarget();
        if (!CanRevertCurrentLiveTarget() || target == null)
            return;

        if (!TryRevertLiveTargetFromPrefab(target))
            return;

        UpdateLiveDefaultPose(target);
        liveModifiedTargets.Remove(target);

        Debug.Log($"Reverted live TP weapon pose overrides on {target.name}.");
    }

    private void RefreshContext()
    {
        if (EditorApplication.isPlaying)
        {
            RefreshLivePlayers();
            RefreshBoundLiveContext();
            SyncModeFromSelection();
            return;
        }

        RefreshPrefabContext();
        SyncModeFromSelection();
    }

    private void RefreshPrefabContext()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (!IsPlayerPrefabStage(stage))
        {
            if (currentPrefabStage != null && currentPrefabStage != stage)
                RestorePreviewState();

            ClearPrefabResolvedContext();
            currentPrefabStage = stage;
            return;
        }

        bool stageChanged = currentPrefabStage != stage;
        currentPrefabStage = stage;

        if (stageChanged || prefabHolderTransform == null || !prefabHolderTransform)
            ResolvePrefabStage();
    }

    private void ResolvePrefabStage()
    {
        ClearPrefabResolvedContext();

        if (currentPrefabStage == null || !IsPlayerPrefabStage(currentPrefabStage) || currentPrefabStage.prefabContentsRoot == null)
            return;

        prefabHolderTransform = FindHolderTransform(currentPrefabStage.prefabContentsRoot.transform);
        if (prefabHolderTransform == null)
            return;

        prefabWeaponTransforms.AddRange(prefabHolderTransform.Cast<Transform>());
        currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, Mathf.Max(prefabWeaponTransforms.Count - 1, 0));

        CapturePrefabDefaults();
        EnsurePreviewVisible();
    }

    private void CapturePrefabDefaults()
    {
        if (prefabHolderTransform == null)
            return;

        prefabDefaultsCaptured = true;
        previewMutatedStage = false;
        prefabPoseChangesMade = false;
        stageWasDirtyAtCapture = currentPrefabStage != null && currentPrefabStage.scene.isDirty;

        prefabDefaultHolderPose = TransformPose.From(prefabHolderTransform);
        prefabDefaultActiveStates.Clear();
        prefabDefaultWeaponPoses.Clear();

        for (int index = 0; index < prefabWeaponTransforms.Count; index++)
        {
            Transform weapon = prefabWeaponTransforms[index];
            prefabDefaultActiveStates[index] = weapon.gameObject.activeSelf;
            prefabDefaultWeaponPoses[index] = TransformPose.From(weapon);
        }
    }

    private void EnsurePreviewVisible()
    {
        if (EditorApplication.isPlaying || !prefabDefaultsCaptured || prefabWeaponTransforms.Count == 0)
            return;

        currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, prefabWeaponTransforms.Count - 1);

        for (int index = 0; index < prefabWeaponTransforms.Count; index++)
        {
            bool shouldBeActive = index == currentWeaponIndex;
            GameObject weaponObject = prefabWeaponTransforms[index].gameObject;
            if (weaponObject.activeSelf == shouldBeActive)
                continue;

            weaponObject.SetActive(shouldBeActive);
            previewMutatedStage = true;
        }
    }

    private void RestorePreviewState()
    {
        if (!prefabDefaultsCaptured)
            return;

        bool restoredAnyState = false;
        foreach (KeyValuePair<int, bool> pair in prefabDefaultActiveStates)
        {
            if (!HasPrefabWeapon(pair.Key))
                continue;

            GameObject weaponObject = prefabWeaponTransforms[pair.Key].gameObject;
            if (weaponObject.activeSelf == pair.Value)
                continue;

            weaponObject.SetActive(pair.Value);
            restoredAnyState = true;
        }

        if ((previewMutatedStage || restoredAnyState) && !prefabPoseChangesMade && !stageWasDirtyAtCapture && currentPrefabStage != null)
            currentPrefabStage.ClearDirtiness();

        previewMutatedStage = false;
    }

    private void ClearPrefabResolvedContext()
    {
        prefabHolderTransform = null;
        prefabWeaponTransforms.Clear();
        prefabDefaultActiveStates.Clear();
        prefabDefaultWeaponPoses.Clear();
        prefabDefaultsCaptured = false;
        previewMutatedStage = false;
        prefabPoseChangesMade = false;
    }

    private void RefreshLivePlayers()
    {
        livePlayers.Clear();
        livePlayerLabels.Clear();

        List<PlayerSetup> discoveredPlayers = Object.FindObjectsByType<PlayerSetup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(IsValidRemotePlayer)
            .OrderBy(GetLivePlayerSortKey)
            .ToList();

        livePlayers.AddRange(discoveredPlayers);
        livePlayerLabels.AddRange(livePlayers.Select(BuildLivePlayerLabel));
        selectedLivePlayerIndex = Mathf.Clamp(selectedLivePlayerIndex, 0, Mathf.Max(livePlayers.Count - 1, 0));
    }

    private void RefreshBoundLiveContext()
    {
        if (boundLivePlayer == null)
            return;

        if (!IsValidRemotePlayer(boundLivePlayer))
        {
            ClearLiveBinding(true);
            return;
        }

        Transform holderTransform = boundLivePlayer.TPweaponHolder != null
            ? boundLivePlayer.TPweaponHolder
            : FindHolderTransform(boundLivePlayer.transform);
        if (holderTransform == null)
        {
            ClearLiveBinding(true);
            return;
        }

        if (liveHolderTransform != holderTransform)
            BindLivePlayer(boundLivePlayer);
    }

    private void BindSelectedLivePlayer()
    {
        PlayerSetup selectedPlayer = GetSelectedRemotePlayerFromHierarchy();
        if (selectedPlayer == null && selectedLivePlayerIndex >= 0 && selectedLivePlayerIndex < livePlayers.Count)
            selectedPlayer = livePlayers[selectedLivePlayerIndex];

        if (selectedPlayer == null)
            return;

        int discoveredIndex = livePlayers.IndexOf(selectedPlayer);
        if (discoveredIndex >= 0)
            selectedLivePlayerIndex = discoveredIndex;

        BindLivePlayer(selectedPlayer);
    }

    private void BindLivePlayer(PlayerSetup player)
    {
        if (!IsValidRemotePlayer(player))
            return;

        ClearLiveBinding(false);

        boundLivePlayer = player;
        liveHolderTransform = player.TPweaponHolder != null ? player.TPweaponHolder : FindHolderTransform(player.transform);
        boundLiveAssetPath = ResolveAssetPath(liveHolderTransform != null ? liveHolderTransform.gameObject : player.gameObject);
        if (liveHolderTransform == null)
        {
            ClearLiveBinding(true);
            return;
        }

        liveWeaponTransforms.AddRange(liveHolderTransform.Cast<Transform>());
        currentWeaponIndex = GetFirstActiveWeaponIndex(liveWeaponTransforms);
        if (currentWeaponIndex < 0)
            currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, Mathf.Max(liveWeaponTransforms.Count - 1, 0));

        editHolder = false;
        liveBindingLost = false;
        CaptureLiveDefaults();
        SelectTarget(GetCurrentTarget());
    }

    private void CaptureLiveDefaults()
    {
        if (liveHolderTransform == null)
            return;

        liveDefaultsCaptured = true;
        liveModifiedTargets.Clear();
        liveDefaultHolderPose = TransformPose.From(liveHolderTransform);
        liveDefaultWeaponPoses.Clear();

        for (int index = 0; index < liveWeaponTransforms.Count; index++)
            liveDefaultWeaponPoses[index] = TransformPose.From(liveWeaponTransforms[index]);
    }

    private void ClearLiveBinding(bool bindingLost)
    {
        boundLivePlayer = null;
        boundLiveAssetPath = null;
        liveHolderTransform = null;
        liveWeaponTransforms.Clear();
        liveDefaultWeaponPoses.Clear();
        liveModifiedTargets.Clear();
        liveDefaultsCaptured = false;
        liveBindingLost = bindingLost;
    }

    private void UpdateLiveDefaultPose(Transform target)
    {
        if (target == null)
            return;

        if (target == liveHolderTransform)
        {
            liveDefaultHolderPose = TransformPose.From(target);
            return;
        }

        int weaponIndex = liveWeaponTransforms.IndexOf(target);
        if (weaponIndex >= 0)
            liveDefaultWeaponPoses[weaponIndex] = TransformPose.From(target);
    }

    private void MarkTargetDirty(Transform target)
    {
        EditorUtility.SetDirty(target);

        if (EditorApplication.isPlaying)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            liveModifiedTargets.Add(target);
            return;
        }

        prefabPoseChangesMade = true;
    }

    private void SyncModeFromSelection()
    {
        Transform selected = Selection.activeTransform;
        if (selected == null)
            return;

        Transform holderTransform = GetHolderTransform();
        if (selected == holderTransform)
        {
            editHolder = true;
            return;
        }

        int weaponIndex = GetWeaponTransforms().IndexOf(selected);
        if (weaponIndex >= 0)
        {
            editHolder = false;
            currentWeaponIndex = weaponIndex;
            EnsurePreviewVisible();
        }
    }

    private void OnSelectionChanged()
    {
        SyncModeFromSelection();
        Repaint();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                RestorePreviewState();
                RefreshLivePlayers();
                break;
            case PlayModeStateChange.ExitingPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                ClearLiveBinding(false);
                livePlayers.Clear();
                livePlayerLabels.Clear();
                break;
        }

        Repaint();
    }

    private void OnPrefabSaving(GameObject prefabRoot)
    {
        if (!HasValidPrefabStage() || currentPrefabStage.prefabContentsRoot != prefabRoot)
            return;

        RestorePreviewState();
    }

    private void OnPrefabStageClosing(PrefabStage stage)
    {
        if (stage != currentPrefabStage)
            return;

        RestorePreviewState();
    }

    private Transform GetCurrentTarget()
    {
        if (editHolder)
            return GetHolderTransform();

        return HasCurrentWeapon() ? GetWeaponTransforms()[currentWeaponIndex] : null;
    }

    private string GetCurrentWeaponName()
    {
        return HasCurrentWeapon() ? GetWeaponTransforms()[currentWeaponIndex].name : "None";
    }

    private string GetEditModeLabel()
    {
        return editHolder ? "Holder" : "Weapon";
    }

    private string GetLiveActiveWeaponName()
    {
        int activeIndex = GetFirstActiveWeaponIndex(liveWeaponTransforms);
        return activeIndex >= 0 ? liveWeaponTransforms[activeIndex].name : "None";
    }

    private string GetBoundLivePlayerLabel()
    {
        return boundLivePlayer != null ? BuildLivePlayerLabel(boundLivePlayer) : "None";
    }

    private bool HasCurrentWeapon()
    {
        return HasCurrentWeapon(currentWeaponIndex);
    }

    private bool HasCurrentWeapon(int index)
    {
        List<Transform> weaponTransforms = GetWeaponTransforms();
        return index >= 0 && index < weaponTransforms.Count && weaponTransforms[index] != null;
    }

    private bool HasPrefabWeapon(int index)
    {
        return index >= 0 && index < prefabWeaponTransforms.Count && prefabWeaponTransforms[index] != null;
    }

    private bool HasValidPrefabStage()
    {
        return prefabHolderTransform != null && currentPrefabStage != null && IsPlayerPrefabStage(currentPrefabStage);
    }

    private bool HasValidLiveContext()
    {
        return boundLivePlayer != null &&
               liveHolderTransform != null &&
               liveWeaponTransforms.All(weapon => weapon != null);
    }

    private bool CanBindAnyLivePlayer()
    {
        return GetSelectedRemotePlayerFromHierarchy() != null || livePlayers.Count > 0;
    }

    private bool CanApplyCurrentLiveTarget()
    {
        Transform target = GetCurrentTarget();
        return HasValidLiveContext() &&
               target != null &&
               liveModifiedTargets.Contains(target);
    }

    private bool CanRevertCurrentLiveTarget()
    {
        Transform target = GetCurrentTarget();
        return HasValidLiveContext() &&
               target != null;
    }

    private Transform GetHolderTransform()
    {
        return EditorApplication.isPlaying ? liveHolderTransform : prefabHolderTransform;
    }

    private List<Transform> GetWeaponTransforms()
    {
        return EditorApplication.isPlaying ? liveWeaponTransforms : prefabWeaponTransforms;
    }

    private PlayerSetup GetSelectedRemotePlayerFromHierarchy()
    {
        Transform selectedTransform = Selection.activeTransform;
        if (selectedTransform == null)
            return null;

        PlayerSetup selectedPlayer = selectedTransform.GetComponentInParent<PlayerSetup>();
        return IsValidRemotePlayer(selectedPlayer) ? selectedPlayer : null;
    }

    private static bool IsPlayerPrefabStage(PrefabStage stage)
    {
        return stage != null && stage.assetPath == PlayerPrefabPath;
    }

    private static bool IsValidRemotePlayer(PlayerSetup player)
    {
        return player != null &&
               player.gameObject.scene.IsValid() &&
               player.gameObject.activeInHierarchy &&
               player.photonView != null &&
               !player.photonView.IsMine;
    }

    private static string GetLivePlayerSortKey(PlayerSetup player)
    {
        return BuildLivePlayerLabel(player);
    }

    private static string BuildLivePlayerLabel(PlayerSetup player)
    {
        if (player == null)
            return "Missing Player";

        string displayName = !string.IsNullOrWhiteSpace(player.nickname)
            ? player.nickname
            : player.photonView != null && player.photonView.Owner != null
                ? $"Actor {player.photonView.Owner.ActorNumber}"
                : player.gameObject.name;

        return $"{displayName} [{player.gameObject.name}]";
    }

    private static Transform FindHolderTransform(Transform root)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == HolderName);
    }

    private static string ResolveAssetPath(Object instanceObject)
    {
        if (instanceObject == null)
            return null;

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceObject);
        if (!string.IsNullOrEmpty(assetPath))
            return assetPath;

        Object sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
        return sourceObject != null ? AssetDatabase.GetAssetPath(sourceObject) : null;
    }

    private bool TryApplyLiveTargetToPrefab(Transform liveTarget)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform assetTarget = ResolveAssetTarget(prefabRoot.transform, liveTarget);
            if (assetTarget == null)
            {
                Debug.LogError("TP Weapon Pose Tool could not resolve the matching target inside Player.prefab.");
                return false;
            }

            assetTarget.localPosition = liveTarget.localPosition;
            assetTarget.localEulerAngles = liveTarget.localEulerAngles;
            assetTarget.localScale = liveTarget.localScale;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("TP Weapon Pose Tool failed to save the updated pose into Player.prefab.");
                return false;
            }

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private bool TryRevertLiveTargetFromPrefab(Transform liveTarget)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform assetTarget = ResolveAssetTarget(prefabRoot.transform, liveTarget);
            if (assetTarget == null)
            {
                Debug.LogError("TP Weapon Pose Tool could not resolve the matching prefab target for revert.");
                return false;
            }

            Undo.RecordObject(liveTarget, $"Revert {liveTarget.name} live pose");
            liveTarget.localPosition = assetTarget.localPosition;
            liveTarget.localEulerAngles = assetTarget.localEulerAngles;
            liveTarget.localScale = assetTarget.localScale;
            EditorUtility.SetDirty(liveTarget);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private Transform ResolveAssetTarget(Transform prefabRoot, Transform liveTarget)
    {
        if (prefabRoot == null || liveTarget == null)
            return null;

        Transform assetHolder = FindHolderTransform(prefabRoot);
        if (assetHolder == null)
            return null;

        if (liveTarget == liveHolderTransform)
            return assetHolder;

        int weaponIndex = liveWeaponTransforms.IndexOf(liveTarget);
        if (weaponIndex < 0 || weaponIndex >= assetHolder.childCount)
            return null;

        return assetHolder.GetChild(weaponIndex);
    }

    private static int GetFirstActiveWeaponIndex(List<Transform> weaponTransforms)
    {
        for (int index = 0; index < weaponTransforms.Count; index++)
        {
            if (weaponTransforms[index] != null && weaponTransforms[index].gameObject.activeSelf)
                return index;
        }

        return -1;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        while (value < 0)
            value += count;

        return value % count;
    }

    private static void AppendVector(StringBuilder builder, Vector3 value)
    {
        builder.Append('(')
            .Append(value.x.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(", ")
            .Append(value.y.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(", ")
            .Append(value.z.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(')');
    }

    private readonly struct TransformPose
    {
        public TransformPose(Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
        }

        public Vector3 LocalPosition { get; }
        public Vector3 LocalEulerAngles { get; }
        public Vector3 LocalScale { get; }

        public static TransformPose From(Transform target)
        {
            return new TransformPose(target.localPosition, target.localEulerAngles, target.localScale);
        }
    }
}
