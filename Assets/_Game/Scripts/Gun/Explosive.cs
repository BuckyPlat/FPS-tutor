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
    public float vfxLifetime = 2.5f;

    [Header("Explosion Sound")]
    public AudioClip explosionSFX;
    public float explosionVolume = 1f;

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

        Explode();                                 // Xử lý damage + kill
        pv.RPC("RpcExplosionEffects", RpcTarget.All, transform.position);

        PhotonNetwork.Destroy(gameObject);
    }

    void Explode()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider col in hitColliders)
        {
            Health health = col.GetComponent<Health>();
            if (!(health != null && !health.hasDied)) continue;   // ← Thêm điều kiện này

            PhotonView targetPV = col.GetComponent<PhotonView>();
            if (targetPV == null || targetPV.IsMine) continue;

            targetPV.RPC("TakeDamage", RpcTarget.All, damage, pv.ViewID);
        }
    }

    [PunRPC]
    public void RpcExplosionEffects(Vector3 position)
    {
        // VFX
        if (explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, position, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }

        // SFX
        if (explosionSFX != null)
        {
            GameObject audioObj = new GameObject("ExplosionSound");
            audioObj.transform.position = position;

            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.clip = explosionSFX;
            audioSource.volume = explosionVolume;
            audioSource.spatialBlend = 2f;
            audioSource.maxDistance = 70f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;

            audioSource.Play();
            Destroy(audioObj, explosionSFX.length + 0.5f);
        }
    }
}