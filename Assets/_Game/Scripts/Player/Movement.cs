using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
public class Movement : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 14f;
    public float MaxVelocityChange = 10f;
    [Space]
    public float airControl = 0.5f;
    [Space]
    public float jumpHeight = 30f;

    private Vector2 input;
    private Rigidbody rb;

    private bool sprinting;
    private bool jumping;

    private bool grounded = false;

    [Header("Animation")] public Animation handAnimation;
    public AnimationClip handWalkAnimation;
    public AnimationClip idleAnimation;

    private PhotonView pv;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        pv = GetComponent<PhotonView>();
    }

    // Update is called once per frame
    void Update()
    {
        if (GameChat.IsPlayerChatting()) return;
        input = new Vector2(x: Input.GetAxisRaw("Horizontal"), y: Input.GetAxisRaw("Vertical"));
        input.Normalize();

        sprinting = Input.GetButton("Sprint");
        jumping = Input.GetButton("Jump");

    }

    private void OnTriggerStay(Collider other)
    {
        grounded = true;
    }

    void FixedUpdate()
    {
        if (!pv.IsMine || GameChat.IsPlayerChatting()) return;
        if (!pv.IsMine) return;
        if (grounded)
        {
            if (jumping)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpHeight, rb.linearVelocity.z);
            }
            else if (input.magnitude > 0.5f)
            {
                handAnimation.clip = handWalkAnimation;
                handAnimation.Play();
                rb.AddForce(CalculationMovement(sprinting ? sprintSpeed : walkSpeed), ForceMode.VelocityChange);
            }
            else
            {
                handAnimation.clip = idleAnimation;
                handAnimation.Play();

                var velocity1 = rb.linearVelocity;
                velocity1 = new Vector3(velocity1.x * 0.2f * Time.fixedDeltaTime, velocity1.y, velocity1.z * 0.2f * Time.fixedDeltaTime);
                rb.linearVelocity = velocity1;
            }
        }
        else
        {
            if (input.magnitude > 0.5f)
            {
                rb.AddForce(CalculationMovement(sprinting ? sprintSpeed * airControl : walkSpeed * airControl), ForceMode.VelocityChange);
            }
            else
            {
                var velocity1 = rb.linearVelocity;
                velocity1 = new Vector3(velocity1.x * 0.2f * Time.fixedDeltaTime, velocity1.y, velocity1.z * 0.2f * Time.fixedDeltaTime);
                rb.linearVelocity = velocity1;
            }
        }

        grounded = false;
    }

    Vector3 CalculationMovement(float _speed)
    {
        Vector3 targerVelocity = new Vector3(input.x, y: 0, z:input.y);
        targerVelocity = transform.TransformDirection(targerVelocity);

        targerVelocity *= _speed;

        Vector3 velocity = rb.linearVelocity;

        if(input.magnitude > 0.5f)
        {
            Vector3 velocityChange = targerVelocity - velocity;

            velocityChange.x = Mathf.Clamp(velocityChange.x, -MaxVelocityChange, MaxVelocityChange);
            velocityChange.z = Mathf.Clamp(velocityChange.z, -MaxVelocityChange, MaxVelocityChange);
            velocityChange.y = 0;

            return(velocityChange);
        }
        else
        {
            return new Vector3();
        }
    }
}
