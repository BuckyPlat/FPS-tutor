using Photon.Pun;
using UnityEngine;

public class HealthDrop : MonoBehaviour
{
    public int healAmount = 25;

    private void OnTriggerEnter(Collider other)
    {
        Health playerHealth = other.GetComponentInParent<Health>();
        if (playerHealth == null) return;

        if (!playerHealth.photonView.IsMine) return;

        playerHealth.RestoreHealth(healAmount);

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

}
