using Photon.Pun;
using UnityEngine;

public class BodyRotationWithDeadzone : MonoBehaviourPun
{
    [Header("Deadzone Settings")]
    [Tooltip("Góc tối đa mà camera có thể quay mà body chưa quay theo")]
    public float deadzoneAngle = 75f;

    [Tooltip("Tốc độ body quay khi vượt deadzone")]
    public float bodyTurnSpeed = 8f;

    [Header("References")]
    public Transform cameraTransform;
    public Transform modelTransform;

    private float currentBodyYaw;

    void Start()
    {
        if (!HasLocalAuthority())
            return;

        if (modelTransform == null)
            modelTransform = GetComponentInChildren<Animator>().transform;

        currentBodyYaw = modelTransform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (!HasLocalAuthority())
            return;

        if (cameraTransform == null || modelTransform == null) return;

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0;
        Vector3 modelForward = modelTransform.forward;
        modelForward.y = 0;

        float angleDiff = Vector3.SignedAngle(modelForward, cameraForward, Vector3.up);

        if (Mathf.Abs(angleDiff) > deadzoneAngle)
        {
            float targetYaw = cameraTransform.eulerAngles.y;
            currentBodyYaw = Mathf.LerpAngle(currentBodyYaw, targetYaw, bodyTurnSpeed * Time.deltaTime);
        }

        modelTransform.rotation = Quaternion.Euler(0, currentBodyYaw, 0);
    }

    private bool HasLocalAuthority()
    {
        return !PhotonNetwork.IsConnected || photonView.IsMine;
    }
}
