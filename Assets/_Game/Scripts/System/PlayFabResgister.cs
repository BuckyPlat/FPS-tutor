using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class PlayFabRegister : MonoBehaviour
{
    private TextField user_Reg;
    private TextField pass_Reg;
    private TextField display_Reg;
    private Label message_Reg;
    private Button registerButton;

    private Coroutine coroutine_Reg;
    private bool isProcessing = false;

    public void Initialize(VisualElement root)
    {
        Deinitialize();

        if (root == null) return;

        user_Reg = root.Q<TextField>("reg-username");
        pass_Reg = root.Q<TextField>("reg-password");
        display_Reg = root.Q<TextField>("reg-displayname");
        message_Reg = root.Q<Label>("reg-message");

        registerButton = root.Q<Button>("btn-register");
        if (registerButton != null)
        {
            registerButton.clicked += RegisterAccount;
        }
    }

    public void Deinitialize()
    {
        if (registerButton != null)
        {
            registerButton.clicked -= RegisterAccount;
            registerButton = null;
        }

        if (coroutine_Reg != null)
        {
            StopCoroutine(coroutine_Reg);
            coroutine_Reg = null;
        }

        user_Reg = null;
        pass_Reg = null;
        display_Reg = null;
        message_Reg = null;
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
            ShowMessage("Please fill in all fields!");
            return;
        }

        if (pass_Reg.value.Length < 6)
        {
            ShowMessage("Password must be at least 6 characters!");
            return;
        }

        isProcessing = true;
        ShowMessage("Registering...");

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
        ShowMessage("Account created successfully!");
    }

    private void OnRegisterError(PlayFabError error)
    {
        isProcessing = false;
        ShowMessage(error.ErrorMessage);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void OnError(PlayFabError error)
    {
        isProcessing = false;
        ShowMessage(error.ErrorMessage);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void ShowMessage(string text)
    {
        if (message_Reg == null) return;
        message_Reg.text = text;
        message_Reg.RemoveFromClassList("hidden");

        if (coroutine_Reg != null) StopCoroutine(coroutine_Reg);
        coroutine_Reg = StartCoroutine(HideMessageRoutine());
    }

    private IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(3f);
        message_Reg.AddToClassList("hidden");
        coroutine_Reg = null;
    }
}
