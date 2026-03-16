using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    private List<Player> players = new();
    private int playersMoving = 0;

    // Timer
    private bool powerupPhaseActive = false;
    private float powerupTimer = 0f;
    private float powerupPhaseDuration = 10f;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // Timer
        if (!powerupPhaseActive) return;

        powerupTimer -= Time.deltaTime;

        if (powerupTimer <= 0f)
        {
            EndPowerupPhase();
        }
    }

    public void RegisterPlayer(Player player)
    {
        if (!players.Contains(player))
        {
            players.Add(player);
        }
    }
    
    public void RollForAllPlayers()
    {
        playersMoving = 0;

        foreach (Player player in players)
        {
            PlayerState playerState = player.GetComponent<PlayerState>();

            if(!playerState.isMoving && playerState.CanPlayTurn())
            {
                // Shield handling
                if(playerState.shieldTurns > 0)
                {
                    playerState.shieldTurns -= 1;
                }

                int roll = Random.Range(1, 7);
                Debug.Log("Player " + playerState.playerIndex + " rolled " + roll);

                playersMoving++;

                if(playerState.addedStepsNextRoll > 0)
                {
                    roll += playerState.addedStepsNextRoll;
                }
                player.MoveSteps(roll);
            }
        }
    }

    public void PlayerFinishedMoving()
    {
        playersMoving--;

        if (playersMoving <= 0)
        {
            AllPlayersFinished();
        }
    }
    private void AllPlayersFinished()
    {
        Debug.Log("All players finished moving!");

        StartPowerupPhase();
    }

    private void StartPowerupPhase()
    {
        Debug.Log("Powerup phase started!");

        powerupPhaseActive = true;
        powerupTimer = powerupPhaseDuration;

        foreach (Player player in players)
        {
            PlayerState state = player.GetComponent<PlayerState>();

            if (state.inventory.Count > 0)
            {
                Debug.Log("Player " + state.playerIndex + " can use powerups.");
            }
        }
    }
    private void EndPowerupPhase()
    {
        powerupPhaseActive = false;

        Debug.Log("Powerup phase ended!");

        // Next phase later:
        // StartMinigame();
    }

    public void UsePowerup(Player player, int inventoryIndex)
    {
        if (powerupPhaseActive)
        {
            PlayerState state = player.playerState;
            if (inventoryIndex < state.inventory.Count)
            {
                PowerupType powerup = state.inventory[inventoryIndex];
                Debug.Log("Before use: " + string.Join(", ", state.inventory));
                ExecutePowerup(player, powerup);
                state.inventory.RemoveAt(inventoryIndex);
                Debug.Log("After use: " + string.Join(", ", state.inventory));
            }
        }
    }
    private void ExecutePowerup(Player player, PowerupType powerup)
    {
        Player target = players.Find(p => p != player);

        switch (powerup)
        {
            case PowerupType.ExtraRoll:
                int roll = Random.Range(1, 7);
                Debug.Log("Extra roll: " + roll);
                player.MoveSteps(roll);
                break;

            case PowerupType.AddedSteps:
                Debug.Log("Next roll +2");
                player.playerState.addedStepsNextRoll += 2;
                break;

            case PowerupType.Shield:
                Debug.Log("Shield activated for 2 turns");
                player.playerState.shieldTurns = 2;
                break;

            case PowerupType.SwapPosition:
                // Temporary keyboard test: swap with first other player
                if (target != null)
                {
                    if (target.playerState.shieldTurns > 0)
                    {
                        Debug.Log("Target is shielded! Powerup blocked.");
                        return;
                    }
                    Vector3 temp = player.transform.position;
                    player.transform.position = target.transform.position;
                    target.transform.position = temp;

                    Tile tempTile = player.currentTile;
                    player.currentTile = target.currentTile;
                    target.currentTile = tempTile;

                    Debug.Log($"{player.playerState.playerIndex} swapped with {target.playerState.playerIndex}");
                }
                break;

            case PowerupType.SendPlayerBack:
                // Temporary: send first other player back 2 tiles
                if (target != null)
                {
                    if (target.playerState.shieldTurns > 0)
                    {
                        Debug.Log("Target is shielded! Powerup blocked.");
                        return;
                    }
                    Tile tile = target.currentTile;
                    for (int i = 0; i < 2; i++)
                    {
                        if (tile.neighbors.Count > 0) tile = tile.neighbors[0]; // move backward along first neighbor for now
                    }
                    target.transform.position = tile.transform.position + Vector3.up;
                    target.currentTile = tile;

                    Debug.Log($"{player.playerState.playerIndex} sent {target.playerState.playerIndex} back 2 tiles");
                }
                break;
        }
    }
}
