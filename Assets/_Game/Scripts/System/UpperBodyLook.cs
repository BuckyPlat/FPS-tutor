using UnityEngine;

public class UpperBodyLook : MonoBehaviour
{
    [Header("Bones")]
    public Transform spine2;

    [Header("Weights")]
    [Range(0, 1)] public float spine2Weight = 0.22f;

    [Header("Look Limits")]
    public float maxLookUp = 62f;
    public float maxLookDown = 50f;

    private Transform cam;
    private float currentPitch = 0f;

    void Start()
    {
        cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        Vector3 camForward = cam.forward;

        // Clamp trước khi Asin để tránh NaN
        float clampedY = Mathf.Clamp(camForward.y, -1f, 1f);
        float targetPitch = Mathf.Asin(clampedY) * Mathf.Rad2Deg;

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, 16f * Time.deltaTime);

        ApplyPitch(spine2, currentPitch, spine2Weight);
    }

    private void ApplyPitch(Transform bone, float pitch, float weight)
    {
        if (bone == null) return;

        float finalPitch = Mathf.Clamp(pitch * weight, -maxLookDown, maxLookUp);

        // Guard thêm để chắc chắn không NaN
        if (float.IsNaN(finalPitch)) return;

        Quaternion targetRot = Quaternion.Euler(-finalPitch, 0, 0);

        bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRot, 1f);
    }
}