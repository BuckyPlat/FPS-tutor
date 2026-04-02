using Photon.Pun;
using TMPro;
using UnityEngine;

public class DemTime : MonoBehaviourPunCallbacks   
{
    public TextMeshProUGUI timerText;        

    public float TimeRemaining { get; set; } 

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            TimeRemaining = 300f;  
        }
    }

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            TimeRemaining -= Time.deltaTime;     
        }

        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(TimeRemaining / 60);
        int seconds = Mathf.FloorToInt(TimeRemaining % 60);

        timerText.text = $"{minutes:D2}:{seconds:D2}";
    }
}