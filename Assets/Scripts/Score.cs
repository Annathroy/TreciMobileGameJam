using UnityEngine;

public static class Score
{
    private const string HIGH_KEY = "HighScore";

    public static int Current { get; private set; }
    public static int High { get; private set; }

    static Score()
    {
        High = PlayerPrefs.GetInt(HIGH_KEY, 0);
        Current = 0;
    }

    public static void ResetRun() => Current = 0;

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Current += amount;
        if (Current > High)
        {
            High = Current;
            PlayerPrefs.SetInt(HIGH_KEY, High);
            PlayerPrefs.Save();
        }
    }

    public static void ResetHigh()
    {
        High = 0;
        PlayerPrefs.DeleteKey(HIGH_KEY);
        PlayerPrefs.Save();
    }
}
