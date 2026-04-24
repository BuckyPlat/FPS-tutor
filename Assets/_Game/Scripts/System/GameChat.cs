using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameChat : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    public int maxMessages = 15;

    public static bool IsChatting { get; private set; }

    public static GameChat Instance;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        UIToolkitGameplayUIController.Instance?.AppendChatMessage("<color=yellow>Welcome to the room!</color>");
    }

    void Update()
    {
        if (UIToolkitGameplayUIController.IsGameplayInputBlocked)
            return;

        if (Input.GetKeyDown(KeyCode.Y) && !IsChatting)
        {
            OpenChat();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && IsChatting)
        {
            CloseChat();
        }

        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && IsChatting)
        {
            string inputValue = UIToolkitGameplayUIController.Instance?.GetChatInputValue() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(inputValue))
            {
                return;
            }

            string messageToSend = $"<color=white>{PhotonNetwork.LocalPlayer.NickName}:</color> {inputValue}";
            GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.All, messageToSend);

            UIToolkitGameplayUIController.Instance?.ClearChatInput();
            CloseChat();
        }
    }

    private void OpenChat()
    {
        IsChatting = true;
        UIToolkitGameplayUIController.Instance?.OpenChat();
    }

    private void CloseChat()
    {
        IsChatting = false;
        UIToolkitGameplayUIController.Instance?.CloseChat(true);
    }

    public void ForceCloseChat()
    {
        if (!IsChatting)
            return;

        CloseChat();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (newPlayer == null) return;
        string msg = $"<color=#00FF00>[JOIN] {newPlayer.NickName} has joined the room</color>";
        AddToChat(msg);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer == null) return;
        string msg = $"<color=#FF4444>[LEAVE] {otherPlayer.NickName} has left the room</color>";
        AddToChat(msg);
    }

    public override void OnJoinedRoom()
    {
        AddToChat("<color=yellow>[OK] You have successfully joined the room!</color>");
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
        UIToolkitGameplayUIController.Instance?.AppendChatMessage(message);
    }

    public static bool IsPlayerChatting()
    {
        return IsChatting;
    }
}
