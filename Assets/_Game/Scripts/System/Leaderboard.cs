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
        if (controller == null)
        {
            return;
        }

        var sortedPlayerList =
            (from player in PhotonNetwork.PlayerList orderby player.GetScore() descending select player).ToList();

        var entries = new List<UIToolkitGameplayUIController.LeaderboardEntryData>(sortedPlayerList.Count);

        for (int i = 0; i < sortedPlayerList.Count; i++)
        {
            var player = sortedPlayerList[i];
            string playerName = string.IsNullOrWhiteSpace(player.NickName) ? "Unnamed" : player.NickName;
            string kd = "0/0";

            if (player.CustomProperties["Kills"] != null)
            {
                kd = player.CustomProperties["Kills"] + "/" + player.CustomProperties["Deaths"];
            }

            entries.Add(new UIToolkitGameplayUIController.LeaderboardEntryData
            {
                Rank = i + 1,
                Name = playerName,
                Score = player.GetScore(),
                Kd = kd
            });
        }

        controller.SetLeaderboardEntries(entries);
    }

    private void Update()
    {
        UIToolkitGameplayUIController.Instance?.SetLeaderboardVisible(Input.GetKey(KeyCode.Tab));
    }
}
