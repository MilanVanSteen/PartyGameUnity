using UnityEngine;

public class RollHintUI : MonoBehaviour
{
    [SerializeField] private GameObject rollHintObject;

    public void Update()
    {
        if (BoardManager.Instance == null) return;

        rollHintObject.SetActive(BoardManager.Instance.currentPhase == GamePhase.WaitingForRoll);
    }
}
