using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuCanvas;

    private bool isOpen;

    void Start()
    {
        pauseMenuCanvas.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();

            Time.timeScale = isOpen ? 0f : 1f;
        }
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;
        pauseMenuCanvas.SetActive(isOpen);
    }

    public void Resume()
    {
        Time.timeScale = 1f;

        isOpen = false;
        pauseMenuCanvas.SetActive(false);
    }

    public void NewLobby()
    {
        Time.timeScale = 1f;
        isOpen = false;
        pauseMenuCanvas.SetActive(false);

        if (SocketManager.Instance != null)
        {
            SocketManager.Instance.ResetToNewLobby();
        }
        else
        {
            SceneManager.LoadScene("LobbyScene");
        }
    }

    public void QuitGame()
    {
        SocketManager.Instance.LeaveRoom();

        Application.Quit();

    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #endif
    }
}
