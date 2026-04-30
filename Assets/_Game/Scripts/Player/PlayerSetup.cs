using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PlayerSetup : MonoBehaviourPunCallbacks
{
    public const string SelectedWeaponPropertyKey = "SelectedWeapon";

    public Movement movement;
    [FormerlySerializedAs("camera")]
    public GameObject localCamera;
    public string nickname;
    public TextMeshPro nicknameText;
    public Transform TPweaponHolder;

    private void Start()
    {
        ApplySelectedWeaponVisual(GetSelectedWeaponIndexFromOwner());
    }

    public void IsLocalPlayer()
    {
        TPweaponHolder.gameObject.SetActive(false);
        movement.enabled = true;
        localCamera.SetActive(true);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (photonView == null || photonView.Owner == null || targetPlayer != photonView.Owner)
            return;

        if (changedProps == null || !changedProps.TryGetValue(SelectedWeaponPropertyKey, out object weaponIndexValue))
            return;

        ApplySelectedWeaponVisual(ParseWeaponIndex(weaponIndexValue));
    }

    private int GetSelectedWeaponIndexFromOwner()
    {
        Player owner = photonView != null ? photonView.Owner : null;
        if (owner != null && owner.CustomProperties != null &&
            owner.CustomProperties.TryGetValue(SelectedWeaponPropertyKey, out object weaponIndexValue))
        {
            return ParseWeaponIndex(weaponIndexValue);
        }

        return 0;
    }

    private int ParseWeaponIndex(object weaponIndexValue)
    {
        int parsedIndex = weaponIndexValue switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            float floatValue => Mathf.RoundToInt(floatValue),
            double doubleValue => Mathf.RoundToInt((float)doubleValue),
            _ => 0
        };

        return ClampWeaponIndex(parsedIndex);
    }

    private int ClampWeaponIndex(int weaponIndex)
    {
        if (TPweaponHolder == null || TPweaponHolder.childCount <= 0)
            return 0;

        return Mathf.Clamp(weaponIndex, 0, TPweaponHolder.childCount - 1);
    }

    private void ApplySelectedWeaponVisual(int weaponIndex)
    {
        if (TPweaponHolder == null)
            return;

        foreach (Transform weapon in TPweaponHolder)
            weapon.gameObject.SetActive(false);

        if (TPweaponHolder.childCount <= 0)
            return;

        TPweaponHolder.GetChild(ClampWeaponIndex(weaponIndex)).gameObject.SetActive(true);
    }

    [PunRPC]
    public void SetNickName(string _name)
    {
        nickname = _name;
        nicknameText.text = nickname;
    }
}
