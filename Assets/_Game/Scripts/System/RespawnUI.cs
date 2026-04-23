using UnityEngine;

public class RespawnUI : MonoBehaviour
{
    public static RespawnUI Instance;

    void Awake()
    {
        Instance = this;
        UIToolkitGameplayUIController.Instance?.HideRespawn();
    }

    public void Show(float time)
    {
        StartCoroutine(Countdown(time));
    }

    System.Collections.IEnumerator Countdown(float time)
    {
        float t = time;

        while (t > 0)
        {
            UIToolkitGameplayUIController.Instance?.ShowRespawnCountdown(Mathf.CeilToInt(t));
            yield return new WaitForSeconds(1f);
            t--;
        }

        UIToolkitGameplayUIController.Instance?.HideRespawn();
    }
}
