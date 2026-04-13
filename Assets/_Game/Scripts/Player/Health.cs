using Photon.Pun;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
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

    [Header("Vignette Effect")]
    public PostProcessVolume volume;
    private Vignette vignette;

    public float maxVignette = 1f;
    public float vignetteSmooth = 5f;
    private float targetVignette = 0f;

    [HideInInspector]
    public bool hasDied;

    private void Start()
    {
        if (!photonView.IsMine && volume != null)
        {
            volume.gameObject.SetActive(false);
        }
        // Initial setup
        health = maxHealth;
        UpdateUI();
        if (volume != null && volume.profile.TryGetSettings(out vignette))
        {
            vignette.intensity.value = 0f;
        }
    }
    private void Update()
    {
        if (!photonView.IsMine) return;
        if (vignette == null) return;

        vignette.intensity.value = Mathf.Lerp(
            vignette.intensity.value,
            targetVignette,
            Time.deltaTime * vignetteSmooth
        );

        // auto fade to 0
        targetVignette = Mathf.Lerp(targetVignette, 0f, Time.deltaTime);
    }

    [PunRPC]
    public void TakeDamage(int _damage, int killerViewID)
    {
        if (hasDied) return;

        health -= _damage;
        if (health < 0) health = 0;

        UpdateUI();

        if (photonView.IsMine && vignette != null)
        {
            targetVignette = maxVignette;
        }

        if (health <= 0 && !hasDied)
        {
            hasDied = true;

            // === NGĂN KNOCKBACK KHI CHẾT ===
            DisableMovementAndPhysics();

            if (!photonView.IsMine) return;

            // Lấy thông tin killer
            PhotonView killerPV = PhotonView.Find(killerViewID);
            Transform killerTransform = killerPV != null ? killerPV.transform : null;

            // Hiển thị thông báo kill
            string killerName = "Unknown";
            if (killerPV != null)
            {
                PlayerSetup ps = killerPV.GetComponent<PlayerSetup>();
                if (ps != null && !string.IsNullOrEmpty(ps.nickname))
                    killerName = ps.nickname;
                else if (killerPV.Owner != null)
                    killerName = killerPV.Owner.NickName;
            }

            PlayerSetup victimPS = GetComponent<PlayerSetup>();
            string victimName = victimPS != null && !string.IsNullOrEmpty(victimPS.nickname)
                                ? victimPS.nickname : "Unknown";

            string msg = $"<color=yellow>[KILL]</color> " +
                         $"<color=red>{killerName}</color> killed <color=blue>{victimName}</color>";

            if (GameChat.Instance != null)
                GameChat.Instance.photonView.RPC("SendSystemMessage", RpcTarget.All, msg);

            // === GỌI SPECTATOR VỚI KILLER ===
            if (Spectator.Instance != null)
                Spectator.Instance.Activate(killerTransform);

            PhotonNetwork.Destroy(gameObject);
            RoomManager.instance.StartRespawn(3f);
        }
    }

    // Hàm mới: Tắt movement và physics để không bị knockback khi chết
    private void DisableMovementAndPhysics()
    {
        // Tắt script di chuyển
        Movement movement = GetComponent<Movement>();
        if (movement != null)
            movement.enabled = false;

        // Tắt Rigidbody để không nhận lực nữa (knockback từ đạn/explosion)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;        // Quan trọng: Không bị physics đẩy
        }

        // Tắt collider nếu cần (tùy game của bạn)
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }

    private void UpdateUI()
    {
        healthText.text = health.ToString();           // Local player HUD health bar

        if (HealthImage != null)
            HealthImage.fillAmount = (float)health / maxHealth;

        // Update overhead health bar (if any)
        if (overheadHealthBar != null)
            overheadHealthBar.UpdateHealthBar();
    }

    void FixedUpdate()
    {
        if (transform.position.y < -5)
        {
            if (isLocalPlayer)
            {
                RoomManager.instance.SpawnPlayer();
                RoomManager.instance.Deaths++;
            }
                
            Destroy(gameObject);
        }
    }


}