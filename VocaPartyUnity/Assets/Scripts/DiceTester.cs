using UnityEngine;

public class DiceTester : MonoBehaviour
{
    private SocketManager SocketManager => SocketManager.Instance;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (SocketManager != null)
            {
                SocketManager.TriggerDiceRoll();
            }
            else
            {
                Debug.LogWarning("BoardManager instance not found!");
            }
        }
    }
}
