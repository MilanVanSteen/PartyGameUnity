using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    private GameObject playerPrefab;
    private Transform playerContainer;
    private Tile startingTile;

    private List<Player> players = new();
    private Dictionary<string, Player> playerDict = new();

    private int playersMoving = 0;

    public bool dicerollActive = false;
    private bool powerupPhaseActive = false;
    public bool extraRollActive = false;

    // Timer
    private float powerupTimer = 0f;
    public readonly float powerupPhaseDuration = 10f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeBoard(PlayerList playerList)
    {
        // Find the starting tile in the scene
        GameObject startTileGO = GameObject.FindWithTag("StartingTile");
        if (startTileGO != null)
        {
            startingTile = startTileGO.GetComponent<Tile>();
        }
        else
        {
            Debug.LogError("No GameObject with tag 'StartingTile' found in the scene!");
            return;
        }

        // Assign player container dynamically
        if (playerContainer == null)
        {
            GameObject containerGO = GameObject.Find("PlayerContainer");
            if (containerGO != null)
            {
                playerContainer = containerGO.transform;
            }
            else
            {
                Debug.LogError("No PlayerContainer found in the scene!");
            }
        }

        // Assign player prefab dynamically (need to be in Resources)
        if (playerPrefab == null)
        {
            playerPrefab = Resources.Load<GameObject>("Prefabs/PlayerPrefab");
        }

        RegisterNetworkPlayers(playerList);

        // other setup logic (tiles, UI, etc.)
    }

    private void Update()
    {
        // Timer
        if (!powerupPhaseActive) return;

        powerupTimer -= Time.deltaTime;

        if (powerupTimer <= 0f && !extraRollActive)
        {
            EndPowerupPhase();
        }
    }

    public void RegisterNetworkPlayers(PlayerList playerList)
    {
        players.Clear();
        playerDict.Clear();

        Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };

        for (int i = 0; i < playerList.players.Length; i++)
        {
            var wp = playerList.players[i];

            Player player = FindOrSpawnPlayer(wp, i, colors[i % colors.Length]);
            players.Add(player);
            playerDict[wp.id] = player;
        }
    }

    // Spawn or find existing player by ID
    private Player FindOrSpawnPlayer(WebPlayer wp, int index, Color color)
    {
        // Check if player already exists in scene
        if (playerDict.TryGetValue(wp.id, out Player existing))
            return existing;

        GameObject go = Instantiate(playerPrefab, playerContainer);
        Player player = go.GetComponent<Player>();
        player.Initialize(wp.id, color, index);

        // Optionally set starting tile
        player.currentTile = startingTile; 
        player.transform.position = player.currentTile.transform.position + Vector3.up;

        return player;
    }

    public bool GetPowerupPhase()
    {
        return powerupPhaseActive;
    }

    public bool TryGetPlayer(string playerId, out Player player)
    {
        Debug.Log("Trying to get player..." + playerId);
        return playerDict.TryGetValue(playerId, out player);
    }

    public void RegisterMovingPlayer()
    {
        playersMoving++;
        Debug.Log("Players moving now: " + playersMoving);
    }

    public void PlayerFinishedMoving()
    {
        playersMoving--;

        if (playersMoving == 0)
        {
            AllPlayersFinished();
        }
    }
    private void AllPlayersFinished()
    {
        Debug.Log("All players finished moving!");

        StartPowerUpPhase();
    }

    private void StartPowerUpPhase()
    {
        if (powerupPhaseActive)
        {
            Debug.LogWarning("Powerup phase already active!");
            return;
        }

        Debug.Log("Starting powerup phase...");

        powerupPhaseActive = true;
        powerupTimer = powerupPhaseDuration;

        foreach (Player player in players)
        {
            PlayerState state = player.GetComponent<PlayerState>();

            if (state.inventory.Count > 0)
            {
                Debug.Log("Player " + state.playerIndex + " can use powerups.");
            }

            SocketManager.Instance.SendPowerUpInventory(
                player.playerId,
                state.inventory
            );
        }
    }
    public void EndPowerupPhase()
    {
        powerupPhaseActive = false;
        
        Debug.Log("Powerup phase ended!");

        SocketManager.Instance.NotifyPowerupPhaseEnded();

        foreach (Player player in players)
        {
            player.DecrementShield();
        }

        // Next phase later:
        // StartMinigame();
    }

    public void UsePowerup(Player player, int inventoryIndex)
    {
        if (powerupPhaseActive)
        {
            PlayerState state = player.PlayerState;
            if (inventoryIndex < state.inventory.Count)
            {
                PowerupType powerup = state.inventory[inventoryIndex];
                Debug.Log("Before use: " + string.Join(", ", state.inventory));
                StartCoroutine(ExecutePowerup(player, powerup));
                state.inventory.RemoveAt(inventoryIndex);
                Debug.Log("After use: " + string.Join(", ", state.inventory));
            }
        }
    }
    private IEnumerator ExecutePowerup(Player player, PowerupType powerup)
    {
        Player target = players.Find(p => p != player);

        switch (powerup)
        {
            case PowerupType.ExtraRoll:
                Debug.Log($"Reach this?");
                yield return ExecuteExtraRoll(player);
                break;

            // Make better (see below dice upon roll that gets extra)
            case PowerupType.AddedSteps:
                Debug.Log("Next roll +2");
                player.PlayerState.addedStepsNextRoll += 2;
                break;

            // Make better (visual)
            case PowerupType.Shield:
                Debug.Log("Shield activated for 2 turns");
                player.PlayerState.shieldTurns += 3; // Extra turn because 1 get instantly taken since turn ends afterwards
                break;

            // Make better (choice on website)
            case PowerupType.SwapPosition:
                if (target != null)
                {
                    if (target.PlayerState.shieldTurns > 0)
                    {
                        Debug.Log("Target is shielded! Powerup blocked.");
                        break;
                    }
                    Vector3 temp = player.transform.position;
                    player.transform.position = target.transform.position;
                    target.transform.position = temp;

                    Tile tempTile = player.currentTile;
                    player.currentTile = target.currentTile;
                    target.currentTile = tempTile;

                    Debug.Log($"{player.PlayerState.playerIndex} swapped with {target.PlayerState.playerIndex}");
                }
                break;

            // Make better (choice on website)
            case PowerupType.SendPlayerBack:
                if (target != null)
                {
                    if (target.PlayerState.shieldTurns > 0)
                    {
                        Debug.Log("Target is shielded! Powerup blocked.");
                        break;
                    }
                    Tile tile = target.currentTile;
                    for (int i = 0; i < 2; i++)
                    {
                        if (tile.neighbors.Count > 0) tile = tile.neighbors[0]; // move backward along first neighbor for now
                    }
                    target.transform.position = tile.transform.position + Vector3.up;
                    target.currentTile = tile;

                    Debug.Log($"{player.PlayerState.playerIndex} sent {target.PlayerState.playerIndex} back 2 tiles");
                }
                break;
        }
    }

    private IEnumerator ExecuteExtraRoll(Player player)
    {
        Debug.Log($"{player.gameObject.name} requested ExtraRoll");

        extraRollActive = true;

        // Ask website to roll dice for this player only
        SocketManager.Instance.RequestExtraRoll(player.playerId);

        // Wait until we get the dice result or timeout
        float timer = 0f;
        bool received = false;
        while (!received && timer < powerupPhaseDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void HandleFinish(Player player)
    {
        player.PlayerState.hasFinished = true;
        //Debug.Log($"{player.Name} has reached the finish!");

        // Stop further movement
        player.PlayerState.canMove = false;

        EndGame();
    }

    private void EndGame()
    {
        Debug.Log("Game Over! Final standings:");
        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"{i + 1}. {players[i].gameObject.name}");
        }

        // TODO: show UI, disable dice, etc.
    }
}
