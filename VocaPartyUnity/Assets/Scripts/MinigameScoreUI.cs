using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class MinigameScoreUI : MonoBehaviour
{
    public static MinigameScoreUI Instance;

    [SerializeField] private GameObject minigameResultsObject;
    [SerializeField] private TextMeshProUGUI scoresText;

    private void Awake()
    {
        Instance = this;
        minigameResultsObject.SetActive(false);
    }

    public void ShowResults(string[] winners, Dictionary<string, int> scores)
    {
        minigameResultsObject.SetActive(true);

        StringBuilder sb = new StringBuilder();

        foreach (var kvp in scores)
        {
            if (BoardManager.Instance.TryGetPlayer(kvp.Key, out Player player))
            {
                sb.AppendLine($"{player.playerName}: {kvp.Value}");
            }
            else
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}"); // fallback
            }
        }

        sb.AppendLine("\nWinner(s):");
        foreach (var w in winners)
        {
            if (BoardManager.Instance.TryGetPlayer(w, out Player player))
            {
                sb.AppendLine(player.playerName);
            }
            else
            {
                sb.AppendLine(w);
            }
        }

        scoresText.text = sb.ToString();
    }

    public void Hide()
    {
        minigameResultsObject.SetActive(false);
    }
}
