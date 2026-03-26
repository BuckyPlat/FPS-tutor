using UnityEngine;
using Photon.Pun;

public class NametagVisibility : MonoBehaviourPun
{
    [SerializeField] private GameObject nameTagParent; // Kéo "NameTagParent" (chứa Text TMP + FaceObjectToCamera) vào đây

    private void Start()
    {
        if (photonView.IsMine) // Đây là local player (chính mình)
        {
            if (nameTagParent != null)
            {
                nameTagParent.SetActive(false); // Ẩn hẳn nametag của mình
            }

            // Optional: tắt script billboard để tiết kiệm CPU
            var faceScript = nameTagParent?.GetComponentInChildren<FaceObjectToCamera>();
            if (faceScript != null) faceScript.enabled = false;
        }
        else // Remote player (người khác)
        {
            if (nameTagParent != null)
            {
                nameTagParent.SetActive(true); // Đảm bảo hiện
            }
        }
    }
}