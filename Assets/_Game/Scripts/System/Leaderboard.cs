using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using UnityEngine;

public class Leaderboard : MonoBehaviour
{
    [Header("Options")]
    public float refreshRate = 1f;

    private void Start()
    {
        InvokeRepeating(nameof(Refresh), 1f, refreshRate);
    }

    public void Refresh()
    {
        var controller = UIToolkitGameplayUIController.Instance;
        if (controller == null || RoomManager.instance == null)
        {
            return;
        }

        controller.SetLeaderboardEntries(RoomManager.instance.BuildLeaderboardEntries());
    }

    private void Update()
    {
        if (UIToolkitGameplayUIController.IsGameplayInputBlocked)
        {
            UIToolkitGameplayUIController.Instance?.SetLeaderboardVisible(false);
            return;
        }

        UIToolkitGameplayUIController.Instance?.SetLeaderboardVisible(Input.GetKey(KeyCode.Tab));
    }
}
