using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;
using Photon.Realtime;

public class Health : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int health;

    [Header("UI References")]
    public Image healthBar;
    public Text healthText;

    [Header("Vignette Effect")]
    public PostProcessVolume volume;
    public float maxVignette = 1f;
    public float vignetteSmooth = 5f;

    [Header("Drops")]
    public GameObject healthDropPrefab;

    [HideInInspector] public bool hasDied;

    private Vignette vignette;
    private float targetVignette;

    private void Start()
    {
        health = maxHealth;
        UpdateUI();

        if (volume != null)
            volume.profile.TryGetSettings(out vignette);
    }

    private void Update()
    {
        if (photonView.IsMine && vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, targetVignette, Time.deltaTime * vignetteSmooth);
            targetVignette = Mathf.Lerp(targetVignette, 0, Time.deltaTime * (vignetteSmooth / 2f));
        }
    }

    [PunRPC]
    public void TakeDamage(int damage, int killerViewID)
    {
        if (hasDied) return;

        health -= damage;
        UpdateUI();

        if (photonView.IsMine && vignette != null)
            targetVignette = maxVignette;

        if (health <= 0)
        {
            HandleDeath(killerViewID, false);
        }
    }

    public void RestoreHealth(int amount)
    {
        if (amount <= 0 || hasDied)
            return;

        health = Mathf.Clamp(health + amount, 0, maxHealth);
        UpdateUI();
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine || hasDied)
            return;

        if (RoomManager.instance != null && !RoomManager.instance.IsMatchLive)
            return;

        if (transform.position.y < -5f)
            HandleDeath(-1, true);
    }

    private void HandleDeath(int killerViewID, bool isEnvironmentKill)
    {
        if (hasDied)
            return;

        hasDied = true;

        // Spawn health drop
        if (PhotonNetwork.IsMasterClient && healthDropPrefab != null)
        {
            PhotonNetwork.Instantiate(
                healthDropPrefab.name,
                transform.position,
                Quaternion.identity
            );
        }

        DisableMovementAndPhysics();

        if (!photonView.IsMine)
            return;

        PhotonView killerView = !isEnvironmentKill && killerViewID >= 0 ? PhotonView.Find(killerViewID) : null;
        Transform killerTransform = killerView != null ? killerView.transform : null;

        RoomManager.instance?.NotifyLocalPlayerDeath(killerViewID, killerTransform, isEnvironmentKill);

        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    private void DisableMovementAndPhysics()
    {
        Movement movement = GetComponent<Movement>();
        if (movement != null)
            movement.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }

    private void UpdateUI()
    {
        float normalizedHealth = maxHealth <= 0 ? 0f : Mathf.Clamp01((float)health / maxHealth);

        if (healthBar != null)
            healthBar.fillAmount = normalizedHealth;

        if (healthText != null)
            healthText.text = health.ToString();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(health);
        }
        else
        {
            health = (int)stream.ReceiveNext();
            UpdateUI();
        }
    }
}
