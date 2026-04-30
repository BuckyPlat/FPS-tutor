using Photon.Pun;
using UnityEngine;

public class PlayerAnimationNetworkSync : MonoBehaviourPun, IPunObservable
{
    [Header("References")]
    [SerializeField] private Movement movement;
    [SerializeField] private PlayerSetup playerSetup;
    [SerializeField] private Animator animator;
    [SerializeField] private UpperBodyLook upperBodyLook;

    private float networkXVelocity;
    private float networkYVelocity;
    private bool networkGrounded;
    private bool networkFalling;
    private float networkAimPitch;
    private int lastReceivedJumpSequence = -1;
    private bool hasReceivedState;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!PhotonNetwork.IsConnected || photonView.IsMine || !hasReceivedState)
            return;

        ApplyRemoteState();
    }

    private void ResolveReferences()
    {
        if (movement == null)
            movement = GetComponent<Movement>();

        if (playerSetup == null)
            playerSetup = GetComponent<PlayerSetup>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (upperBodyLook == null)
            upperBodyLook = GetComponentInChildren<UpperBodyLook>(true);
    }

    private void ApplyRemoteState()
    {
        if (animator != null)
        {
            animator.SetFloat("X_Velocity", networkXVelocity);
            animator.SetFloat("Y_Velocity", networkYVelocity);
            animator.SetBool("Grounded", networkGrounded);
            animator.SetBool("Falling", networkFalling);
        }

        if (upperBodyLook != null)
            upperBodyLook.SetNetworkPitch(networkAimPitch);
    }

    private float GetOwnerAimPitch()
    {
        if (upperBodyLook != null)
            return upperBodyLook.CurrentPitch;

        if (playerSetup != null && playerSetup.GetComponent<Camera>() != null)
        {
            Vector3 forward = playerSetup.GetComponent<Camera>().transform.forward;
            float clampedY = Mathf.Clamp(forward.y, -1f, 1f);
            return Mathf.Asin(clampedY) * Mathf.Rad2Deg;
        }

        return 0f;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        ResolveReferences();

        if (stream.IsWriting)
        {
            stream.SendNext(movement != null ? movement.AnimationXVelocity : 0f);
            stream.SendNext(movement != null ? movement.AnimationYVelocity : 0f);
            stream.SendNext(movement != null && movement.IsGroundedForAnimation);
            stream.SendNext(movement != null && movement.IsFallingForAnimation);
            stream.SendNext(movement != null ? movement.JumpSequence : 0);
            stream.SendNext(GetOwnerAimPitch());
            return;
        }

        networkXVelocity = (float)stream.ReceiveNext();
        networkYVelocity = (float)stream.ReceiveNext();
        networkGrounded = (bool)stream.ReceiveNext();
        networkFalling = (bool)stream.ReceiveNext();

        int jumpSequence = (int)stream.ReceiveNext();
        networkAimPitch = (float)stream.ReceiveNext();
        hasReceivedState = true;

        if (animator != null && lastReceivedJumpSequence >= 0 && jumpSequence != lastReceivedJumpSequence)
            animator.SetTrigger("Jump");

        lastReceivedJumpSequence = jumpSequence;
        ApplyRemoteState();
    }
}
