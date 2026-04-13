using UnityEngine;

public class SendAnimationEventToSFXManager : MonoBehaviour
{
    public PlayerPhotonSoundManager soundManager;

    public void TriggerFootStepSFX()
    {
        soundManager.PlayFootStepsSFX();
    }
}
