using UnityEngine;

public class DiceTester : MonoBehaviour
{
    private SocketManager SocketManager => SocketManager.Instance;
    private BoardManager BoardManager => BoardManager.Instance;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (SocketManager != null)
            {
                if(BoardManager.currentPhase == GamePhase.WaitingForRoll)
                {
                    SocketManager.TriggerDiceRoll();
                }
                else
                {
                    Debug.LogWarning("DiceRoll or Powerup phase already active");
                }
            }
            else
            {
                Debug.LogWarning("BoardManager instance not found!");
            }
        }
    }
}
