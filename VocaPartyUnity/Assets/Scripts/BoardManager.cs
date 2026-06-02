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

    public GamePhase currentPhase = GamePhase.WaitingForRoll;
    private int extraRollsPending = 0;

    // Timer powerup phase
    private float powerupTimer = 0f;
    public readonly float powerupPhaseDuration = 10f;

    public List<Player> powerUpPlayers;

    // Minigames
    private MinigameType[] availableMinigames;

    // Minigame timer
    private float minigameTimer = 0f;
    public float minigamePhaseDuration = 30f;
    private bool minigameEnding = false;

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

        availableMinigames = (MinigameType[])System.Enum.GetValues(typeof(MinigameType));
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
            Debug.LogError("BoardManager: No GameObject with tag 'StartingTile' found in the scene!");
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
                Debug.LogError("BoardManager: No PlayerContainer found in the scene!");
            }
        }

        // Assign player prefab dynamically (need to be in Resources)
        if (playerPrefab == null)
        {
            playerPrefab = Resources.Load<GameObject>("Prefabs/PlayerPrefab");
        }

        RegisterNetworkPlayers(playerList);

        // Other setup logic (tiles, UI, etc.)
    }

    private void Update()
    {
        if (!IsGameActive()) return;

        // Timer for powerup phase
        if (currentPhase == GamePhase.Powerup)
        {
            powerupTimer -= Time.deltaTime;

            if (powerupTimer <= 0f && extraRollsPending == 0)
            {
                EndPowerupPhase();
            }
        }

        // Timer for minigame phase
        if (currentPhase == GamePhase.Minigame)
        {
            minigameTimer -= Time.deltaTime;

            if (minigameTimer <= 0f && extraRollsPending == 0)
            {
                EndMinigamePhase();
            }
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
        player.Initialize(wp.id, color, index, wp.name);

        // Optionally set starting tile
        player.currentTile = startingTile; 
        player.transform.position = player.currentTile.transform.position + Vector3.up;

        return player;
    }

    public bool TryGetPlayer(string playerId, out Player player)
    {
        Debug.Log("BoardManager: Trying to get player..." + playerId);
        return playerDict.TryGetValue(playerId, out player);
    }

    public void RegisterMovingPlayer()
    {
        if (!IsGameActive()) return;

        playersMoving++;
        Debug.Log("BoardManager: Players moving now: " + playersMoving);

        if (currentPhase == GamePhase.DiceRoll)
        {
            currentPhase = GamePhase.Movement;
        }
    }

    public void PlayerFinishedMoving()
    {
        if (!IsGameActive()) return;

        playersMoving--;
        Debug.Log("BoardManager: Player finished moving. Remaining: " + playersMoving);

        if (playersMoving == 0)
        {
            Debug.Log("BoardManager: All players finished moving!");
            AllPlayersFinished();
        }

        if (playersMoving < 0)
        {
            Debug.LogError("BoardManager: playersMoving went negative!");
            playersMoving = 0;
        }
    }
    private void AllPlayersFinished()
    {
        if (!IsGameActive()) return;

        if (currentPhase == GamePhase.Movement)
        {
            StartPowerUpPhase();
        }
    }

    public void ResetMovementCounter()
    {
        Debug.Log("BoardManager: Resetting movement counter");
        playersMoving = 0;
    }

    private void StartPowerUpPhase()
    {
        if (!IsGameActive()) return;
        if (currentPhase != GamePhase.Movement)
        {
            Debug.LogWarning("BoardManager: Powerup phase already active!");
            return;
        }

        Debug.Log("BoardManager: Starting powerup phase...");

        currentPhase = GamePhase.Powerup;
        powerupTimer = powerupPhaseDuration;
        extraRollsPending = 0;
        powerUpPlayers = new List<Player>(players);

        foreach (Player player in powerUpPlayers)
        {
            PlayerState state = player.GetComponent<PlayerState>();

            if (state.inventory.Count > 0)
            {
                Debug.Log("BoardManager: Player " + state.playerIndex + " can use powerups.");
            }

            SocketManager.Instance.SendPowerUpInventory(
                player.playerId,
                state.inventory
            );
        }
    }
    public void EndPowerupPhase()
    {        
        if (!IsGameActive()) return;
        Debug.Log("BoardManager: Powerup phase ended!");

        SocketManager.Instance.NotifyPowerupPhaseEnded();

        foreach (Player player in players)
        {
            player.DecrementShield();
        }

        StartMinigamePhase();
    }

    public void UsePowerup(Player player, int inventoryIndex)
    {
        if (currentPhase == GamePhase.Powerup)
        {
            PlayerState state = player.PlayerState;
            if (inventoryIndex < state.inventory.Count)
            {
                PowerupType powerup = state.inventory[inventoryIndex];
                StartCoroutine(ExecutePowerup(player, powerup));
                state.inventory.RemoveAt(inventoryIndex);
            }
            Debug.Log($"BoardManager: Player {player.playerId} selected powerup.");
            OnPlayerSelectedPowerup(player.playerId);
        }
    }
    private IEnumerator ExecutePowerup(Player player, PowerupType powerup)
    {
        Player target = players.Find(p => p != player);

        switch (powerup)
        {
            case PowerupType.ExtraRoll:
                yield return ExecuteExtraRoll(player);
                break;

            case PowerupType.AddedSteps:
                player.PlayerState.addedStepsNextRoll += 2;
                break;

            // Make better (visual in Unity)
            case PowerupType.Shield:
                player.PlayerState.shieldTurns += 3; // Extra turn because 1 gets instantly taken since turn ends afterwards
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
        extraRollsPending++;

        Debug.Log($"BoardManager: {player.gameObject.name} requested ExtraRoll, total pending: {extraRollsPending}");

        // Ask website to roll dice for this player only
        SocketManager.Instance.RequestExtraRoll(player.playerId);

        yield break;
    }
    public void OnExtraRollFinished(string playerId)
    {
        extraRollsPending--;

        Debug.Log($"BoardManager: ExtraRoll finished for {playerId}. Remaining: {extraRollsPending}");

        if (extraRollsPending < 0) 
        {
            extraRollsPending = 0;
        }
    }
    public bool HasNoPendingExtraRolls()
    {
        return extraRollsPending == 0;
    }

    public void OnPlayerSelectedPowerup(string playerId)
    {
        if (!powerUpPlayers.Exists(p => p.playerId == playerId)) return;

        Player player = powerUpPlayers.Find(p => p.playerId == playerId);

        powerUpPlayers.Remove(player);

        // When all finished
        if (powerUpPlayers.Count == 0 && extraRollsPending == 0)
        {
            Debug.Log($"BoardManager: Ending powerup phase: all players selected and no pending extra rolls.");
            EndPowerupPhase();
        }
    }

    private void StartMinigamePhase()
    {
        if (!IsGameActive()) return;
        if (currentPhase == GamePhase.Minigame)
        {
            Debug.LogWarning("BoardManager: Minigame phase already active!");
            return;
        }

        Debug.Log("BoardManager: Starting Minigame Phase...");
        currentPhase = GamePhase.Minigame;
        minigameEnding = false;

        MinigameType selectedMinigame = GetRandomMinigame();
        Debug.Log("BoardManager: Selected Minigame: " + selectedMinigame);

        switch (selectedMinigame)
        {
            case MinigameType.WordRushNL:
                minigamePhaseDuration = 30f;
                break;

            case MinigameType.WordRushEN:
                minigamePhaseDuration = 30f;
                break;

            case MinigameType.MemoryMatch:
                minigamePhaseDuration = 20f;
                break;

            case MinigameType.WordSnake:
                minigamePhaseDuration = 30f;
                break;
            
            case MinigameType.RocketFuel:
                minigamePhaseDuration = 25f;
                break;
            
            default:
                minigamePhaseDuration = 30f;
                break;
        }
        minigameTimer = minigamePhaseDuration;

        // Start minigame manager
        MinigameManager.Instance.StartMinigame(selectedMinigame, minigamePhaseDuration);
    }
    private MinigameType lastMinigame;
    private MinigameType GetRandomMinigame()
    {
        MinigameType selected;

        int safety = 0;

        do
        {
            selected = availableMinigames[Random.Range(0, availableMinigames.Length)];
            safety++;
        }
        while (selected == lastMinigame && safety < 10);

        lastMinigame = selected;
        return selected;
    }

    public void EndMinigamePhase()
    {
        if (!IsGameActive()) return;
        // Prevent this from running multiple times
        if (currentPhase != GamePhase.Minigame) return;
        if (minigameEnding) return;

        minigameEnding = true;

        Debug.Log("BoardManager: Minigame phase ended!");

        // Tell all website clients to close the minigame screen
        SocketManager.Instance.NotifyMinigamePhaseEnded();
    }

    public void StartNextTurn()
    {
        currentPhase = GamePhase.WaitingForRoll; //Later goes automatically
    }

    public void HandleFinish(Player player)
    {
        if (currentPhase == GamePhase.GameOver) return;

        player.PlayerState.hasFinished = true;
        player.PlayerState.canMove = false;

        if (GameData.WinnerName != null) return;

        GameData.WinnerName = player.playerName;
        Debug.Log($"BoardManager: {player.playerName} has reached the finish!");

        currentPhase = GamePhase.GameOver;

        SocketManager.Instance.EndGame(player.playerName);
    }

    private bool IsGameActive()
    {
        return currentPhase != GamePhase.GameOver;
    }
}
