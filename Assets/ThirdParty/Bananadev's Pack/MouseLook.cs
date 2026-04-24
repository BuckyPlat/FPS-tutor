using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public static MouseLook instance;

    [Header("Settings")]
    public Vector2 clampInDegrees = new Vector2(360, 180);
    public bool lockCursor = true;

    [Space]
    public Vector2 sensitivity = new Vector2(2, 2);
    public bool invertMouse = false;

    [Space]
    public Vector2 smoothing = new Vector2(3, 3);

    [Header("Save Keys")]
    private const string SENS_X_KEY = "MouseSensX";
    private const string SENS_Y_KEY = "MouseSensY";
    private const string SMOOTH_KEY = "MouseSmooth";
    private const string INVERT_KEY = "MouseInvert";

    [Header("First Person")]
    public GameObject characterBody;

    private Rigidbody characterBodyRigidbody;
    private Vector2 targetDirection;
    private Vector2 targetCharacterDirection;
    private Quaternion desiredBodyRotation = Quaternion.identity;

    private Vector2 _mouseAbsolute;
    private Vector2 _smoothMouse;

    private Vector2 mouseDelta;

    [HideInInspector]
    public bool scoped;

    public float PitchDegrees => -_mouseAbsolute.y;
    public float DesiredBodyYawDegrees =>
        characterBody != null ? desiredBodyRotation.eulerAngles.y : transform.eulerAngles.y;

    void Start()
    {
        instance = this;
        ApplyRuntimeSettingsFromPrefs();

        // Set target direction to the camera's initial orientation.
        targetDirection = transform.localRotation.eulerAngles;

        // Set target direction for the character body to its inital state.
        if (characterBody)
        {
            targetCharacterDirection = characterBody.transform.localRotation.eulerAngles;
            desiredBodyRotation = characterBody.transform.rotation;
            characterBodyRigidbody = characterBody.GetComponent<Rigidbody>();
        }
        
        if (lockCursor)
            LockCursor();

    }

    public void LockCursor()
    {
        // make the cursor hidden and locked
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (UIToolkitGameplayUIController.IsGameplayInputBlocked)
            return;

        // Allow the script to clamp based on a desired target value.
        var targetOrientation = Quaternion.Euler(targetDirection);
        var targetCharacterOrientation = Quaternion.Euler(targetCharacterDirection);

        // Get raw mouse input for a cleaner reading on more sensitive mice.
        float mouseY = Input.GetAxisRaw("Mouse Y");
        if (invertMouse)
            mouseY *= -1f;

        mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), mouseY);

        // Scale input against the sensitivity setting and multiply that against the smoothing value.
        mouseDelta = Vector2.Scale(mouseDelta, new Vector2(sensitivity.x * smoothing.x, sensitivity.y * smoothing.y));

        // Interpolate mouse movement over time to apply smoothing delta.
        _smoothMouse.x = Mathf.Lerp(_smoothMouse.x, mouseDelta.x, 1f / smoothing.x);
        _smoothMouse.y = Mathf.Lerp(_smoothMouse.y, mouseDelta.y, 1f / smoothing.y);

        // Find the absolute mouse movement value from point zero.
        _mouseAbsolute += _smoothMouse;

        // Clamp and apply the local x value first, so as not to be affected by world transforms.
        if (clampInDegrees.x < 360)
            _mouseAbsolute.x = Mathf.Clamp(_mouseAbsolute.x, -clampInDegrees.x * 0.5f, clampInDegrees.x * 0.5f);

        // Then clamp and apply the global y value.
        if (clampInDegrees.y < 360)
            _mouseAbsolute.y = Mathf.Clamp(_mouseAbsolute.y, -clampInDegrees.y * 0.5f, clampInDegrees.y * 0.5f);

        transform.localRotation = Quaternion.AngleAxis(-_mouseAbsolute.y, targetOrientation * Vector3.right) * targetOrientation;

        // If there's a character body that acts as a parent to the camera
        if (characterBody)
        {
            var yRotation = Quaternion.AngleAxis(_mouseAbsolute.x, Vector3.up);
            desiredBodyRotation = yRotation * targetCharacterOrientation;

            if (characterBodyRigidbody == null)
                characterBody.transform.localRotation = desiredBodyRotation;
        }
        else
        {
            var yRotation = Quaternion.AngleAxis(_mouseAbsolute.x, transform.InverseTransformDirection(Vector3.up));
            transform.localRotation *= yRotation;
        }
    }

    void FixedUpdate()
    {
        if (characterBodyRigidbody == null || UIToolkitGameplayUIController.IsGameplayInputBlocked)
            return;

        Quaternion bodyRotation = Quaternion.Euler(0f, desiredBodyRotation.eulerAngles.y, 0f);
        characterBodyRigidbody.MoveRotation(bodyRotation);
    }

    public void ApplyRuntimeSettings(float sensX, float sensY, float smoothAmount, bool invert)
    {
        sensitivity.x = sensX;
        sensitivity.y = sensY;
        smoothing.x = Mathf.Max(0.01f, smoothAmount);
        smoothing.y = smoothing.x;
        invertMouse = invert;
    }

    public void ApplyRuntimeSettingsFromPrefs()
    {
        ApplyRuntimeSettings(
            PlayerPrefs.GetFloat(SENS_X_KEY, 2f),
            PlayerPrefs.GetFloat(SENS_Y_KEY, 2f),
            PlayerPrefs.GetFloat(SMOOTH_KEY, 3f),
            PlayerPrefs.GetInt(INVERT_KEY, 0) == 1);
    }
}
