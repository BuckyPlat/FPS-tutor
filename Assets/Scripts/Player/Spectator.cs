using UnityEngine;
using Photon.Pun;

public class Spectator : MonoBehaviour
{
    public static Spectator Instance;

    public Camera cam;
    private Transform target;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void Activate()
    {
        gameObject.SetActive(true);

        // tắt camera khác
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera c in cams)
        {
            if (c != cam)
                c.gameObject.SetActive(false);
        }

        cam.gameObject.SetActive(true);

        FindNewTarget();
    }

    void Update()
    {
        if (target == null)
        {
            FindNewTarget();
            return;
        }

        cam.transform.position = target.position + new Vector3(0, 3, -5);
        cam.transform.LookAt(target);
    }

    void FindNewTarget()
    {
        PhotonView[] players = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);

        foreach (var p in players)
        {
            if (!p.IsMine)
            {
                target = p.transform;
                return;
            }
        }
    }
}