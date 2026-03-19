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
            Transport = TransportProtocol.WebSocket
        });

        // Listen for server events
        socket.On("PLAYER_JOINED", OnPlayerJoin);
        socket.On("ROOM_CREATED", OnRoomCreated);
        socket.On("GAME_STARTED", OnGameStarted);

        // Connect
        socket.Connect();
        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("Unity connected! Creating room...");
            socket.Emit("CREATE_ROOM");
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

    // Call this method from your game (UI button, dice roll, etc.)
    public void SendRollDice(string playerId, int rollValue)
    {
        var data = new { playerId, roll = rollValue };
        socket.Emit("roll-dice", data);
    }

    private void OnApplicationQuit()
    {
        socket.Disconnect();
    }
}
