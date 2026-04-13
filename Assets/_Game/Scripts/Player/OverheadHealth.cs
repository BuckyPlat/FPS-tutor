using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OverheadHealth : MonoBehaviour
{
    [Header("References")]
    public Image healthFillImage;        // Image fill of health bar (red)
    public TextMeshProUGUI healthText;   // (Optional) text showing health value

    private PhotonView photonView;
    private Health playerHealth;         // Reference to your Health script

    private void Awake()
    {
        photonView = GetComponentInParent<PhotonView>(); // Because Canvas is a child of Player
    }

    private void Start()
    {
        // Find Health script on parent player
        playerHealth = GetComponentInParent<Health>();

        if (playerHealth != null)
        {
            // Register update event (if you want to optimize)
            UpdateHealthBar();

            // Or you can call from Health script whenever health changes
        }

        // Hide own health bar (optional - many games only show others' bars)
        if (photonView != null && photonView.IsMine)
        {
            gameObject.SetActive(false);   // Or set alpha = 0
        }
    }

    // This function will be called from Health script whenever health changes
    public void UpdateHealthBar()
    {
        if (playerHealth == null) return;

        float fillAmount = (float)playerHealth.health / playerHealth.maxHealth;
        healthFillImage.fillAmount = fillAmount;

        if (healthText != null)
            healthText.text = playerHealth.health.ToString();
    }
}