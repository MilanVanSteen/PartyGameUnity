using System.Collections;
using TMPro;
using UnityEngine;

public class Player : MonoBehaviour
{
    public string playerName;
    public string playerId;

    [SerializeField] private TextMeshPro nameTag;
    private Renderer rend;

    public Tile currentTile;
    public float moveSpeed = 2f;
    public PlayerState PlayerState { get; private set; }
    
    private void Awake(){
        PlayerState = GetComponent<PlayerState>();
        rend = GetComponentInChildren<Renderer>();
    }

    public void Initialize(string id, int playerIndex, string name)
    {
        playerId = id;
        PlayerState.playerIndex = playerIndex;

        gameObject.name = $"Player_{playerIndex} ({name})";
        playerName = name;

        if (nameTag != null)
            nameTag.text = playerName;
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
        if (PlayerState.isMoving) return;

        // Added Steps powerup handling
        if(PlayerState.addedStepsNextRoll > 0)
        {
            steps += PlayerState.addedStepsNextRoll;
            PlayerState.addedStepsNextRoll = 0;
        }

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
                Debug.LogWarning("No neighbors, stopping movement safely.");

                yield return HandleTileEffect();

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

    public IEnumerator MoveToTile(Tile tile)
    {
        Vector3 start = transform.position;
        Vector3 end = tile.transform.position + BoardManager.Instance.tileOffset;

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
                break;

            case TileType.Ladder:
                yield return MoveToTile(currentTile.targetTile);
                currentTile = currentTile.targetTile;
                break;

            case TileType.Snake:
                yield return MoveToTile(currentTile.targetTile);
                currentTile = currentTile.targetTile;
                break;

            case TileType.Powerup:
                PowerupType randomPowerup = GetRandomPowerup();
                PlayerState.inventory.Add(randomPowerup);
                break;

            case TileType.Stuck:
                PlayerState.stuck = true;
                SocketManager.Instance.ShowPlayerStuck(playerId, true); // Notify website that player is stuck
                break;

            case TileType.Finish:
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
