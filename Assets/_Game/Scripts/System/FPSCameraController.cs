using UnityEngine;
using Photon.Pun;

public class FPSCameraController : MonoBehaviourPun
{
    [Header("References")]
    public Transform cameraTarget;   // Empty Object gắn trong Head bone
    public Transform playerBody;     // Player root

    [Header("Sensitivity")]
    public float mouseSensitivity = 2f;

    [Header("Pitch Limits")]
    public float maxLookUp = 80f;
    public float maxLookDown = 80f;

    private Camera cam;
    private float pitch = 0f;
    private float yaw = 0f;

    void Start()
    {
        if (!photonView.IsMine)
        {
            // Tắt camera của remote player
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
            return;
        }

        cam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yaw = playerBody.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (!photonView.IsMine || cam == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookDown, maxLookUp);

        // Xoay body ngang
        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Snap camera vào vị trí mắt, xoay dọc
        cam.transform.position = cameraTarget.position;
        cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}