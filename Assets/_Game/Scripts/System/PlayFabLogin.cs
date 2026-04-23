using System;
using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayFabLogin : MonoBehaviour
{
    private const string MessageInfoClass = "status-info";
    private const string MessageSuccessClass = "status-success";
    private const string MessageErrorClass = "status-error";
    private const string RememberedCustomIdKey = "PlayFabRememberCustomId";
    private const string UsernameKey = "USERNAME";
    private const string LegacySessionTicketKey = "PlayFabSessionTicket";
    private const float SuccessTransitionDelay = 2f;

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
    private int authOperationVersion;

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

        ResetVisualState(clearUsername: true, clearPassword: true, clearMessage: true);
    }

    public void Deinitialize()
    {
        AbortPendingOperations();

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

        string submittedUsername = user_Login.value.Trim();

        int operationId = BeginOperation("Signing In...");
        ClearFieldErrors();
        ShowMessage("Checking credentials...", MessageInfoClass, false);

        var request = new LoginWithPlayFabRequest
        {
            Username = submittedUsername,
            Password = pass_Login.value,

            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

        PlayFabClientAPI.LoginWithPlayFab(
            request,
            result => OnManualLoginSuccess(operationId, result, submittedUsername),
            error => OnLoginError(operationId, error, true));
    }

    public void TryAutoLogin()
    {
        if (isProcessing || user_Login == null || pass_Login == null)
        {
            return;
        }

        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            DisplayNameFromPlayFab ??= PlayerPrefs.GetString(UsernameKey, string.Empty);
            AutoSwitchToMainMenu();
            return;
        }

        string customId = PlayerPrefs.GetString(RememberedCustomIdKey, string.Empty);
        if (string.IsNullOrEmpty(customId))
        {
            return;
        }

        ResetVisualState(clearUsername: true, clearPassword: true, clearMessage: true);

        int operationId = BeginOperation("Restoring...");
        ShowMessage("Restoring remembered session...", MessageInfoClass, false);

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = false,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

        PlayFabClientAPI.LoginWithCustomID(
            request,
            result => OnAutoLoginSuccess(operationId, result),
            error => OnAutoLoginError(operationId, error));
    }

    public void LogoutToLoginScreen()
    {
        if (menuController != null)
        {
            menuController.ShowScreen("login-screen");
        }

        ResetVisualState(clearUsername: true, clearPassword: true, clearMessage: true);

        string rememberedCustomId = PlayerPrefs.GetString(RememberedCustomIdKey, string.Empty);
        bool canUnlinkRememberedSession = !string.IsNullOrEmpty(rememberedCustomId) && PlayFabClientAPI.IsClientLoggedIn();

        if (!canUnlinkRememberedSession)
        {
            FinishLogoutCleanup();
            ShowMessage("Signed out. Sign in to continue.", MessageInfoClass);
            return;
        }

        int operationId = BeginOperation("Signing Out...");
        ShowMessage("Clearing remembered session...", MessageInfoClass, false);

        var request = new UnlinkCustomIDRequest
        {
            CustomId = rememberedCustomId
        };

        PlayFabClientAPI.UnlinkCustomID(
            request,
            _ => OnLogoutUnlinkComplete(operationId, null),
            error => OnLogoutUnlinkComplete(operationId, error));
    }

    public void PrepareAfterRegistration(string username)
    {
        if (user_Login == null || pass_Login == null)
        {
            return;
        }

        AbortPendingOperations();
        ResetVisualState(clearUsername: false, clearPassword: true, clearMessage: true);
        user_Login.SetValueWithoutNotify(username ?? string.Empty);
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

    private void OnManualLoginSuccess(int operationId, LoginResult result, string fallbackUsername)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        CompleteLoginSuccess(result, fallbackUsername, immediateTransition: false);
        ShowMessage("Login successful. Opening range console...", MessageSuccessClass);
        BeginRememberedSessionLink(operationId);
        Invoke(nameof(AutoSwitchToMainMenu), SuccessTransitionDelay);
    }

    private void OnAutoLoginSuccess(int operationId, LoginResult result)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        CompleteLoginSuccess(result, PlayerPrefs.GetString(UsernameKey, string.Empty), immediateTransition: true);
    }

    private void AutoSwitchToMainMenu()
    {
        if (menuController != null)
        {
            menuController.ShowScreen("main-menu-screen");
        }

        SetBusyState(false);
    }

    private void OnAutoLoginError(int operationId, PlayFabError error)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        ClearRememberedSession();
        DisplayNameFromPlayFab = null;
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.Save();
        ResetIdleState();
        ResetVisualState(clearUsername: true, clearPassword: true, clearMessage: true);
        ShowMessage("Saved session expired. Sign in again.", MessageInfoClass);
        Debug.LogWarning("[PlayFabLogin] Auto-login failed: " + error.ErrorMessage);
        Debug.LogWarning(error.GenerateErrorReport());
    }

    private void OnLoginError(int operationId, PlayFabError error, bool setFieldErrors)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        ResetIdleState();

        if (setFieldErrors)
        {
            SetFieldErrorState(user_Login, true);
            SetFieldErrorState(pass_Login, true);
        }

        ShowMessage(error.ErrorMessage, MessageErrorClass);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void OnLogoutUnlinkComplete(int operationId, PlayFabError error)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        if (error != null)
        {
            Debug.LogWarning("[PlayFabLogin] Failed to unlink remembered session during logout: " + error.ErrorMessage);
            Debug.LogWarning(error.GenerateErrorReport());
        }

        FinishLogoutCleanup();
        ShowMessage("Signed out. Sign in to continue.", MessageInfoClass);
    }

    private void BeginRememberedSessionLink(int operationId)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        string customId = PlayerPrefs.GetString(RememberedCustomIdKey, string.Empty);
        if (string.IsNullOrEmpty(customId))
        {
            customId = Guid.NewGuid().ToString("N");
        }

        var request = new LinkCustomIDRequest
        {
            CustomId = customId,
            ForceLink = false
        };

        PlayFabClientAPI.LinkCustomID(
            request,
            _ => OnRememberedSessionLinked(operationId, customId),
            error => OnRememberedSessionLinkFailed(operationId, error));
    }

    private void OnRememberedSessionLinked(int operationId, string customId)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        PlayerPrefs.SetString(RememberedCustomIdKey, customId);
        PlayerPrefs.Save();
        Debug.Log("[PlayFabLogin] Remembered session linked.");
    }

    private void OnRememberedSessionLinkFailed(int operationId, PlayFabError error)
    {
        if (!IsOperationCurrent(operationId))
        {
            return;
        }

        ClearRememberedSession();
        Debug.LogWarning("[PlayFabLogin] Remembered session link failed: " + error.ErrorMessage);
        Debug.LogWarning(error.GenerateErrorReport());
    }

    private void CompleteLoginSuccess(LoginResult result, string fallbackUsername, bool immediateTransition)
    {
        DisplayNameFromPlayFab = ResolveDisplayName(result, fallbackUsername);
        CacheDisplayName(DisplayNameFromPlayFab);

        isProcessing = false;
        Debug.Log("Login Successful! Player Name: " + DisplayNameFromPlayFab);

        if (immediateTransition)
        {
            AutoSwitchToMainMenu();
        }
    }

    private static string ResolveDisplayName(LoginResult result, string fallbackUsername)
    {
        if (result.InfoResultPayload != null &&
            result.InfoResultPayload.PlayerProfile != null &&
            !string.IsNullOrEmpty(result.InfoResultPayload.PlayerProfile.DisplayName))
        {
            return result.InfoResultPayload.PlayerProfile.DisplayName;
        }

        return string.IsNullOrEmpty(fallbackUsername) ? "OPERATOR" : fallbackUsername;
    }

    private static void CacheDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            PlayerPrefs.DeleteKey(UsernameKey);
        }
        else
        {
            PlayerPrefs.SetString(UsernameKey, displayName);
        }

        PlayerPrefs.Save();
    }

    private void FinishLogoutCleanup()
    {
        AbortPendingOperations();
        try
        {
            PlayFabClientAPI.ForgetAllCredentials();
        }
        catch
        {
        }

        DisplayNameFromPlayFab = null;
        PlayerPrefs.DeleteKey(UsernameKey);
        ClearRememberedSession();
        PlayerPrefs.DeleteKey(LegacySessionTicketKey);
        PlayerPrefs.Save();
        ResetVisualState(clearUsername: true, clearPassword: true, clearMessage: true);
        Debug.Log("[PlayFabLogin] Logged out.");
    }

    private static void ClearRememberedSession()
    {
        PlayerPrefs.DeleteKey(RememberedCustomIdKey);
        PlayerPrefs.DeleteKey(LegacySessionTicketKey);
        PlayerPrefs.Save();
    }

    private int BeginOperation(string busyText)
    {
        AbortPendingOperations();
        isProcessing = true;
        SetBusyState(true, busyText);
        return authOperationVersion;
    }

    private void AbortPendingOperations()
    {
        authOperationVersion++;
        isProcessing = false;
        CancelInvoke(nameof(AutoSwitchToMainMenu));

        if (coroutine_Login != null)
        {
            StopCoroutine(coroutine_Login);
            coroutine_Login = null;
        }

        SetBusyState(false);
    }

    private void ResetIdleState()
    {
        isProcessing = false;
        SetBusyState(false);
    }

    private bool IsOperationCurrent(int operationId)
    {
        return isActiveAndEnabled && operationId == authOperationVersion;
    }

    private void ResetVisualState(bool clearUsername, bool clearPassword, bool clearMessage)
    {
        if (clearUsername)
        {
            user_Login?.SetValueWithoutNotify(string.Empty);
        }

        if (clearPassword)
        {
            pass_Login?.SetValueWithoutNotify(string.Empty);
        }

        ClearFieldErrors();
        RemoveFieldFocusState(user_Login);
        RemoveFieldFocusState(pass_Login);

        if (clearMessage)
        {
            ClearMessage();
        }
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

    private static void RemoveFieldFocusState(TextField field)
    {
        field?.RemoveFromClassList("auth-field-focus");
    }

    private void ClearMessage()
    {
        if (message_Login != null)
        {
            message_Login.text = string.Empty;
        }

        messageRow_Login?.AddToClassList("hidden");
        messageRow_Login?.RemoveFromClassList(MessageInfoClass);
        messageRow_Login?.RemoveFromClassList(MessageSuccessClass);
        messageRow_Login?.RemoveFromClassList(MessageErrorClass);
        messageBadge_Login?.RemoveFromClassList(MessageInfoClass);
        messageBadge_Login?.RemoveFromClassList(MessageSuccessClass);
        messageBadge_Login?.RemoveFromClassList(MessageErrorClass);

        if (messageBadge_Login != null)
        {
            messageBadge_Login.text = "ERR";
        }
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
            ClearMessage();
        }
        coroutine_Login = null;
    }
}
