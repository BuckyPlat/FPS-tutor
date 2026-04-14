using UnityEngine;
using Photon.Pun;

public class PlayerLayerSetup : MonoBehaviourPun
{
    [Header("References")]
    public GameObject fullBodyModel;  // Low Poly Soldier
    public GameObject fpsProp;        // model súng FPS (child của camera)

    void Start()
    {
        if (photonView.IsMine)
        {
            SetLayer(fullBodyModel, LayerMask.NameToLayer("RemotePlayer"));
            SetLayer(fpsProp, LayerMask.NameToLayer("LocalPlayer"));
        }
        else
        {
            SetLayer(fullBodyModel, LayerMask.NameToLayer("RemotePlayer"));
            // súng FPS của remote player không cần hiện
            // vì họ có model súng gắn vào tay trong fullBodyModel
        }
    }

    private void SetLayer(GameObject obj, int layer)
    {
        if (obj == null) return;
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}