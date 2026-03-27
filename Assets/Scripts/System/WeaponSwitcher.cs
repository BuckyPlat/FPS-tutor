using Photon.Pun;
using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    public PhotonView playerSetupView;
    public Animation _animation;
    public AnimationClip draw;

    private int selectedWeapon = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SelectWeapon();
    }

    // Update is called once per frame
    void Update()
    {
        int previousSelectedWeapon = selectedWeapon;

        KeyCode pressedKey = GetPressedWeaponKey();

        switch (pressedKey)
        {
            case KeyCode.Alpha1: selectedWeapon = 0; break;
            case KeyCode.Alpha2: selectedWeapon = 1; break;
            case KeyCode.Alpha3: selectedWeapon = 2; break;
            case KeyCode.Alpha4: selectedWeapon = 3; break;
            case KeyCode.Alpha5: selectedWeapon = 4; break;
            case KeyCode.Alpha6: selectedWeapon = 5; break;
            case KeyCode.Alpha7: selectedWeapon = 6; break;
        }

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
        foreach(Transform _weapon in transform)
        {
            if(i== selectedWeapon)
            {
                _weapon.gameObject.SetActive(true);
            }
            else
            {
                _weapon.gameObject.SetActive(false);
            }
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
