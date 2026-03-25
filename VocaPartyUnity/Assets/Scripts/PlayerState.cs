using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    public bool stuck = false;
    public bool isMoving = false;
    public List<PowerupType> inventory = new();
    public int playerIndex = 0;

    public int addedStepsNextRoll = 0;
    public int shieldTurns = 0;

    public bool hasFinished = false;
    public bool canMove = true;

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

    public void ShieldHandling(string playerId)
    {
        if (shieldTurns > 0)
        {
            shieldTurns--;
            Debug.Log($"{gameObject.name} shield turns left: {shieldTurns}");

            // Notify website if expired
            if (shieldTurns == 0)
            {
                // This assumes you have a SocketManager singleton
                SocketManager.Instance.ShieldExpired(playerId);
            }
        }
    }
}
