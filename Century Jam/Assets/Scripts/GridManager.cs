using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager instance;

    public int width = 6;
    public int height = 6;
    public float cellSize = 1f;

    private Pipe[,] grid;

    void Awake()
    {
        instance = this;
        grid = new Pipe[width, height];
    }

    public Vector2Int WorldToGrid(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / cellSize);
        int y = Mathf.FloorToInt(pos.y / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            x * cellSize + cellSize / 2f,
            y * cellSize + cellSize / 2f,
            0
        );
    }

    public bool IsValid(Vector2Int pos)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < width && pos.y < height;
    }

    public bool IsOccupied(Vector2Int pos)
    {
        return grid[pos.x, pos.y] != null;
    }

    public void Place(Pipe pipe, Vector2Int pos)
    {
        if (!IsValid(pos)) return;

        grid[pos.x, pos.y] = pipe;
        pipe.transform.position = GridToWorld(pos.x, pos.y);
        pipe.gridPos = pos;
    }

    public void Remove(Vector2Int pos)
    {
        if (IsValid(pos))
            grid[pos.x, pos.y] = null;
    }

    public Pipe Get(Vector2Int pos)
    {
        if (!IsValid(pos)) return null;
        return grid[pos.x, pos.y];
    }
}