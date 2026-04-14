using UnityEngine;

public class BodyRotationWithDeadzone : MonoBehaviour
{
    [Header("Deadzone Settings")]
    [Tooltip("Góc tối đa mà camera có thể quay mà body chưa quay theo")]
    public float deadzoneAngle = 75f;           // 60-90 là đẹp nhất

    [Tooltip("Tốc độ body quay khi vượt deadzone")]
    public float bodyTurnSpeed = 8f;

    [Header("References")]
    public Transform cameraTransform;           // Kéo Main Camera vào
    public Transform modelTransform;            // Kéo Low Poly Soldier vào

    private float currentBodyYaw;

    void Start()
    {
        if (modelTransform == null)
            modelTransform = GetComponentInChildren<Animator>().transform; // tự tìm model

        currentBodyYaw = modelTransform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (cameraTransform == null || modelTransform == null) return;

        // Tính góc giữa camera và body hiện tại
        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0;
        Vector3 modelForward = modelTransform.forward;
        modelForward.y = 0;

        float angleDiff = Vector3.SignedAngle(modelForward, cameraForward, Vector3.up);

        // Nếu góc vượt deadzone → body bắt đầu quay theo
        if (Mathf.Abs(angleDiff) > deadzoneAngle)
        {
            float targetYaw = cameraTransform.eulerAngles.y;
            currentBodyYaw = Mathf.LerpAngle(currentBodyYaw, targetYaw, bodyTurnSpeed * Time.deltaTime);
        }

        // Áp dụng rotation chỉ cho trục Y của model
        modelTransform.rotation = Quaternion.Euler(0, currentBodyYaw, 0);
    }
}