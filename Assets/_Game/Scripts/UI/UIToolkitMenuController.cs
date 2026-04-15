using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using SlimUI.ModernMenu;

public class UIToolkitMenuController : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;

    [Header("Audio")]
    public AudioSource hoverSound;
    public AudioSource clickSound;
    public AudioSource swooshSound;

    [Header("PlayFab Controllers")]
    public PlayFabLogin playFabLogin;
    public PlayFabRegister playFabRegister;

    private VisualElement root;
    private VisualElement screenMain, screenPlay, screenSettings, screenExit, screenLogin, screenRegister;
    private VisualElement screenShop, screenInventory, screenAccount, screenLoading;
    private VisualElement panelGame, panelVideo, panelControls;
    private Label loginMessage, regMessage;
    private Label lblCurrency;
    private ProgressBar loadingProgress;
    private Label loadingPrompt;

    private Animator cameraAnimator;

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        root = uiDocument.rootVisualElement;
        cameraAnimator = GetComponent<Animator>();

        QueryElements();
        RegisterCallbacks();

        // Initialize sub-controllers
        if (playFabLogin != null) playFabLogin.Initialize(root, this);
        if (playFabRegister != null) playFabRegister.Initialize(root);
        
        // Music initialization
        if (GetComponent<AudioSource>() != null && PlayerPrefs.HasKey("MusicVolume"))
        {
            GetComponent<AudioSource>().volume = PlayerPrefs.GetFloat("MusicVolume");
        }

        ShowScreen("login-screen");
        LoadSettingsIntoUI();
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

        loginMessage = root.Q<Label>("login-message");
        regMessage = root.Q<Label>("reg-message");
        
        lblCurrency = root.Q<Label>("lbl-currency");
        loadingProgress = root.Q<ProgressBar>("loading-progress");
        loadingPrompt = root.Q<Label>("loading-prompt");
    }

    void RegisterCallbacks()
    {
        // Navigation buttons
        var btnLogin = root.Q<Button>("btn-login");
        if(btnLogin != null) btnLogin.clicked += OnLoginClicked;

        var btnGotoReg = root.Q<Button>("btn-goto-register");
        if(btnGotoReg != null) btnGotoReg.clicked += OnGotoRegisterClicked;

        var btnBackToLogin = root.Q<Button>("btn-back-to-login");
        if(btnBackToLogin != null) btnBackToLogin.clicked += OnBackToLoginClicked;

        // Main Menu Top Level
        root.Q<Button>("btn-play")?.RegisterCallback<ClickEvent>(e => { ShowScreen("play-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-shop")?.RegisterCallback<ClickEvent>(e => { ShowScreen("shop-screen"); PlayClick(); });
        root.Q<Button>("btn-inventory")?.RegisterCallback<ClickEvent>(e => { ShowScreen("inventory-screen"); PlayClick(); });
        root.Q<Button>("btn-account")?.RegisterCallback<ClickEvent>(e => { UpdateAccountDetails(); ShowScreen("account-screen"); PlayClick(); });
        root.Q<Button>("btn-options")?.RegisterCallback<ClickEvent>(e => { ShowScreen("settings-screen"); PlaySwoosh(); Position2(); });
        root.Q<Button>("btn-exit")?.RegisterCallback<ClickEvent>(e => { ShowScreen("exit-screen"); PlayClick(); });

        // Back Buttons
        root.Q<Button>("btn-shop-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-inventory-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-account-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-play-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        root.Q<Button>("btn-settings-back")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); Position1(); PlayClick(); });
        root.Q<Button>("btn-exit-no")?.RegisterCallback<ClickEvent>(e => { ShowScreen("main-menu-screen"); PlayClick(); });
        
        // Action Buttons
        root.Q<Button>("btn-exit-yes")?.RegisterCallback<ClickEvent>(e => QuitGame());
        root.Q<Button>("btn-mode-campaign")?.RegisterCallback<ClickEvent>(e => StartCampaign());
        
        // Social / Extra Links
        root.Q<Button>("btn-social-discord")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://discord.com"); PlayClick(); });
        root.Q<Button>("btn-social-twitter")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://twitter.com"); PlayClick(); });
        root.Q<Button>("btn-social-web")?.RegisterCallback<ClickEvent>(e => { Application.OpenURL("https://unity.com"); PlayClick(); });

        // Tabs
        root.Q<Button>("tab-game")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-game"));
        root.Q<Button>("tab-video")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-video"));
        root.Q<Button>("tab-controls")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-controls"));

        // Hover Sounds
        root.Query<Button>().ForEach(btn => {
            btn.RegisterCallback<PointerOverEvent>(evt => PlayHover());
        });

        // Settings Sliders Callbacks
        root.Q<Slider>("slider-sens-x")?.RegisterValueChangedCallback(e => PlayerPrefs.SetFloat("MouseSensX", e.newValue));
        root.Q<Slider>("slider-sens-y")?.RegisterValueChangedCallback(e => PlayerPrefs.SetFloat("MouseSensY", e.newValue));
        root.Q<Slider>("slider-smooth")?.RegisterValueChangedCallback(e => PlayerPrefs.SetFloat("MouseSmooth", e.newValue));
        root.Q<Toggle>("toggle-invert")?.RegisterValueChangedCallback(e => {
            PlayerPrefs.SetInt("MouseInvert", e.newValue ? 1 : 0);
        });
    }

    void OnLoginClicked() { PlayClick(); }
    void OnGotoRegisterClicked() { ShowScreen("register-screen"); PlayClick(); }
    void OnBackToLoginClicked() { ShowScreen("login-screen"); PlayClick(); }

    void StartCampaign()
    {
        PlayClick();
        ShowScreen("loading-screen");
        StartCoroutine(SimulateLoading());
    }

    IEnumerator SimulateLoading()
    {
        if (loadingProgress != null) loadingProgress.value = 0;
        float progress = 0;
        while(progress < 1f)
        {
            progress += Time.deltaTime * 0.5f;
            if (loadingProgress != null) loadingProgress.value = progress;
            yield return null;
        }
        
        if (TransitionManager.instance != null)
            TransitionManager.instance.ChangeScene();
        else
            Debug.LogWarning("No TransitionManager found to change scene.");
    }

    void UpdateAccountDetails()
    {
        var lblName = root.Q<Label>("lbl-account-name");
        var lblId = root.Q<Label>("lbl-account-id");
        if (lblName != null) lblName.text = "Username: " + PlayerPrefs.GetString("USERNAME", "Player");
        if (lblId != null) lblId.text = "Status: Online";
    }

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

    void ShowSettingsPanel(string panelName)
    {
        panelGame?.AddToClassList("hidden");
        panelVideo?.AddToClassList("hidden");
        panelControls?.AddToClassList("hidden");

        root.Q(panelName)?.RemoveFromClassList("hidden");

        root.Q<Button>("tab-game")?.RemoveFromClassList("active");
        root.Q<Button>("tab-video")?.RemoveFromClassList("active");
        root.Q<Button>("tab-controls")?.RemoveFromClassList("active");

        string tabName = panelName.Replace("panel", "tab");
        root.Q<Button>(tabName)?.AddToClassList("active");
        PlayClick();
    }

    void LoadSettingsIntoUI()
    {
        var fs = root.Q<Toggle>("toggle-fullscreen");
        if(fs != null) fs.value = Screen.fullScreen;
        
        var hud = root.Q<Toggle>("toggle-hud");
        if(hud != null) hud.value = PlayerPrefs.GetInt("ShowHUD") == 1;

        // Mouse Settings
        var sensX = root.Q<Slider>("slider-sens-x");
        if(sensX != null) sensX.value = PlayerPrefs.GetFloat("MouseSensX", 2f);

        var sensY = root.Q<Slider>("slider-sens-y");
        if(sensY != null) sensY.value = PlayerPrefs.GetFloat("MouseSensY", 2f);

        var smooth = root.Q<Slider>("slider-smooth");
        if(smooth != null) smooth.value = PlayerPrefs.GetFloat("MouseSmooth", 3f);

        var invert = root.Q<Toggle>("toggle-invert");
        if(invert != null) invert.value = PlayerPrefs.GetInt("MouseInvert", 0) == 1;
    }

    void Position2() { if(cameraAnimator) cameraAnimator.SetFloat("Animate", 1); }
    void Position1() { if(cameraAnimator) cameraAnimator.SetFloat("Animate", 0); }
    void PlayHover() { if(hoverSound) hoverSound.Play(); }
    void PlayClick() { if(clickSound) clickSound.Play(); }
    void PlaySwoosh() { if(swooshSound) swooshSound.Play(); }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
