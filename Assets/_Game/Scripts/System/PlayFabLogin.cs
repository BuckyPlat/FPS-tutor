using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class PlayFabLogin : MonoBehaviour
{
    private const string MessageInfoClass = "status-info";
    private const string MessageSuccessClass = "status-success";
    private const string MessageErrorClass = "status-error";

    private TextField user_Login;
    private TextField pass_Login;
    private VisualElement messageRow_Login;
    private Label message_Login;
    private Label messageBadge_Login;
    private Button loginButton;
    private Button registerButton;
    private VisualElement loginPanel;
    private string loginButtonDefaultText = "Sign In";

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
        messageRow_Login = root.Q("login-message-row");
        message_Login = root.Q<Label>("login-message");
        messageBadge_Login = root.Q<Label>("login-message-badge");
        loginPanel = root.Q("login-panel");

        loginButton = root.Q<Button>("btn-login");
        registerButton = root.Q<Button>("btn-goto-register");
        if (loginButton != null)
        {
            loginButtonDefaultText = loginButton.text;
            loginButton.clicked += Login;
        }

        user_Login?.RegisterValueChangedCallback(OnLoginFieldChanged);
        pass_Login?.RegisterValueChangedCallback(OnLoginFieldChanged);
        user_Login?.RegisterCallback<FocusInEvent>(OnLoginFieldFocusIn);
        user_Login?.RegisterCallback<FocusOutEvent>(OnLoginFieldFocusOut);
        pass_Login?.RegisterCallback<FocusInEvent>(OnLoginFieldFocusIn);
        pass_Login?.RegisterCallback<FocusOutEvent>(OnLoginFieldFocusOut);
    }

    public void Deinitialize()
    {
        SetBusyState(false);

        if (loginButton != null)
        {
            loginButton.clicked -= Login;
            loginButton = null;
        }

        registerButton = null;

        user_Login?.UnregisterValueChangedCallback(OnLoginFieldChanged);
        pass_Login?.UnregisterValueChangedCallback(OnLoginFieldChanged);
        user_Login?.UnregisterCallback<FocusInEvent>(OnLoginFieldFocusIn);
        user_Login?.UnregisterCallback<FocusOutEvent>(OnLoginFieldFocusOut);
        pass_Login?.UnregisterCallback<FocusInEvent>(OnLoginFieldFocusIn);
        pass_Login?.UnregisterCallback<FocusOutEvent>(OnLoginFieldFocusOut);

        CancelInvoke(nameof(AutoSwitchToMainMenu));

        if (coroutine_Login != null)
        {
            StopCoroutine(coroutine_Login);
            coroutine_Login = null;
        }

        user_Login = null;
        pass_Login = null;
        messageRow_Login = null;
        message_Login = null;
        messageBadge_Login = null;
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
            SetFieldErrorState(user_Login, string.IsNullOrEmpty(user_Login.value));
            SetFieldErrorState(pass_Login, string.IsNullOrEmpty(pass_Login.value));
            ShowMessage("Enter both username and password.", MessageErrorClass);
            return;
        }

        if (pass_Login.value.Length < 6)
        {
            SetFieldErrorState(pass_Login, true);
            ShowMessage("Password must be at least 6 characters.", MessageErrorClass);
            return;
        }

        isProcessing = true;
        SetBusyState(true, "Signing In...");
        ClearFieldErrors();
        ShowMessage("Checking credentials...", MessageInfoClass, false);

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

    public void PrepareAfterRegistration(string username)
    {
        if (user_Login == null || pass_Login == null)
        {
            return;
        }

        CancelInvoke(nameof(AutoSwitchToMainMenu));

        if (coroutine_Login != null)
        {
            StopCoroutine(coroutine_Login);
            coroutine_Login = null;
        }

        isProcessing = false;
        SetBusyState(false);
        user_Login.SetValueWithoutNotify(username ?? string.Empty);
        pass_Login.SetValueWithoutNotify(string.Empty);
        ClearFieldErrors();
        ShowMessage("Account created. Enter your password to continue.", MessageSuccessClass);

        pass_Login.schedule.Execute(() =>
        {
            if (!isActiveAndEnabled || pass_Login == null)
            {
                return;
            }

            pass_Login.Focus();
        }).ExecuteLater(160);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        if (!isActiveAndEnabled || user_Login == null)
        {
            isProcessing = false;
            SetBusyState(false);
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
        ShowMessage("Login successful. Opening range console...", MessageSuccessClass);

        Invoke(nameof(AutoSwitchToMainMenu), 2f);
    }

    private void AutoSwitchToMainMenu()
    {
        if (menuController != null)
        {
            menuController.ShowScreen("main-menu-screen");
        }

        SetBusyState(false);
    }

    private void OnError(PlayFabError error)
    {
        isProcessing = false;
        SetBusyState(false);
        SetFieldErrorState(user_Login, true);
        SetFieldErrorState(pass_Login, true);
        ShowMessage(error.ErrorMessage, MessageErrorClass);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void ShowMessage(string text, string statusClass = MessageErrorClass, bool autoHide = true)
    {
        if (message_Login == null) return;

        message_Login.text = text;
        messageRow_Login?.RemoveFromClassList("hidden");
        messageRow_Login?.RemoveFromClassList(MessageInfoClass);
        messageRow_Login?.RemoveFromClassList(MessageSuccessClass);
        messageRow_Login?.RemoveFromClassList(MessageErrorClass);
        messageRow_Login?.AddToClassList(statusClass);
        UpdateBadge(statusClass);

        if (coroutine_Login != null)
        {
            StopCoroutine(coroutine_Login);
            coroutine_Login = null;
        }

        if (autoHide)
        {
            coroutine_Login = StartCoroutine(HideMessageRoutine());
        }
    }

    private void UpdateBadge(string statusClass)
    {
        if (messageBadge_Login == null)
        {
            return;
        }

        messageBadge_Login.RemoveFromClassList(MessageInfoClass);
        messageBadge_Login.RemoveFromClassList(MessageSuccessClass);
        messageBadge_Login.RemoveFromClassList(MessageErrorClass);
        messageBadge_Login.AddToClassList(statusClass);

        messageBadge_Login.text = statusClass switch
        {
            MessageInfoClass => "INFO",
            MessageSuccessClass => "OK",
            _ => "ERR"
        };
    }

    private void SetBusyState(bool busy, string busyText = null)
    {
        if (loginButton == null)
        {
            return;
        }

        loginButton.SetEnabled(!busy);
        registerButton?.SetEnabled(!busy);
        user_Login?.SetEnabled(!busy);
        pass_Login?.SetEnabled(!busy);
        loginPanel?.EnableInClassList("auth-panel-busy", busy);
        loginButton.text = busy && !string.IsNullOrEmpty(busyText) ? busyText : loginButtonDefaultText;
    }

    private void ClearFieldErrors()
    {
        SetFieldErrorState(user_Login, false);
        SetFieldErrorState(pass_Login, false);
    }

    private static void SetFieldErrorState(TextField field, bool isError)
    {
        if (field == null)
        {
            return;
        }

        field.EnableInClassList("auth-field-error", isError);
    }

    private void OnLoginFieldChanged(ChangeEvent<string> evt)
    {
        if (evt.target is TextField field)
        {
            SetFieldErrorState(field, false);
        }
    }

    private void OnLoginFieldFocusIn(FocusInEvent evt)
    {
        if (evt.target is TextField field)
        {
            field.AddToClassList("auth-field-focus");
        }
    }

    private void OnLoginFieldFocusOut(FocusOutEvent evt)
    {
        if (evt.target is TextField field)
        {
            field.RemoveFromClassList("auth-field-focus");
        }
    }

    private IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (message_Login != null)
        {
            messageRow_Login?.AddToClassList("hidden");
            messageRow_Login?.RemoveFromClassList(MessageInfoClass);
            messageRow_Login?.RemoveFromClassList(MessageSuccessClass);
            messageRow_Login?.RemoveFromClassList(MessageErrorClass);
            messageBadge_Login?.RemoveFromClassList(MessageInfoClass);
            messageBadge_Login?.RemoveFromClassList(MessageSuccessClass);
            messageBadge_Login?.RemoveFromClassList(MessageErrorClass);
        }
        coroutine_Login = null;
    }
}
