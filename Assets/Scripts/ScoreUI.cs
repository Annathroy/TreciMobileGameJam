using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    private int lastScore = -1;

    private void Update()
    {
        if (!scoreText) return;

        // Only update text when score changes
        if (Score.Current != lastScore)
        {
            lastScore = Score.Current;
            scoreText.text = lastScore.ToString();
        }
    }
}
