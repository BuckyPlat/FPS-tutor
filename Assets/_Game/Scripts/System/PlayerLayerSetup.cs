using Photon.Pun;
using UnityEngine;

public class PlayerLayerSetup : MonoBehaviourPun
{
    [Header("References")]
    public GameObject fullBodyModel;   // Low Poly Soldier
    public GameObject fpsProp;         // FPS weapon camera stack
    public GameObject worldWeaponRoot; // Third-person/world weapon holder

    [Header("Local Visibility")]
    public bool hideLocalFullBodyRenderers = true;

    private static bool playerCollisionConfigured;

    void Start()
    {
        bool isMine = photonView == null || photonView.IsMine;
        ConfigurePlayerCollisionMatrix();
        ConfigureRootPhysics(isMine);

        // Keep the owner's first-person view clean without disabling the skeleton/animator.
        SetRenderersEnabled(fullBodyModel, !isMine || !hideLocalFullBodyRenderers);

        int defaultLayer = LayerMask.NameToLayer("Default");
        int localPlayerLayer = LayerMask.NameToLayer("LocalPlayer");
        int remotePlayerLayer = LayerMask.NameToLayer("RemotePlayer");

        int rootLayer = isMine ? localPlayerLayer : remotePlayerLayer;
        if (rootLayer >= 0)
            gameObject.layer = rootLayer;

        if (fullBodyModel != null && defaultLayer >= 0)
            SetLayer(fullBodyModel, defaultLayer);

        if (worldWeaponRoot != null && defaultLayer >= 0)
        {
            SetLayer(worldWeaponRoot, defaultLayer);
            SetCollidersEnabled(worldWeaponRoot, false);
        }

        if (fpsProp != null)
        {
            int fpsLayer = (isMine && localPlayerLayer >= 0) ? localPlayerLayer : defaultLayer;
            SetLayer(fpsProp, fpsLayer);
            SetCollidersEnabled(fpsProp, false);
        }
    }

    private void ConfigureRootPhysics(bool isMine)
    {
        PhotonTransformView transformView = GetComponent<PhotonTransformView>();
        if (transformView != null)
            transformView.enabled = false;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.interpolation = isMine ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;

        if (isMine)
        {
            body.isKinematic = false;
            body.useGravity = true;
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
    }

    private void ConfigurePlayerCollisionMatrix()
    {
        if (playerCollisionConfigured)
            return;

        int localPlayerLayer = LayerMask.NameToLayer("LocalPlayer");
        int remotePlayerLayer = LayerMask.NameToLayer("RemotePlayer");

        if (localPlayerLayer >= 0)
            Physics.IgnoreLayerCollision(localPlayerLayer, localPlayerLayer, true);

        if (remotePlayerLayer >= 0)
            Physics.IgnoreLayerCollision(remotePlayerLayer, remotePlayerLayer, true);

        if (localPlayerLayer >= 0 && remotePlayerLayer >= 0)
            Physics.IgnoreLayerCollision(localPlayerLayer, remotePlayerLayer, true);

        playerCollisionConfigured = true;
    }

    private void SetLayer(GameObject obj, int layer)
    {
        if (obj == null || layer < 0) return;

        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private void SetRenderersEnabled(GameObject obj, bool enabled)
    {
        if (obj == null) return;

        foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = enabled;
    }

    private void SetCollidersEnabled(GameObject obj, bool enabled)
    {
        if (obj == null) return;

        foreach (Collider collider in obj.GetComponentsInChildren<Collider>(true))
            collider.enabled = enabled;
    }
}
