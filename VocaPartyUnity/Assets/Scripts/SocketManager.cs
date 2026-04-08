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
        if (scene.name == "SampleScene") // Actual game scene later
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
        socket.On("EXTRA_ROLL_RESULT", OnExtraRollResult);
        socket.On("POWERUP_SKIPPED", OnPowerupSkipped);
        socket.On("POWERUP_PHASE_FORCE_END", OnEndPowerupPhase);
        socket.On("PLAYER_FINISHED_MINIGAME", OnFinishedMinigame);

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
            Debug.LogWarning("Socket disconnected!");
        };

        socket.OnReconnectAttempt += (sender, e) =>
        {
            Debug.Log("Reconnecting...");
        };

        socket.OnReconnected += (sender, e) =>
        {
            Debug.Log("Reconnected!");
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
        Debug.Log("Room code: " + roomCode);

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
                Debug.LogWarning("No LobbyManager in this scene, skipping UI update");
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
            Debug.Log("No players in room yet.");
            return;
        }

        // Pick the last player who joined to show
        WebPlayer newPlayer = data.players[data.players.Length - 1];
        Debug.Log($"Player joined: {newPlayer.name} ({newPlayer.id})");

        // Update UI
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            if (lobbyManager != null)
            {
                lobbyManager.UpdatePlayers(data);
            }
            else
            {
                Debug.LogWarning("No LobbyManager in this scene, skipping UI update");
            }
        });

        // TODO: update your board/UI with the new player
    }

    public void StartGame()
    {
        Debug.Log("Start button pressed → sending START_GAME");
        Debug.Log("Socket connected? " + socket.Connected);

        socket.Emit("START_GAME", new { });
    }

    private void OnGameStarted(SocketIOResponse response)
    {
        Debug.Log("Game is starting!");

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
        Debug.Log("Loading game scene...");

        SceneManager.LoadScene("SampleScene"); // Change to actual next scene later
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
            Debug.Log("Dice roll already active.");
            return;
        }

        if (socket != null && socket.Connected)
        {
            BoardManager.Instance.currentPhase = GamePhase.DiceRoll;
            Debug.Log("Host triggering dice roll...");
            socket.Emit("ROLL_DICE");
        }
        else
        {
            Debug.LogWarning("Socket not connected. Cannot roll dice.");
        }
    }

    private void OnPlayerMove(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        PlayerMoveList moveList = JsonConvert.DeserializeObject<PlayerMoveList>(rawJson);

        Debug.Log("All dice rolls finished, moving players...");

        BoardManager.Instance.ResetMovementCounter();
        BoardManager.Instance.currentPhase = GamePhase.Movement;

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            Debug.Log("Inside thread now...");
            foreach (var move in moveList.moves)
            {
                Debug.Log("Foreach is handling...");
                if (BoardManager.Instance.TryGetPlayer(move.playerId, out Player player))
                {
                    if(player.PlayerState.canMove && player.PlayerState.CanPlayTurn())
                    {
                        BoardManager.Instance.RegisterMovingPlayer();

                        player.MoveSteps(move.roll); // Move the player in Unity
                    }
                }
                else
                {
                    Debug.LogWarning("Player not found: " + move.playerId);
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

            Debug.Log($"Sending inventory to {playerId}");

            socket.Emit("POWERUP_PHASE_START", data);
        }
        else
        {
            Debug.LogWarning("Socket not connected. Cannot send inventory.");
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
        if (socket != null && socket.Connected)
        {
            socket.Emit("REQUEST_EXTRA_ROLL", new { playerId });
            Debug.Log($"Requested ExtraRoll for player {playerId}");
        }
    }

    public void ShieldExpired(string playerId)
    {
        if (socket != null && socket.Connected)
        {
            socket.Emit("SHIELD_EXPIRED", new { playerId });
            Debug.Log($"Sent SHIELD_EXPIRED for player {playerId}");
        }
    }

    [Serializable]
    public class ExtraRollResult
    {
        public string playerId;
        public int roll;
    }
    private void OnExtraRollResult(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        ExtraRollResult result = JsonConvert.DeserializeObject<ExtraRollResult>(rawJson);

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            if (BoardManager.Instance.TryGetPlayer(result.playerId, out Player player))
            {
                Debug.Log($"Extra roll received for {player.PlayerState.playerIndex}: {result.roll}");
                // Trigger the player's movement
                BoardManager.Instance.RegisterMovingPlayer();
                player.MoveSteps(result.roll); // Or MoveStepsCoroutine if you want animation

                BoardManager.Instance.OnExtraRollFinished(result.playerId);
            }
            else
            {
                Debug.LogWarning("Extra roll: player not found: " + result.playerId);
            }
        });
    }

    private void OnPowerupSkipped(SocketIOResponse response)
    {
        Debug.Log("Player skipped powerup.");

        string rawJson = response.GetValue().ToString();
        var data = JsonConvert.DeserializeObject<MinigameFinishData>(rawJson);
        
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
            Debug.Log("Website ended powerup phase.");
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

            socket.Emit("MINIGAME_START", data);

            Debug.Log($"Minigame {minigame} started");
        }
    }

    public void NotifyMinigameEnded()
    {
        socket.Emit("MINIGAME_END");
    }
    private void OnFinishedMinigame(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        var data = JsonConvert.DeserializeObject<MinigameFinishData>(rawJson);
        
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            MinigameManager.Instance.OnPlayerFinishedMinigame(data.playerId, data.correct);
        });
    }

    [Serializable]
    public class MinigameFinishData
    {
        public string playerId;
        public bool correct;
    }


    private void OnApplicationQuit()
    {
        socket.Disconnect();
    }
}
