using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class PlayFabRegister : MonoBehaviour
{
    private const float ReturnToLoginDelay = 1.5f;
    private const string MessageInfoClass = "status-info";
    private const string MessageSuccessClass = "status-success";
    private const string MessageErrorClass = "status-error";

    private TextField user_Reg;
    private TextField pass_Reg;
    private TextField display_Reg;
    private VisualElement messageRow_Reg;
    private Label message_Reg;
    private Label messageBadge_Reg;
    private Button registerButton;
    private Button backToLoginButton;
    private VisualElement registerPanel;
    private string registerButtonDefaultText = "Create Account";

    private Coroutine coroutine_Reg;
    private bool isProcessing = false;
    private string pendingRegisteredUsername;

    private UIToolkitMenuController menuController;

    public void Initialize(VisualElement root, UIToolkitMenuController controller)
    {
        Deinitialize();

        if (root == null) return;

        menuController = controller;
        user_Reg = root.Q<TextField>("reg-username");
        pass_Reg = root.Q<TextField>("reg-password");
        display_Reg = root.Q<TextField>("reg-displayname");
        messageRow_Reg = root.Q("reg-message-row");
        message_Reg = root.Q<Label>("reg-message");
        messageBadge_Reg = root.Q<Label>("reg-message-badge");
        registerPanel = root.Q("register-panel");

        registerButton = root.Q<Button>("btn-register");
        backToLoginButton = root.Q<Button>("btn-back-to-login");
        if (registerButton != null)
        {
            registerButtonDefaultText = registerButton.text;
            registerButton.clicked += RegisterAccount;
        }

        user_Reg?.RegisterValueChangedCallback(OnRegisterFieldChanged);
        pass_Reg?.RegisterValueChangedCallback(OnRegisterFieldChanged);
        display_Reg?.RegisterValueChangedCallback(OnRegisterFieldChanged);
        user_Reg?.RegisterCallback<FocusInEvent>(OnRegisterFieldFocusIn);
        user_Reg?.RegisterCallback<FocusOutEvent>(OnRegisterFieldFocusOut);
        pass_Reg?.RegisterCallback<FocusInEvent>(OnRegisterFieldFocusIn);
        pass_Reg?.RegisterCallback<FocusOutEvent>(OnRegisterFieldFocusOut);
        display_Reg?.RegisterCallback<FocusInEvent>(OnRegisterFieldFocusIn);
        display_Reg?.RegisterCallback<FocusOutEvent>(OnRegisterFieldFocusOut);
    }

    public void Deinitialize()
    {
        SetBusyState(false);

        if (registerButton != null)
        {
            registerButton.clicked -= RegisterAccount;
            registerButton = null;
        }

        backToLoginButton = null;

        user_Reg?.UnregisterValueChangedCallback(OnRegisterFieldChanged);
        pass_Reg?.UnregisterValueChangedCallback(OnRegisterFieldChanged);
        display_Reg?.UnregisterValueChangedCallback(OnRegisterFieldChanged);
        user_Reg?.UnregisterCallback<FocusInEvent>(OnRegisterFieldFocusIn);
        user_Reg?.UnregisterCallback<FocusOutEvent>(OnRegisterFieldFocusOut);
        pass_Reg?.UnregisterCallback<FocusInEvent>(OnRegisterFieldFocusIn);
        pass_Reg?.UnregisterCallback<FocusOutEvent>(OnRegisterFieldFocusOut);
        display_Reg?.UnregisterCallback<FocusInEvent>(OnRegisterFieldFocusIn);
        display_Reg?.UnregisterCallback<FocusOutEvent>(OnRegisterFieldFocusOut);

        if (coroutine_Reg != null)
        {
            StopCoroutine(coroutine_Reg);
            coroutine_Reg = null;
        }

        CancelInvoke(nameof(ReturnToLoginAfterSuccess));

        user_Reg = null;
        pass_Reg = null;
        display_Reg = null;
        messageRow_Reg = null;
        message_Reg = null;
        messageBadge_Reg = null;
        pendingRegisteredUsername = null;
        menuController = null;
        isProcessing = false;
    }

    private void OnDisable()
    {
        Deinitialize();
    }

    public void RegisterAccount()
    {
        if (isProcessing) return;

        if (user_Reg == null || pass_Reg == null || display_Reg == null)
        {
            ShowMessage("Registration form is not ready.");
            return;
        }

        if (string.IsNullOrEmpty(user_Reg.value) || string.IsNullOrEmpty(pass_Reg.value) || string.IsNullOrEmpty(display_Reg.value))
        {
            SetFieldErrorState(display_Reg, string.IsNullOrEmpty(display_Reg.value));
            SetFieldErrorState(user_Reg, string.IsNullOrEmpty(user_Reg.value));
            SetFieldErrorState(pass_Reg, string.IsNullOrEmpty(pass_Reg.value));
            ShowMessage("Fill in display name, username, and password.", MessageErrorClass);
            return;
        }

        if (pass_Reg.value.Length < 6)
        {
            SetFieldErrorState(pass_Reg, true);
            ShowMessage("Password must be at least 6 characters.", MessageErrorClass);
            return;
        }

        isProcessing = true;
        SetBusyState(true, "Creating...");
        ClearFieldErrors();
        ShowMessage("Creating operator profile...", MessageInfoClass, false);

        var request = new RegisterPlayFabUserRequest
        {
            Username = user_Reg.value,
            Password = pass_Reg.value,
            RequireBothUsernameAndEmail = false
        };
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnRegisterError);
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        if (!isActiveAndEnabled || display_Reg == null)
        {
            isProcessing = false;
            SetBusyState(false);
            return;
        }

        UpdateDisplayName();
    }

    private void UpdateDisplayName()
    {
        if (display_Reg == null)
        {
            isProcessing = false;
            return;
        }

        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = display_Reg.value
        };
        PlayFabClientAPI.UpdateUserTitleDisplayName(request, OnDisplayNameUpdate, OnError);
    }

    private void OnDisplayNameUpdate(UpdateUserTitleDisplayNameResult result)
    {
        isProcessing = false;
        pendingRegisteredUsername = user_Reg != null ? user_Reg.value : string.Empty;
        ShowMessage("Account created. Returning to sign in...", MessageSuccessClass, false);
        SetBusyState(true, "Returning...");
        Invoke(nameof(ReturnToLoginAfterSuccess), ReturnToLoginDelay);
    }

    private void OnRegisterError(PlayFabError error)
    {
        isProcessing = false;
        SetBusyState(false);
        SetFieldErrorState(display_Reg, true);
        SetFieldErrorState(user_Reg, true);
        SetFieldErrorState(pass_Reg, true);
        ShowMessage(error.ErrorMessage, MessageErrorClass);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void OnError(PlayFabError error)
    {
        isProcessing = false;
        SetBusyState(false);
        ShowMessage(error.ErrorMessage, MessageErrorClass);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void ReturnToLoginAfterSuccess()
    {
        string username = pendingRegisteredUsername ?? string.Empty;

        menuController?.ShowScreen("login-screen");
        menuController?.playFabLogin?.PrepareAfterRegistration(username);

        SetBusyState(false);
        ResetRegisterFormState();
        pendingRegisteredUsername = null;
    }

    private void ShowMessage(string text, string statusClass = MessageErrorClass, bool autoHide = true)
    {
        if (message_Reg == null) return;
        message_Reg.text = text;
        messageRow_Reg?.RemoveFromClassList("hidden");
        messageRow_Reg?.RemoveFromClassList(MessageInfoClass);
        messageRow_Reg?.RemoveFromClassList(MessageSuccessClass);
        messageRow_Reg?.RemoveFromClassList(MessageErrorClass);
        messageRow_Reg?.AddToClassList(statusClass);
        UpdateBadge(statusClass);

        if (coroutine_Reg != null)
        {
            StopCoroutine(coroutine_Reg);
            coroutine_Reg = null;
        }

        if (autoHide)
        {
            coroutine_Reg = StartCoroutine(HideMessageRoutine());
        }
    }

    private void UpdateBadge(string statusClass)
    {
        if (messageBadge_Reg == null)
        {
            return;
        }

        messageBadge_Reg.RemoveFromClassList(MessageInfoClass);
        messageBadge_Reg.RemoveFromClassList(MessageSuccessClass);
        messageBadge_Reg.RemoveFromClassList(MessageErrorClass);
        messageBadge_Reg.AddToClassList(statusClass);

        messageBadge_Reg.text = statusClass switch
        {
            MessageInfoClass => "INFO",
            MessageSuccessClass => "OK",
            _ => "ERR"
        };
    }

    private void SetBusyState(bool busy, string busyText = null)
    {
        if (registerButton == null)
        {
            return;
        }

        registerButton.SetEnabled(!busy);
        backToLoginButton?.SetEnabled(!busy);
        display_Reg?.SetEnabled(!busy);
        user_Reg?.SetEnabled(!busy);
        pass_Reg?.SetEnabled(!busy);
        registerPanel?.EnableInClassList("auth-panel-busy", busy);
        registerButton.text = busy && !string.IsNullOrEmpty(busyText) ? busyText : registerButtonDefaultText;
    }

    private void ClearFieldErrors()
    {
        SetFieldErrorState(display_Reg, false);
        SetFieldErrorState(user_Reg, false);
        SetFieldErrorState(pass_Reg, false);
    }

    private void ResetRegisterFormState()
    {
        if (coroutine_Reg != null)
        {
            StopCoroutine(coroutine_Reg);
            coroutine_Reg = null;
        }

        display_Reg?.SetValueWithoutNotify(string.Empty);
        user_Reg?.SetValueWithoutNotify(string.Empty);
        pass_Reg?.SetValueWithoutNotify(string.Empty);
        ClearFieldErrors();

        if (message_Reg != null)
        {
            message_Reg.text = string.Empty;
        }

        messageRow_Reg?.AddToClassList("hidden");
        messageRow_Reg?.RemoveFromClassList(MessageInfoClass);
        messageRow_Reg?.RemoveFromClassList(MessageSuccessClass);
        messageRow_Reg?.RemoveFromClassList(MessageErrorClass);
        messageBadge_Reg?.RemoveFromClassList(MessageInfoClass);
        messageBadge_Reg?.RemoveFromClassList(MessageSuccessClass);
        messageBadge_Reg?.RemoveFromClassList(MessageErrorClass);
        if (messageBadge_Reg != null)
        {
            messageBadge_Reg.text = "ERR";
        }
    }

    private static void SetFieldErrorState(TextField field, bool isError)
    {
        if (field == null)
        {
            return;
        }

        field.EnableInClassList("auth-field-error", isError);
    }

    private void OnRegisterFieldChanged(ChangeEvent<string> evt)
    {
        if (evt.target is TextField field)
        {
            SetFieldErrorState(field, false);
        }
    }

    private void OnRegisterFieldFocusIn(FocusInEvent evt)
    {
        if (evt.target is TextField field)
        {
            field.AddToClassList("auth-field-focus");
        }
    }

    private void OnRegisterFieldFocusOut(FocusOutEvent evt)
    {
        if (evt.target is TextField field)
        {
            field.RemoveFromClassList("auth-field-focus");
        }
    }

    private IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (message_Reg != null)
        {
            messageRow_Reg?.AddToClassList("hidden");
            messageRow_Reg?.RemoveFromClassList(MessageInfoClass);
            messageRow_Reg?.RemoveFromClassList(MessageSuccessClass);
            messageRow_Reg?.RemoveFromClassList(MessageErrorClass);
            messageBadge_Reg?.RemoveFromClassList(MessageInfoClass);
            messageBadge_Reg?.RemoveFromClassList(MessageSuccessClass);
            messageBadge_Reg?.RemoveFromClassList(MessageErrorClass);
        }
        coroutine_Reg = null;
    }
}
