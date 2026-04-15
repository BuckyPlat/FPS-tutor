using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

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

    // ── Root-level shells ──────────────────────────────────────────
    private VisualElement root;
    private VisualElement shellLogin;       // login-screen     (root level)
    private VisualElement shellRegister;    // register-screen  (root level)
    private VisualElement shellMain;        // main-shell       (root level)

    // ── Content screens inside main-shell ─────────────────────────
    private VisualElement screenPlay;
    private VisualElement screenShop;
    private VisualElement screenInventory;
    private VisualElement screenAccount;
    private VisualElement screenSettings;
    private VisualElement screenExit;
    private VisualElement screenLoading;

    // ── Settings sub-panels ───────────────────────────────────────
    private VisualElement panelGame, panelVideo, panelControls, panelKeyBindings;

    // ── Shared labels ─────────────────────────────────────────────
    private Label loginMessage, regMessage;
    private Label sidebarCoins, shopCoins;
    private Label sidebarPlayerName;
    private ProgressBar loadingProgress;
    private Label loadingPrompt;

    // ── Nav buttons (for active-class management) ─────────────────
    private static readonly string[] NavIds =
        { "nav-play", "nav-shop", "nav-inventory", "nav-account", "nav-settings" };

    private Animator cameraAnimator;
    private int coinCount;

    // Shop data
    private static readonly string[] ShopItemNames   = { "Cartoon Pack", "Sci-Fi Pack", "Epic Bundle" };
    private static readonly int[]    ShopItemPrices  = { 50, 100, 200 };
    private static readonly string[] ShopItemTooltips =
    {
        "Colorful cartoon weapon skins. Great for a fun look!",
        "Sleek sci-fi weapon skins. Look futuristic!",
        "Exclusive bundle with all premium skins included!"
    };

    // ═══════════════════════════════════════════════════════════════
    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        root = uiDocument.rootVisualElement;

        var cam = Camera.main;
        if (cam != null) cameraAnimator = cam.GetComponent<Animator>();

        QueryElements();
        RegisterCallbacks();

        if (playFabLogin    != null) playFabLogin.Initialize(root, this);
        if (playFabRegister != null) playFabRegister.Initialize(root);

        // Music restore
        var mainAudio = cam != null ? cam.GetComponent<AudioSource>() : null;
        if (mainAudio != null && PlayerPrefs.HasKey("MusicVolume"))
            mainAudio.volume = PlayerPrefs.GetFloat("MusicVolume");

        coinCount = PlayerPrefs.GetInt("CoinCount", 1000);
        RefreshCoinDisplays();

        MigratePlayerPrefs();
        ShowLogin();
        LoadSettingsIntoUI();
    }

    // ═══════════════════════════════════════════════════════════════
    // ELEMENT QUERIES
    // ═══════════════════════════════════════════════════════════════

    void QueryElements()
    {
        // Root-level shells
        shellLogin    = root.Q("login-screen");
        shellRegister = root.Q("register-screen");
        shellMain     = root.Q("main-shell");

        // Content screens
        screenPlay      = root.Q("play-screen");
        screenShop      = root.Q("shop-screen");
        screenInventory = root.Q("inventory-screen");
        screenAccount   = root.Q("account-screen");
        screenSettings  = root.Q("settings-screen");
        screenExit      = root.Q("exit-screen");
        screenLoading   = root.Q("loading-screen");

        // Settings panels
        panelGame        = root.Q("panel-game");
        panelVideo       = root.Q("panel-video");
        panelControls    = root.Q("panel-controls");
        panelKeyBindings = root.Q("panel-keybindings");

        // Labels
        loginMessage     = root.Q<Label>("login-message");
        regMessage       = root.Q<Label>("reg-message");
        sidebarCoins     = root.Q<Label>("sidebar-coins");
        shopCoins        = root.Q<Label>("lbl-shop-coins");
        sidebarPlayerName = root.Q<Label>("sidebar-player-name");
        loadingProgress  = root.Q<ProgressBar>("loading-progress");
        loadingPrompt    = root.Q<Label>("loading-prompt");
    }

    // ═══════════════════════════════════════════════════════════════
    // CALLBACKS
    // ═══════════════════════════════════════════════════════════════

    void RegisterCallbacks()
    {
        // ── Auth ──────────────────────────────────────────────────
        root.Q<Button>("btn-login")?.RegisterCallback<ClickEvent>(e => OnLoginClicked());
        root.Q<Button>("btn-goto-register")?.RegisterCallback<ClickEvent>(e => { ShowRegister(); PlayClick(); });
        root.Q<Button>("btn-back-to-login")?.RegisterCallback<ClickEvent>(e => { ShowLogin(); PlayClick(); });

        // ── Sidebar Navigation ────────────────────────────────────
        root.Q<Button>("nav-play")?.RegisterCallback<ClickEvent>(e =>
        {
            SetActiveNav("nav-play");
            ShowContentScreen(screenPlay);
            PlayClick();
        });
        root.Q<Button>("nav-shop")?.RegisterCallback<ClickEvent>(e =>
        {
            SetActiveNav("nav-shop");
            RefreshCoinDisplays();
            ShowContentScreen(screenShop);
            PlayClick();
        });
        root.Q<Button>("nav-inventory")?.RegisterCallback<ClickEvent>(e =>
        {
            SetActiveNav("nav-inventory");
            ShowContentScreen(screenInventory);
            PlayClick();
        });
        root.Q<Button>("nav-account")?.RegisterCallback<ClickEvent>(e =>
        {
            SetActiveNav("nav-account");
            UpdateAccountDetails();
            ShowContentScreen(screenAccount);
            PlayClick();
        });
        root.Q<Button>("nav-settings")?.RegisterCallback<ClickEvent>(e =>
        {
            SetActiveNav("nav-settings");
            ShowContentScreen(screenSettings);
            PlaySwoosh();
            Position2();
        });
        root.Q<Button>("nav-exit")?.RegisterCallback<ClickEvent>(e =>
        {
            ShowContentScreen(screenExit);
            PlayClick();
        });

        // ── Play modes ────────────────────────────────────────────
        root.Q<Button>("btn-mode-campaign")?.RegisterCallback<ClickEvent>(e => StartCampaign());
        root.Q<Button>("btn-mode-survival")?.RegisterCallback<ClickEvent>(e =>
        {
            PlayClick();
            Debug.Log("Survival mode not yet implemented.");
        });

        // ── Exit dialog ───────────────────────────────────────────
        root.Q<Button>("btn-exit-yes")?.RegisterCallback<ClickEvent>(e => QuitGame());
        root.Q<Button>("btn-exit-no")?.RegisterCallback<ClickEvent>(e =>
        {
            ShowContentScreen(screenPlay);
            SetActiveNav("nav-play");
            PlayClick();
        });

        // ── Logout ────────────────────────────────────────────────
        root.Q<Button>("btn-logout")?.RegisterCallback<ClickEvent>(e => Logout());

        // ── Social links ──────────────────────────────────────────
        root.Q<Button>("btn-social-discord")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://discord.com"); PlayClick(); });
        root.Q<Button>("btn-social-twitter")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://twitter.com"); PlayClick(); });
        root.Q<Button>("btn-social-web")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://unity.com"); PlayClick(); });

        // ── Settings Tabs ─────────────────────────────────────────
        root.Q<Button>("tab-game")?        .RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-game",        "tab-game"));
        root.Q<Button>("tab-video")?       .RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-video",       "tab-video"));
        root.Q<Button>("tab-controls")?    .RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-controls",    "tab-controls"));
        root.Q<Button>("tab-keybindings")? .RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-keybindings", "tab-keybindings"));

        // ── KB Sub-Tabs ───────────────────────────────────────────
        root.Q<Button>("tab-kb-movement")?.RegisterCallback<ClickEvent>(e => ShowKBPanel("panel-kb-movement", "tab-kb-movement"));
        root.Q<Button>("tab-kb-combat")?  .RegisterCallback<ClickEvent>(e => ShowKBPanel("panel-kb-combat",   "tab-kb-combat"));
        root.Q<Button>("tab-kb-general")? .RegisterCallback<ClickEvent>(e => ShowKBPanel("panel-kb-general",  "tab-kb-general"));

        // ── Game Settings ─────────────────────────────────────────
        root.Q<Button>("btn-diff-normal")?.RegisterCallback<ClickEvent>(e => { SetDifficulty(true);  PlayClick(); });
        root.Q<Button>("btn-diff-hard")?  .RegisterCallback<ClickEvent>(e => { SetDifficulty(false); PlayClick(); });

        root.Q<Toggle>("toggle-hud")?      .RegisterValueChangedCallback(e => PlayerPrefs.SetInt("ShowHUD", e.newValue ? 1 : 0));
        root.Q<Toggle>("toggle-tooltips")? .RegisterValueChangedCallback(e => PlayerPrefs.SetInt("ToolTips", e.newValue ? 1 : 0));

        root.Q<Slider>("slider-music")?.RegisterValueChangedCallback(e =>
        {
            PlayerPrefs.SetFloat("MusicVolume", e.newValue);
            PlaySlider();
            var cam = Camera.main;
            if (cam != null)
            {
                var src = cam.GetComponent<AudioSource>();
                if (src != null) src.volume = e.newValue;
            }
        });

        // ── Video ─────────────────────────────────────────────────
        root.Q<Toggle>("toggle-fullscreen")?   .RegisterValueChangedCallback(e => Screen.fullScreen = e.newValue);
        root.Q<Toggle>("toggle-vsync")?        .RegisterValueChangedCallback(e => QualitySettings.vSyncCount = e.newValue ? 1 : 0);
        root.Q<Toggle>("toggle-motionblur")?   .RegisterValueChangedCallback(e => PlayerPrefs.SetInt("MotionBlur", e.newValue ? 1 : 0));
        root.Q<Toggle>("toggle-ao")?           .RegisterValueChangedCallback(e => PlayerPrefs.SetInt("AmbientOcclusion", e.newValue ? 1 : 0));
        root.Q<Toggle>("toggle-cameraeffects")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("CameraEffects", e.newValue ? 1 : 0));

        root.Q<Button>("btn-tex-low")?   .RegisterCallback<ClickEvent>(e => { SetTextureQuality(0); PlayClick(); });
        root.Q<Button>("btn-tex-med")?   .RegisterCallback<ClickEvent>(e => { SetTextureQuality(1); PlayClick(); });
        root.Q<Button>("btn-tex-high")?  .RegisterCallback<ClickEvent>(e => { SetTextureQuality(2); PlayClick(); });

        root.Q<Button>("btn-shadow-off")?.RegisterCallback<ClickEvent>(e => { SetShadowQuality(0); PlayClick(); });
        root.Q<Button>("btn-shadow-low")?.RegisterCallback<ClickEvent>(e => { SetShadowQuality(1); PlayClick(); });
        root.Q<Button>("btn-shadow-high")?.RegisterCallback<ClickEvent>(e => { SetShadowQuality(2); PlayClick(); });

        root.Q<Button>("btn-aa-off")?.RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 0; PlayClick(); });
        root.Q<Button>("btn-aa-2x")? .RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 2; PlayClick(); });
        root.Q<Button>("btn-aa-4x")? .RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 4; PlayClick(); });
        root.Q<Button>("btn-aa-8x")? .RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 8; PlayClick(); });

        // ── Controls ──────────────────────────────────────────────
        root.Q<DropdownField>("dropdown-control-scheme")?.RegisterValueChangedCallback(e =>
            PlayerPrefs.SetInt("ControlScheme", e.newValue == "Controller" ? 1 : 0));
        root.Q<Slider>("slider-sens-x")?.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("MouseSensX", e.newValue); PlaySlider(); });
        root.Q<Slider>("slider-sens-y")?.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("MouseSensY", e.newValue); PlaySlider(); });
        root.Q<Slider>("slider-smooth")?.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("MouseSmooth", e.newValue); PlaySlider(); });
        root.Q<Toggle>("toggle-invert")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("MouseInvert", e.newValue ? 1 : 0));

        // ── Shop Grid ─────────────────────────────────────────────
        var shopTooltip = root.Q<Label>("lbl-shop-tooltip");
        for (int i = 0; i < ShopItemNames.Length; i++)
        {
            int idx = i;
            var item = root.Q("shop-item-" + idx);
            if (item != null)
            {
                item.RegisterCallback<PointerOverEvent>(_ =>
                {
                    if (shopTooltip != null) shopTooltip.text = ShopItemTooltips[idx];
                    PlayHover();
                });
                item.RegisterCallback<PointerOutEvent>(_ =>
                {
                    if (shopTooltip != null) shopTooltip.text = " ";
                });
            }
            root.Q<Button>("btn-buy-item-" + idx)?.RegisterCallback<ClickEvent>(e => BuyItem(idx));
        }

        // ── Global hover sound ────────────────────────────────────
        root.Query<Button>().ForEach(btn =>
            btn.RegisterCallback<PointerOverEvent>(evt => PlayHover()));
    }

    // ═══════════════════════════════════════════════════════════════
    // SHELL NAVIGATION  (Login / Register / Main)
    // ═══════════════════════════════════════════════════════════════

    void ShowLogin()
    {
        shellLogin?   .RemoveFromClassList("hidden");
        shellRegister?.AddToClassList("hidden");
        shellMain?    .AddToClassList("hidden");
    }

    void ShowRegister()
    {
        shellLogin?   .AddToClassList("hidden");
        shellRegister?.RemoveFromClassList("hidden");
        shellMain?    .AddToClassList("hidden");
    }

    /// <summary>
    /// Shows the main shell (sidebar + content area) and activates a specific content screen.
    /// Call this after successful login.
    /// </summary>
    public void ShowMainMenu()
    {
        shellLogin?   .AddToClassList("hidden");
        shellRegister?.AddToClassList("hidden");
        shellMain?    .RemoveFromClassList("hidden");

        // Default: show Play screen
        SetActiveNav("nav-play");
        ShowContentScreen(screenPlay);
        RefreshCoinDisplays();

        // Update sidebar player name
        string name = PlayFabLogin.DisplayNameFromPlayFab
                      ?? PlayerPrefs.GetString("USERNAME", "OPERATOR");
        if (sidebarPlayerName != null) sidebarPlayerName.text = name.ToUpper();
    }

    // ═══════════════════════════════════════════════════════════════
    // CONTENT SCREEN MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    void ShowContentScreen(VisualElement target)
    {
        screenPlay?     .AddToClassList("hidden");
        screenShop?     .AddToClassList("hidden");
        screenInventory?.AddToClassList("hidden");
        screenAccount?  .AddToClassList("hidden");
        screenSettings? .AddToClassList("hidden");
        screenExit?     .AddToClassList("hidden");
        screenLoading?  .AddToClassList("hidden");

        target?.RemoveFromClassList("hidden");
    }

    /// <summary>
    /// Legacy compatibility: called by PlayFabLogin to switch screens by name.
    /// </summary>
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
                ShowContentScreen(screenExit);
                break;
            case "loading-screen":
                ShowContentScreen(screenLoading);
                break;
        }
    }

    // ── Active nav indicator ──────────────────────────────────────
    void SetActiveNav(string activeId)
    {
        foreach (var id in NavIds)
        {
            var btn = root.Q<Button>(id);
            if (btn == null) continue;
            if (id == activeId)
                btn.AddToClassList("active");
            else
                btn.RemoveFromClassList("active");
        }
    }

    // ── Settings sub-tabs ─────────────────────────────────────────
    void ShowSettingsPanel(string panelName, string tabName)
    {
        panelGame?       .AddToClassList("hidden");
        panelVideo?      .AddToClassList("hidden");
        panelControls?   .AddToClassList("hidden");
        panelKeyBindings?.AddToClassList("hidden");

        root.Q(panelName)?.RemoveFromClassList("hidden");

        root.Q<Button>("tab-game")?        .RemoveFromClassList("active");
        root.Q<Button>("tab-video")?       .RemoveFromClassList("active");
        root.Q<Button>("tab-controls")?    .RemoveFromClassList("active");
        root.Q<Button>("tab-keybindings")? .RemoveFromClassList("active");

        root.Q<Button>(tabName)?.AddToClassList("active");
        PlayClick();
    }

    void ShowKBPanel(string panelName, string tabName)
    {
        root.Q("panel-kb-movement")?.AddToClassList("hidden");
        root.Q("panel-kb-combat")?  .AddToClassList("hidden");
        root.Q("panel-kb-general")? .AddToClassList("hidden");

        root.Q(panelName)?.RemoveFromClassList("hidden");

        root.Q<Button>("tab-kb-movement")?.RemoveFromClassList("active");
        root.Q<Button>("tab-kb-combat")?  .RemoveFromClassList("active");
        root.Q<Button>("tab-kb-general")? .RemoveFromClassList("active");

        root.Q<Button>(tabName)?.AddToClassList("active");
        PlayClick();
    }

    // ═══════════════════════════════════════════════════════════════
    // HANDLERS
    // ═══════════════════════════════════════════════════════════════

    void OnLoginClicked() { PlayClick(); }

    void StartCampaign()
    {
        PlayClick();
        ShowContentScreen(screenLoading);
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("No next scene in Build Settings.");
            ShowContentScreen(screenPlay);
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(nextIndex);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (loadingProgress != null) loadingProgress.value = progress;

            if (op.progress >= 0.9f)
            {
                if (loadingProgress != null) loadingProgress.value = 1f;
                if (loadingPrompt   != null) loadingPrompt.text = "Press ENTER to continue";

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    op.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    void UpdateAccountDetails()
    {
        string name = PlayFabLogin.DisplayNameFromPlayFab
                      ?? PlayerPrefs.GetString("USERNAME", "Player");

        var lblName = root.Q<Label>("lbl-account-name");
        var lblId   = root.Q<Label>("lbl-account-id");

        if (lblName != null) lblName.text = name.ToUpper();
        if (lblId   != null) lblId.text   = "STATUS: ONLINE";

        // Also update sidebar
        if (sidebarPlayerName != null) sidebarPlayerName.text = name.ToUpper();

        // Update avatar first letter
        var avatarLetter = root.Q<Label>("player-avatar-letter"); // sidebar small
        if (avatarLetter != null && name.Length > 0)
            avatarLetter.text = name[0].ToString().ToUpper();

        var bigLetter = root.Q<Label>("account-avatar-letter");
        if (bigLetter != null && name.Length > 0)
            bigLetter.text = name[0].ToString().ToUpper();
    }

    void SetDifficulty(bool normal)
    {
        PlayerPrefs.SetInt("NormalDifficulty", normal ? 1 : 0);
        PlayerPrefs.SetInt("HardCoreDifficulty", normal ? 0 : 1);
    }

    void SetTextureQuality(int level)
    {
        PlayerPrefs.SetInt("Textures", level);
        QualitySettings.globalTextureMipmapLimit = 2 - level;
    }

    void SetShadowQuality(int level)
    {
        PlayerPrefs.SetInt("Shadows", level);
        switch (level)
        {
            case 0: QualitySettings.shadowCascades = 0; QualitySettings.shadowDistance = 0;   break;
            case 1: QualitySettings.shadowCascades = 2; QualitySettings.shadowDistance = 75;  break;
            case 2: QualitySettings.shadowCascades = 4; QualitySettings.shadowDistance = 500; break;
        }
    }

    void BuyItem(int idx)
    {
        PlayClick();
        int cost = ShopItemPrices[idx];
        var tip  = root.Q<Label>("lbl-shop-tooltip");

        if (coinCount < cost)
        {
            Debug.Log($"Not enough coins! Need {cost} but have {coinCount}.");
            if (tip != null) tip.text = "⚠  NOT ENOUGH COINS";
            return;
        }

        coinCount -= cost;

        // Lucky chest reward
        float roll = Random.value;
        int reward;
        if      (roll < 0.70f) reward = Random.Range(10,  51);
        else if (roll < 0.95f) reward = Random.Range(100, 301);
        else                   reward = Random.Range(500, 1001);

        coinCount += reward;
        PlayerPrefs.SetInt("CoinCount", coinCount);
        PlayerPrefs.Save();
        RefreshCoinDisplays();

        if (tip != null) tip.text = $"Chest reward: +{reward} COINS";
        Debug.Log($"[Shop] Bought '{ShopItemNames[idx]}' (−{cost}): chest gave +{reward}. Total: {coinCount}");
    }

    void RefreshCoinDisplays()
    {
        if (sidebarCoins != null) sidebarCoins.text = coinCount.ToString("N0");
        if (shopCoins    != null) shopCoins.text    = coinCount.ToString("N0") + "  COINS";
    }

    void Logout()
    {
        PlayClick();
        try { PlayFab.PlayFabClientAPI.ForgetAllCredentials(); } catch { }
        PlayerPrefs.DeleteKey("PlayFabSessionTicket");
        PlayerPrefs.Save();
        ShowLogin();
        Debug.Log("[UIToolkitMenuController] Logged out.");
    }

    // ═══════════════════════════════════════════════════════════════
    // SETTINGS LOAD
    // ═══════════════════════════════════════════════════════════════

    void LoadSettingsIntoUI()
    {
        SetToggle("toggle-hud",           PlayerPrefs.GetInt("ShowHUD")                == 1);
        SetToggle("toggle-tooltips",      PlayerPrefs.GetInt("ToolTips")               == 1);
        SetSlider("slider-music",         PlayerPrefs.GetFloat("MusicVolume", 1f));

        SetToggle("toggle-fullscreen",    Screen.fullScreen);
        SetToggle("toggle-vsync",         QualitySettings.vSyncCount > 0);
        SetToggle("toggle-motionblur",    PlayerPrefs.GetInt("MotionBlur")             == 1);
        SetToggle("toggle-ao",            PlayerPrefs.GetInt("AmbientOcclusion")       == 1);
        SetToggle("toggle-cameraeffects", PlayerPrefs.GetInt("CameraEffects")          == 1);

        SetSlider("slider-sens-x",        PlayerPrefs.GetFloat("MouseSensX",  2f));
        SetSlider("slider-sens-y",        PlayerPrefs.GetFloat("MouseSensY",  2f));
        SetSlider("slider-smooth",        PlayerPrefs.GetFloat("MouseSmooth", 3f));
        SetToggle("toggle-invert",        PlayerPrefs.GetInt("MouseInvert", 0)         == 1);

        var dd = root.Q<DropdownField>("dropdown-control-scheme");
        if (dd != null)
            dd.SetValueWithoutNotify(PlayerPrefs.GetInt("ControlScheme", 0) == 1
                ? "Controller" : "Keyboard & Mouse");
    }

    void SetToggle(string name, bool value)
    {
        var t = root.Q<Toggle>(name);
        if (t != null) t.SetValueWithoutNotify(value);
    }

    void SetSlider(string name, float value)
    {
        var s = root.Q<Slider>(name);
        if (s != null) s.SetValueWithoutNotify(value);
    }

    // ═══════════════════════════════════════════════════════════════
    // LEGACY MIGRATION
    // ═══════════════════════════════════════════════════════════════

    void MigratePlayerPrefs()
    {
        bool dirty = false;
        if (PlayerPrefs.HasKey("XSensitivity") && !PlayerPrefs.HasKey("MouseSensX"))
        { PlayerPrefs.SetFloat("MouseSensX", PlayerPrefs.GetFloat("XSensitivity")); PlayerPrefs.DeleteKey("XSensitivity"); dirty = true; }
        if (PlayerPrefs.HasKey("YSensitivity") && !PlayerPrefs.HasKey("MouseSensY"))
        { PlayerPrefs.SetFloat("MouseSensY", PlayerPrefs.GetFloat("YSensitivity")); PlayerPrefs.DeleteKey("YSensitivity"); dirty = true; }
        if (PlayerPrefs.HasKey("MouseSmoothing") && !PlayerPrefs.HasKey("MouseSmooth"))
        { PlayerPrefs.SetFloat("MouseSmooth", PlayerPrefs.GetFloat("MouseSmoothing")); PlayerPrefs.DeleteKey("MouseSmoothing"); dirty = true; }
        if (PlayerPrefs.HasKey("Inverted") && !PlayerPrefs.HasKey("MouseInvert"))
        { PlayerPrefs.SetInt("MouseInvert", PlayerPrefs.GetInt("Inverted")); PlayerPrefs.DeleteKey("Inverted"); dirty = true; }
        if (dirty) { PlayerPrefs.Save(); Debug.Log("[UIToolkitMenuController] Legacy PlayerPrefs migrated."); }
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════

    void Position2() { if (cameraAnimator) cameraAnimator.SetFloat("Animate", 1); }
    void Position1() { if (cameraAnimator) cameraAnimator.SetFloat("Animate", 0); }
    void PlayHover()  { if (hoverSound)  hoverSound.Play();  }
    void PlayClick()  { if (clickSound)  clickSound.Play();  }
    void PlaySlider() { if (sliderSound) sliderSound.Play(); }
    void PlaySwoosh() { if (swooshSound) swooshSound.Play(); }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
