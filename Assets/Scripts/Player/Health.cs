using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public Image HealthImage;
    public int maxHealth = 100;     // ← Thêm biến này
    public int health;              // health hiện tại

    public bool isLocalPlayer;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    private void Start()
    {
        // Khởi tạo ban đầu
        health = maxHealth;
        UpdateUI();
    }

    [PunRPC]
    public void TakeDamage(int _damage)
    {
        health -= _damage;
        if (health < 0) health = 0;

        UpdateUI();                     // ← Gọi hàm chung để cập nhật cả Text + Image

        if (health <= 0)
        {
            if (isLocalPlayer)
            {
                RoomManager.instance.SpawnPlayer();
                RoomManager.instance.Deaths++;
                RoomManager.instance.SetHashes();
            }
            Destroy(gameObject);
        }
    }

    // Hàm cập nhật cả Text và FillAmount
    private void UpdateUI()
    {
        healthText.text = health.ToString();

        // Đây là dòng quan trọng nhất
        HealthImage.fillAmount = (float)health / maxHealth;
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