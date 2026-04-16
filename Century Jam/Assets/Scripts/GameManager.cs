using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Tooltip("All fluid simulations in the level (water, lava, etc).")]
    [SerializeField] private SPHFluid2D[] fluids;
    [Tooltip("Lava/water interaction manager. Auto-found if left empty.")]
    [SerializeField] private LavaWaterInteraction lavaWaterInteraction;
    [SerializeField] private GameObject resetButton;
    [SerializeField] private GameObject clearButton;
    [SerializeField] private GameObject pipeButtons;
    [SerializeField] private GameObject playAnimation;

    [Header("Victory")]
    [SerializeField] private GameObject victoryUI;

    private bool isPlaying = false;
    private bool hasWon = false;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (instance.gameObject.scene != gameObject.scene)
                Destroy(instance.gameObject);
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Start()
    {
        DestroyOldGameplayCanvases();
        ResolveSceneReferences();
        EnsureLevelStats();
        ResetToReadyState(false);
    }

    private void Update()
    {
        if (isPlaying && playAnimation != null)
            playAnimation.transform.Rotate(0, 0, -(Time.deltaTime * 100.0f));
    }

    // START GAME
    public void onGreenButtonPress()
    {
        ResolveSceneReferences();

        if (isPlaying)
            return;

        Time.timeScale = 1f;
        isPlaying = true;
        hasWon = false;

        HideAllVictoryPanels();

        LevelStats stats = EnsureLevelStats();
        if (stats != null)
            stats.StartTracking();

        if (fluids != null)
        {
            for (int i = 0; i < fluids.Length; i++)
            {
                if (fluids[i] == null) continue;
                fluids[i].enabled = true;
                fluids[i].startGame();
            }
        }

        WindMachine[] windMachines = FindObjectsOfType<WindMachine>(true);
        for (int i = 0; i < windMachines.Length; i++)
            windMachines[i].RefreshFluids();

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

        if (pipeButtons != null)
            pipeButtons.SetActive(false);
        if (resetButton != null)
            resetButton.SetActive(true);
        if (clearButton != null)
            clearButton.SetActive(false);
    }

    // RED BUTTON
    public void onRedButtonPress()
    {
        ResolveSceneReferences();

        if (isPlaying)
        {
            ResetToReadyState(true);
            return;
        }

        DestroyAllPipes();
        ResetToReadyState(true);
    }

    /// <summary>
    /// Called by WaterCollector's onFilled event when the collector reaches 100%.
    /// </summary>
    public void OnVictory()
    {
        ResolveSceneReferences();

        if (hasWon || !isPlaying)
            return;

        hasWon = true;
        isPlaying = false;

        StopFluids(false);

        if (playAnimation != null)
            playAnimation.transform.rotation = Quaternion.Euler(0, 0, 0);

        LevelStats stats = EnsureLevelStats();
        if (stats != null)
            stats.ShowVictory();

        HideAllVictoryPanels();
        if (victoryUI != null)
            victoryUI.SetActive(true);
    }

    public void OnRetryButton()
    {
        PrepareForSceneExit();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnNextLevelButton()
    {
        LevelStats stats = EnsureLevelStats();
        string nextScene = stats != null ? stats.NextLevelScene : "";

        PrepareForSceneExit();

        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("No next level scene assigned or inferred.");
    }

    public void onExitButtonPress()
    {
        PrepareForSceneExit();
        SceneManager.LoadScene("MainMenu");
    }

    private void ResolveSceneReferences()
    {
        if (fluids == null || fluids.Length == 0)
            fluids = FindObjectsOfType<SPHFluid2D>(true);

        if (lavaWaterInteraction == null)
            lavaWaterInteraction = FindObjectOfType<LavaWaterInteraction>(true);

        if (resetButton == null)
            resetButton = FindSceneGameObject("ResetButton");

        if (clearButton == null)
            clearButton = FindSceneGameObject("Clear");

        if (pipeButtons == null)
            pipeButtons = FindSceneGameObject("PipeUI");

        if (playAnimation == null)
            playAnimation = FindSceneGameObject("Start");

        if (victoryUI == null || victoryUI.scene != gameObject.scene)
            victoryUI = FindSceneGameObject("VictoryUI");
    }

    private LevelStats EnsureLevelStats()
    {
        if (LevelStats.instance != null && LevelStats.instance.gameObject.scene == gameObject.scene)
            return LevelStats.instance;

        LevelStats[] statsObjects = FindObjectsOfType<LevelStats>(true);
        for (int i = 0; i < statsObjects.Length; i++)
        {
            if (statsObjects[i] != null && statsObjects[i].gameObject.scene == gameObject.scene)
                return statsObjects[i];
        }

        if (LevelStats.instance != null)
            Destroy(LevelStats.instance.gameObject);

        LevelStats stats = gameObject.GetComponent<LevelStats>();
        if (stats == null)
            stats = gameObject.AddComponent<LevelStats>();

        return stats;
    }

    private void ResetToReadyState(bool clearParticles)
    {
        ResolveSceneReferences();

        Time.timeScale = 1f;
        isPlaying = false;
        hasWon = false;

        HideAllVictoryPanels();
        StopFluids(clearParticles);

        if (clearParticles && lavaWaterInteraction != null)
            lavaWaterInteraction.ClearRocks();

        ResetCollectors();
        ResetPipesForEditing();

        LevelStats stats = EnsureLevelStats();
        if (stats != null)
            stats.Reset();

        if (playAnimation != null)
            playAnimation.transform.rotation = Quaternion.Euler(0, 0, 0);

        if (pipeButtons != null)
            pipeButtons.SetActive(true);
        if (resetButton != null)
            resetButton.SetActive(false);
        if (clearButton != null)
            clearButton.SetActive(true);
    }

    private void PrepareForSceneExit()
    {
        ResolveSceneReferences();

        Time.timeScale = 1f;
        isPlaying = false;
        hasWon = false;

        HideAllVictoryPanels();
        StopFluids(true);

        if (lavaWaterInteraction != null)
            lavaWaterInteraction.ClearRocks();

        if (LevelStats.instance != null)
            LevelStats.instance.Reset();
    }

    private void StopFluids(bool clearParticles)
    {
        if (fluids == null)
            return;

        for (int i = 0; i < fluids.Length; i++)
        {
            if (fluids[i] == null) continue;

            if (clearParticles)
                fluids[i].ClearParticles();

            fluids[i].stopGame();
            fluids[i].enabled = false;
        }
    }

    private void ResetCollectors()
    {
        WaterCollector[] collectors = FindObjectsOfType<WaterCollector>(true);
        for (int i = 0; i < collectors.Length; i++)
            collectors[i].ResetCollector();
    }

    private void ResetPipesForEditing()
    {
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
    }

    private void DestroyAllPipes()
    {
        GameObject[] pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
        foreach (GameObject pipe in pipesArray)
            Destroy(pipe);
    }

    private static void HideAllVictoryPanels()
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != "VictoryUI")
                continue;

            GameObject go = candidate.gameObject;
            if (go.scene.IsValid())
                go.SetActive(false);
        }
    }

    private static void DestroyOldGameplayCanvases()
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != "Canvas")
                continue;

            GameObject go = candidate.gameObject;
            if (!go.scene.IsValid() || go.scene == activeScene)
                continue;

            if (candidate.GetComponent<Canvas>() == null)
                continue;

            if (HasChildNamed(candidate, "VictoryUI") || HasChildNamed(candidate, "PipeUI"))
                Destroy(go);
        }
    }

    private static bool HasChildNamed(Transform root, string childName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName || HasChildNamed(child, childName))
                return true;
        }

        return false;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject fallback = null;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
                continue;

            GameObject go = candidate.gameObject;
            if (!go.scene.IsValid())
                continue;

            if (go.scene == activeScene)
                return go;

            if (fallback == null)
                fallback = go;
        }

        return fallback;
    }
}
