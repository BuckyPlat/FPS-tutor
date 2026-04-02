using Photon.Pun;
using Photon.Pun.UtilityScripts;
using UnityEngine;

public class Explosive : MonoBehaviour
{
    [HideInInspector]
    public bool isLocalExplosive = false;
    private bool alreadyExplode = false;

    [Header("Stats")]
    public float explosionRadius = 5f;
    public int damage = 30;

    [Header("Fire Settings")]
    public float fireForce;

    [Header("VFX")]
    public GameObject explosionVFX;
    public float vfxLifetime = 1f;

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
        if (!isLocalExplosive || alreadyExplode)
            return;

        alreadyExplode = true;
        Explode();

        // Gửi RPC spawn VFX cho tất cả client, VFX là object độc lập
        pv.RPC("ShowExplosionVFX", RpcTarget.All, transform.position);

        // Destroy projectile ngay — VFX đã là object riêng, không bị ảnh hưởng
        PhotonNetwork.Destroy(gameObject);
    }

    void Explode()
    {
        foreach (Collider col in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            Health health = col.GetComponent<Health>();
            if (health == null) continue;

            PhotonView targetPV = col.GetComponent<PhotonView>();
            if (targetPV != null && targetPV.IsMine)
                continue;

            PhotonNetwork.LocalPlayer.AddScore(damage);

            if (damage >= health.health)
            {
                RoomManager.instance.Kills++;
                RoomManager.instance.SetHashes();
                PhotonNetwork.LocalPlayer.AddScore(100);
            }

            if (targetPV != null)
                targetPV.RPC("TakeDamage", RpcTarget.All, damage);
        }
    }

    [PunRPC]
    public void ShowExplosionVFX(Vector3 position)
    {
        if (explosionVFX == null) return;

        // Instantiate VFX là object hoàn toàn độc lập, không gắn vào projectile
        GameObject vfx = Instantiate(explosionVFX, position, Quaternion.identity);

        // Tự destroy sau khi particle chạy xong
        Destroy(vfx, vfxLifetime);
    }
}