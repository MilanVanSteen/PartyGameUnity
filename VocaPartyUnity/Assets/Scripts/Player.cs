using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public Tile currentTile;
    public float moveSpeed = 3f;
    public PlayerState playerState { get; private set; }
    
    private void Awake(){
        playerState = GetComponent<PlayerState>();
    }

    private void Start()
    {
        BoardManager.Instance.RegisterPlayer(this);
    }

    public void MoveSteps(int steps)
    {
        if (playerState.isMoving) return;
        StartCoroutine(MoveRoutine(steps));
    }
    private IEnumerator MoveRoutine(int steps)
    {
        playerState.isMoving = true;

        Tile nextTile;

        for (int i = 0; i < steps; i++)
        {
            if (currentTile.neighbors.Count == 0)
                yield break;

            nextTile = currentTile.neighbors[0]; // first path for now

            yield return StartCoroutine(MoveToTile(nextTile));

            currentTile = nextTile;
        }

        yield return HandleTileEffect();

        playerState.isMoving = false;
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
                Debug.Log("Player landed on ladder tile, go forward");
                yield return MoveToTile(currentTile.targetTile);
                currentTile = currentTile.targetTile;
                break;

            case TileType.Snake:
                Debug.Log("Player landed on snake tile, go back");
                yield return MoveToTile(currentTile.targetTile);
                currentTile = currentTile.targetTile;
                break;

            case TileType.Powerup:
                PowerupType randomPowerup = GetRandomPowerup();
                playerState.inventory.Add(randomPowerup);
                Debug.Log(gameObject.name + " received powerup: " + randomPowerup);
                break;

            case TileType.Stuck:
                Debug.Log("Player stuck next turn");
                playerState.stuck = true;
                break;

            case TileType.Finish:
                Debug.Log("Player finished!");
                break;
        }
    }
    private PowerupType GetRandomPowerup()
    {
        PowerupType[] values = (PowerupType[])System.Enum.GetValues(typeof(PowerupType));
        return values[Random.Range(0, values.Length)];
    }
}
