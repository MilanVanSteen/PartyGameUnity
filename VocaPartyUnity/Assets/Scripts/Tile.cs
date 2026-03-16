using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Connected Tiles")]
    public List<Tile> neighbors = new();

    [Header("Tile Type")]
    public TileType tileType;

    [Header("Special Target")]
    public Tile targetTile; // used for Snake or Ladder

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        foreach (Tile neighbor in neighbors)
        {
            if (neighbor != null)
            {
                Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }

        if (targetTile != null)
        {
            if(tileType == TileType.Ladder)
            {
                Gizmos.color = Color.green;
            }
            else if(tileType == TileType.Snake)
            {
                Gizmos.color = Color.red;
            }
            Gizmos.DrawLine(transform.position, targetTile.transform.position);
        }
    }
}
