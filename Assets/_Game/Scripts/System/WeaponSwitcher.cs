using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class WeaponSwitcher : MonoBehaviour
{
    public Animation _animation;
    public AnimationClip draw;
    private int selectedWeapon;
    private int weaponCount;
    private bool hasStarted;
    private bool hasExternalInitialization;

    void Start()
    {
        RefreshWeaponCount();
        hasStarted = true;

        if (!hasExternalInitialization)
            selectedWeapon = GetInitialSelectedWeapon();

        SelectWeapon();
    }

    void Update()
    {
        if (GameChat.IsPlayerChatting()) return;

        int previousSelectedWeapon = selectedWeapon;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            selectedWeapon = (selectedWeapon - 1 + weaponCount) % weaponCount;
        }
        else if (scroll < 0f)
        {
            selectedWeapon = (selectedWeapon + 1) % weaponCount;
        }

        KeyCode pressedKey = GetPressedWeaponKey();
        switch (pressedKey)
        {
            case KeyCode.Alpha1: selectedWeapon = 0; break;
            case KeyCode.Alpha2: selectedWeapon = 1; break;
            case KeyCode.Alpha3: selectedWeapon = 2; break;
            case KeyCode.Alpha4: selectedWeapon = 3; break;
        }

        selectedWeapon = ClampWeaponIndex(selectedWeapon);

        if (previousSelectedWeapon != selectedWeapon)
            SelectWeapon();
    }

    public void InitializeSelectedWeapon(int weaponIndex)
    {
        RefreshWeaponCount();
        selectedWeapon = ClampWeaponIndex(weaponIndex);
        hasExternalInitialization = true;

        if (hasStarted)
            SelectWeapon();
    }

    void SelectWeapon()
    {
        RefreshWeaponCount();
        selectedWeapon = ClampWeaponIndex(selectedWeapon);

        if (_animation != null)
        {
            _animation.Stop();

            if (draw != null)
                _animation.Play(draw.name);
        }

        int i = 0;
        foreach (Transform weapon in transform)
        {
            weapon.gameObject.SetActive(i == selectedWeapon);
            i++;
        }

        PublishSelectedWeaponStateIfNeeded();
    }

    private int GetInitialSelectedWeapon()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null)
            return 0;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerSetup.SelectedWeaponPropertyKey, out object weaponIndexValue))
            return ParseWeaponIndex(weaponIndexValue);

        return 0;
    }

    private void PublishSelectedWeaponStateIfNeeded()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null)
            return;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerSetup.SelectedWeaponPropertyKey, out object currentValue) &&
            ParseWeaponIndex(currentValue) == selectedWeapon)
        {
            return;
        }

        Hashtable selectedWeaponProperty = new Hashtable
        {
            { PlayerSetup.SelectedWeaponPropertyKey, selectedWeapon }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(selectedWeaponProperty);
    }

    private void RefreshWeaponCount()
    {
        weaponCount = transform.childCount;
    }

    private int ClampWeaponIndex(int weaponIndex)
    {
        if (weaponCount <= 0)
            return 0;

        return Mathf.Clamp(weaponIndex, 0, weaponCount - 1);
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

    private KeyCode GetPressedWeaponKey()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) return KeyCode.Alpha1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return KeyCode.Alpha2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return KeyCode.Alpha3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return KeyCode.Alpha4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) return KeyCode.Alpha5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) return KeyCode.Alpha6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) return KeyCode.Alpha7;
        return KeyCode.None;
    }
}
