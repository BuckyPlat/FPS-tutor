using Photon.Pun;
using Photon.Pun.UtilityScripts;
using UnityEngine;
using UnityEngine.Pool;

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

    private static IObjectPool<GameObject> vfxPool;
    private static GameObject vfxPrefabRef;

    private void Awake()
    {
        if (vfxPool == null)
        {
            vfxPrefabRef = explosionVFX;
            vfxPool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(vfxPrefabRef),

                actionOnGet: obj =>
                {
                    obj.SetActive(true);
                    foreach (var light in obj.GetComponentsInChildren<Light>())
                        light.enabled = true;
                },

                actionOnRelease: obj =>
                {
                    foreach (var light in obj.GetComponentsInChildren<Light>())
                        light.enabled = false;

                    if (obj.TryGetComponent<ParticleSystem>(out var ps))
                        ps.Clear(true);

                    obj.SetActive(false);
                },

                actionOnDestroy: Destroy,
                defaultCapacity: 4,
                maxSize: 8
            );
        }
    }

    private void Start()
    {
        GetComponent<Rigidbody>().AddForce(transform.forward * fireForce);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!isLocalExplosive) return;

        SpawnVFX();
        Explode();
    }

    void SpawnVFX()
    {
        GameObject vfx = vfxPool.Get();
        vfx.transform.position = transform.position;
        vfx.transform.rotation = Quaternion.identity;

        if (vfx.TryGetComponent<ParticleSystem>(out var ps))
        {
            // Stop hoàn toàn trước khi set duration
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 4f;

            ps.Play();
        }

        StartCoroutine(ReturnVFXToPool(vfx, 4f));
    }

    void Explode()
    {
        if (alreadyExplode) return;
        alreadyExplode = true;

        foreach (var collider in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            if (collider.transform.gameObject.GetComponent<Health>())
            {
                PhotonNetwork.LocalPlayer.AddScore(damage);
                if (damage >= collider.transform.gameObject.GetComponent<Health>().health)
                {
                    RoomManager.instance.Kills++;
                    RoomManager.instance.SetHashes();
                    PhotonNetwork.LocalPlayer.AddScore(100);
                }
                collider.transform.gameObject.GetComponent<PhotonView>().RPC("TakeDamage", RpcTarget.All, damage);
            }
        }

        PhotonNetwork.Destroy(gameObject);
    }

    private System.Collections.IEnumerator ReturnVFXToPool(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        vfxPool.Release(obj);
    }
}