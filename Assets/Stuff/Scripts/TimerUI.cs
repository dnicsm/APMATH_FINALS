using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimerUI : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public float timerMinutes = 5f;

    private float timeRemaining;
    private bool isTimerRunning = false;

    void Start()
    {
        timeRemaining = timerMinutes * 60f;
        isTimerRunning = true;
    }

    void Update()
    {
        if (!isTimerRunning) return;

        if (timeRemaining > 0f)
        {
            timeRemaining -= Time.deltaTime;
            
            if (timeRemaining < 0f)
            {
                timeRemaining = 0f;
                OnTimerComplete();
            }

            DisplayTime(timeRemaining);
        }
    }

    void DisplayTime(float timeToDisplay)
    {
        int minutes = Mathf.FloorToInt(timeToDisplay / 60f);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60f);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    void OnTimerComplete()
    {
        isTimerRunning = false;
        Debug.LogWarning("[TIMER] Time's up!");

        Player playerInstance = Object.FindFirstObjectByType<Player>();
        if (playerInstance != null)
        {
            playerInstance.TakeDamage(playerInstance.MaxHealth, "Time Out");
            Time.timeScale = 0f;
        }
    }
}