using TMPro;
using UnityEngine;

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
}
