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

    // ==================== THÊM MỚI ====================
    [Header("After Login")]
    public GameObject mainMenuPanel;     // Kéo Panel Main Menu vào đây

    private void Start()
    {
        if (message_Login != null)
            message_Login.gameObject.SetActive(false);
    }

    public void Login()
    {
        if (string.IsNullOrEmpty(user_Login.text) || string.IsNullOrEmpty(pass_Login.text))
        {
            ShowMessage("Please enter username and password!");
            return;
        }

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

        // Sau 3 giây tự động đóng Login Panel và mở Main Menu Panel
        Invoke(nameof(AutoSwitchToMainMenu), 3f);
    }

    // Hàm mới: Tự động đóng Login và mở Main Menu sau 3 giây
    private void AutoSwitchToMainMenu()
    {
        // Đóng panel Login hiện tại
        gameObject.SetActive(false);

        // Mở panel Main Menu
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("Đã tự động chuyển từ Login sang Main Menu Panel");
        }
        else
        {
            Debug.LogWarning("Main Menu Panel chưa được gán trong PlayFabLogin!");
        }
    }

    private void OnError(PlayFabError error)
    {
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