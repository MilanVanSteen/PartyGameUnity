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

    // Timer
    private bool powerupPhaseActive = false;
    private float powerupTimer = 0f;
    private readonly float powerupPhaseDuration = 10f;

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

        if (powerupTimer <= 0f)
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
            PlayerState state = player.PlayerState;
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
                player.PlayerState.addedStepsNextRoll += 2;
                break;

            case PowerupType.Shield:
                Debug.Log("Shield activated for 2 turns");
                player.PlayerState.shieldTurns = 2;
                break;

            case PowerupType.SwapPosition:
                // Temporary keyboard test: swap with first other player
                if (target != null)
                {
                    if (target.PlayerState.shieldTurns > 0)
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

                    Debug.Log($"{player.PlayerState.playerIndex} swapped with {target.PlayerState.playerIndex}");
                }
                break;

            case PowerupType.SendPlayerBack:
                // Temporary: send first other player back 2 tiles
                if (target != null)
                {
                    if (target.PlayerState.shieldTurns > 0)
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

                    Debug.Log($"{player.PlayerState.playerIndex} sent {target.PlayerState.playerIndex} back 2 tiles");
                }
                break;
        }
    }
}
