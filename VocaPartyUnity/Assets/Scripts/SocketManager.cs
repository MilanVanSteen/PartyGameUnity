using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Transport;
using UnityEngine;

public class SocketManager : MonoBehaviour
{
    private SocketIOUnity socket;
    private string roomCode;

    void Awake()
    {
        // Keep this object alive across scenes
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
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

        // Connect
        socket.Connect();
        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("Unity connected! Creating room...");
            socket.Emit("CREATE_ROOM");
        };
    }

    private void OnRoomCreated(SocketIOResponse response)
    {
        Debug.Log(response.GetValue().ToString());
    }

    private class Player
    {
        public string id;
        public string name;
    }
    private class PlayerList
    {
        public Player[] players;
    }
    private void OnPlayerJoin(SocketIOResponse response)
    {
        string rawJson = response.GetValue().ToString();
        PlayerList data = null;
        try
        {
            data = JsonConvert.DeserializeObject<PlayerList>(rawJson);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to parse PLAYER_JOINED payload: " + e);
            return;
        }

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
        Player newPlayer = data.players[data.players.Length - 1];

        Debug.Log($"Player joined: {newPlayer.name} ({newPlayer.id})");

        // TODO: update your board/UI with the new player
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
