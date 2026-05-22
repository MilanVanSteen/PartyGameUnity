using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    private string playerName;
    public string playerId;

    public Color playerColor = Color.white;
    private Renderer rend;

    public Tile currentTile;
    public float moveSpeed = 3f;
    public PlayerState PlayerState { get; private set; }
    
    private void Awake(){
        PlayerState = GetComponent<PlayerState>();
        rend = GetComponentInChildren<Renderer>();
    }

    public void Initialize(string id, Color color, int playerIndex)
    {
        playerId = id;
        playerColor = color;
        PlayerState.playerIndex = playerIndex;

        if (rend != null)
            rend.material.color = playerColor;

        gameObject.name = $"Player_{playerIndex}";
        playerName = gameObject.name;
    }

    public IEnumerator MoveStepsCoroutine(int steps)
    {
        // Handle AddedSteps powerup
        if (PlayerState.addedStepsNextRoll > 0)
        {
            steps += PlayerState.addedStepsNextRoll;
            PlayerState.addedStepsNextRoll = 0;
        }

        yield return MoveRoutine(steps);
    }

    public void MoveSteps(int steps)
    {
        Debug.Log("PLayer 1 Current game phase: " + BoardManager.Instance.currentPhase);
        if (PlayerState.isMoving) return;

        // Added Steps powerup handling
        if(PlayerState.addedStepsNextRoll > 0)
        {
            steps += PlayerState.addedStepsNextRoll;
            PlayerState.addedStepsNextRoll = 0;
        }

        Debug.Log($"Moving player {playerName} by {steps} steps");
        StartCoroutine(MoveRoutine(steps));
    }
    private IEnumerator MoveRoutine(int steps)
    {
        PlayerState.isMoving = true;

        Tile nextTile;

        for (int i = 0; i < steps; i++)
        {
            if (currentTile.neighbors.Count == 0) 
            {
                Debug.Log("No neighbors, stopping movement safely.");

                PlayerState.isMoving = false;
                BoardManager.Instance.PlayerFinishedMoving();
                yield break;
            }

            nextTile = currentTile.neighbors[0]; // first path for now

            yield return StartCoroutine(MoveToTile(nextTile));

            currentTile = nextTile;
        }

        yield return HandleTileEffect();

        PlayerState.isMoving = false;
        BoardManager.Instance.PlayerFinishedMoving();
    }

    private IEnumerator MoveToTile(Tile tile)
    {
        Vector3 start = transform.position;
        Vector3 end = tile.transform.position + Vector3.up;

        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
    }

    private IEnumerator HandleTileEffect()
    {
        switch (currentTile.tileType)
        {
            case TileType.Normal:
                Debug.Log("Nothing happened");
                break;

            case TileType.Ladder:
                Debug.Log(playerName + " landed on ladder tile, go forward");
                yield return MoveToTile(currentTile.targetTile);
                currentTile = currentTile.targetTile;
                break;

            case TileType.Snake:
                Debug.Log(playerName + " landed on snake tile, go back");
                yield return MoveToTile(currentTile.targetTile);
                currentTile = currentTile.targetTile;
                break;

            case TileType.Powerup:
                PowerupType randomPowerup = GetRandomPowerup();
                PlayerState.inventory.Add(randomPowerup);
                Debug.Log(playerName + " received powerup: " + randomPowerup);
                break;

            case TileType.Stuck:
                Debug.Log(playerName + " stuck next turn");
                PlayerState.stuck = true;
                break;

            case TileType.Finish:
                Debug.Log(playerName + " finished!");
                BoardManager.Instance.HandleFinish(this);

                PlayerState.isMoving = false;
                BoardManager.Instance.PlayerFinishedMoving();
                yield break;
        }
    }
    public PowerupType GetRandomPowerup()
    {
        PowerupType[] values = (PowerupType[])System.Enum.GetValues(typeof(PowerupType));
        return values[Random.Range(0, values.Length)];
    }

    public void DecrementShield()
    {
        PlayerState.ShieldHandling(playerId);
    }
}
