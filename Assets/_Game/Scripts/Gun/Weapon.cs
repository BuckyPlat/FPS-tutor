using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

public class Weapon : MonoBehaviourPun
{
    private const float MaxHitscanDistance = 100f;
    private const int MaxHitscanResults = 16;

    public Image ammoCircle;

    public int damage;

    public int pelletsCount = 1;
    public float sprayMultiplier = 0f;

    public Camera camera;

    public float fireRate;

    [Header("Projecttile Weapon Settings")]
    public bool isProjectileWeapon = false;
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

    [Header("SFX")]
    public int shootSFXindex = 0;
    public int reloadSFXindex = 0;
    public PlayerPhotonSoundManager playerPhotonSoundManager;

    [Header("Animation")]
    public Animation animation;
    public AnimationClip reload;

    [Header("Recoil Setting")]
    [Range(0, 2)]
    public float recoverPercent = 0.7f;

    public float recoilUp = 1f;
    public float recoilBack = 0f;

    private Vector3 originalPosition;
    private Vector3 recoilVelocity = Vector3.zero;

    private float recoilLenght;
    private float recoverLenght;

    private bool recoiling;
    public bool recovering;

    private IObjectPool<GameObject> vfxPool;
    private readonly RaycastHit[] hitscanResults = new RaycastHit[MaxHitscanResults];

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
        if (magText != null)
            magText.text = mag.ToString();
        if (ammoText != null)
            ammoText.text = ammo + "/" + magAmmo;
        SetAmmo();

