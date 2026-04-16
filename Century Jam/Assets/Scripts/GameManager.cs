using System.Data.SqlTypes;
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
    [SerializeField] private TextMeshProUGUI time;

    [Header("Victory")]
    [Tooltip("UI panel to show when the water collector reaches 100%.")]
    [SerializeField] private GameObject victoryUI;

    private bool isPlaying = false;
    private bool hasWon = false;
    private float timePassed = 0.0f;
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Auto-populate if the array wasn't set in the inspector
        if (fluids == null || fluids.Length == 0)
            fluids = FindObjectsOfType<SPHFluid2D>();

        if (lavaWaterInteraction == null)
            lavaWaterInteraction = FindObjectOfType<LavaWaterInteraction>();

        if (victoryUI != null)
            victoryUI.SetActive(false);
    }

    private void Update()
    {
        if (isPlaying)
        {
            timePassed += Time.deltaTime;
            time.text = timePassed.ToString("F2");
        }
    }

    // START GAME
    public void onGreenButtonPress()
    {
        if (isPlaying) return;

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

        resetButton.SetActive(true);
        clearButton.SetActive(false);
    }

    // RED BUTTON
    public void onRedButtonPress()
    {
        // CASE 1: Game is running then STOP (freeze, do NOT clear)
        if (isPlaying)
        {
            isPlaying = false;

            if (fluids != null)
            {
                for (int i = 0; i < fluids.Length; i++)
                {
                    if (fluids[i] == null) continue;
                    // Stop the stream so no new particles spawn, and clear
                    // any existing particles in the simulation.
                    fluids[i].ClearParticles();
                    fluids[i].stopGame();
                    fluids[i].enabled = false;
                }
            }

            // Clean up rocks spawned by water/lava contact
            if (lavaWaterInteraction != null)
                lavaWaterInteraction.ClearRocks();

            GameObject[] pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
            foreach (GameObject pipe in pipesArray)
            {
                DraggablePipe pipeScript = pipe.GetComponent<DraggablePipe>();
                if (pipeScript != null)
                {
                    pipeScript.enabled = true;   // allow editing again
                    pipeScript.resetGame();
                }
            }

            resetButton.SetActive(false);
            clearButton.SetActive(true);
        }
        // CASE 2: Game NOT running then CLEAR (reset everything)
        else
        {
            // Destroy all pipes
            GameObject[] pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
            foreach (GameObject pipe in pipesArray)
            {
                Destroy(pipe);
            }
            timePassed = 0.0f;
            time.text = "Time";

            // Refresh pipe array
            pipesArray = new GameObject[0];
        }
    }

    /// <summary>
    /// Called by WaterCollector's onFilled event when the collector reaches 100%.
    /// </summary>
    public void OnVictory()
    {
        if (hasWon || !isPlaying)
            return;

        hasWon = true;
        isPlaying = false;

        Debug.Log($"Victory! Time: {timePassed:F2}s");

        if (victoryUI != null)
            victoryUI.SetActive(true);
    }

    public void onExitButtonPress()
    {
        SceneManager.LoadScene("MainMenu");
    }

}
