using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // The Singleton instance
    public static GameManager instance;

    [Header("Game State")]
    public bool isFlowing = false;
    public float currentPressure = 100f;

    [Header("Settings")]
    public float startingPressure = 100f;

    void Awake()
    {
        // Make sure there's only one GameManager
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartGameFlow()
    {
        if (isFlowing) return;

        isFlowing = true;
        currentPressure = startingPressure;
        Debug.Log("Water is now pouring!");

        // This is where you will call your "Pathfinding" logic later
    }

    public void EndGame(bool won)
    {
        isFlowing = false;
        if (won)
        {
            Debug.Log("You Win! Score: " + currentPressure);
            // Show Win UI and calculate Stars
        }
        else
        {
            Debug.Log("Game Over! Pressure lost.");
            // Show Loss UI
        }
    }
}