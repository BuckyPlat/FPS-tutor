using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using System.Collections;

public class PlayFabRegister : MonoBehaviour
{
    [Header("Register UI")]
    public TextMeshPro message_Reg;
    public TMP_InputField user_Reg;
    public TMP_InputField pass_Reg;
    public TMP_InputField display_Reg;

    private Coroutine coroutine_Reg;

    private void Start()
    {
        if (message_Reg != null)
            message_Reg.gameObject.SetActive(false);
    }

    public void RegisterAccount()
    {
        if (string.IsNullOrEmpty(user_Reg.text) || string.IsNullOrEmpty(pass_Reg.text))
        {
            ShowMessage("Please fill in Username and Password!");
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Username = user_Reg.text,
            Password = pass_Reg.text,
            RequireBothUsernameAndEmail = false
        };
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnError);
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        UpdateDisplayName();
    }

    private void UpdateDisplayName()
    {
        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = display_Reg.text
        };
        PlayFabClientAPI.UpdateUserTitleDisplayName(request, OnDisplayNameUpdate, OnError);
    }

    private void OnDisplayNameUpdate(UpdateUserTitleDisplayNameResult result)
    {
        ShowMessage("Account created successfully!");
        // BỎ VIỆC BACK TO LOGIN (NGƯỜI DÙNG SẼ ON/OFF PANEL THỦ CÔNG BẰNG BUTTON)
    }

    private void OnError(PlayFabError error)
    {
        ShowMessage(error.ErrorMessage);
        Debug.LogError(error.GenerateErrorReport());
    }

    private void ShowMessage(string text)
    {
        if (message_Reg == null) return;
        message_Reg.text = text;
        message_Reg.gameObject.SetActive(true);

        if (coroutine_Reg != null) StopCoroutine(coroutine_Reg);
        coroutine_Reg = StartCoroutine(HideMessageRoutine());
    }

    private IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(3f);
        message_Reg.gameObject.SetActive(false);
        coroutine_Reg = null;
    }
}