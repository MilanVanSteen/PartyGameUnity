using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    public bool stuck = false;
    public bool isMoving = false;
    public List<PowerupType> inventory = new();
    public int playerIndex = 0; // player number gotten from server -> later

    public int addedStepsNextRoll = 0;
    public int shieldTurns = 0;

    // Called at the start of the player's turn
    public bool CanPlayTurn()
    {
        if(stuck)
        {
            Debug.Log(gameObject.name + " is stuck and skips a turn!");
            stuck = false; // clear the stuck status AFTER skipping
            return false;
        }
        return true;
    }
}
