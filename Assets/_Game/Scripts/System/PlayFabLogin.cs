using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class PlayFabLogin : MonoBehaviour
{
    private TextField user_Login;
    private TextField pass_Login;
    private Label message_Login;
    private Button loginButton;

    public static string DisplayNameFromPlayFab;

    private Coroutine coroutine_Login;
    private bool isProcessing = false;

    private UIToolkitMenuController menuController;

    public void Initialize(VisualElement root, UIToolkitMenuController controller)
    {
        Deinitialize();

        if (root == null) return;

        menuController = controller;
        user_Login = root.Q<TextField>("login-username");
        pass_Login = root.Q<TextField>("login-password");
        message_Login = root.Q<Label>("login-message");

        loginButton = root.Q<Button>("btn-login");
        if (loginButton != null)
        {
            loginButton.clicked += Login;
        }
    }

    public void Deinitialize()
    {
        if (loginButton != null)
        {
            loginButton.clicked -= Login;
            loginButton = null;
        }

        CancelInvoke(nameof(AutoSwitchToMainMenu));

        if (coroutine_Login != null)
        {
            StopCoroutine(coroutine_Login);
            coroutine_Login = null;
        }

        user_Login = null;
        pass_Login = null;
        message_Login = null;
        menuController = null;
        isProcessing = false;
    }

    private void OnDisable()
    {
        Deinitialize();
    }

    public void Login()
    {
        if (isProcessing) return;

        if (user_Login == null || pass_Login == null)
        {
            ShowMessage("Login form is not ready.");
            return;
        }

        if (string.IsNullOrEmpty(user_Login.value) || string.IsNullOrEmpty(pass_Login.value))
        {
            ShowMessage("Please enter username and password!");
            return;
        }

        if (pass_Login.value.Length < 6)
        {
            ShowMessage("Password must be at least 6 characters!");
            return;
        }

        isProcessing = true;
        ShowMessage("Logging in...");

        var request = new LoginWithPlayFabRequest
        {
            Username = user_Login.value,
            Password = pass_Login.value,

            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

        PlayFabClientAPI.LoginWithPlayFab(request, OnLoginSuccess, OnError);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        if (!isActiveAndEnabled || user_Login == null)
        {
            isProcessing = false;
            return;
        }

        if (result.InfoResultPayload != null && result.InfoResultPayload.PlayerProfile != null &&
            !string.IsNullOrEmpty(result.InfoResultPayload.PlayerProfile.DisplayName))
        {
            DisplayNameFromPlayFab = result.InfoResultPayload.PlayerProfile.DisplayName;
        }
        else
        {
            DisplayNameFromPlayFab = user_Login.value;
        }

        Debug.Log("Login Successful! Player Name: " + DisplayNameFromPlayFab);
        ShowMessage("Login Successful!");

        Invoke(nameof(AutoSwitchToMainMenu), 2f);
    }

    private void AutoSwitchToMainMenu()
    {
        if (menuController != null)
        {
            menuController.ShowScreen("main-menu-screen");
        }
    }

    private void OnError(PlayFabError error)
    {
        isProcessing = false;
        ShowMessage(error.ErrorMessage);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void ShowMessage(string text)
    {
        if (message_Login == null) return;

        message_Login.text = text;
        message_Login.RemoveFromClassList("hidden");

        if (coroutine_Login != null) StopCoroutine(coroutine_Login);
        coroutine_Login = StartCoroutine(HideMessageRoutine());
    }

    private IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(3f);
        message_Login.AddToClassList("hidden");
        coroutine_Login = null;
    }
}
