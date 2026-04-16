using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Tooltip("All fluid simulations in the level (water, lava, etc). The game manager enables/disables all of them together.")]
    [SerializeField] private SPHFluid2D[] fluids;
    [Tooltip("Lava/water interaction manager. Auto-found if left empty. Used to clean up spawned rocks on reset.")]
    [SerializeField] private LavaWaterInteraction lavaWaterInteraction;
    [SerializeField] private GameObject resetButton;
    [SerializeField] private GameObject clearButton;

    [SerializeField] private GameObject pipeButtons;
    [SerializeField] private GameObject playAnimation;
    [SerializeField] private TextMeshProUGUI time;
    [SerializeField] private TextMeshProUGUI winTime;


    [Header("Victory")]
    [Tooltip("UI panel to show when the water collector reaches 100%.")]
    [SerializeField] private GameObject victoryUI;

    private bool isPlaying = false;
    private bool hasWon = false;
    private float timePassed = 0.0f;
    private WaterCollector[] collectors;

    public bool IsPlaying => isPlaying;

    /// <summary>
    /// Total collected particles across all water collectors / total capacity.
    /// </summary>
    public float TotalFillPercent
    {
        get
        {
            if (collectors == null || collectors.Length == 0) return 0f;
            int totalCollected = 0;
            int totalCapacity = 0;
            for (int i = 0; i < collectors.Length; i++)
            {
                if (collectors[i] == null) continue;
                totalCollected += collectors[i].CollectedCount;
                totalCapacity += collectors[i].Capacity;
            }
            return totalCapacity > 0 ? (float)totalCollected / totalCapacity : 0f;
        }
    }

    void Awake()
    {
        // Each scene has its own GameManager  always use the newest one.
        // The old instance from the previous scene may not be destroyed yet
        // when this runs, so we replace it rather than destroying ourselves.
        instance = this;
    }

    void Start()
    {
        // Auto-populate if the array wasn't set in the inspector
        if (fluids == null || fluids.Length == 0)
            fluids = FindObjectsOfType<SPHFluid2D>();

        if (lavaWaterInteraction == null)
            lavaWaterInteraction = FindObjectOfType<LavaWaterInteraction>();

        collectors = FindObjectsOfType<WaterCollector>();

        // Ensure we start in a clean ready state
        isPlaying = false;
        hasWon = false;
        timePassed = 0.0f;

        if (victoryUI != null)
            victoryUI.SetActive(false);

        // Set button visibility for ready state
        if (resetButton != null) resetButton.SetActive(false);
        if (clearButton != null) clearButton.SetActive(true);
        if (pipeButtons != null) pipeButtons.SetActive(true);

        if (time != null) time.text = "Time";

        if (playAnimation != null)
            playAnimation.transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    private void Update()
    {
        if (isPlaying)
        {
            timePassed += Time.deltaTime;
            if (time != null) time.text = timePassed.ToString("F2");
            if (winTime != null) winTime.text = timePassed.ToString("F2");
            if (playAnimation != null)
                playAnimation.transform.Rotate(0, 0, -(Time.deltaTime * 100.0f));

            // Check victory: total collected across ALL collectors >= total capacity
            float fill = TotalFillPercent;
            if (Time.frameCount % 60 == 0) // log once per second
                Debug.Log($"[Victory Check] Fill: {fill * 100f:F1}% | Collectors: {(collectors != null ? collectors.Length : 0)}");
            if (!hasWon && fill >= 1f)
                OnVictory();
        }
    }

    // START GAME
    public void onGreenButtonPress()
    {
        if (isPlaying || hasWon) return;

        isPlaying = true;
        hasWon = false;

        timePassed = 0.0f;

        if (victoryUI != null)
            victoryUI.SetActive(false);

        if (fluids != null)
        {
            for (int i = 0; i < fluids.Length; i++)
            {
                if (fluids[i] == null) continue;
                fluids[i].enabled = true;
                fluids[i].startGame();
            }
        }

        GameObject[] pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
        foreach (GameObject pipe in pipesArray)
        {
            DraggablePipe pipeScript = pipe.GetComponent<DraggablePipe>();
            if (pipeScript != null)
            {
                pipeScript.enabled = false;
                pipeScript.startGame();
            }
        }
        if (pipeButtons != null) pipeButtons.SetActive(false);
        if (resetButton != null) resetButton.SetActive(true);
        if (clearButton != null) clearButton.SetActive(false);

        if (LevelStats.instance != null)
            LevelStats.instance.StartTracking();
    }

    // RED BUTTON
    public void onRedButtonPress()
    {
        // CASE 1: Game is running OR player just won  STOP and reset to ready state
        if (isPlaying || hasWon)
        {
            ResetToReadyState();
        }
        // CASE 2: Game NOT running (ready state)  CLEAR all pipes
        else
        {
            // Destroy all pipes
            GameObject[] pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
            foreach (GameObject pipe in pipesArray)
            {
                Destroy(pipe);
            }
            timePassed = 0.0f;
            if (time != null) time.text = "Time";
        }
    }

    /// <summary>
    /// Reset game back to the ready state (as if we just loaded the scene).
    /// Stops fluids, clears particles, hides victory UI, re-enables pipe editing.
    /// </summary>
    private void ResetToReadyState()
    {
        isPlaying = false;
        hasWon = false;

        // Hide victory UI
        if (victoryUI != null)
            victoryUI.SetActive(false);

        // Stop and clear all fluids
        if (fluids != null)
        {
            for (int i = 0; i < fluids.Length; i++)
            {
                if (fluids[i] == null) continue;
                fluids[i].ClearParticles();
                fluids[i].stopGame();
                fluids[i].enabled = false;
            }
        }

        // Clean up rocks spawned by water/lava contact
        if (lavaWaterInteraction != null)
            lavaWaterInteraction.ClearRocks();

        // Reset water collectors
        WaterCollector[] collectors = FindObjectsOfType<WaterCollector>();
        foreach (WaterCollector collector in collectors)
            collector.ResetCollector();

        // Re-enable pipe editing
        GameObject[] pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
        foreach (GameObject pipe in pipesArray)
        {
            DraggablePipe pipeScript = pipe.GetComponent<DraggablePipe>();
            if (pipeScript != null)
            {
                pipeScript.enabled = true;
                pipeScript.resetGame();
            }
        }

        // Reset UI
        if (playAnimation != null)
            playAnimation.transform.rotation = Quaternion.Euler(0, 0, 0);
        if (pipeButtons != null) pipeButtons.SetActive(true);
        if (resetButton != null) resetButton.SetActive(false);
        if (clearButton != null) clearButton.SetActive(true);
        timePassed = 0.0f;
        if (time != null) time.text = "Time";

        if (LevelStats.instance != null)
            LevelStats.instance.Reset();
    }

    /// <summary>
    /// Triggered when total collected particles across all collectors reach total capacity.
    /// </summary>
    public void OnVictory()
    {
        if (hasWon || !isPlaying)
            return;

        hasWon = true;
        isPlaying = false;

        Debug.Log($"Victory! Time: {timePassed:F2}s");

        // Stop all fluid simulations so particles freeze in place
        if (fluids != null)
        {
            for (int i = 0; i < fluids.Length; i++)
            {
                if (fluids[i] == null) continue;
                fluids[i].stopGame();
                fluids[i].enabled = false;
            }
        }

        // Stop play animation
        if (playAnimation != null)
            playAnimation.transform.rotation = Quaternion.Euler(0, 0, 0);

        // Show victory UI and tell LevelStats to calculate stars
        if (victoryUI != null)
            victoryUI.SetActive(true);

        if (LevelStats.instance != null)
            LevelStats.instance.ShowVictory();
    }

    public void onExitButtonPress()
    {
        ResetToReadyState();
        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Called by LevelStats.LoadNextLevel(). Uses LevelStats to determine the next scene.
    /// </summary>
    public void OnNextLevelButton()
    {
        string nextScene = LevelStats.instance != null ? LevelStats.instance.NextLevelScene : "";
        ResetToReadyState();
        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("No next level scene configured.");
    }

    /// <summary>
    /// Overload: load a specific scene by name (for inspector onClick with string arg).
    /// </summary>
    public void OnNextLevelButton(string sceneName)
    {
        ResetToReadyState();
        SceneManager.LoadScene(sceneName);
    }

    public void onRetryButton()
    {
        ResetToReadyState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
