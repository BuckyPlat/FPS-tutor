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

    // Biến quan trọng: Trạng thái đang chat hay không
    public static bool IsChatting { get; private set; } = false;

    private void Start()
    {
        if (chatText != null)
            chatText.text = "<color=yellow>Chào mừng đến với phòng!</color>\n";
    }

    void Update()
    {
        // ===== XỬ LÝ MỞ / ĐÓNG CHAT =====
        if (Input.GetKeyDown(KeyCode.Y) && !IsChatting)
        {
            OpenChat();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && IsChatting)
        {
            CloseChat();
        }

        // ===== GỬI TIN NHẮN =====
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            && IsChatting && !string.IsNullOrEmpty(inputField.text))
        {
            string messageToSend = $"<color=white>{PhotonNetwork.LocalPlayer.NickName}:</color> {inputField.text}";

            GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.All, messageToSend);

            inputField.text = "";
            CloseChat();        // Tự động đóng sau khi gửi
        }
    }

    // ====================== CHAT CONTROL ======================
    private void OpenChat()
    {
        IsChatting = true;
        inputField.Select();
        inputField.ActivateInputField();
        inputField.text = "";                    // Xóa nội dung cũ
    }

    private void CloseChat()
    {
        IsChatting = false;
        EventSystem.current.SetSelectedGameObject(null);
        inputField.DeactivateInputField();       // Quan trọng
    }

    // ====================== JOIN / LEAVE NOTIFICATION ======================
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (newPlayer == null) return;
        string msg = $"<color=#00FF00>→ {newPlayer.NickName} vừa tham gia phòng</color>";
        AddToChat(msg);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer == null) return;
        string msg = $"<color=#FF4444>← {otherPlayer.NickName} đã rời phòng</color>";
        AddToChat(msg);
    }

    public override void OnJoinedRoom()
    {
        AddToChat("<color=yellow>✓ Bạn đã tham gia phòng thành công!</color>");
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

        // Giới hạn số dòng
        string[] lines = chatText.text.Split('\n');
        if (lines.Length > maxMessages)
        {
            chatText.text = string.Join("\n", lines, lines.Length - maxMessages, maxMessages);
        }
    }

    // ====================== PUBLIC METHOD ĐỂ KIỂM TRA ======================
    // Các script khác (Movement, Weapon, WeaponSwitcher...) sẽ dùng cái này
    public static bool IsPlayerChatting()
    {
        return IsChatting;
    }
}