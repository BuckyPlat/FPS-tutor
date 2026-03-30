using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public Image HealthImage;
    public int maxHealth = 100;     
    public int health;              

    public bool isLocalPlayer;

    [Header("Overhead Health Bar")]
    public OverheadHealth overheadHealthBar;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    private bool hasDied;

    private void Start()
    {
        // Khởi tạo ban đầu
        health = maxHealth;
        UpdateUI();
    }

    [PunRPC]
    public void TakeDamage(int _damage)
    {
        if (hasDied)
            return;

        health -= _damage;
        if (health < 0) health = 0;

        UpdateUI();                     

        if (health <= 0)
        {
            hasDied = true;
            if (isLocalPlayer)
            {
                RoomManager.instance.SpawnPlayer();
                RoomManager.instance.Deaths++;
                RoomManager.instance.SetHashes();
            }
            Destroy(gameObject);
        }
    }

    private void UpdateUI()
    {
        healthText.text = health.ToString();           // Thanh máu HUD của local player

        if (HealthImage != null)
            HealthImage.fillAmount = (float)health / maxHealth;

        // Cập nhật thanh máu trên đầu (nếu có)
        if (overheadHealthBar != null)
            overheadHealthBar.UpdateHealthBar();
    }

    void FixedUpdate()
    {
        if (transform.position.y < -5)
        {
            if (isLocalPlayer)
                RoomManager.instance.SpawnPlayer();

            Destroy(gameObject);
        }
    }


}