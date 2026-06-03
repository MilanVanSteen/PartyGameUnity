using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance;

    private SocketIOUnity socket;
    private LobbyManager lobbyManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject); // avoid duplicates
        }
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sceneToLoad) // Only initialize board on the actual game scene
        {
            Debug.Log("Scene loaded, initializing board...");
            BoardManager.Instance.InitializeBoard(GameData.CurrentPlayers);
        }
    }
        
    void Start()
    {
        if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();

        var uri = new Uri("https://partygame-gwgre4gjebg9h0fk.germanywestcentral-01.azurewebsites.net/"); 

        // Initialize socket
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Query = new Dictionary<string, string> { { "token", "UNITY" } },
            Transport = TransportProtocol.WebSocket,

            Reconnection = true,
            ReconnectionAttempts = 999999,
            ReconnectionDelay = 2000
        });

        // Listen for server events
        socket.On("PLAYER_JOINED", OnPlayerJoin);
        socket.On("ROOM_CREATED", OnRoomCreated);
        socket.On("GAME_STARTED", OnGameStarted);
        socket.On("PLAYER_MOVE", OnPlayerMove);
        socket.On("POWERUP_SELECTED", OnPowerupSelected);
        socket.On("POWERUP_SKIPPED", OnPowerupSkipped);
        socket.On("POWERUP_PHASE_FORCE_END", OnEndPowerupPhase);
        socket.On("MINIGAME_RESULTS", OnMinigameResults);
        socket.On("MINIGAME_PHASE_FORCE_END", OnEndMinigamePhase);
        socket.On("GAME_ENDED", OnGameEnded);
    
        // Connect
        socket.Connect();
        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("Unity connected! Creating room...");
            socket.Emit("CREATE_ROOM");
        };

        // Debug
        socket.OnDisconnected += (sender, e) =>
        {
            Debug.LogWarning("SocketManager: Socket disconnected!");
        };

        socket.OnReconnectAttempt += (sender, e) =>
        {
            Debug.Log("SocketManager: Reconnecting...");
        };

        socket.OnReconnected += (sender, e) =>
        {
            Debug.Log("SocketManager: Reconnected!");
        };
    }

    [Serializable]
    private class RoomCodeRaw
    {
        public string roomCode;
    }

    private void OnRoomCreated(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        RoomCodeRaw roomCodeRaw = JsonConvert.DeserializeObject<RoomCodeRaw>(rawJson);

        string roomCode = roomCodeRaw.roomCode;
        Debug.Log("SocketManager: Room code: " + roomCode);

        // Update UI
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.SetRoomCode(roomCode);
            }
            else
            {
                Debug.LogWarning("SocketManager: No LobbyManager in this scene, skipping UI update");
            }
        });
    }

    private void OnPlayerJoin(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        PlayerList data = JsonConvert.DeserializeObject<PlayerList>(rawJson);

        // Playername null handling
        foreach (var p in data.players)
        {
            if (string.IsNullOrEmpty(p.name)) p.name = "HOST";
        }

        // Player error handling
        if (data?.players == null || data.players.Length == 0)
        {
            Debug.Log("SocketManager: No players in room yet.");
            return;
        }

        // Pick the last player who joined to show
        WebPlayer newPlayer = data.players[data.players.Length - 1];
        Debug.Log($"SocketManager: Player joined: {newPlayer.name} ({newPlayer.id})");

        // Update UI
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            if (lobbyManager != null)
            {
                lobbyManager.UpdatePlayers(data);
            }
            else
            {
                Debug.LogWarning("SocketManager: No LobbyManager in this scene, skipping UI update");
            }
        });

        // TODO: update your board/UI with the new player
    }

    public void StartGame()
    {
        Debug.Log("SocketManager: Socket connected? " + socket.Connected);

        socket.Emit("START_GAME", new { });
    }

    public string sceneToLoad; // Change this to actual game map choice later
    private void OnGameStarted(SocketIOResponse response)
    {
        // Save playerList
        string rawJson = response.GetValue().ToString();
        PlayerList data = JsonConvert.DeserializeObject<PlayerList>(rawJson);
        GameData.CurrentPlayers = data;

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            StartGameClient();
        });
    }
    private void StartGameClient()
    {
        SceneManager.LoadScene(sceneToLoad);
    }

    [Serializable]
    public class PlayerMove
    {
        public string playerId;
        public int roll;
    }

    [Serializable]
    public class PlayerMoveList
    {
        public List<PlayerMove> moves;
    }

    public void TriggerDiceRoll()
    {
        if (BoardManager.Instance.currentPhase == GamePhase.DiceRoll)
        {
            Debug.LogWarning("SocketManager: Dice roll already active.");
            return;
        }

        if (socket != null && socket.Connected)
        {
            BoardManager.Instance.currentPhase = GamePhase.DiceRoll;
            Debug.Log("Dice roll...");
            socket.Emit("ROLL_DICE");
        }
        else
        {
            Debug.LogWarning("SocketManager: Socket not connected. Cannot roll dice.");
        }
    }

    private void OnPlayerMove(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        PlayerMoveList moveList = JsonConvert.DeserializeObject<PlayerMoveList>(rawJson);

        Debug.Log("SocketManager: All dice rolls finished, moving players...");

        BoardManager.Instance.ResetMovementCounter();
        if (BoardManager.Instance.currentPhase == GamePhase.DiceRoll)
        {
            BoardManager.Instance.currentPhase = GamePhase.Movement;
        }

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            foreach (var move in moveList.moves)
            {
                if (BoardManager.Instance.TryGetPlayer(move.playerId, out Player player))
                {
                    if(player.PlayerState.canMove && player.PlayerState.CanPlayTurn())
                    {
                        BoardManager.Instance.RegisterMovingPlayer();

                        player.MoveSteps(move.roll); // Move the player in Unity

                        if(BoardManager.Instance.currentPhase == GamePhase.Powerup)
                        {
                            BoardManager.Instance.OnExtraRollFinished(move.playerId);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("SocketManager: Player not found: " + move.playerId);
                }
            }
        });
    }

    public void SendPowerUpInventory(string playerId, List<PowerupType> inventory)
    {
        if (socket != null && socket.Connected)
        {
            List<string> inventoryNames = new();
            foreach (var powerup in inventory)
            {
                inventoryNames.Add(powerup.ToString());
            }

            float duration = BoardManager.Instance.powerupPhaseDuration;
            var data = new
            {
                playerId,
                inventory = inventoryNames,
                duration
            };

            socket.Emit("POWERUP_PHASE_START", data);
        }
        else
        {
            Debug.LogWarning("SocketManager: Socket not connected. Cannot send inventory.");
        }
    }

    [Serializable]
    public class PowerupSelection
    {
        public string playerId;
        public int inventoryIndex;
    }
    private void OnPowerupSelected(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        var data = JsonConvert.DeserializeObject<PowerupSelection>(rawJson);

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            if (BoardManager.Instance.TryGetPlayer(data.playerId, out Player player))
            {
                BoardManager.Instance.UsePowerup(
                    player,
                    data.inventoryIndex
                );
            }
        });
    }

    public void RequestExtraRoll(string playerId)
    {
        Debug.Log("0 Current game phase: " + BoardManager.Instance.currentPhase);

        if (socket != null && socket.Connected)
        {
            socket.Emit("REQUEST_EXTRA_ROLL", new { playerId });
            Debug.Log($"SocketManager: Requested ExtraRoll for player {playerId}");
        }
    }

    public void ShieldExpired(string playerId)
    {
        if (socket != null && socket.Connected)
        {
            socket.Emit("SHIELD_EXPIRED", new { playerId });
            Debug.Log($"SocketManager: Sent SHIELD_EXPIRED for player {playerId}");
        }
    }

    [Serializable]
    public class PowerupSkippedData
    {
        public string playerId;
    }
    private void OnPowerupSkipped(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        var data = JsonConvert.DeserializeObject<PowerupSkippedData>(rawJson);
        
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            BoardManager.Instance.OnPlayerSelectedPowerup(data.playerId);
        });
    }

    public void NotifyPowerupPhaseEnded()
    {
        socket.Emit("POWERUP_PHASE_END");
    }
    private void OnEndPowerupPhase(SocketIOResponse response)
    {
        BoardManager boardManager = BoardManager.Instance;
        if (boardManager.currentPhase == GamePhase.Powerup && boardManager.HasNoPendingExtraRolls())
        {
            Debug.Log("SocketManager: Website ended powerup phase.");
            boardManager.EndPowerupPhase();
        }
    }

    public void StartMinigame(MinigameType minigame, float minigameDuration)
    {
        if (socket != null && socket.Connected)
        {
            var data = new 
            { 
                minigame = minigame.ToString(),
                duration = minigameDuration
            };

            minigameEnding = false;

            socket.Emit("MINIGAME_START", data);

            Debug.Log($"SocketManager: Minigame {minigame} started");
        }
    }

    private bool minigameEnding = false;
    private void OnEndMinigamePhase(SocketIOResponse response)
    {
        if (minigameEnding)
        {
            Debug.Log("Minigame already ending.");
            return;
        }

        if (BoardManager.Instance.currentPhase != GamePhase.Minigame)
        {
            Debug.Log("Not in minigame phase.");
            return;
        }

        minigameEnding = true;

        Debug.Log("SocketManager: Website ended minigame phase.");

        NotifyMinigamePhaseEnded();
    }

    public void NotifyMinigamePhaseEnded()
    {
        if (socket != null && socket.Connected)
        {
            socket.Emit("MINIGAME_PHASE_END");
            Debug.Log("SocketManager: Sent MINIGAME_PHASE_END");
        }
        else
        {
            Debug.LogWarning("SocketManager: Socket not connected. Cannot send MINIGAME_PHASE_END.");
        }
    }

    [Serializable]
    public class MinigameResults
    {
        public string[] winners;
        public Newtonsoft.Json.Linq.JObject scores;
    }

    private void OnMinigameResults(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        var data = JsonConvert.DeserializeObject<MinigameResults>(rawJson);

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            Debug.Log("SocketManager: Minigame finished!");

            var scoresDict = data.scores.ToObject<Dictionary<string, int>>();

            MinigameManager.Instance.OnResults(data.winners, scoresDict);
        });
    }

    public void EndGame(string playerName)
    {
        if (socket == null || !socket.Connected)
        {
            Debug.LogWarning("Socket not connected, cannot end game.");
            return;
        }
        
        socket.Emit("END_GAME", new
        {
            playerName
        });
    }

    private void OnGameEnded(SocketIOResponse response)
    {
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            SceneManager.LoadScene("EndScene");
        });
    }

    private void OnApplicationQuit()
    {
        socket.Disconnect();
    }
}
