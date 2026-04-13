using Photon.Pun;
using Photon.Pun.UtilityScripts;
using UnityEngine;

public class Explosive : MonoBehaviourPun
{
    [HideInInspector]
    public bool isLocalExplosive = false;
    private bool alreadyExplode = false;

    [Header("Stats")]
    public float explosionRadius = 5f;
    public int damage = 30;

    [Header("Fire Settings")]
    public float fireForce;

    [Header("VFX & SFX")]
    public GameObject explosionVFX;
    public float vfxLifetime = 2.5f;           // Time to destroy VFX

    [Header("Explosion Sound")]
    public AudioClip explosionSFX;             // Drag explosion AudioClip here
    public float explosionVolume = 1f;         // Volume (0.0 - 1.0)

    private PhotonView pv;

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
    }

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GetComponent<Rigidbody>().AddForce(transform.forward * fireForce);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!isLocalExplosive || alreadyExplode) return;

        alreadyExplode = true;

        Explode();                                 // Damage + Score
        pv.RPC("RpcExplosionEffects", RpcTarget.All, transform.position);   // Sync VFX + SFX

        PhotonNetwork.Destroy(gameObject);
    }

    void Explode()
    {
        foreach (Collider col in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            Health health = col.GetComponent<Health>();
            if (health == null) continue;

            // Prevent explosion damage to self
            PhotonView targetPV = col.GetComponent<PhotonView>();
            if (targetPV != null && targetPV.IsMine) continue;

            //PhotonNetwork.LocalPlayer.AddScore(damage);

            //if (damage >= health.health)
            //{
            //    RoomManager.instance.Kills++;
            //    RoomManager.instance.SetHashes();
            //    PhotonNetwork.LocalPlayer.AddScore(100);
            //}

            if (targetPV != null)
                targetPV.RPC("TakeDamage", RpcTarget.All, damage, photonView.ViewID);
        }
    }

    [PunRPC]
    public void RpcExplosionEffects(Vector3 position)
    {
        // === VFX ===
        if (explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, position, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }

        // === SFX - Play explosion sound for all players ===
        if (explosionSFX != null)
        {
            // Create a temporary AudioSource at explosion position
            GameObject audioObj = new GameObject("ExplosionSound");
            audioObj.transform.position = position;

            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.clip = explosionSFX;
            audioSource.volume = explosionVolume;
            audioSource.spatialBlend = 2f;        // 3D sound
            audioSource.maxDistance = 70f;        // Hearing range
            audioSource.rolloffMode = AudioRolloffMode.Linear;

            audioSource.Play();
            Destroy(audioObj, explosionSFX.length + 0.5f);   // Destroy after playing completes
        }
    }
}