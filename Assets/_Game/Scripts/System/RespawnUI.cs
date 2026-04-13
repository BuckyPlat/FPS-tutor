using TMPro;
using UnityEngine;

public class RespawnUI : MonoBehaviour
{
    public static RespawnUI Instance;

    public GameObject panel;
    public TextMeshProUGUI countdownText;

    void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    public void Show(float time)
    {
        panel.SetActive(true);
        StartCoroutine(Countdown(time));
    }

    System.Collections.IEnumerator Countdown(float time)
    {
        float t = time;

        while (t > 0)
        {
            countdownText.text = "Respawn in: " + Mathf.Ceil(t);
            yield return new WaitForSeconds(1f);
            t--;
        }

        panel.SetActive(false);
    }
}