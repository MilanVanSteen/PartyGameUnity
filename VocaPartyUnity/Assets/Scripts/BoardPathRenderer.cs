using UnityEngine;

public class BoardPathRenderer : MonoBehaviour
{
    private Tile[] tiles;
    public Material lineMaterial;
    public float width = 0.1f;

    private void Start()
    {
        tiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);

        foreach (Tile tile in tiles)
        {
            foreach (Tile neighbor in tile.neighbors)
            {
                if (neighbor == null) continue;

                CreateLine(tile.transform.position, neighbor.transform.position);
            }
        }
    }

    private void CreateLine(Vector3 start, Vector3 end)
    {
        GameObject obj = new GameObject("Path");
        obj.transform.SetParent(transform);

        var lr = obj.AddComponent<LineRenderer>();

        lr.material = lineMaterial;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;

        lr.useWorldSpace = true;

        // Prevent z-fighting with the board
        Vector3 offset = Vector3.up * 0.05f;

        lr.SetPosition(0, start + offset);
        lr.SetPosition(1, end + offset);
    }
}