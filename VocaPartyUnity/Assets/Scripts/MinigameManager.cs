using System.Collections.Generic;
using UnityEngine;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance;

    //[SerializeField] private GameObject minigamePanelPrefab;
    //private GameObject activePanel;

    private List<Player> players;

    // Timer
    private float minigameTimer = 0f;
    public readonly float minigameDuration = 15f;
    private bool minigameActive = false;

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

    private void Update()
    {
        // Timer for minigame
        if (BoardManager.Instance.currentPhase == GamePhase.Minigame && minigameActive)
        {
            minigameTimer -= Time.deltaTime;

            if (minigameTimer <= 0f)
            {
                Debug.Log("Minigame timer expired!");
                minigameActive = false;
                minigameTimer = 0f;

                SocketManager.Instance.NotifyMinigameEnded();

                foreach (Player player in players.ToArray())
                {
                    OnPlayerFinishedMinigame(player.playerId, false);
                }
            }
        }
    }

    public void StartMinigame(List<Player> currentPlayers, MinigameType minigame)
    {
        players = new List<Player>(currentPlayers);

        // Show UI
        //activePanel = Instantiate(minigamePanelPrefab);
        //activePanel.SetActive(true);

        // Reset and start timer
        minigameTimer = minigameDuration;
        minigameActive = true;

        // Tell website to start the minigame
        foreach (var player in players)
        {
            SocketManager.Instance.StartMinigameForPlayer(player.playerId, minigame, minigameDuration);
        }

        Debug.Log($"Minigame phase started: {minigame}, duration: {minigameDuration}s");
    }

    public void OnPlayerFinishedMinigame(string playerId, bool correct)
    {
        if (!players.Exists(p => p.playerId == playerId)) return;

        Player player = players.Find(p => p.playerId == playerId);

        if (correct)
        {
            PowerupType randomPowerup = player.GetRandomPowerup();
            player.PlayerState.inventory.Add(randomPowerup);
            Debug.Log($"{player.playerId} answered correctly and received powerup: " + randomPowerup);
        }
        else
        {
            Debug.Log($"{player.playerId} answered incorrectly.");
        }

        players.Remove(player);

        // When all finished
        if (players.Count == 0)
        {
            EndMinigamePhase();
        }
    }

    private void EndMinigamePhase()
    {
        minigameActive = false;
        //Destroy(activePanel);
        Debug.Log("Minigame phase ended!");
        BoardManager.Instance.StartNextTurn();
    }
}
