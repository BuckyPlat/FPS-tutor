using UnityEngine;
using TMPro;
using System;
using Unity.Mathematics;
public class Coin : MonoBehaviour
{
    public int coinValue = 200;
    public int coinCount = 0;
    public TextMeshPro coinText;
    private void Start()
    {
        if (coinText != null)
        {
            coinCount = PlayerPrefs.GetInt("CoinCount", 1000);

        }
    }
    private void Update()
    {
        if (coinText != null)
        {
            coinText.text = "Coins: " + coinCount.ToString();
        }
    }
    public void Buychest()
    {

        if(coinCount < 50)
        {

            Debug.Log("Not enough coins to buy chest! Coin count reset to 0.");
        }
        else         
        {
            luckychest();
            coinCount -= 50;
        }
    }
    public void Buychest1()
    {
        if (coinCount < 100)
        {
            
            Debug.Log("Not enough coins to buy chest! Coin count reset to 0.");
        }
        else
        {
            coinCount -= 100;
            luckychest();
        }
    }
    public void Buychest2()
    {
        if (coinCount < 200)
        {

            Debug.Log("Not enough coins to buy chest! Coin count reset to 0.");
        }
        else
        {
            luckychest();
            coinCount -= 200;
        }
    }


    public void luckychest()
    {
        // Probability tiers:
        // 70% common  -> small reward
        // 25% rare    -> medium reward
        // 5%  epic    -> large reward
        float roll = UnityEngine.Random.value; // 0..1
        int reward;

        if (roll < 0.70f)
        {
            // Common reward: 10 - 50
            reward = UnityEngine.Random.Range(10, 51);
        }
        else if (roll < 0.95f)
        {
            // Rare reward: 100 - 300
            reward = UnityEngine.Random.Range(100, 301);
        }
        else
        {
            // Epic reward: 500 - 1000
            reward = UnityEngine.Random.Range(500, 1001);
        }

        coinCount += reward;
        PlayerPrefs.SetInt("CoinCount", coinCount);
        PlayerPrefs.Save();

        Debug.Log($"Lucky chest opened: +{reward} coins (roll={roll:F2}). Total coins: {coinCount}");

        // Immediately update UI if present (Update() will also keep it current)
        if (coinText != null)
            coinText.text = "Coins: " + coinCount.ToString();
    }
}