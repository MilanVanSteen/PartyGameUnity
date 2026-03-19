using UnityEngine;

public class DiceTester : MonoBehaviour
{
    private BoardManager BoardManager => BoardManager.Instance;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (BoardManager != null)
            {
                BoardManager.RollForAllPlayers();
            }
            else
            {
                Debug.LogWarning("BoardManager instance not found!");
            }
        }
    }
}
