using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class LocalPlayerCameraPresenter : MonoBehaviour
{
    [SerializeField, Min(0f)] private float positionSmoothTime = 0.015f;

    private MouseLook mouseLook;
    private PhotonView playerPhotonView;
    private Transform playerRoot;
    private Vector3 originalLocalOffset;
    private Vector3 positionVelocity;
    private bool hasAuthority;
    private bool hasSnappedToPose;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (!hasAuthority)
        {
            enabled = false;
            return;
        }

        SnapToDesiredPose();
    }

    private void LateUpdate()
    {
        if (!hasAuthority || mouseLook == null || playerRoot == null)
            return;

        Vector3 desiredWorldPosition = playerRoot.TransformPoint(originalLocalOffset);
        if (!hasSnappedToPose || positionSmoothTime <= 0f)
        {
            transform.position = desiredWorldPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredWorldPosition,
                ref positionVelocity,
                positionSmoothTime);
        }

        transform.rotation = Quaternion.Euler(mouseLook.PitchDegrees, mouseLook.DesiredBodyYawDegrees, 0f);
        hasSnappedToPose = true;
    }

    private void OnDisable()
    {
        positionVelocity = Vector3.zero;
        hasSnappedToPose = false;
    }

    private void ResolveReferences()
    {
        if (mouseLook == null)
            mouseLook = GetComponent<MouseLook>();

        if (playerPhotonView == null)
            playerPhotonView = GetComponentInParent<PhotonView>();

        if (playerRoot == null)
        {
            if (mouseLook != null && mouseLook.characterBody != null)
                playerRoot = mouseLook.characterBody.transform;
            else if (transform.parent != null)
                playerRoot = transform.parent;
        }

        originalLocalOffset = transform.localPosition;
        hasAuthority = !PhotonNetwork.IsConnected || playerPhotonView == null || playerPhotonView.IsMine;
    }

    private void SnapToDesiredPose()
    {
        if (mouseLook == null || playerRoot == null)
            return;

        transform.position = playerRoot.TransformPoint(originalLocalOffset);
        transform.rotation = Quaternion.Euler(mouseLook.PitchDegrees, mouseLook.DesiredBodyYawDegrees, 0f);
        positionVelocity = Vector3.zero;
        hasSnappedToPose = true;
    }
}
