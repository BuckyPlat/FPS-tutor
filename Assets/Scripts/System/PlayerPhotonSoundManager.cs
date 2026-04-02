using JetBrains.Annotations;
using Photon.Pun;
using UnityEngine;

public class PlayerPhotonSoundManager : MonoBehaviour
{
    public AudioSource FootStepsSouce;
    public AudioClip footstepsSFX;

    public AudioSource gunshootSource;
    public AudioClip[] allGunShootSFX;

    public AudioSource reloadSource;
    public AudioClip[] allReloadSFX;

    public AudioSource explosideSource;
    public AudioClip explosionSFX;

    public void PlayFootStepsSFX()
    {
        GetComponent<PhotonView>().RPC("PlayerFootStepsSFX_RPC", RpcTarget.All);
    }

    [PunRPC]
    public void PlayerFootStepsSFX_RPC()
    {
        FootStepsSouce.clip = footstepsSFX;

        FootStepsSouce.pitch = UnityEngine.Random.Range(0.7f, 1.2f);
        FootStepsSouce.volume = UnityEngine.Random.Range(0.2f, 0.35f);

        FootStepsSouce.Play();
    }

    public void PlayShootSFX(int index)
    {
        GetComponent<PhotonView>().RPC("PlayShootSFX_RPC", RpcTarget.All, index);
    }

    [PunRPC]
    public void PlayShootSFX_RPC(int index)
    {
        gunshootSource.clip = allGunShootSFX[index];

        gunshootSource.Play();
    }

    public void PlayReloadSFX(int index)
    {
        GetComponent<PhotonView>().RPC("PlayReloadSFX_RPC", RpcTarget.All, index);
    }

    [PunRPC]
    public void PlayReloadSFX_RPC(int index)
    {
        reloadSource.clip = allReloadSFX[index];

        reloadSource.Play();
    }

    public void PlayExplosionSFX(Vector3 position)
    {
        GetComponent<PhotonView>().RPC("PlayExplosionSFX_RPC", RpcTarget.All, position);
    }
    [PunRPC]
    public void PlayExplosionSFX_RPC(Vector3 position)
    {
        if (explosionSFX == null) return;
        // Phát đúng tại vị trí vụ nổ (3D spatial sound)
        AudioSource.PlayClipAtPoint(explosionSFX, position);
    }
}
