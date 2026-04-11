using System.Collections.Generic;
using UnityEngine;

public class FlowManager : MonoBehaviour
{
    public Vector2Int startPos;

    public void StartFlow()
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(startPos);

        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();

            if (visited.Contains(pos)) continue;
            visited.Add(pos);

            Pipe pipe = GridManager.instance.Get(pos);
            if (pipe == null) continue;

            pipe.SetWater(true);

            for (int i = 0; i < 4; i++)
            {
                if (!pipe.connections[i]) continue;

                Vector2Int dir = Direction(i);
                Vector2Int next = pos + dir;

                Pipe neighbor = GridManager.instance.Get(next);

                if (neighbor != null && neighbor.connections[(i + 2) % 4])
                {
                    queue.Enqueue(next);
                }
            }
        }
    }

    Vector2Int Direction(int i)
    {
        switch (i)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.right;
            case 2: return Vector2Int.down;
            case 3: return Vector2Int.left;
        }
        return Vector2Int.zero;
    }
}