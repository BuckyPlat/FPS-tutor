using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerSetup : MonoBehaviour
{
    public Movement movement;
    public GameObject camera;
    public string nickname;
    public TextMeshPro nicknameText;
    public Transform TPweaponHolder;

    public void IsLocalPlayer()
    {
        TPweaponHolder.gameObject.SetActive(false);
        movement.enabled = true;
        camera.SetActive(true);
    }

    [PunRPC]
    public void SetTPWeapon(int _weaponIndex)
    {
        foreach (Transform _weapon in TPweaponHolder)
        {
            _weapon.gameObject.SetActive(false);
        }

        TPweaponHolder.GetChild(_weaponIndex).gameObject.SetActive(true);
    }



    [PunRPC]
    public void SetNickName(string _name)
    {
        nickname = _name;
        nicknameText.text = nickname;
    }
}
