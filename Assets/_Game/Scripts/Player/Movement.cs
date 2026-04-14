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
    public float jumpDelay = 0.1f;        // delay trước khi vọt lên

    [Header("Animation")]
    public Animator animator;

    private Vector2 input;
    private Rigidbody rb;
    private PhotonView pv;
    private bool sprinting;
    private bool isGrounded = false;

    private bool jumpQueued = false;  // đang chờ delay
    private bool hasJumped = false;  // đã vọt lên rồi, chờ chạm đất
    private float jumpTimer = 0f;

    private float currentXVelocity = 0f;
    private float currentYVelocity = 0f;
    public float animationSmoothSpeed = 12f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        pv = GetComponent<PhotonView>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (GameChat.IsPlayerChatting()) return;

        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input.Normalize();
        sprinting = Input.GetButton("Sprint");

        // Chỉ nhận input khi đang đứng yên dưới đất, chưa queue jump
        if (Input.GetButtonDown("Jump") && isGrounded && !jumpQueued && !hasJumped)
        {
            jumpQueued = true;
            jumpTimer = 0f;
            animator.SetTrigger("Jump");   // kích hoạt animation chuẩn bị nhảy
        }

        // Đếm delay
        if (jumpQueued)
        {
            jumpTimer += Time.deltaTime;
            if (jumpTimer >= jumpDelay)
            {
                jumpQueued = false;
                hasJumped = true;
                DoJump();
            }
        }
    }

    private void DoJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpHeight, rb.linearVelocity.z);
    }

    void FixedUpdate()
    {
        if (!pv.IsMine || GameChat.IsPlayerChatting()) return;

        if (isGrounded)
        {
            if (input.magnitude > 0.1f)
            {
                float speed = sprinting ? sprintSpeed : walkSpeed;
                rb.AddForce(CalculationMovement(speed), ForceMode.VelocityChange);
            }
            else
            {
                var vel = rb.linearVelocity;
                vel.x = Mathf.Lerp(vel.x, 0, 0.25f);
                vel.z = Mathf.Lerp(vel.z, 0, 0.25f);
                rb.linearVelocity = vel;
            }
        }
        else
        {
            if (input.magnitude > 0.1f)
            {
                float speed = sprinting ? sprintSpeed * airControl : walkSpeed * airControl;
                rb.AddForce(CalculationMovement(speed), ForceMode.VelocityChange);
            }
        }

        // Reset hasJumped khi chạm đất
        if (isGrounded && hasJumped && rb.linearVelocity.y <= 0f)
            hasJumped = false;

        UpdateAnimationParameters();
        isGrounded = false;
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null) return;

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        currentXVelocity = Mathf.Lerp(currentXVelocity, localVel.x, animationSmoothSpeed * Time.deltaTime);
        currentYVelocity = Mathf.Lerp(currentYVelocity, localVel.z, animationSmoothSpeed * Time.deltaTime);

        animator.SetFloat("X_Velocity", currentXVelocity);
        animator.SetFloat("Y_Velocity", currentYVelocity);
        animator.SetBool("Grounded", isGrounded);
        animator.SetBool("Falling", !isGrounded && rb.linearVelocity.y < -1f);
    }

    Vector3 CalculationMovement(float _speed)
    {
        Vector3 targetVel = new Vector3(input.x, 0, input.y);
        targetVel = transform.TransformDirection(targetVel) * _speed;
        Vector3 velocityChange = targetVel - rb.linearVelocity;
        velocityChange.x = Mathf.Clamp(velocityChange.x, -MaxVelocityChange, MaxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -MaxVelocityChange, MaxVelocityChange);
        velocityChange.y = 0;
        return velocityChange;
    }

    private void OnTriggerStay(Collider other)
    {
        isGrounded = true;
    }
}