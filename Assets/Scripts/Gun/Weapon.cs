using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

public class Weapon : MonoBehaviourPun
{
    public Image ammoCircle;

    public int damage;

    public int pelletsCount = 1;
    public float sprayMultiplier = 0f;

    public Camera camera;

    public float fireRate;

    [Header("Projecttile Weapon Settings")] public bool isProjectileWeapon = false;
    public GameObject projectile;
    public Transform projectileExit;

    [Header("VFX")]
    public GameObject hitVFX;

    private float nextFire;

    [Header("Ammo")]
    public int mag = 5;

    public int ammo = 30;

    public int magAmmo = 30;

    [Header("UI")]
    public TextMeshProUGUI magText;
    public TextMeshProUGUI ammoText;

    [Header("SFX")] public int shootSFXindex = 0;
    public int reloadSFXindex = 0;
    public PlayerPhotonSoundManager playerPhotonSoundManager;

    [Header("Animation")]
    public Animation animation;
    public AnimationClip reload;

    [Header("Recoil Setting")]
    //[Range(0,1)]
    //public float recoilPercent = 0.3f;

    [Range(0, 2)]
    public float recoverPercent = 0.7f;

    [Space]
    public float recoilUp = 1f;
    public float recoilBack = 0f;

    private Vector3 originalPosition;
    private Vector3 recoilVelocity = Vector3.zero;

    private float recoilLenght;
    private float recoverLenght;

    private bool recoiling;
    public bool recovering;

    private IObjectPool<GameObject> vfxPool;

    void Awake()
    {
        vfxPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(hitVFX),
            actionOnGet: obj => obj.SetActive(true),
            actionOnRelease: obj =>
            {
                obj.SetActive(false);
                if (obj.TryGetComponent<ParticleSystem>(out var ps))
                    ps.Clear(true);
            },
            actionOnDestroy: Destroy,
            defaultCapacity: 8,
            maxSize: 10
        );
    }

    void Start()
    {
        magText.text = mag.ToString();
        ammoText.text = ammo + "/" + magAmmo;

        SetAmmo();

        originalPosition = transform.localPosition;

        recoilLenght = 0;
        recoverLenght = 1 / fireRate * recoverPercent;
    }

    // Update is called once per frame
    void Update()
    {
        if (GameChat.IsPlayerChatting()) return;
        if (nextFire > 0)
        {
            nextFire -= Time.deltaTime;
        }

        if (Input.GetButton("Fire1")&& nextFire <= 0 && ammo >0 && animation.isPlaying == false)
        {
            nextFire = 1 / fireRate;
            ammo--;
            magText.text = mag.ToString();
            ammoText.text = ammo + "/" + magAmmo;
            SetAmmo();
            if (isProjectileWeapon)
            {
                ProjecttileFire();
            }
            else
            {
                Fire();
            }
        }
        if (Input.GetKeyDown(KeyCode.R) && mag > 0 && ammo <30)
        {
            Reload();
        }
        if (recoiling)
        {
            Recoil();
        }
        if (recovering)
        {
            Recovering();
        }
    }

    void ProjecttileFire()
    {
        playerPhotonSoundManager.PlayShootSFX(shootSFXindex);
        GameObject myProjectile = PhotonNetwork.Instantiate(projectile.name, projectileExit.position, projectileExit.rotation);
        myProjectile.GetComponent<Explosive>().isLocalExplosive = true;
    }

    void SetAmmo()
    {
        ammoCircle.fillAmount = (float)ammo / magAmmo;
    }

    void Reload()
    {
        animation.Play(reload.name);
        playerPhotonSoundManager.PlayReloadSFX(reloadSFXindex);
        if(mag > 0)
        {
            mag--;

            ammo = magAmmo;
        }

        magText.text = mag.ToString();
        ammoText.text = ammo + "/" + magAmmo;
        SetAmmo();
    }

    void Fire()
    {
        recoiling = true;
        recovering = false;
        playerPhotonSoundManager.PlayShootSFX(shootSFXindex);

        for(int i = 0; i < pelletsCount; i++)
        {
            Vector2 circle = Random.insideUnitCircle * sprayMultiplier;

            Vector3 spreadDirection = camera.transform.forward
            +camera.transform.right * circle.x 
            + camera.transform.up * circle.y;

            Ray ray = new Ray(camera.transform.position, spreadDirection.normalized);

            RaycastHit hit;

            PhotonNetwork.LocalPlayer.AddScore(1);
            if (Physics.Raycast(ray.origin, ray.direction, out hit, 100f))
            {
                GameObject vfx = vfxPool.Get();
                vfx.transform.position = hit.point;
                vfx.transform.rotation = Quaternion.LookRotation(hit.normal);
                StartCoroutine(ReturnToPoolAfter(vfx, 2f));
                if (hit.transform.gameObject.GetComponent<Health>())
                {
                    //PhotonNetwork.LocalPlayer.AddScore(damage);
                    //if (damage >= hit.transform.gameObject.GetComponent<Health>().health)
                    //{
                    //    RoomManager.instance.Kills++;
                    //    RoomManager.instance.SetHashes();

                    //    PhotonNetwork.LocalPlayer.AddScore(100);
                    //}
                    hit.transform.gameObject.GetComponent<PhotonView>().RPC("TakeDamage", RpcTarget.All, damage, photonView.ViewID);
                    Debug.Log("FIRE!!!!!!!");
                }
            }
        }
    }

    void Recoil()
    {
        Vector3 finalPosition = new Vector3(originalPosition.x, originalPosition.y + recoilUp, originalPosition.z- recoilBack);

        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, finalPosition, ref recoilVelocity, recoilLenght);

        if(transform.localPosition == finalPosition)
        {
            recoiling = false;
            recovering = true;
        }
    }

    void Recovering()
    {
        Vector3 finalPosition = originalPosition;

        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, finalPosition, ref recoilVelocity, recoverLenght);

        if (transform.localPosition == finalPosition)
        {
            recoiling = false;
            recovering = false;
        }
    }

    private System.Collections.IEnumerator ReturnToPoolAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        vfxPool.Release(obj);
    }
}
