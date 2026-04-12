using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;
    
    public GameObject player;

    [Space]
    public Transform[] spawnPoints;

    [Space]
    public GameObject roomCam;

    [Space]
    public GameObject nameUI;
    public GameObject connectingUI;

    private string nickname = "unnamed";

    public string mapName = "Nothing";

    [HideInInspector]
    public int Kills = 0;
    [HideInInspector]
    public int Deaths = 0;

    void Awake()
    {
        instance = this;
    }

    public void ChangeNickName(string _name)
    {
        nickname = _name;
    }

    public void JoinRoomButtonPressed()
    {
        Debug.Log(message: "Connecting....");

        RoomOptions ro = new RoomOptions();

        ro.CustomRoomProperties = new Hashtable()
        {
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
            { "mapName", mapName }
        };

        ro.CustomRoomPropertiesForLobby = new[]
        {
            "mapSceneIndex",
            "mapName"
        };

        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin"), ro,null);

        nameUI.SetActive(false);
        connectingUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        roomCam.SetActive(false);

        Debug.Log("We'er connected and in a room!!!");

        SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        _player.GetComponent<PlayerSetup>().IsLocalPlayer();
        _player.GetComponent<Health>().isLocalPlayer = true;

        _player.GetComponent<PhotonView>().RPC("SetNickName", RpcTarget.AllBuffered, nickname);
        PhotonNetwork.LocalPlayer.NickName = nickname;
    }
    public void SetHashes()
    {
        try
        {
            Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash["Kills"] = Kills;
            hash["Deaths"] = Deaths;
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
        }
        catch
        {
            //Do nothing
        }
    }
    public void StartRespawn(float delay)
    {
        StartCoroutine(RespawnDelay(delay));
    }

    IEnumerator RespawnDelay(float delay)
    {
        if (RespawnUI.Instance != null)
            RespawnUI.Instance.Show(delay);

        yield return new WaitForSeconds(delay);

        if (Spectator.Instance != null)
            Spectator.Instance.gameObject.SetActive(false);

        SpawnPlayer();
        Deaths++;
        SetHashes();
    }
}
