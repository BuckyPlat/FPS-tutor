using Photon.Pun;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviourPun
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
    public void TakeDamage(int _damage, int killerViewID)
    {
        if (hasDied) return;

        health -= _damage;
        if (health < 0) health = 0;

        UpdateUI();

        if (health <= 0)
        {
            hasDied = true;

            DisablePlayer();

            if (!photonView.IsMine) return;

            PhotonView killerPV = PhotonView.Find(killerViewID);

            string killerName = "Unknown";
            if (killerPV != null)
            {
                PlayerSetup ps = killerPV.GetComponent<PlayerSetup>();
                if (ps != null)
                    killerName = ps.nickname;
            }

            PlayerSetup victimPS = GetComponent<PlayerSetup>();
            string victimName = victimPS != null ? victimPS.nickname : "Unknown";

            string msg = $"<color=yellow>[KILL]</color> " +
                         $"<color=red>{killerName}</color> đã hạ <color=blue>{victimName}</color>";

            GameChat.Instance.photonView
                .RPC("SendSystemMessage", RpcTarget.All, msg);

            if (killerPV != null && killerPV.IsMine)
            {
                RoomManager.instance.Kills++;
                RoomManager.instance.SetHashes(); 
            }

            StartCoroutine(RespawnAndDestroy());
        }
    }

    IEnumerator RespawnAndDestroy()
    {
        RespawnUI.Instance.Show(3f);

        yield return new WaitForSeconds(3f);

        RoomManager.instance.SpawnPlayer();
        RoomManager.instance.Deaths++;
        RoomManager.instance.SetHashes();

        PhotonNetwork.Destroy(gameObject);
    }
    void DisablePlayer()
    {
        // tắt movement
        Movement movement = GetComponent<Movement>();
        if (movement != null)
            movement.enabled = false;
        Weapon weapon = GetComponentInChildren<Weapon>();
        if (weapon != null)
            weapon.enabled = false;

        // tắt collider (không bị bắn tiếp)
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        // tắt rigidbody (ngừng di chuyển)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        // (optional) ẩn model
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
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