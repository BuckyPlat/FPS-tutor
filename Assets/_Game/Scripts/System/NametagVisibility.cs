using UnityEngine;
using Photon.Pun;

public class NametagVisibility : MonoBehaviourPun
{
    [SerializeField] private GameObject nameTagParent; // Drag "NameTagParent" (contains Text TMP + FaceObjectToCamera) here

    private void Start()
    {
        if (photonView.IsMine) // This is the local player (yourself)
        {
            if (nameTagParent != null)
            {
                nameTagParent.SetActive(false); // Completely hide own nametag
            }

            // Optional: disable billboard script to save CPU
            var faceScript = nameTagParent?.GetComponentInChildren<FaceObjectToCamera>();
            if (faceScript != null) faceScript.enabled = false;
        }
        else // Remote player (others)
        {
            if (nameTagParent != null)
            {
                nameTagParent.SetActive(true); // Ensure visible
            }
        }
    }
}