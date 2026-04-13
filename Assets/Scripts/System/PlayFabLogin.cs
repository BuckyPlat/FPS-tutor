using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayFabLogin : MonoBehaviour
{
    [Header("Login UI")]
    public TextMeshPro message_Login;
    public TMP_InputField user_Login;
    public TMP_InputField pass_Login;

    public static string DisplayNameFromPlayFab;

    private Coroutine coroutine_Login;
    private bool isProcessing = false;

    // ==================== NEWLY ADDED ====================
    [Header("After Login")]
    public GameObject mainMenuPanel;     // Drag Main Menu Panel here

    private void Start()
    {
        if (message_Login != null)
            message_Login.gameObject.SetActive(false);
    }

    public void Login()
    {
        if (isProcessing) return;

        if (string.IsNullOrEmpty(user_Login.text) || string.IsNullOrEmpty(pass_Login.text))
        {
            ShowMessage("Please enter username and password!");
            return;
        }

        if (pass_Login.text.Length < 6)
        {
            ShowMessage("Password must be at least 6 characters!");
            return;
        }

        isProcessing = true;
        ShowMessage("Logging in...");

        var request = new LoginWithPlayFabRequest
        {
            Username = user_Login.text,
            Password = pass_Login.text,

            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

        PlayFabClientAPI.LoginWithPlayFab(request, OnLoginSuccess, OnError);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        if (result.InfoResultPayload != null && result.InfoResultPayload.PlayerProfile != null &&
            !string.IsNullOrEmpty(result.InfoResultPayload.PlayerProfile.DisplayName))
        {
            DisplayNameFromPlayFab = result.InfoResultPayload.PlayerProfile.DisplayName;
        }
        else
        {
            DisplayNameFromPlayFab = user_Login.text;
        }

        Debug.Log("Login Successful! Player Name: " + DisplayNameFromPlayFab);

        ShowMessage("Login Successful!");

        // Automatically close Login Panel and open Main Menu Panel after 3 seconds
        Invoke(nameof(AutoSwitchToMainMenu), 3f);
    }

    // New method: Auto close Login and open Main Menu after 3 seconds
    private void AutoSwitchToMainMenu()
    {
        // Close current Login panel
        gameObject.SetActive(false);

        // Open Main Menu panel
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("Automatically switched from Login to Main Menu Panel");
        }
        else
        {
            Debug.LogWarning("Main Menu Panel is not assigned in PlayFabLogin!");
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
        message_Login.gameObject.SetActive(true);

        if (coroutine_Login != null) StopCoroutine(coroutine_Login);
        coroutine_Login = StartCoroutine(HideMessageRoutine());
    }

    private IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(3f);
        message_Login.gameObject.SetActive(false);
        coroutine_Login = null;
    }
}