using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class UIToolkitMenuController : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;

    [Header("Audio")]
    public AudioSource hoverSound;
    public AudioSource clickSound;
    public AudioSource sliderSound;
    public AudioSource swooshSound;

    [Header("PlayFab Controllers")]
    public PlayFabLogin playFabLogin;
    public PlayFabRegister playFabRegister;

    private VisualElement root;
    private VisualElement shellLogin;
    private VisualElement shellRegister;
    private VisualElement shellMain;

    private VisualElement screenPlay;
    private VisualElement screenShop;
    private VisualElement screenInventory;
    private VisualElement screenAccount;
    private VisualElement screenSettings;
    private VisualElement screenExit;
    private VisualElement screenLoading;

    private VisualElement panelGame;
    private VisualElement panelVideo;
    private VisualElement panelControls;
    private VisualElement panelKeyBindings;

    private Label sidebarCoins;
    private Label shopCoins;
    private Label sidebarPlayerName;
    private ProgressBar loadingProgress;
    private Label loadingPrompt;
    private VisualElement loadingPanel;

    private readonly List<VisualElement> shellLayers = new List<VisualElement>();
    private readonly List<VisualElement> primaryScreens = new List<VisualElement>();
    private readonly List<VisualElement> overlayScreens = new List<VisualElement>();
    private readonly List<VisualElement> settingsPanels = new List<VisualElement>();
    private readonly List<VisualElement> keybindingPanels = new List<VisualElement>();
    private readonly List<VisualElement> reticleElements = new List<VisualElement>();
    private readonly Dictionary<VisualElement, int> motionTickets = new Dictionary<VisualElement, int>();

    private static readonly string[] NavIds =
    {
        "nav-play",
        "nav-shop",
        "nav-inventory",
        "nav-account",
        "nav-settings"
    };

    private static readonly string[] SettingsTabIds =
    {
        "tab-game",
        "tab-video",
        "tab-controls",
        "tab-keybindings"
    };

    private static readonly string[] KeybindTabIds =
    {
        "tab-kb-movement",
        "tab-kb-combat",
        "tab-kb-general"
    };

    private const int MotionWarmupMs = 18;
    private const int MotionFadeMs = 260;
    private const int ReticleMotionIntervalMs = 6800;
    private const int LoadingPulseIntervalMs = 900;

    private Animator cameraAnimator;
    private int coinCount;
    private bool callbacksRegistered;
    private bool reticleMotionAlt;
    private bool loadingReadyState;
    private VisualElement currentPrimaryScreen;
    private VisualElement currentOverlayScreen;
    private VisualElement currentSettingsPanel;
    private VisualElement currentKeybindingPanel;
    private IVisualElementScheduledItem reticleMotionItem;
    private IVisualElementScheduledItem loadingPulseItem;

    private static readonly string[] ShopItemNames = { "Cartoon Pack", "Sci-Fi Pack", "Epic Bundle" };
    private static readonly int[] ShopItemPrices = { 50, 100, 200 };
    private static readonly string[] ShopItemTooltips =
    {
        "Colorful cartoon weapon skins. Great for a fun look!",
        "Sleek sci-fi weapon skins. Look futuristic!",
        "Exclusive bundle with all premium skins included!"
    };

    private static readonly Dictionary<string, string> MenuIconResources = new Dictionary<string, string>()
    {
            { "icon-auth-login", "Icons/Menu/user-circle" },
        { "icon-auth-register", "Icons/Menu/user-plus" },
        { "icon-currency", "Icons/Menu/coins" },
        { "icon-nav-play", "Icons/Menu/player-play" },
        { "icon-nav-shop", "Icons/Menu/shopping-bag" },
        { "icon-nav-inventory", "Icons/Menu/luggage" },
        { "icon-nav-account", "Icons/Menu/user-circle" },
        { "icon-nav-settings", "Icons/Menu/settings" },
        { "icon-nav-exit", "Icons/Menu/logout" },
        { "icon-screen-play", "Icons/Menu/player-play" },
        { "icon-screen-shop", "Icons/Menu/shopping-bag" },
        { "icon-screen-inventory", "Icons/Menu/luggage" },
        { "icon-screen-account", "Icons/Menu/user-circle" },
        { "icon-screen-settings", "Icons/Menu/settings" },
        { "icon-hero-play", "Icons/Menu/target-arrow" }
    };

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            return;
        }

        root = uiDocument.rootVisualElement;

        var cam = Camera.main;
        if (cam != null)
        {
            cameraAnimator = cam.GetComponent<Animator>();
        }

        QueryElements();
        ApplyMenuIcons();
        InitializeMotionState();
        RegisterCallbacks();

        if (playFabLogin != null)
        {
            playFabLogin.Initialize(root, this);
        }

        if (playFabRegister != null)
        {
            playFabRegister.Initialize(root, this);
        }

        var mainAudio = cam != null ? cam.GetComponent<AudioSource>() : null;
        if (mainAudio != null && PlayerPrefs.HasKey("MusicVolume"))
        {
            mainAudio.volume = PlayerPrefs.GetFloat("MusicVolume");
        }

        coinCount = PlayerPrefs.GetInt("CoinCount", 1000);
        RefreshCoinDisplays();

        MigratePlayerPrefs();
        ShowLogin();
        LoadSettingsIntoUI();
        playFabLogin?.TryAutoLogin();
    }

    private void OnDisable()
    {
        StopAmbientMotion();
        StopLoadingPulse();
        UnregisterCallbacks();

        if (playFabLogin != null)
        {
            playFabLogin.Deinitialize();
        }

        if (playFabRegister != null)
        {
            playFabRegister.Deinitialize();
        }

        root = null;
    }

    private void QueryElements()
    {
        shellLogin = root.Q("login-screen");
        shellRegister = root.Q("register-screen");
        shellMain = root.Q("main-shell");

        screenPlay = root.Q("play-screen");
        screenShop = root.Q("shop-screen");
        screenInventory = root.Q("inventory-screen");
        screenAccount = root.Q("account-screen");
        screenSettings = root.Q("settings-screen");
        screenExit = root.Q("exit-screen");
        screenLoading = root.Q("loading-screen");

        panelGame = root.Q("panel-game");
        panelVideo = root.Q("panel-video");
        panelControls = root.Q("panel-controls");
        panelKeyBindings = root.Q("panel-keybindings");

        sidebarCoins = root.Q<Label>("sidebar-coins");
        shopCoins = root.Q<Label>("lbl-shop-coins");
        sidebarPlayerName = root.Q<Label>("sidebar-player-name");
        loadingProgress = root.Q<ProgressBar>("loading-progress");
        loadingPrompt = root.Q<Label>("loading-prompt");
        loadingPanel = root.Q(className: "loading-panel");

        var topOverlay = root.Q("top-overlay");
        if (topOverlay != null)
        {
            topOverlay.pickingMode = PickingMode.Ignore;
        }

        shellLayers.Clear();
        AddIfPresent(shellLayers, shellLogin);
        AddIfPresent(shellLayers, shellRegister);
        AddIfPresent(shellLayers, shellMain);

        primaryScreens.Clear();
        AddIfPresent(primaryScreens, screenPlay);
        AddIfPresent(primaryScreens, screenShop);
        AddIfPresent(primaryScreens, screenInventory);
        AddIfPresent(primaryScreens, screenAccount);
        AddIfPresent(primaryScreens, screenSettings);

        overlayScreens.Clear();
        AddIfPresent(overlayScreens, screenExit);
        AddIfPresent(overlayScreens, screenLoading);

        settingsPanels.Clear();
        AddIfPresent(settingsPanels, panelGame);
        AddIfPresent(settingsPanels, panelVideo);
        AddIfPresent(settingsPanels, panelControls);
        AddIfPresent(settingsPanels, panelKeyBindings);

        keybindingPanels.Clear();
        AddIfPresent(keybindingPanels, root.Q("panel-kb-movement"));
        AddIfPresent(keybindingPanels, root.Q("panel-kb-combat"));
        AddIfPresent(keybindingPanels, root.Q("panel-kb-general"));

        reticleElements.Clear();
        root.Query<VisualElement>(className: "range-reticle").ForEach(element => reticleElements.Add(element));
    }

    private static void AddIfPresent(List<VisualElement> elements, VisualElement element)
    {
        if (element != null)
        {
            elements.Add(element);
        }
    }

    private void ApplyMenuIcons()
    {
        if (root == null)
        {
            return;
        }

        foreach (var iconEntry in MenuIconResources)
        {
            var iconElement = root.Q<VisualElement>(iconEntry.Key);
            if (iconElement == null)
            {
                continue;
            }

            var vectorImage = Resources.Load<VectorImage>(iconEntry.Value);
            if (vectorImage == null)
            {
                continue;
            }

            iconElement.style.backgroundImage = new StyleBackground(vectorImage);
            iconElement.pickingMode = PickingMode.Ignore;
        }
    }

    private void InitializeMotionState()
    {
        StopAmbientMotion();
        StopLoadingPulse();

        foreach (var shell in shellLayers)
        {
            SetHiddenImmediate(shell);
        }

        foreach (var screen in primaryScreens)
        {
            SetHiddenImmediate(screen);
        }

        foreach (var overlay in overlayScreens)
        {
            SetHiddenImmediate(overlay);
        }

        foreach (var panel in settingsPanels)
        {
            SetHiddenImmediate(panel);
        }

        foreach (var panel in keybindingPanels)
        {
            SetHiddenImmediate(panel);
        }

        currentPrimaryScreen = screenPlay;
        currentOverlayScreen = null;
        currentSettingsPanel = panelGame;
        currentKeybindingPanel = keybindingPanels.Count > 0 ? keybindingPanels[0] : null;

        SetVisibleImmediate(screenPlay);
        SetVisibleImmediate(panelGame);
        SetVisibleImmediate(currentKeybindingPanel);
        SetActiveButtons(SettingsTabIds, "tab-game");
        SetActiveButtons(KeybindTabIds, "tab-kb-movement");

        ResetLoadingState();
        StartAmbientMotion();
    }

    private void RegisterCallbacks()
    {
        if (root == null || callbacksRegistered)
        {
            return;
        }

        RegisterButton("btn-login", OnLoginButtonClicked);
        RegisterButton("btn-goto-register", OnGotoRegisterClicked);
        RegisterButton("btn-back-to-login", OnBackToLoginClicked);

        RegisterButton("nav-play", OnNavPlayClicked);
        RegisterButton("nav-shop", OnNavShopClicked);
        RegisterButton("nav-inventory", OnNavInventoryClicked);
        RegisterButton("nav-account", OnNavAccountClicked);
        RegisterButton("nav-settings", OnNavSettingsClicked);
        RegisterButton("nav-exit", OnNavExitClicked);

        RegisterButton("btn-mode-campaign", OnCampaignClicked);
        RegisterButton("btn-mode-survival", OnSurvivalClicked);
        RegisterButton("btn-exit-yes", OnExitYesClicked);
        RegisterButton("btn-exit-no", OnExitNoClicked);
        RegisterButton("btn-logout", OnLogoutClicked);

        RegisterButton("btn-social-discord", OnDiscordClicked);
        RegisterButton("btn-social-twitter", OnTwitterClicked);
        RegisterButton("btn-social-web", OnWebClicked);

        RegisterButton("tab-game", OnGameTabClicked);
        RegisterButton("tab-video", OnVideoTabClicked);
        RegisterButton("tab-controls", OnControlsTabClicked);
        RegisterButton("tab-keybindings", OnKeybindingsTabClicked);
        RegisterButton("tab-kb-movement", OnMovementKbTabClicked);
        RegisterButton("tab-kb-combat", OnCombatKbTabClicked);
        RegisterButton("tab-kb-general", OnGeneralKbTabClicked);

        RegisterButton("btn-diff-normal", OnDiffNormalClicked);
        RegisterButton("btn-diff-hard", OnDiffHardClicked);
        RegisterButton("btn-tex-low", OnTextureLowClicked);
        RegisterButton("btn-tex-med", OnTextureMediumClicked);
        RegisterButton("btn-tex-high", OnTextureHighClicked);
        RegisterButton("btn-shadow-off", OnShadowOffClicked);
        RegisterButton("btn-shadow-low", OnShadowLowClicked);
        RegisterButton("btn-shadow-high", OnShadowHighClicked);
        RegisterButton("btn-aa-off", OnAaOffClicked);
        RegisterButton("btn-aa-2x", OnAa2xClicked);
        RegisterButton("btn-aa-4x", OnAa4xClicked);
        RegisterButton("btn-aa-8x", OnAa8xClicked);

        RegisterToggle("toggle-hud", OnHudToggleChanged);
        RegisterToggle("toggle-tooltips", OnTooltipsToggleChanged);
        RegisterToggle("toggle-fullscreen", OnFullscreenToggleChanged);
        RegisterToggle("toggle-vsync", OnVsyncToggleChanged);
        RegisterToggle("toggle-motionblur", OnMotionBlurToggleChanged);
        RegisterToggle("toggle-ao", OnAoToggleChanged);
        RegisterToggle("toggle-cameraeffects", OnCameraEffectsToggleChanged);
        RegisterToggle("toggle-invert", OnInvertToggleChanged);

        RegisterSlider("slider-music", OnMusicSliderChanged);
        RegisterSlider("slider-sens-x", OnSensitivityXChanged);
        RegisterSlider("slider-sens-y", OnSensitivityYChanged);
        RegisterSlider("slider-smooth", OnSmoothSliderChanged);
        RegisterDropdown("dropdown-control-scheme", OnControlSchemeChanged);

        for (int i = 0; i < ShopItemNames.Length; i++)
        {
            var item = root.Q("shop-item-" + i);
            if (item != null)
            {
                item.RegisterCallback<PointerOverEvent>(OnShopItemPointerOver);
                item.RegisterCallback<PointerOutEvent>(OnShopItemPointerOut);
            }

            RegisterButton("btn-buy-item-" + i, OnBuyItemClicked);
        }

        root.Query<Button>().ForEach(button =>
        {
            button.RegisterCallback<PointerOverEvent>(OnAnyButtonPointerOver);
        });

        callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (root == null || !callbacksRegistered)
        {
            return;
        }

        UnregisterButton("btn-login", OnLoginButtonClicked);
        UnregisterButton("btn-goto-register", OnGotoRegisterClicked);
        UnregisterButton("btn-back-to-login", OnBackToLoginClicked);

        UnregisterButton("nav-play", OnNavPlayClicked);
        UnregisterButton("nav-shop", OnNavShopClicked);
        UnregisterButton("nav-inventory", OnNavInventoryClicked);
        UnregisterButton("nav-account", OnNavAccountClicked);
        UnregisterButton("nav-settings", OnNavSettingsClicked);
        UnregisterButton("nav-exit", OnNavExitClicked);

        UnregisterButton("btn-mode-campaign", OnCampaignClicked);
        UnregisterButton("btn-mode-survival", OnSurvivalClicked);
        UnregisterButton("btn-exit-yes", OnExitYesClicked);
        UnregisterButton("btn-exit-no", OnExitNoClicked);
        UnregisterButton("btn-logout", OnLogoutClicked);

        UnregisterButton("btn-social-discord", OnDiscordClicked);
        UnregisterButton("btn-social-twitter", OnTwitterClicked);
        UnregisterButton("btn-social-web", OnWebClicked);

        UnregisterButton("tab-game", OnGameTabClicked);
        UnregisterButton("tab-video", OnVideoTabClicked);
        UnregisterButton("tab-controls", OnControlsTabClicked);
        UnregisterButton("tab-keybindings", OnKeybindingsTabClicked);
        UnregisterButton("tab-kb-movement", OnMovementKbTabClicked);
        UnregisterButton("tab-kb-combat", OnCombatKbTabClicked);
        UnregisterButton("tab-kb-general", OnGeneralKbTabClicked);

        UnregisterButton("btn-diff-normal", OnDiffNormalClicked);
        UnregisterButton("btn-diff-hard", OnDiffHardClicked);
        UnregisterButton("btn-tex-low", OnTextureLowClicked);
        UnregisterButton("btn-tex-med", OnTextureMediumClicked);
        UnregisterButton("btn-tex-high", OnTextureHighClicked);
        UnregisterButton("btn-shadow-off", OnShadowOffClicked);
        UnregisterButton("btn-shadow-low", OnShadowLowClicked);
        UnregisterButton("btn-shadow-high", OnShadowHighClicked);
        UnregisterButton("btn-aa-off", OnAaOffClicked);
        UnregisterButton("btn-aa-2x", OnAa2xClicked);
        UnregisterButton("btn-aa-4x", OnAa4xClicked);
        UnregisterButton("btn-aa-8x", OnAa8xClicked);

        UnregisterToggle("toggle-hud", OnHudToggleChanged);
        UnregisterToggle("toggle-tooltips", OnTooltipsToggleChanged);
        UnregisterToggle("toggle-fullscreen", OnFullscreenToggleChanged);
        UnregisterToggle("toggle-vsync", OnVsyncToggleChanged);
        UnregisterToggle("toggle-motionblur", OnMotionBlurToggleChanged);
        UnregisterToggle("toggle-ao", OnAoToggleChanged);
        UnregisterToggle("toggle-cameraeffects", OnCameraEffectsToggleChanged);
        UnregisterToggle("toggle-invert", OnInvertToggleChanged);

        UnregisterSlider("slider-music", OnMusicSliderChanged);
        UnregisterSlider("slider-sens-x", OnSensitivityXChanged);
        UnregisterSlider("slider-sens-y", OnSensitivityYChanged);
        UnregisterSlider("slider-smooth", OnSmoothSliderChanged);
        UnregisterDropdown("dropdown-control-scheme", OnControlSchemeChanged);

        for (int i = 0; i < ShopItemNames.Length; i++)
        {
            var item = root.Q("shop-item-" + i);
            if (item != null)
            {
                item.UnregisterCallback<PointerOverEvent>(OnShopItemPointerOver);
                item.UnregisterCallback<PointerOutEvent>(OnShopItemPointerOut);
            }

            UnregisterButton("btn-buy-item-" + i, OnBuyItemClicked);
        }

        root.Query<Button>().ForEach(button =>
        {
            button.UnregisterCallback<PointerOverEvent>(OnAnyButtonPointerOver);
        });

        callbacksRegistered = false;
    }

    private void RegisterButton(string name, EventCallback<ClickEvent> callback)
    {
        root.Q<Button>(name)?.RegisterCallback<ClickEvent>(callback);
    }

    private void UnregisterButton(string name, EventCallback<ClickEvent> callback)
    {
        root.Q<Button>(name)?.UnregisterCallback<ClickEvent>(callback);
    }

    private void RegisterToggle(string name, EventCallback<ChangeEvent<bool>> callback)
    {
        root.Q<Toggle>(name)?.RegisterValueChangedCallback(callback);
    }

    private void UnregisterToggle(string name, EventCallback<ChangeEvent<bool>> callback)
    {
        root.Q<Toggle>(name)?.UnregisterValueChangedCallback(callback);
    }

    private void RegisterSlider(string name, EventCallback<ChangeEvent<float>> callback)
    {
        root.Q<Slider>(name)?.RegisterValueChangedCallback(callback);
    }

    private void UnregisterSlider(string name, EventCallback<ChangeEvent<float>> callback)
    {
        root.Q<Slider>(name)?.UnregisterValueChangedCallback(callback);
    }

    private void RegisterDropdown(string name, EventCallback<ChangeEvent<string>> callback)
    {
        root.Q<DropdownField>(name)?.RegisterValueChangedCallback(callback);
    }

    private void UnregisterDropdown(string name, EventCallback<ChangeEvent<string>> callback)
    {
        root.Q<DropdownField>(name)?.UnregisterValueChangedCallback(callback);
    }

    private int NextMotionTicket(VisualElement element)
    {
        if (element == null)
        {
            return 0;
        }

        int nextTicket = motionTickets.TryGetValue(element, out int currentTicket) ? currentTicket + 1 : 1;
        motionTickets[element] = nextTicket;
        return nextTicket;
    }

    private bool IsMotionTicketCurrent(VisualElement element, int ticket)
    {
        return element != null
            && motionTickets.TryGetValue(element, out int currentTicket)
            && currentTicket == ticket;
    }

    private void SetHiddenImmediate(VisualElement element)
    {
        if (element == null)
        {
            return;
        }

        NextMotionTicket(element);
        element.RemoveFromClassList("motion-active");
        element.AddToClassList("hidden");
        element.pickingMode = PickingMode.Ignore;
    }

    private void SetVisibleImmediate(VisualElement element)
    {
        if (element == null)
        {
            return;
        }

        NextMotionTicket(element);
        element.RemoveFromClassList("hidden");
        element.AddToClassList("motion-active");
        element.pickingMode = PickingMode.Position;
    }

    private void ShowMotionElement(VisualElement element, bool immediate = false)
    {
        if (element == null)
        {
            return;
        }

        int ticket = NextMotionTicket(element);
        element.RemoveFromClassList("hidden");
        element.pickingMode = PickingMode.Position;

        if (immediate)
        {
            element.AddToClassList("motion-active");
            return;
        }

        element.RemoveFromClassList("motion-active");
        element.schedule.Execute(() =>
        {
            if (root == null || !IsMotionTicketCurrent(element, ticket))
            {
                return;
            }

            element.AddToClassList("motion-active");
        }).ExecuteLater(MotionWarmupMs);
    }

    private void HideMotionElement(VisualElement element, bool immediate = false)
    {
        if (element == null)
        {
            return;
        }

        int ticket = NextMotionTicket(element);
        element.RemoveFromClassList("motion-active");
        element.pickingMode = PickingMode.Ignore;

        if (immediate)
        {
            element.AddToClassList("hidden");
            return;
        }

        element.schedule.Execute(() =>
        {
            if (root == null || !IsMotionTicketCurrent(element, ticket))
            {
                return;
            }

            element.AddToClassList("hidden");
        }).ExecuteLater(MotionFadeMs);
    }

    private void ShowShell(VisualElement target, bool immediate = false)
    {
        foreach (var shell in shellLayers)
        {
            if (shell == target)
            {
                continue;
            }

            HideMotionElement(shell, immediate);
        }

        ShowMotionElement(target, immediate);
    }

    private void ShowPrimaryScreen(VisualElement target, bool immediate = false)
    {
        if (target == null)
        {
            return;
        }

        foreach (var screen in primaryScreens)
        {
            if (screen == target)
            {
                continue;
            }

            HideMotionElement(screen, immediate);
        }

        ShowMotionElement(target, immediate);
        currentPrimaryScreen = target;
    }

    private void ShowOverlayScreen(VisualElement target, bool immediate = false)
    {
        foreach (var overlay in overlayScreens)
        {
            if (overlay == target)
            {
                continue;
            }

            HideOverlayScreen(overlay, immediate);
        }

        if (target == screenLoading)
        {
            ResetLoadingState();
        }

        ShowMotionElement(target, immediate);
        currentOverlayScreen = target;
    }

    private void HideOverlayScreen(VisualElement target, bool immediate = false)
    {
        if (target == null)
        {
            return;
        }

        HideMotionElement(target, immediate);

        if (target == screenLoading)
        {
            StopLoadingPulse();
            ResetLoadingState();
        }

        if (currentOverlayScreen == target)
        {
            currentOverlayScreen = null;
        }
    }

    private void SetActiveButtons(string[] buttonIds, string activeId)
    {
        foreach (var id in buttonIds)
        {
            var button = root.Q<Button>(id);
            if (button == null)
            {
                continue;
            }

            button.EnableInClassList("active", id == activeId);
        }
    }

    private void SwitchPanel(VisualElement target, List<VisualElement> panels, ref VisualElement currentPanel)
    {
        if (target == null)
        {
            return;
        }

        foreach (var panel in panels)
        {
            if (panel == target)
            {
                continue;
            }

            HideMotionElement(panel);
        }

        ShowMotionElement(target);
        currentPanel = target;
    }

    private void StartAmbientMotion()
    {
        StopAmbientMotion();
        ApplyReticleDrift(false);

        if (root == null || reticleElements.Count == 0)
        {
            return;
        }

        reticleMotionItem = root.schedule.Execute(() =>
        {
            reticleMotionAlt = !reticleMotionAlt;
            ApplyReticleDrift(reticleMotionAlt);
        }).Every(ReticleMotionIntervalMs);
    }

    private void StopAmbientMotion()
    {
        reticleMotionItem?.Pause();
        reticleMotionItem = null;
        reticleMotionAlt = false;
        ApplyReticleDrift(false);
    }

    private void ApplyReticleDrift(bool altState)
    {
        foreach (var reticle in reticleElements)
        {
            if (reticle == null)
            {
                continue;
            }

            reticle.EnableInClassList("reticle-drift-a", !altState);
            reticle.EnableInClassList("reticle-drift-b", altState);
        }
    }

    private void ResetLoadingState()
    {
        loadingReadyState = false;

        if (loadingProgress != null)
        {
            loadingProgress.value = 0f;
        }

        if (loadingPrompt != null)
        {
            loadingPrompt.text = "Preparing scenario";
            loadingPrompt.RemoveFromClassList("loading-ready");
            loadingPrompt.RemoveFromClassList("loading-pulse");
        }

        loadingPanel?.RemoveFromClassList("loading-ready");
    }

    private void SetLoadingReadyState()
    {
        if (loadingReadyState)
        {
            return;
        }

        loadingReadyState = true;

        if (loadingPrompt != null)
        {
            loadingPrompt.text = "Press ENTER to continue";
            loadingPrompt.AddToClassList("loading-ready");
        }

        loadingPanel?.AddToClassList("loading-ready");

        loadingPulseItem = loadingPrompt?.schedule.Execute(() =>
        {
            if (loadingPrompt == null)
            {
                return;
            }

            loadingPrompt.EnableInClassList("loading-pulse", !loadingPrompt.ClassListContains("loading-pulse"));
        }).Every(LoadingPulseIntervalMs);
    }

    private void StopLoadingPulse()
    {
        loadingPulseItem?.Pause();
        loadingPulseItem = null;

        if (loadingPrompt != null)
        {
            loadingPrompt.RemoveFromClassList("loading-pulse");
            loadingPrompt.RemoveFromClassList("loading-ready");
        }

        loadingPanel?.RemoveFromClassList("loading-ready");
        loadingReadyState = false;
    }

    private void ShowLogin()
    {
        HideOverlayScreen(currentOverlayScreen, true);
        ShowShell(shellLogin);
    }

    private void ShowRegister()
    {
        HideOverlayScreen(currentOverlayScreen, true);
        ShowShell(shellRegister);
    }

    public void ShowMainMenu()
    {
        HideOverlayScreen(currentOverlayScreen, true);
        ShowShell(shellMain);

        SetActiveNav("nav-play");
        ShowPrimaryScreen(screenPlay);
        RefreshCoinDisplays();

        string name = PlayFabLogin.DisplayNameFromPlayFab ?? PlayerPrefs.GetString("USERNAME", "OPERATOR");
        if (sidebarPlayerName != null)
        {
            sidebarPlayerName.text = name.ToUpper();
        }
    }

    private void ShowContentScreen(VisualElement target)
    {
        HideOverlayScreen(currentOverlayScreen);
        ShowPrimaryScreen(target);
    }

    public void ShowScreen(string screenName)
    {
        switch (screenName)
        {
            case "login-screen":
                ShowLogin();
                break;
            case "register-screen":
                ShowRegister();
                break;
            case "main-menu-screen":
            case "play-menu-screen":
                ShowMainMenu();
                break;
            case "play-screen":
                ShowContentScreen(screenPlay);
                break;
            case "shop-screen":
                ShowContentScreen(screenShop);
                break;
            case "inventory-screen":
                ShowContentScreen(screenInventory);
                break;
            case "account-screen":
                ShowContentScreen(screenAccount);
                break;
            case "settings-screen":
                ShowContentScreen(screenSettings);
                break;
            case "exit-screen":
                ShowOverlayScreen(screenExit);
                break;
            case "loading-screen":
                ShowOverlayScreen(screenLoading);
                break;
        }
    }

    private void SetActiveNav(string activeId)
    {
        foreach (var id in NavIds)
        {
            var button = root.Q<Button>(id);
            if (button == null)
            {
                continue;
            }

            if (id == activeId)
            {
                button.AddToClassList("active");
            }
            else
            {
                button.RemoveFromClassList("active");
            }
        }
    }

    private void ShowSettingsPanel(string panelName, string tabName)
    {
        SwitchPanel(root.Q(panelName), settingsPanels, ref currentSettingsPanel);
        SetActiveButtons(SettingsTabIds, tabName);

        PlayClick();
    }

    private void ShowKBPanel(string panelName, string tabName)
    {
        SwitchPanel(root.Q(panelName), keybindingPanels, ref currentKeybindingPanel);
        SetActiveButtons(KeybindTabIds, tabName);

        PlayClick();
    }

    private void OnLoginButtonClicked(ClickEvent evt)
    {
        PlayClick();
    }

    private void OnGotoRegisterClicked(ClickEvent evt)
    {
        ShowRegister();
        PlayClick();
    }

    private void OnBackToLoginClicked(ClickEvent evt)
    {
        ShowLogin();
        PlayClick();
    }

    private void OnNavPlayClicked(ClickEvent evt)
    {
        SetActiveNav("nav-play");
        ShowContentScreen(screenPlay);
        PlayClick();
    }

    private void OnNavShopClicked(ClickEvent evt)
    {
        SetActiveNav("nav-shop");
        RefreshCoinDisplays();
        ShowContentScreen(screenShop);
        PlayClick();
    }

    private void OnNavInventoryClicked(ClickEvent evt)
    {
        SetActiveNav("nav-inventory");
        ShowContentScreen(screenInventory);
        PlayClick();
    }

    private void OnNavAccountClicked(ClickEvent evt)
    {
        SetActiveNav("nav-account");
        UpdateAccountDetails();
        ShowContentScreen(screenAccount);
        PlayClick();
    }

    private void OnNavSettingsClicked(ClickEvent evt)
    {
        SetActiveNav("nav-settings");
        ShowContentScreen(screenSettings);
        PlaySwoosh();
        Position2();
    }

    private void OnNavExitClicked(ClickEvent evt)
    {
        ShowOverlayScreen(screenExit);
        PlayClick();
    }

    private void OnCampaignClicked(ClickEvent evt)
    {
        StartCampaign();
    }

    private void OnSurvivalClicked(ClickEvent evt)
    {
        PlayClick();
        Debug.Log("Survival mode not yet implemented.");
    }

    private void OnExitYesClicked(ClickEvent evt)
    {
        QuitGame();
    }

    private void OnExitNoClicked(ClickEvent evt)
    {
        HideOverlayScreen(screenExit);
        PlayClick();
    }

    private void OnLogoutClicked(ClickEvent evt)
    {
        Logout();
    }

    private void OnDiscordClicked(ClickEvent evt)
    {
        Application.OpenURL("https://discord.com");
        PlayClick();
    }

    private void OnTwitterClicked(ClickEvent evt)
    {
        Application.OpenURL("https://twitter.com");
        PlayClick();
    }

    private void OnWebClicked(ClickEvent evt)
    {
        Application.OpenURL("https://unity.com");
        PlayClick();
    }

    private void OnGameTabClicked(ClickEvent evt) => ShowSettingsPanel("panel-game", "tab-game");
    private void OnVideoTabClicked(ClickEvent evt) => ShowSettingsPanel("panel-video", "tab-video");
    private void OnControlsTabClicked(ClickEvent evt) => ShowSettingsPanel("panel-controls", "tab-controls");
    private void OnKeybindingsTabClicked(ClickEvent evt) => ShowSettingsPanel("panel-keybindings", "tab-keybindings");
    private void OnMovementKbTabClicked(ClickEvent evt) => ShowKBPanel("panel-kb-movement", "tab-kb-movement");
    private void OnCombatKbTabClicked(ClickEvent evt) => ShowKBPanel("panel-kb-combat", "tab-kb-combat");
    private void OnGeneralKbTabClicked(ClickEvent evt) => ShowKBPanel("panel-kb-general", "tab-kb-general");

    private void OnDiffNormalClicked(ClickEvent evt)
    {
        SetDifficulty(true);
        PlayClick();
    }

    private void OnDiffHardClicked(ClickEvent evt)
    {
        SetDifficulty(false);
        PlayClick();
    }

    private void OnTextureLowClicked(ClickEvent evt)
    {
        SetTextureQuality(0);
        PlayClick();
    }

    private void OnTextureMediumClicked(ClickEvent evt)
    {
        SetTextureQuality(1);
        PlayClick();
    }

    private void OnTextureHighClicked(ClickEvent evt)
    {
        SetTextureQuality(2);
        PlayClick();
    }

    private void OnShadowOffClicked(ClickEvent evt)
    {
        SetShadowQuality(0);
        PlayClick();
    }

    private void OnShadowLowClicked(ClickEvent evt)
    {
        SetShadowQuality(1);
        PlayClick();
    }

    private void OnShadowHighClicked(ClickEvent evt)
    {
        SetShadowQuality(2);
        PlayClick();
    }

    private void OnAaOffClicked(ClickEvent evt)
    {
        QualitySettings.antiAliasing = 0;
        PlayClick();
    }

    private void OnAa2xClicked(ClickEvent evt)
    {
        QualitySettings.antiAliasing = 2;
        PlayClick();
    }

    private void OnAa4xClicked(ClickEvent evt)
    {
        QualitySettings.antiAliasing = 4;
        PlayClick();
    }

    private void OnAa8xClicked(ClickEvent evt)
    {
        QualitySettings.antiAliasing = 8;
        PlayClick();
    }

    private void OnHudToggleChanged(ChangeEvent<bool> evt) => PlayerPrefs.SetInt("ShowHUD", evt.newValue ? 1 : 0);
    private void OnTooltipsToggleChanged(ChangeEvent<bool> evt) => PlayerPrefs.SetInt("ToolTips", evt.newValue ? 1 : 0);
    private void OnFullscreenToggleChanged(ChangeEvent<bool> evt) => Screen.fullScreen = evt.newValue;
    private void OnVsyncToggleChanged(ChangeEvent<bool> evt) => QualitySettings.vSyncCount = evt.newValue ? 1 : 0;
    private void OnMotionBlurToggleChanged(ChangeEvent<bool> evt) => PlayerPrefs.SetInt("MotionBlur", evt.newValue ? 1 : 0);
    private void OnAoToggleChanged(ChangeEvent<bool> evt) => PlayerPrefs.SetInt("AmbientOcclusion", evt.newValue ? 1 : 0);
    private void OnCameraEffectsToggleChanged(ChangeEvent<bool> evt) => PlayerPrefs.SetInt("CameraEffects", evt.newValue ? 1 : 0);
    private void OnInvertToggleChanged(ChangeEvent<bool> evt) => PlayerPrefs.SetInt("MouseInvert", evt.newValue ? 1 : 0);

    private void OnMusicSliderChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MusicVolume", evt.newValue);
        PlaySlider();

        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        var source = cam.GetComponent<AudioSource>();
        if (source != null)
        {
            source.volume = evt.newValue;
        }
    }

    private void OnSensitivityXChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MouseSensX", evt.newValue);
        PlaySlider();
    }

    private void OnSensitivityYChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MouseSensY", evt.newValue);
        PlaySlider();
    }

    private void OnSmoothSliderChanged(ChangeEvent<float> evt)
    {
        PlayerPrefs.SetFloat("MouseSmooth", evt.newValue);
        PlaySlider();
    }

    private void OnControlSchemeChanged(ChangeEvent<string> evt)
    {
        PlayerPrefs.SetInt("ControlScheme", evt.newValue == "Controller" ? 1 : 0);
    }

    private void OnShopItemPointerOver(PointerOverEvent evt)
    {
        var element = evt.currentTarget as VisualElement;
        int index = ParseTrailingIndex(element?.name);
        var shopTooltip = root.Q<Label>("lbl-shop-tooltip");

        if (index >= 0 && index < ShopItemTooltips.Length && shopTooltip != null)
        {
            shopTooltip.text = ShopItemTooltips[index];
        }

        PlayHover();
    }

    private void OnShopItemPointerOut(PointerOutEvent evt)
    {
        var shopTooltip = root.Q<Label>("lbl-shop-tooltip");
        if (shopTooltip != null)
        {
            shopTooltip.text = "Select equipment to preview acquisition details.";
        }
    }

    private void OnBuyItemClicked(ClickEvent evt)
    {
        var element = evt.currentTarget as VisualElement;
        int index = ParseTrailingIndex(element?.name);
        if (index >= 0 && index < ShopItemNames.Length)
        {
            BuyItem(index);
        }
    }

    private void OnAnyButtonPointerOver(PointerOverEvent evt)
    {
        PlayHover();
    }

    private void StartCampaign()
    {
        PlayClick();
        ShowOverlayScreen(screenLoading);
        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("No next scene in Build Settings.");
            HideOverlayScreen(screenLoading);
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(nextIndex);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (loadingProgress != null)
            {
                loadingProgress.value = progress;
            }

            if (op.progress >= 0.9f)
            {
                if (loadingProgress != null)
                {
                    loadingProgress.value = 1f;
                }

                SetLoadingReadyState();

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    op.allowSceneActivation = true;
                }
            }

            yield return null;
        }
    }

    private void UpdateAccountDetails()
    {
        string name = PlayFabLogin.DisplayNameFromPlayFab ?? PlayerPrefs.GetString("USERNAME", "Player");

        var nameLabel = root.Q<Label>("lbl-account-name");
        var idLabel = root.Q<Label>("lbl-account-id");

        if (nameLabel != null)
        {
            nameLabel.text = name.ToUpper();
        }

        if (idLabel != null)
        {
            idLabel.text = "STATUS: ONLINE";
        }

        if (sidebarPlayerName != null)
        {
            sidebarPlayerName.text = name.ToUpper();
        }

        string initial = name.Length > 0 ? name[0].ToString().ToUpper() : "O";

        var avatarLetter = root.Q<Label>("player-avatar-letter");
        if (avatarLetter != null)
        {
            avatarLetter.text = initial;
        }

        var bigLetter = root.Q<Label>("account-avatar-letter");
        if (bigLetter != null)
        {
            bigLetter.text = initial;
        }
    }

    private void SetDifficulty(bool normal)
    {
        PlayerPrefs.SetInt("NormalDifficulty", normal ? 1 : 0);
        PlayerPrefs.SetInt("HardCoreDifficulty", normal ? 0 : 1);
    }

    private void SetTextureQuality(int level)
    {
        PlayerPrefs.SetInt("Textures", level);
        QualitySettings.globalTextureMipmapLimit = 2 - level;
    }

    private void SetShadowQuality(int level)
    {
        PlayerPrefs.SetInt("Shadows", level);
        switch (level)
        {
            case 0:
                QualitySettings.shadowCascades = 0;
                QualitySettings.shadowDistance = 0;
                break;
            case 1:
                QualitySettings.shadowCascades = 2;
                QualitySettings.shadowDistance = 75;
                break;
            case 2:
                QualitySettings.shadowCascades = 4;
                QualitySettings.shadowDistance = 500;
                break;
        }
    }

    private void BuyItem(int index)
    {
        PlayClick();

        int cost = ShopItemPrices[index];
        var tip = root.Q<Label>("lbl-shop-tooltip");

        if (coinCount < cost)
        {
            Debug.Log($"Not enough coins! Need {cost} but have {coinCount}.");
            if (tip != null)
            {
                tip.text = "Not enough coins";
            }

            return;
        }

        coinCount -= cost;

        float roll = Random.value;
        int reward;
        if (roll < 0.70f)
        {
            reward = Random.Range(10, 51);
        }
        else if (roll < 0.95f)
        {
            reward = Random.Range(100, 301);
        }
        else
        {
            reward = Random.Range(500, 1001);
        }

        coinCount += reward;
        PlayerPrefs.SetInt("CoinCount", coinCount);
        PlayerPrefs.Save();
        RefreshCoinDisplays();

        if (tip != null)
        {
            tip.text = $"Chest reward: +{reward} COINS";
        }

        Debug.Log($"[Shop] Bought '{ShopItemNames[index]}' (-{cost}): chest gave +{reward}. Total: {coinCount}");
    }

    private void RefreshCoinDisplays()
    {
        if (sidebarCoins != null)
        {
            sidebarCoins.text = coinCount.ToString("N0");
        }

        if (shopCoins != null)
        {
            shopCoins.text = coinCount.ToString("N0") + " COINS";
        }
    }

    private void Logout()
    {
        PlayClick();

        if (playFabLogin != null)
        {
            playFabLogin.LogoutToLoginScreen();
            return;
        }

        try
        {
            PlayFab.PlayFabClientAPI.ForgetAllCredentials();
        }
        catch
        {
        }

        PlayFabLogin.DisplayNameFromPlayFab = null;
        PlayerPrefs.DeleteKey("USERNAME");
        PlayerPrefs.DeleteKey("PlayFabSessionTicket");
        PlayerPrefs.Save();
        ShowLogin();
        Debug.Log("[UIToolkitMenuController] Logged out.");
    }

    private void LoadSettingsIntoUI()
    {
        SetToggle("toggle-hud", PlayerPrefs.GetInt("ShowHUD") == 1);
        SetToggle("toggle-tooltips", PlayerPrefs.GetInt("ToolTips") == 1);
        SetSlider("slider-music", PlayerPrefs.GetFloat("MusicVolume", 1f));

        SetToggle("toggle-fullscreen", Screen.fullScreen);
        SetToggle("toggle-vsync", QualitySettings.vSyncCount > 0);
        SetToggle("toggle-motionblur", PlayerPrefs.GetInt("MotionBlur") == 1);
        SetToggle("toggle-ao", PlayerPrefs.GetInt("AmbientOcclusion") == 1);
        SetToggle("toggle-cameraeffects", PlayerPrefs.GetInt("CameraEffects") == 1);

        SetSlider("slider-sens-x", PlayerPrefs.GetFloat("MouseSensX", 2f));
        SetSlider("slider-sens-y", PlayerPrefs.GetFloat("MouseSensY", 2f));
        SetSlider("slider-smooth", PlayerPrefs.GetFloat("MouseSmooth", 3f));
        SetToggle("toggle-invert", PlayerPrefs.GetInt("MouseInvert", 0) == 1);

        var dropdown = root.Q<DropdownField>("dropdown-control-scheme");
        if (dropdown != null)
        {
            dropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ControlScheme", 0) == 1
                ? "Controller"
                : "Keyboard & Mouse");
        }
    }

    private void SetToggle(string name, bool value)
    {
        var toggle = root.Q<Toggle>(name);
        if (toggle != null)
        {
            toggle.SetValueWithoutNotify(value);
        }
    }

    private void SetSlider(string name, float value)
    {
        var slider = root.Q<Slider>(name);
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }

    private void MigratePlayerPrefs()
    {
        bool dirty = false;

        if (PlayerPrefs.HasKey("XSensitivity") && !PlayerPrefs.HasKey("MouseSensX"))
        {
            PlayerPrefs.SetFloat("MouseSensX", PlayerPrefs.GetFloat("XSensitivity"));
            PlayerPrefs.DeleteKey("XSensitivity");
            dirty = true;
        }

        if (PlayerPrefs.HasKey("YSensitivity") && !PlayerPrefs.HasKey("MouseSensY"))
        {
            PlayerPrefs.SetFloat("MouseSensY", PlayerPrefs.GetFloat("YSensitivity"));
            PlayerPrefs.DeleteKey("YSensitivity");
            dirty = true;
        }

        if (PlayerPrefs.HasKey("MouseSmoothing") && !PlayerPrefs.HasKey("MouseSmooth"))
        {
            PlayerPrefs.SetFloat("MouseSmooth", PlayerPrefs.GetFloat("MouseSmoothing"));
            PlayerPrefs.DeleteKey("MouseSmoothing");
            dirty = true;
        }

        if (PlayerPrefs.HasKey("Inverted") && !PlayerPrefs.HasKey("MouseInvert"))
        {
            PlayerPrefs.SetInt("MouseInvert", PlayerPrefs.GetInt("Inverted"));
            PlayerPrefs.DeleteKey("Inverted");
            dirty = true;
        }

        if (dirty)
        {
            PlayerPrefs.Save();
            Debug.Log("[UIToolkitMenuController] Legacy PlayerPrefs migrated.");
        }
    }

    private static int ParseTrailingIndex(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return -1;
        }

        int separatorIndex = value.LastIndexOf('-');
        if (separatorIndex < 0 || separatorIndex >= value.Length - 1)
        {
            return -1;
        }

        return int.TryParse(value.Substring(separatorIndex + 1), out int index) ? index : -1;
    }

    private void Position2()
    {
        if (cameraAnimator != null)
        {
            cameraAnimator.SetFloat("Animate", 1);
        }
    }

    private void PlayHover()
    {
        if (hoverSound != null)
        {
            hoverSound.Play();
        }
    }

    private void PlayClick()
    {
        if (clickSound != null)
        {
            clickSound.Play();
        }
    }

    private void PlaySlider()
    {
        if (sliderSound != null)
        {
            sliderSound.Play();
        }
    }

    private void PlaySwoosh()
    {
        if (swooshSound != null)
        {
            swooshSound.Play();
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
