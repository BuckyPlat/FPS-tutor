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

    void Start()
    {
        bool isMine = photonView == null || photonView.IsMine;

        // Keep the owner's first-person view clean without disabling the skeleton/animator.
        SetRenderersEnabled(fullBodyModel, !isMine || !hideLocalFullBodyRenderers);

        int defaultLayer = LayerMask.NameToLayer("Default");
        int localPlayerLayer = LayerMask.NameToLayer("LocalPlayer");

        if (fullBodyModel != null && defaultLayer >= 0)
            SetLayer(fullBodyModel, defaultLayer);

        if (worldWeaponRoot != null && defaultLayer >= 0)
            SetLayer(worldWeaponRoot, defaultLayer);

        if (fpsProp != null)
        {
            int fpsLayer = (isMine && localPlayerLayer >= 0) ? localPlayerLayer : defaultLayer;
            SetLayer(fpsProp, fpsLayer);
        }
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
}
