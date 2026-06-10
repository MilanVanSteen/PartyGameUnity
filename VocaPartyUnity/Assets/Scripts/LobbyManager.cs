using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private TextMeshProUGUI roomCodeText;

    private Dictionary<string, GameObject> playerEntries = new Dictionary<string, GameObject>();
    
    public void SetRoomCode(string roomCode)
    {
        if (roomCodeText != null)
        {
            roomCodeText.text = roomCode;
        }
    }

    // Call when PLAYER_JOINED event comes in
    public void UpdatePlayers(PlayerList data)
    {
        foreach (var p in data.players)
        {
            if (!playerEntries.ContainsKey(p.id))
            {
                Debug.Log("LobbyManager: Spawning player UI: " + p.name);
                GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
                entry.GetComponentInChildren<TMP_Text>().text = string.IsNullOrEmpty(p.name) ? "HOST" : p.name;
                playerEntries[p.id] = entry;
            }
            else
            {
                // Update name/status if changed
                playerEntries[p.id].GetComponentInChildren<TMP_Text>().text = string.IsNullOrEmpty(p.name) ? "HOST" : p.name;
            }
        }
    }

    public void OnStartButton()
    {
        SocketManager.Instance.StartGame();
    }

    public void RemovePlayer(string playerId)
    {
        if (playerEntries.TryGetValue(playerId, out GameObject entry))
        {
            Destroy(entry);
            playerEntries.Remove(playerId);
            Debug.Log("LobbyManager: Removed player UI " + playerId);
        }
    }
}
