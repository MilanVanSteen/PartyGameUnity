using System.Collections.Generic;
using UnityEngine;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance;

    //[SerializeField] private GameObject minigamePanelPrefab;
    //private GameObject activePanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartMinigame(MinigameType minigame, float duration)
    {
        // Show UI
        //activePanel = Instantiate(minigamePanelPrefab);
        //activePanel.SetActive(true);

        // Tell website to start the minigame
        SocketManager.Instance.StartMinigame(minigame, duration);

        Debug.Log($"MinigameManager: Minigame phase started: {minigame}, duration: {duration}s");
    }

    public void OnResults(string[] winners, Dictionary<string, int> scores)
    {
        Debug.Log("MinigameManager: Minigame finished!");

        foreach (var kvp in scores)
        {
            Debug.Log($"MinigameManager: Player {kvp.Key} score: {kvp.Value}");
        }

        // 1. Reward phase
        foreach (var winnerId in winners)
        {
            if (BoardManager.Instance.TryGetPlayer(winnerId, out Player player))
            {
                PowerupType reward = player.GetRandomPowerup();
                player.PlayerState.inventory.Add(reward);

                Debug.Log($"MinigameManager: {player.playerId} WON → got {reward}");
            }
        }

        // 2. Continue next turn
        Debug.Log("MinigameManager: Minigame phase ended!");
        BoardManager.Instance.StartNextTurn();
    }
}
