using Photon.Pun;
using UnityEngine;

public class UpperBodyLook : MonoBehaviourPun
{
    [Header("Bones")]
    public Transform spine2;

    [Header("References")]
    [SerializeField] private Transform localLookSource;

    [Header("Weights")]
    [Range(0, 1)] public float spine2Weight = 0.22f;

    [Header("Look Limits")]
    public float maxLookUp = 62f;
    public float maxLookDown = 50f;

    private float currentPitch = 0f;
    private float networkPitch = 0f;

    public float CurrentPitch => currentPitch;

    void Start()
    {
        ResolveLocalLookSource();
    }

    void LateUpdate()
    {
        bool useLocalLookSource = ShouldUseLocalLookSource();
        float targetPitch = useLocalLookSource
            ? GetLocalTargetPitch()
            : networkPitch;

        currentPitch = useLocalLookSource
            ? Mathf.Lerp(currentPitch, targetPitch, 16f * Time.deltaTime)
            : targetPitch;
        ApplyPitch(spine2, currentPitch, spine2Weight);
    }

    public void SetNetworkPitch(float pitch)
    {
        networkPitch = pitch;
    }

    private bool ShouldUseLocalLookSource()
    {
        return !PhotonNetwork.IsConnected || photonView.IsMine;
    }

    private void ResolveLocalLookSource()
    {
        if (localLookSource != null)
            return;

        PlayerSetup playerSetup = GetComponentInParent<PlayerSetup>();
        if (playerSetup != null && playerSetup.GetComponent<Camera>() != null)
        {
            localLookSource = playerSetup.GetComponent<Camera>().transform;
            return;
        }

        if (Camera.main != null)
            localLookSource = Camera.main.transform;
    }

    private float GetLocalTargetPitch()
    {
        ResolveLocalLookSource();
        if (localLookSource == null)
            return currentPitch;

        Vector3 lookForward = localLookSource.forward;
        float clampedY = Mathf.Clamp(lookForward.y, -1f, 1f);
        return Mathf.Asin(clampedY) * Mathf.Rad2Deg;
    }

    private void ApplyPitch(Transform bone, float pitch, float weight)
    {
        if (bone == null) return;

        float finalPitch = Mathf.Clamp(pitch * weight, -maxLookDown, maxLookUp);
        if (float.IsNaN(finalPitch)) return;

        Quaternion targetRot = Quaternion.Euler(-finalPitch, 0, 0);
        bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRot, 1f);
    }
}
