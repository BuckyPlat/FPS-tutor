using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameChat : MonoBehaviourPunCallbacks
{
    [Header("Chat UI")]
    public TextMeshProUGUI chatText;
    public TMP_InputField inputField;

    [Header("Settings")]
    public int maxMessages = 15;

    // Important variable: Chatting state
    public static bool IsChatting { get; private set; } = false;

    public static GameChat Instance;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (chatText != null)
            chatText.text = "<color=yellow>Welcome to the room!</color>\n";
    }

    void Update()
    {
        // ===== HANDLE OPEN / CLOSE CHAT =====
        if (Input.GetKeyDown(KeyCode.Y) && !IsChatting)
        {
            OpenChat();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && IsChatting)
        {
            CloseChat();
        }

        // ===== SEND MESSAGE =====
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            && IsChatting && !string.IsNullOrEmpty(inputField.text))
        {
            string messageToSend = $"<color=white>{PhotonNetwork.LocalPlayer.NickName}:</color> {inputField.text}";

            GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.All, messageToSend);

            inputField.text = "";
            CloseChat();        // Auto-close after sending
        }
    }

    // ====================== CHAT CONTROL ======================
    private void OpenChat()
    {
        IsChatting = true;
        inputField.Select();
        inputField.ActivateInputField();
        inputField.text = "";                    // Clear old content
    }

    private void CloseChat()
    {
        IsChatting = false;
        EventSystem.current.SetSelectedGameObject(null);
        inputField.DeactivateInputField();       // Important
    }

    // ====================== JOIN / LEAVE NOTIFICATION ======================
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (newPlayer == null) return;
        string msg = $"<color=#00FF00>→ {newPlayer.NickName} has joined the room</color>";
        AddToChat(msg);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer == null) return;
        string msg = $"<color=#FF4444>← {otherPlayer.NickName} has left the room</color>";
        AddToChat(msg);
    }

    public override void OnJoinedRoom()
    {
        AddToChat("<color=yellow>✓ You have successfully joined the room!</color>");
    }

    [PunRPC]
    public void SendSystemMessage(string _message)
    {
        AddToChat(_message);
    }

    [PunRPC]
    public void SendChatMessage(string _message)
    {
        AddToChat(_message);
    }

    private void AddToChat(string message)
    {
        if (chatText == null) return;

        chatText.text += "\n" + message;

        // Limit number of lines
        string[] lines = chatText.text.Split('\n');
        if (lines.Length > maxMessages)
        {
            chatText.text = string.Join("\n", lines, lines.Length - maxMessages, maxMessages);
        }
    }

    // ====================== PUBLIC METHOD FOR CHECKING ======================
    // Other scripts (Movement, Weapon, WeaponSwitcher...) will use this
    public static bool IsPlayerChatting()
    {
        return IsChatting;
    }
}