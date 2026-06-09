using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerWinnerText;
    
    void Start()
    {
        if (GameData.WinnerName != null)
        {
            playerWinnerText.text = GameData.WinnerName;
        }
    }

    public void NewLobby()
    {
        if (SocketManager.Instance != null)
        {
            SocketManager.Instance.ResetToNewLobby();
        }
        else
        {
            SceneManager.LoadScene("LobbyScene");
        }
    }
}
