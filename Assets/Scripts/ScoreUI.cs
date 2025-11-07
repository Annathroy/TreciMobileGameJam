// ScoreUI.cs
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    private int lastShown = int.MinValue;

    private void Update()
    {
        if (!scoreText) return;
        if (Score.Current == lastShown) return;

        lastShown = Score.Current;
        scoreText.text = lastShown.ToString();
    }
}
