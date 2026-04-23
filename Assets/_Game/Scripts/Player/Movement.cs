using Photon.Pun;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 14f;
    public float MaxVelocityChange = 10f;
    public float airControl = 0.5f;
    public float jumpHeight = 30f;

    [Header("Jump")]
    [Tooltip("Legacy compatibility only. Jump now commits immediately on the next physics tick.")]
    public float jumpDelay = 0f;

    [Header("Animation")]
    public Animator animator;

    private const int GroundProbeCapacity = 16;

    private Vector2 input;
    private Rigidbody rb;
    private PhotonView pv;
    private SphereCollider groundProbe;
    private readonly Collider[] groundProbeResults = new Collider[GroundProbeCapacity];
    private bool sprinting;
    private bool isGrounded;
    private bool isFalling;
    private bool animationGrounded;
    private bool jumpRequested;
    private int jumpSequence;

    private float currentXVelocity;
    private float currentYVelocity;
    public float animationSmoothSpeed = 12f;

    public float AnimationXVelocity => currentXVelocity;
    public float AnimationYVelocity => currentYVelocity;
    public bool IsGroundedForAnimation => animationGrounded;
    public bool IsFallingForAnimation => isFalling;
    public int JumpSequence => jumpSequence;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        pv = GetComponent<PhotonView>();
        groundProbe = GetComponent<SphereCollider>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (pv == null)
            pv = GetComponent<PhotonView>();

        if (PhotonNetwork.IsConnected && pv != null && !pv.IsMine)
            return;

        if (GameChat.IsPlayerChatting())
            return;

        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input.Normalize();
        sprinting = Input.GetButton("Sprint");

        if (Input.GetButtonDown("Jump"))
            jumpRequested = true;
    }

    void FixedUpdate()
    {
        if (pv == null)
            pv = GetComponent<PhotonView>();

        if (PhotonNetwork.IsConnected && pv != null && !pv.IsMine)
            return;

        if (GameChat.IsPlayerChatting())
            return;

        bool groundedThisTick = EvaluateGrounded();
        bool shouldJump = jumpRequested && groundedThisTick;

        if (groundedThisTick)
        {
            if (input.magnitude > 0.1f)
            {
                float speed = sprinting ? sprintSpeed : walkSpeed;
                rb.AddForce(CalculationMovement(speed), ForceMode.VelocityChange);
            }
            else
            {
                Vector3 velocity = rb.linearVelocity;
                velocity.x = Mathf.Lerp(velocity.x, 0f, 0.25f);
                velocity.z = Mathf.Lerp(velocity.z, 0f, 0.25f);
                rb.linearVelocity = velocity;
            }
        }
        else if (input.magnitude > 0.1f)
        {
            float speed = sprinting ? sprintSpeed * airControl : walkSpeed * airControl;
            rb.AddForce(CalculationMovement(speed), ForceMode.VelocityChange);
        }

        if (shouldJump)
            CommitJump();

        UpdateAnimationParameters();
        jumpRequested = false;
    }

    private void CommitJump()
    {
        jumpSequence++;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpHeight, rb.linearVelocity.z);
        isGrounded = false;
        animationGrounded = false;
        isFalling = false;

        if (animator != null)
            animator.SetTrigger("Jump");
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null)
            return;

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float smoothingDelta = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;

        currentXVelocity = Mathf.Lerp(currentXVelocity, localVel.x, animationSmoothSpeed * smoothingDelta);
        currentYVelocity = Mathf.Lerp(currentYVelocity, localVel.z, animationSmoothSpeed * smoothingDelta);
        animationGrounded = isGrounded;
        isFalling = !isGrounded && rb.linearVelocity.y < -1f;

        animator.SetFloat("X_Velocity", currentXVelocity);
        animator.SetFloat("Y_Velocity", currentYVelocity);
        animator.SetBool("Grounded", animationGrounded);
        animator.SetBool("Falling", isFalling);
    }

    private Vector3 CalculationMovement(float speed)
    {
        Vector3 targetVelocity = new Vector3(input.x, 0f, input.y);
        targetVelocity = transform.TransformDirection(targetVelocity) * speed;

        Vector3 velocityChange = targetVelocity - rb.linearVelocity;
        velocityChange.x = Mathf.Clamp(velocityChange.x, -MaxVelocityChange, MaxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -MaxVelocityChange, MaxVelocityChange);
        velocityChange.y = 0f;
        return velocityChange;
    }

    private bool EvaluateGrounded()
    {
        if (groundProbe == null)
        {
            groundProbe = GetComponent<SphereCollider>();
            if (groundProbe == null)
            {
                isGrounded = false;
                return false;
            }
        }

        Vector3 probeCenter = groundProbe.transform.TransformPoint(groundProbe.center);
        Vector3 probeScale = groundProbe.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(probeScale.x), Mathf.Abs(probeScale.y), Mathf.Abs(probeScale.z));
        float probeRadius = groundProbe.radius * maxScale;

        int hitCount = Physics.OverlapSphereNonAlloc(
            probeCenter,
            probeRadius,
            groundProbeResults,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            if (!IsValidGroundContact(groundProbeResults[i]))
                continue;

            isGrounded = true;
            return true;
        }

        isGrounded = false;
        return false;
    }

    private bool IsValidGroundContact(Collider other)
    {
        if (other == null || other.isTrigger)
            return false;

        Transform otherRoot = other.transform.root;
        if (otherRoot == transform.root)
            return false;

        if (otherRoot.CompareTag("Player"))
            return false;

        if (otherRoot.GetComponent<Movement>() != null)
            return false;

        return true;
    }

    private void OnDisable()
    {
        isGrounded = false;
        animationGrounded = false;
        jumpRequested = false;
    }
}
