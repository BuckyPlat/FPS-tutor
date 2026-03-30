using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OverheadHealth : MonoBehaviour
{
    [Header("References")]
    public Image healthFillImage;        // Image fill của thanh máu (đỏ)
    public TextMeshProUGUI healthText;   // (Tùy chọn) text hiển thị số máu

    private PhotonView photonView;
    private Health playerHealth;         // Reference đến script Health của bạn

    private void Awake()
    {
        photonView = GetComponentInParent<PhotonView>(); // Vì Canvas là child của Player
    }

    private void Start()
    {
        // Tìm script Health trên player cha
        playerHealth = GetComponentInParent<Health>();

        if (playerHealth != null)
        {
            // Đăng ký sự kiện cập nhật (nếu bạn muốn tối ưu)
            UpdateHealthBar();

            // Hoặc bạn có thể gọi từ script Health mỗi khi máu thay đổi
        }

        // Ẩn thanh máu của chính mình (tùy chọn - nhiều game chỉ hiện thanh của người khác)
        if (photonView != null && photonView.IsMine)
        {
            gameObject.SetActive(false);   // Hoặc set alpha = 0
        }
    }

    // Hàm này sẽ được gọi từ script Health mỗi khi máu thay đổi
    public void UpdateHealthBar()
    {
        if (playerHealth == null) return;

        float fillAmount = (float)playerHealth.health / playerHealth.maxHealth;
        healthFillImage.fillAmount = fillAmount;

        if (healthText != null)
            healthText.text = playerHealth.health.ToString();
    }
}