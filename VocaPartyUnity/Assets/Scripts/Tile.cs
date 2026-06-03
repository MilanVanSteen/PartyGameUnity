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

    private Renderer tileRenderer;

    private void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        ApplyTileColor();
    }

    private void ApplyTileColor()
    {
        if (tileRenderer == null) return;

        Color color;

        switch (tileType)
        {
            case TileType.Start:
                color = new Color(0.20f, 0.80f, 0.35f);
                break;

            case TileType.Ladder:
                color = new Color(0.35f, 0.85f, 0.45f);
                break;

            case TileType.Snake:
                color = new Color(0.85f, 0.25f, 0.25f);
                break;

            case TileType.Powerup:
                color = new Color(0.35f, 0.55f, 0.95f);
                break;

            case TileType.Stuck:
                color = new Color(0.95f, 0.55f, 0.15f);
                break;

            case TileType.Finish:
                color = new Color(0.95f, 0.80f, 0.20f);
                break;

            default:
                color = new Color(0.92f, 0.92f, 0.92f);
                break;
        }

        SetTileColor(color);
    }

    public void SetTileColor(Color color)
    {
        if (tileRenderer != null)
        {
            tileRenderer.material.color = color;
        }
    }

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
