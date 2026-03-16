using UnityEngine;

public class DiceTester : MonoBehaviour
{
    public Player testPlayer;

    private BoardManager boardManager;

    private void Start()
    {
        boardManager = BoardManager.Instance;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            boardManager.RollForAllPlayers();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            boardManager.UsePowerup(testPlayer, 0);
        }
    }
}