        originalPosition = transform.localPosition;
        recoilLenght = 0;
        recoverLenght = 1 / fireRate * recoverPercent;
    }

    void Update()
    {
        if (GameChat.IsPlayerChatting() || UIToolkitGameplayUIController.IsGameplayInputBlocked || !gameObject.activeInHierarchy)
            return;

        if (RoomManager.instance != null && !RoomManager.instance.IsMatchLive)
            return;

        if (nextFire > 0)
            nextFire -= Time.deltaTime;

        if (Input.GetButton("Fire1") && nextFire <= 0 && ammo > 0 && !animation.isPlaying)
        {
            nextFire = 1 / fireRate;
            ammo--;
            UpdateAmmoUI();

            if (isProjectileWeapon)
                ProjecttileFire();
            else
                Fire();
        }

        if (Input.GetKeyDown(KeyCode.R) && mag > 0 && ammo < magAmmo)
        {
            Reload();
        }

        if (recoiling) Recoil();
        if (recovering) Recovering();
    }

    void UpdateAmmoUI()
    {
        if (magText != null)
            magText.text = mag.ToString();
        if (ammoText != null)
            ammoText.text = ammo + "/" + magAmmo;
        SetAmmo();
    }

    void ProjecttileFire()
    {
        playerPhotonSoundManager.PlayShootSFX(shootSFXindex);
        GameObject myProjectile = PhotonNetwork.Instantiate(projectile.name, projectileExit.position, projectileExit.rotation);
        if (myProjectile.TryGetComponent<Explosive>(out var exp))
            exp.isLocalExplosive = true;
    }

    void SetAmmo()
    {
        if (ammoCircle != null)
            ammoCircle.fillAmount = (float)ammo / magAmmo;

        if (photonView != null && photonView.IsMine)
            UIToolkitGameplayUIController.Instance?.SetAmmo(ammo, mag, magAmmo);
    }

    void Reload()
    {
        if (RoomManager.instance != null && !RoomManager.instance.IsMatchLive)
            return;

        animation.Play(reload.name);
        playerPhotonSoundManager.PlayReloadSFX(reloadSFXindex);

        if (mag > 0)
        {
            mag--;
            ammo = magAmmo;
        }

        UpdateAmmoUI();
    }

    void Fire()
    {
        if (!gameObject.activeInHierarchy) return;

        recoiling = true;
        recovering = false;
        playerPhotonSoundManager.PlayShootSFX(shootSFXindex);

        for (int i = 0; i < pelletsCount; i++)
        {
            Vector2 circle = Random.insideUnitCircle * sprayMultiplier;

            Vector3 spreadDirection = camera.transform.forward
                + camera.transform.right * circle.x
                + camera.transform.up * circle.y;

            if (TryGetFirstValidHitscanHit(camera.transform.position, spreadDirection.normalized, out RaycastHit hit))
            {
                // VFX
                GameObject vfx = vfxPool.Get();
                vfx.transform.position = hit.point;
                vfx.transform.rotation = Quaternion.LookRotation(hit.normal);

                if (gameObject.activeInHierarchy)
                    StartCoroutine(ReturnToPoolAfter(vfx, 2f));
                else
                    vfxPool.Release(vfx);

                // Xử lý damage
                if (TryGetDamageTarget(hit, out var health, out var targetPhotonView))
                {
                    if (IsSelfHitscanTarget(targetPhotonView.transform))
                        continue;

                    targetPhotonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.ViewID);
                }
            }
        }
    }

    void Recoil()
    {
        Vector3 finalPosition = new Vector3(originalPosition.x, originalPosition.y + recoilUp, originalPosition.z - recoilBack);
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, finalPosition, ref recoilVelocity, recoilLenght);

        if (Vector3.Distance(transform.localPosition, finalPosition) < 0.01f)
        {
            recoiling = false;
            recovering = true;
        }
    }

    void Recovering()
    {
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, originalPosition, ref recoilVelocity, recoverLenght);

        if (Vector3.Distance(transform.localPosition, originalPosition) < 0.01f)
        {
            recoiling = false;
            recovering = false;
        }
    }

    private bool TryGetFirstValidHitscanHit(Vector3 origin, Vector3 direction, out RaycastHit validHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            hitscanResults,
            MaxHitscanDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            validHit = default;
            return false;
        }

        SortHitsByDistance(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitscanResults[i];
            if (IsIgnoredHitscanHit(hit.collider))
                continue;

            validHit = hit;
            return true;
        }

        validHit = default;
        return false;
    }

    private bool TryGetDamageTarget(RaycastHit hit, out Health health, out PhotonView targetPhotonView)
    {
        health = hit.collider != null ? hit.collider.GetComponentInParent<Health>() : null;
        targetPhotonView = hit.collider != null ? hit.collider.GetComponentInParent<PhotonView>() : null;
        return health != null && targetPhotonView != null;
    }

    private bool IsIgnoredHitscanHit(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return true;

        Transform shooterRoot = GetShooterRoot();
        if (collider.transform.root == shooterRoot)
            return true;

        PhotonView hitPhotonView = collider.GetComponentInParent<PhotonView>();
        return hitPhotonView != null && photonView != null && hitPhotonView.ViewID == photonView.ViewID;
    }

    private bool IsSelfHitscanTarget(Transform targetRoot)
    {
        return targetRoot != null && targetRoot == GetShooterRoot();
    }

    private Transform GetShooterRoot()
    {
        return photonView != null ? photonView.transform.root : transform.root;
    }

    private void SortHitsByDistance(int hitCount)
    {
        for (int i = 0; i < hitCount - 1; i++)
        {
            int nearestIndex = i;
            float nearestDistance = hitscanResults[i].distance;

            for (int j = i + 1; j < hitCount; j++)
            {
                if (hitscanResults[j].distance < nearestDistance)
                {
                    nearestIndex = j;
                    nearestDistance = hitscanResults[j].distance;
                }
            }

            if (nearestIndex == i)
                continue;

            RaycastHit swap = hitscanResults[i];
            hitscanResults[i] = hitscanResults[nearestIndex];
            hitscanResults[nearestIndex] = swap;
        }
    }

    private IEnumerator ReturnToPoolAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            vfxPool.Release(obj);
    }

    public void RefillCurrentMagazine()
    {
        ammo = magAmmo;
        UpdateAmmoUI();
    }
}
