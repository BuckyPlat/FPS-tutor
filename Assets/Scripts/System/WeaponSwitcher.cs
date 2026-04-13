using Photon.Pun;
using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    public PhotonView playerSetupView;
    public Animation _animation;
    public AnimationClip draw;
    private int selectedWeapon = 0;
    private int weaponCount = 0;

    void Start()
    {
        weaponCount = transform.childCount;
        SelectWeapon();
    }

    void Update()
    {
        if (GameChat.IsPlayerChatting()) return;

        int previousSelectedWeapon = selectedWeapon;

        // Mouse scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            selectedWeapon = (selectedWeapon - 1 + weaponCount) % weaponCount;
        }
        else if (scroll < 0f)
        {
            selectedWeapon = (selectedWeapon + 1) % weaponCount;
        }

        // Number keys
        KeyCode pressedKey = GetPressedWeaponKey();
        switch (pressedKey)
        {
            case KeyCode.Alpha1: selectedWeapon = 0; break;
            case KeyCode.Alpha2: selectedWeapon = 1; break;
            case KeyCode.Alpha3: selectedWeapon = 2; break;
            case KeyCode.Alpha4: selectedWeapon = 3; break;
        }

        // Clamp in case weaponCount changes
        selectedWeapon = Mathf.Clamp(selectedWeapon, 0, weaponCount - 1);

        if (previousSelectedWeapon != selectedWeapon)
        {
            SelectWeapon();
        }
    }

    void SelectWeapon()
    {
        playerSetupView.RPC("SetTPWeapon", RpcTarget.All, selectedWeapon);
        _animation.Stop();
        _animation.Play(draw.name);

        int i = 0;
        foreach (Transform _weapon in transform)
        {
            _weapon.gameObject.SetActive(i == selectedWeapon);
            i++;
        }
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