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

    private bool hasDied;

    private void Start()
    {
        if (!photonView.IsMine && volume != null)
        {
            volume.gameObject.SetActive(false);
        }
        // Khởi tạo ban đầu
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

        // tự fade về 0
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

        if (health <= 0)
        {
            hasDied = true;

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

            Debug.Log(Spectator.Instance);
            if (Spectator.Instance != null)
                Spectator.Instance.Activate();

            PhotonNetwork.Destroy(gameObject);

            RoomManager.instance.StartRespawn(3f);
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
            {
                RoomManager.instance.SpawnPlayer();
                RoomManager.instance.Deaths++;
            }
                
            Destroy(gameObject);
        }
    }


}