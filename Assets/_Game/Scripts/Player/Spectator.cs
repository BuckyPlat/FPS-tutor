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

    // Hàm này sẽ được gọi từ Health.cs khi chết, truyền vào killer
    public void Activate(Transform killerTransform = null)
    {
        gameObject.SetActive(true);

        // Tắt tất cả camera khác
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera c in cams)
        {
            if (c != cam)
                c.gameObject.SetActive(false);
        }

        cam.tag = "MainCamera";
        cam.gameObject.SetActive(true);

        // Nếu có killer → ưu tiên nhìn thẳng vào người vừa giết mình
        if (killerTransform != null)
        {
            target = killerTransform;
            Debug.Log("Spectator: Đang nhìn vào killer - " + killerTransform.name);
        }
        else
        {
            FindNewTarget();
        }
    }

    void Update()
    {
        if (target == null)
        {
            FindNewTarget();
            return;
        }

        // Camera follow mượt mà hơn một chút
        Vector3 desiredPosition = target.position + new Vector3(0, 3f, -6f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPosition, Time.deltaTime * 5f);
        cam.transform.LookAt(target.position + Vector3.up * 1.5f);   // Nhìn vào thân trên
    }

    void FindNewTarget()
    {
        PhotonView[] players = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);

        foreach (var p in players)
        {
            // Tìm người chơi còn sống và không phải bản thân
            Health health = p.GetComponent<Health>();
            if (p.IsMine == false && health != null && !health.hasDied)
            {
                target = p.transform;
                return;
            }
        }

        target = null; // Không tìm thấy ai sống
    }
}