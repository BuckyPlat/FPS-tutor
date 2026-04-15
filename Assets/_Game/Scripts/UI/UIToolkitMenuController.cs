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
    public AudioSource sliderSound;  // SFX_Tick — plays on slider drag
    public AudioSource swooshSound;

    [Header("PlayFab Controllers")]
    public PlayFabLogin playFabLogin;
    public PlayFabRegister playFabRegister;

    private VisualElement root;
    private VisualElement screenMain, screenPlay, screenSettings, screenExit, screenLogin, screenRegister;
    private VisualElement screenShop, screenInventory, screenAccount, screenLoading;
    private VisualElement panelGame, panelVideo, panelControls, panelKeyBindings;
    private Label loginMessage, regMessage;
    private Label lblCurrency, lblShopCoins;
    private ProgressBar loadingProgress;
    private Label loadingPrompt;

    private Animator cameraAnimator;
    private int coinCount;

    // Shop item data: name, price, tooltip description
    private static readonly string[] ShopItemNames   = { "Cartoon Pack", "Sci-Fi Pack", "Epic Bundle" };
    private static readonly int[]    ShopItemPrices  = { 50, 100, 200 };
    private static readonly string[] ShopItemTooltips = {
        "Colorful cartoon weapon skins. Great for a fun look!",
        "Sleek sci-fi weapon skins. Look futuristic!",
        "Exclusive bundle with all premium skins included!"
    };

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        root = uiDocument.rootVisualElement;

        // Animator is on the Camera, not on this GO
        var cam = Camera.main;
        if (cam != null) cameraAnimator = cam.GetComponent<Animator>();

        QueryElements();
        RegisterCallbacks();

        // Initialize sub-controllers
        if (playFabLogin != null) playFabLogin.Initialize(root, this);
        if (playFabRegister != null) playFabRegister.Initialize(root);

        // Music initialization from legacy CheckMusicVolume
        var mainAudio = cam != null ? cam.GetComponent<AudioSource>() : null;
        if (mainAudio != null && PlayerPrefs.HasKey("MusicVolume"))
        {
            mainAudio.volume = PlayerPrefs.GetFloat("MusicVolume");
        }

        // Coin initialization
        coinCount = PlayerPrefs.GetInt("CoinCount", 1000);
        RefreshCoinDisplays();

        MigratePlayerPrefs();  // silently upgrade legacy UISettingsManager keys
        ShowScreen("login-screen");
        LoadSettingsIntoUI();
    }

    /// <summary>
    /// One-time migration: converts legacy UISettingsManager PlayerPrefs keys
    /// to the keys used by MouseSettings.cs (and this controller).
    /// Legacy keys: XSensitivity, YSensitivity, MouseSmoothing, Inverted
    /// New keys:    MouseSensX,   MouseSensY,   MouseSmooth,    MouseInvert
    /// </summary>
    void MigratePlayerPrefs()
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
            Debug.Log("[UIToolkitMenuController] Legacy PlayerPrefs keys migrated.");
        }
    }

    void QueryElements()
    {
        screenMain = root.Q("main-menu-screen");
        screenPlay = root.Q("play-menu-screen");
        screenSettings = root.Q("settings-screen");
        screenExit = root.Q("exit-screen");
        screenLogin = root.Q("login-screen");
        screenRegister = root.Q("register-screen");

        screenShop = root.Q("shop-screen");
        screenInventory = root.Q("inventory-screen");
        screenAccount = root.Q("account-screen");
        screenLoading = root.Q("loading-screen");

        panelGame = root.Q("panel-game");
        panelVideo = root.Q("panel-video");
        panelControls = root.Q("panel-controls");
        panelKeyBindings = root.Q("panel-keybindings");

        loginMessage = root.Q<Label>("login-message");
        regMessage = root.Q<Label>("reg-message");

        lblCurrency = root.Q<Label>("lbl-currency");
        lblShopCoins = root.Q<Label>("lbl-shop-coins");
        loadingProgress = root.Q<ProgressBar>("loading-progress");
        loadingPrompt = root.Q<Label>("loading-prompt");
    }

    void RegisterCallbacks()
    {
        // ── Auth Buttons ──
        root.Q<Button>("btn-login")?.RegisterCallback<ClickEvent>(e => OnLoginClicked());
        root.Q<Button>("btn-goto-register")?.RegisterCallback<ClickEvent>(e => { ShowScreen("register-screen"); PlayClick(); });
        root.Q<Button>("btn-back-to-login")?.RegisterCallback<ClickEvent>(e => { ShowScreen("login-screen"); PlayClick(); });

        // ── Main Menu ──
        root.Q<Button>("btn-play")?.RegisterCallback<ClickEvent>(e => { ShowScreen("play-menu-screen"); PlayClick(); });
        // btn-shop is set in Back Buttons block (needs RefreshCoinDisplays call first)
        root.Q<Button>("btn-inventory")?.RegisterCallback<ClickEvent>(e => { ShowScreen("inventory-screen"); PlayClick(); });
        root.Q<Button>("btn-account")?.RegisterCallback<ClickEvent>(e => { UpdateAccountDetails(); ShowScreen("account-screen"); PlayClick(); });
        root.Q<Button>("btn-options")?.RegisterCallback<ClickEvent>(e => { ShowScreen("settings-screen"); PlaySwoosh(); Position2(); });
        root.Q<Button>("btn-exit")?.RegisterCallback<ClickEvent>(e => { ShowScreen("exit-screen"); PlayClick(); });

        // ── Back Buttons ──
        root.Q<Button>("btn-shop-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-inventory-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-account-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-play-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-settings-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); Position1(); PlayClick(); });
        root.Q<Button>("btn-exit-no")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-exit-yes")?.RegisterCallback<ClickEvent>(e => QuitGame());

        // ── Logout ──
        root.Q<Button>("btn-logout")?.RegisterCallback<ClickEvent>(e => Logout());

        // ── Open Shop → sync coin label ──
        root.Q<Button>("btn-shop")?.RegisterCallback<ClickEvent>(e => { RefreshCoinDisplays(); ShowScreen("shop-screen"); PlayClick(); });

        // ── Play Sub-Menu ──
        root.Q<Button>("btn-mode-campaign")?.RegisterCallback<ClickEvent>(e => StartCampaign());
        root.Q<Button>("btn-mode-survival")?.RegisterCallback<ClickEvent>(e => { PlayClick(); Debug.Log("Survival mode not yet implemented."); });

        // ── Social / Extra Links ──
        root.Q<Button>("btn-social-discord")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://discord.com"); PlayClick(); });
        root.Q<Button>("btn-social-twitter")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://twitter.com"); PlayClick(); });
        root.Q<Button>("btn-social-web")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://unity.com"); PlayClick(); });

        // ── Settings Tabs ──
        root.Q<Button>("tab-game")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-game", "tab-game"));
        root.Q<Button>("tab-video")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-video", "tab-video"));
        root.Q<Button>("tab-controls")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-controls", "tab-controls"));
        root.Q<Button>("tab-keybindings")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-keybindings", "tab-keybindings"));

        // ── Key Bindings Sub-Tabs ──
        root.Q<Button>("tab-kb-movement")?.RegisterCallback<ClickEvent>(e => ShowKBPanel("panel-kb-movement", "tab-kb-movement"));
        root.Q<Button>("tab-kb-combat")?.RegisterCallback<ClickEvent>(e => ShowKBPanel("panel-kb-combat", "tab-kb-combat"));
        root.Q<Button>("tab-kb-general")?.RegisterCallback<ClickEvent>(e => ShowKBPanel("panel-kb-general", "tab-kb-general"));

        // ── Game Settings Handlers ──
        root.Q<Button>("btn-diff-normal")?.RegisterCallback<ClickEvent>(e => { SetDifficulty(true); PlayClick(); });
        root.Q<Button>("btn-diff-hard")?.RegisterCallback<ClickEvent>(e => { SetDifficulty(false); PlayClick(); });

        root.Q<Toggle>("toggle-hud")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("ShowHUD", e.newValue ? 1 : 0));
        root.Q<Toggle>("toggle-tooltips")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("ToolTips", e.newValue ? 1 : 0));

        // Music Volume Slider → drives Camera AudioSource
        root.Q<Slider>("slider-music")?.RegisterValueChangedCallback(e => {
            PlayerPrefs.SetFloat("MusicVolume", e.newValue);
            PlaySlider();
            var cam = Camera.main;
            if (cam != null)
            {
                var src = cam.GetComponent<AudioSource>();
                if (src != null) src.volume = e.newValue;
            }
        });

        // ── Video Settings Handlers ──
        root.Q<Toggle>("toggle-fullscreen")?.RegisterValueChangedCallback(e => Screen.fullScreen = e.newValue);
        root.Q<Toggle>("toggle-vsync")?.RegisterValueChangedCallback(e => QualitySettings.vSyncCount = e.newValue ? 1 : 0);
        root.Q<Toggle>("toggle-motionblur")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("MotionBlur", e.newValue ? 1 : 0));
        root.Q<Toggle>("toggle-ao")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("AmbientOcclusion", e.newValue ? 1 : 0));
        root.Q<Toggle>("toggle-cameraeffects")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("CameraEffects", e.newValue ? 1 : 0));

        // Texture Quality
        root.Q<Button>("btn-tex-low")?.RegisterCallback<ClickEvent>(e => { SetTextureQuality(0); PlayClick(); });
        root.Q<Button>("btn-tex-med")?.RegisterCallback<ClickEvent>(e => { SetTextureQuality(1); PlayClick(); });
        root.Q<Button>("btn-tex-high")?.RegisterCallback<ClickEvent>(e => { SetTextureQuality(2); PlayClick(); });

        // Shadow Quality
        root.Q<Button>("btn-shadow-off")?.RegisterCallback<ClickEvent>(e => { SetShadowQuality(0); PlayClick(); });
        root.Q<Button>("btn-shadow-low")?.RegisterCallback<ClickEvent>(e => { SetShadowQuality(1); PlayClick(); });
        root.Q<Button>("btn-shadow-high")?.RegisterCallback<ClickEvent>(e => { SetShadowQuality(2); PlayClick(); });

        // Anti-Aliasing
        root.Q<Button>("btn-aa-off")?.RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 0; PlayClick(); });
        root.Q<Button>("btn-aa-2x")?.RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 2; PlayClick(); });
        root.Q<Button>("btn-aa-4x")?.RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 4; PlayClick(); });
        root.Q<Button>("btn-aa-8x")?.RegisterCallback<ClickEvent>(e => { QualitySettings.antiAliasing = 8; PlayClick(); });

        // ── Controls Settings ──
        root.Q<DropdownField>("dropdown-control-scheme")?.RegisterValueChangedCallback(e =>
            PlayerPrefs.SetInt("ControlScheme", e.newValue == "Controller" ? 1 : 0));
        root.Q<Slider>("slider-sens-x")?.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("MouseSensX", e.newValue); PlaySlider(); });
        root.Q<Slider>("slider-sens-y")?.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("MouseSensY", e.newValue); PlaySlider(); });
        root.Q<Slider>("slider-smooth")?.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("MouseSmooth", e.newValue); PlaySlider(); });
        root.Q<Toggle>("toggle-invert")?.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("MouseInvert", e.newValue ? 1 : 0));

        // ── Shop Item Grid (3 tiles with hover tooltip) ──
        var shopTooltip = root.Q<Label>("lbl-shop-tooltip");
        for (int i = 0; i < ShopItemNames.Length; i++)
        {
            int idx = i; // capture for closure
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

        // ── Hover Sounds (must be last to capture all buttons) ──
        root.Query<Button>().ForEach(btn => {
            btn.RegisterCallback<PointerOverEvent>(evt => PlayHover());
        });
    }

    // ═══════════════════════════════════════════════
    // HANDLERS
    // ═══════════════════════════════════════════════

    void OnLoginClicked() { PlayClick(); }

    void StartCampaign()
    {
        PlayClick();
        ShowScreen("loading-screen");
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("No next scene in Build Settings.");
            ShowScreen("play-menu-screen");
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
                if (loadingPrompt != null) loadingPrompt.text = "Press ENTER to continue";

                if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    op.allowSceneActivation = true;
                }
            }
            yield return null;
        }
    }

    void UpdateAccountDetails()
    {
        var lblName = root.Q<Label>("lbl-account-name");
        var lblId = root.Q<Label>("lbl-account-id");
        string displayName = PlayFabLogin.DisplayNameFromPlayFab ?? PlayerPrefs.GetString("USERNAME", "Player");
        if (lblName != null) lblName.text = "Username: " + displayName;
        if (lblId != null) lblId.text = "Status: Online";
    }

    void SetDifficulty(bool normal)
    {
        PlayerPrefs.SetInt("NormalDifficulty", normal ? 1 : 0);
        PlayerPrefs.SetInt("HardCoreDifficulty", normal ? 0 : 1);
    }

    void SetTextureQuality(int level)
    {
        PlayerPrefs.SetInt("Textures", level);
        // 0=Low(mip2), 1=Med(mip1), 2=High(mip0)
        QualitySettings.globalTextureMipmapLimit = 2 - level;
    }

    void SetShadowQuality(int level)
    {
        PlayerPrefs.SetInt("Shadows", level);
        switch (level)
        {
            case 0: QualitySettings.shadowCascades = 0; QualitySettings.shadowDistance = 0; break;
            case 1: QualitySettings.shadowCascades = 2; QualitySettings.shadowDistance = 75; break;
            case 2: QualitySettings.shadowCascades = 4; QualitySettings.shadowDistance = 500; break;
        }
    }

    void BuyItem(int idx)
    {
        PlayClick();
        int cost = ShopItemPrices[idx];
        if (coinCount < cost)
        {
            Debug.Log($"Not enough coins! Need {cost} but have {coinCount}.");
            var tip = root.Q<Label>("lbl-shop-tooltip");
            if (tip != null) tip.text = "⚠ Not enough coins!";
            return;
        }
        coinCount -= cost;

        // Lucky chest reward logic (port from Coin.cs)
        float roll = Random.value;
        int reward;
        if (roll < 0.70f) reward = Random.Range(10, 51);
        else if (roll < 0.95f) reward = Random.Range(100, 301);
        else reward = Random.Range(500, 1001);

        coinCount += reward;
        PlayerPrefs.SetInt("CoinCount", coinCount);
        PlayerPrefs.Save();
        RefreshCoinDisplays();

        Debug.Log($"[Shop] Bought '{ShopItemNames[idx]}' (-{cost}): chest gave +{reward}. Total: {coinCount}");
    }

    void RefreshCoinDisplays()
    {
        string text = "Coins: " + coinCount;
        if (lblCurrency != null) lblCurrency.text = text;
        if (lblShopCoins != null) lblShopCoins.text = text;
    }

    void Logout()
    {
        PlayClick();
        // Clear PlayFab session if available
        try { PlayFab.PlayFabClientAPI.ForgetAllCredentials(); } catch { }
        // Clear local session data
        PlayerPrefs.DeleteKey("PlayFabSessionTicket");
        PlayerPrefs.Save();
        // Return to login screen
        ShowScreen("login-screen");
        Debug.Log("[UIToolkitMenuController] Logged out.");
    }

    // ═══════════════════════════════════════════════
    // SCREEN MANAGEMENT
    // ═══════════════════════════════════════════════

    public void ShowScreen(string screenName)
    {
        screenMain?.AddToClassList("hidden");
        screenPlay?.AddToClassList("hidden");
        screenSettings?.AddToClassList("hidden");
        screenExit?.AddToClassList("hidden");
        screenLogin?.AddToClassList("hidden");
        screenRegister?.AddToClassList("hidden");
        screenShop?.AddToClassList("hidden");
        screenInventory?.AddToClassList("hidden");
        screenAccount?.AddToClassList("hidden");
        screenLoading?.AddToClassList("hidden");

        root.Q(screenName)?.RemoveFromClassList("hidden");
    }

    void ShowSettingsPanel(string panelName, string tabName)
    {
        panelGame?.AddToClassList("hidden");
        panelVideo?.AddToClassList("hidden");
        panelControls?.AddToClassList("hidden");
        panelKeyBindings?.AddToClassList("hidden");

        root.Q(panelName)?.RemoveFromClassList("hidden");

        // Reset all tab highlights
        root.Q<Button>("tab-game")?.RemoveFromClassList("active");
        root.Q<Button>("tab-video")?.RemoveFromClassList("active");
        root.Q<Button>("tab-controls")?.RemoveFromClassList("active");
        root.Q<Button>("tab-keybindings")?.RemoveFromClassList("active");

        root.Q<Button>(tabName)?.AddToClassList("active");
        PlayClick();
    }

    void ShowKBPanel(string panelName, string tabName)
    {
        root.Q("panel-kb-movement")?.AddToClassList("hidden");
        root.Q("panel-kb-combat")?.AddToClassList("hidden");
        root.Q("panel-kb-general")?.AddToClassList("hidden");

        root.Q(panelName)?.RemoveFromClassList("hidden");

        root.Q<Button>("tab-kb-movement")?.RemoveFromClassList("active");
        root.Q<Button>("tab-kb-combat")?.RemoveFromClassList("active");
        root.Q<Button>("tab-kb-general")?.RemoveFromClassList("active");

        root.Q<Button>(tabName)?.AddToClassList("active");
        PlayClick();
    }

    // ═══════════════════════════════════════════════
    // SETTINGS LOAD
    // ═══════════════════════════════════════════════

    void LoadSettingsIntoUI()
    {
        // Game
        SetToggle("toggle-hud", PlayerPrefs.GetInt("ShowHUD") == 1);
        SetToggle("toggle-tooltips", PlayerPrefs.GetInt("ToolTips") == 1);
        SetSlider("slider-music", PlayerPrefs.GetFloat("MusicVolume", 1f));

        // Video
        SetToggle("toggle-fullscreen", Screen.fullScreen);
        SetToggle("toggle-vsync", QualitySettings.vSyncCount > 0);
        SetToggle("toggle-motionblur", PlayerPrefs.GetInt("MotionBlur") == 1);
        SetToggle("toggle-ao", PlayerPrefs.GetInt("AmbientOcclusion") == 1);
        SetToggle("toggle-cameraeffects", PlayerPrefs.GetInt("CameraEffects") == 1);

        // Controls
        SetSlider("slider-sens-x", PlayerPrefs.GetFloat("MouseSensX", 2f));
        SetSlider("slider-sens-y", PlayerPrefs.GetFloat("MouseSensY", 2f));
        SetSlider("slider-smooth", PlayerPrefs.GetFloat("MouseSmooth", 3f));
        SetToggle("toggle-invert", PlayerPrefs.GetInt("MouseInvert", 0) == 1);

        // Controls – Dropdown (Control Scheme)
        var dd = root.Q<DropdownField>("dropdown-control-scheme");
        if (dd != null) dd.SetValueWithoutNotify(
            PlayerPrefs.GetInt("ControlScheme", 0) == 1 ? "Controller" : "Keyboard & Mouse");
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

    // ═══════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════

    void Position2() { if (cameraAnimator) cameraAnimator.SetFloat("Animate", 1); }
    void Position1() { if (cameraAnimator) cameraAnimator.SetFloat("Animate", 0); }
    void PlayHover()  { if (hoverSound)  hoverSound.Play(); }
    void PlayClick()  { if (clickSound)  clickSound.Play(); }
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
