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
    private VisualElement panelGame, panelVideo, panelControls;
    private Label loginMessage, regMessage;

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

        panelGame = root.Q("panel-game");
        panelVideo = root.Q("panel-video");
        panelControls = root.Q("panel-controls");

        loginMessage = root.Q<Label>("login-message");
        regMessage = root.Q<Label>("reg-message");
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

        var btnPlay = root.Q<Button>("btn-play");
        if(btnPlay != null) btnPlay.clicked += OnPlayClicked;

        var btnOptions = root.Q<Button>("btn-options");
        if(btnOptions != null) btnOptions.clicked += OnOptionsClicked;

        var btnExit = root.Q<Button>("btn-exit");
        if(btnExit != null) btnExit.clicked += OnExitClicked;

        var btnPlayBack = root.Q<Button>("btn-play-back");
        if(btnPlayBack != null) btnPlayBack.clicked += OnBackToMainClicked;

        var btnSettingsBack = root.Q<Button>("btn-settings-back");
        if(btnSettingsBack != null) btnSettingsBack.clicked += OnSettingsBackClicked;

        var btnExitNo = root.Q<Button>("btn-exit-no");
        if(btnExitNo != null) btnExitNo.clicked += OnBackToMainClicked;

        var btnExitYes = root.Q<Button>("btn-exit-yes");
        if(btnExitYes != null) btnExitYes.clicked += QuitGame;

        // Tabs
        root.Q<Button>("tab-game")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-game"));
        root.Q<Button>("tab-video")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-video"));
        root.Q<Button>("tab-controls")?.RegisterCallback<ClickEvent>(e => ShowSettingsPanel("panel-controls"));

        // Hover Sounds
        root.Query<Button>().ForEach(btn => {
            btn.RegisterCallback<PointerOverEvent>(evt => PlayHover());
        });
    }

    void OnLoginClicked() { PlayClick(); }
    void OnGotoRegisterClicked() { ShowScreen("register-screen"); PlayClick(); }
    void OnBackToLoginClicked() { ShowScreen("login-screen"); PlayClick(); }
    void OnPlayClicked() { ShowScreen("play-menu-screen"); PlayClick(); }
    void OnOptionsClicked() { ShowScreen("settings-screen"); PlaySwoosh(); Position2(); }
    void OnExitClicked() { ShowScreen("exit-screen"); PlayClick(); }
    void OnBackToMainClicked() { ShowScreen("main-menu-screen"); PlayClick(); }
    void OnSettingsBackClicked() { ShowScreen("main-menu-screen"); Position1(); PlayClick(); }

    public void ShowScreen(string screenName)
    {
        screenMain?.AddToClassList("hidden");
        screenPlay?.AddToClassList("hidden");
        screenSettings?.AddToClassList("hidden");
        screenExit?.AddToClassList("hidden");
        screenLogin?.AddToClassList("hidden");
        screenRegister?.AddToClassList("hidden");

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
