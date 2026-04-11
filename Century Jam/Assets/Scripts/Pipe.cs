using UnityEngine;

public enum PipeType
{
    Straight,
    Corner
}

public class Pipe : MonoBehaviour
{
    public PipeType type;
    public Vector2Int gridPos;

    public bool hasWater = false;

    // Directions: Up, Right, Down, Left
    public bool[] connections = new bool[4];

    void Start()
    {
        UpdateConnections();
    }

    public void Rotate()
    {
        transform.Rotate(0, 0, -90);
        UpdateConnections();
    }

    void UpdateConnections()
    {
        // Reset
        connections = new bool[4];

        float rot = transform.eulerAngles.z;

        if (type == PipeType.Straight)
        {
            if (Mathf.RoundToInt(rot) % 180 == 0)
            {
                connections[0] = true; // up
                connections[2] = true; // down
            }
            else
            {
                connections[1] = true; // right
                connections[3] = true; // left
            }
        }

        if (type == PipeType.Corner)
        {
            int r = Mathf.RoundToInt(rot / 90) % 4;

            connections[r] = true;
            connections[(r + 1) % 4] = true;
        }
    }

    public void SetWater(bool state)
    {
        hasWater = state;

        // Visual feedback
        GetComponent<SpriteRenderer>().color = state ? Color.blue : Color.white;
    }
}