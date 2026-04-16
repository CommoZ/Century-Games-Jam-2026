using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks level statistics and drives the victory UI. GameManager owns the
/// gameplay state; this script only owns time, star, and result text state.
/// </summary>
public class LevelStats : MonoBehaviour
{
    public static LevelStats instance;

    [Header("HUD - Time Display")]
    [Tooltip("In-game timer text. If empty, the script searches for TimeText.")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Victory Panel")]
    [Tooltip("Large victory title. If empty, the script searches for WinText.")]
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [Tooltip("Elapsed time text. If empty, the script searches for TimeScore.")]
    [SerializeField] private TextMeshProUGUI victoryTimeText;
    [Tooltip("Pipe count text. Optional.")]
    [SerializeField] private TextMeshProUGUI victoryPipesText;

    [Header("Star Positions (drag GameObjects here)")]
    [Tooltip("GameObject whose Transform marks where star 1 appears.")]
    [SerializeField] private Transform star1Position;
    [Tooltip("GameObject whose Transform marks where star 2 appears.")]
    [SerializeField] private Transform star2Position;
    [Tooltip("GameObject whose Transform marks where star 3 appears.")]
    [SerializeField] private Transform star3Position;

    [Header("Star Prefabs")]
    [Tooltip("Prefab or sprite to instantiate when a star is earned.")]
    [SerializeField] private GameObject starEarnedPrefab;
    [Tooltip("Prefab or sprite to instantiate when a star is not earned.")]
    [SerializeField] private GameObject starUnearnedPrefab;

    [Header("Star Condition Text")]
    [SerializeField] private TextMeshProUGUI star2ConditionText;
    [SerializeField] private TextMeshProUGUI star3ConditionText;

    [Header("Star Conditions")]
    [SerializeField] private int maxPipesForStar2 = 5;
    [SerializeField] private float maxTimeForStar3 = 60f;

    [Header("Navigation")]
    [Tooltip("Optional next-level button. Do not assign the whole VictoryUI panel here.")]
    [SerializeField] private GameObject nextLevelButton;
#if UNITY_EDITOR
    [Tooltip("Editor-only scene picker. For builds, use Next Level Scene Name.")]
    [SerializeField] private UnityEditor.SceneAsset nextLevelScene;
#endif
    [Tooltip("Exact scene name to load. If empty, Level 1 infers Level 2, Level 2 infers Level 3, etc.")]
    [SerializeField] private string nextLevelSceneName;

    private float timePassed;
    private bool isTracking;
    private readonly System.Collections.Generic.List<GameObject> spawnedStars = new System.Collections.Generic.List<GameObject>();
    private int starsEarned;

    public string NextLevelScene
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(nextLevelSceneName))
                return nextLevelSceneName.Trim();

#if UNITY_EDITOR
            if (nextLevelScene != null)
                return nextLevelScene.name;
#endif

            return InferNextLevelScene();
        }
    }

    public float TimePassed => timePassed;
    public int StarsEarned => starsEarned;
    public int MaxPipesForStar2 => maxPipesForStar2;
    public float MaxTimeForStar3 => maxTimeForStar3;

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
        ResolveReferences();
        Reset();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Start()
    {
        ResolveReferences();
        UpdateConditionTexts();
    }

    private void Update()
    {
        if (!isTracking)
            return;

        timePassed += Time.deltaTime;
        UpdateTimeText();
    }

    public void StartTracking()
    {
        ResolveReferences();

        timePassed = 0f;
        isTracking = true;
        starsEarned = 0;

        UpdateTimeText();
        ClearStars();
    }

    public void Reset()
    {
        ResolveReferences();

        isTracking = false;
        timePassed = 0f;
        starsEarned = 0;

        UpdateTimeText();
        ClearStars();

        if (nextLevelButton != null && nextLevelButton.name != "VictoryUI")
            nextLevelButton.SetActive(false);

        // Hide victory UI in case it's active by default in the prefab
        HideVictoryUI();
    }

    private void HideVictoryUI()
    {
        // GameManager owns the victoryUI reference, but as a safety net
        // also find and hide it if GameManager hasn't done so yet
        if (GameManager.instance != null) return; // GameManager will handle it

        GameObject victoryGO = FindSceneGameObject("VictoryUI");
        if (victoryGO != null)
            victoryGO.SetActive(false);
    }

    public void ShowVictory()
    {
        ResolveReferences();
        isTracking = false;

        int pipeCount = GameObject.FindGameObjectsWithTag("Pipe").Length;

        bool earnedStar1 = true;
        bool earnedStar2 = pipeCount <= maxPipesForStar2;
        bool earnedStar3 = timePassed <= maxTimeForStar3;

        starsEarned = 1;
        if (earnedStar2) starsEarned = 2;
        if (earnedStar2 && earnedStar3) starsEarned = 3;

        ClearStars();
        SpawnStar(star1Position, earnedStar1);
        SpawnStar(star2Position, earnedStar2);
        SpawnStar(star3Position, earnedStar3);

        // Title: "BEAT LEVEL X"
        if (victoryTitleText != null)
        {
            string levelName = SceneManager.GetActiveScene().name;
            victoryTitleText.text = "BEAT " + levelName.ToUpper();
        }

        // Score: elapsed time
        if (victoryTimeText != null)
            victoryTimeText.text = FormatTime(timePassed);

        // Pipe count used
        if (victoryPipesText != null)
            victoryPipesText.text = pipeCount + " PIPE USED";

        // Star 2 condition: pipe limit
        if (star2ConditionText != null)
            star2ConditionText.text = maxPipesForStar2 + " PIPES USED";

        // Star 3 condition: time limit
        if (star3ConditionText != null)
            star3ConditionText.text = "UNDER " + FormatTime(maxTimeForStar3);

        UpdateNextLevelButtonVisibility();

        Debug.Log("[LevelStats] Victory! Stars: " + starsEarned + "/3 | Time: " +
                  FormatTime(timePassed) + " | Pipes: " + pipeCount);
    }

    public void LoadNextLevel()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.OnNextLevelButton();
            return;
        }

        string nextScene = NextLevelScene;
        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("No next level scene assigned or inferred.");
    }

    private void ResolveReferences()
    {
        if (timeText == null)
            timeText = FindSceneComponent<TextMeshProUGUI>("TimeText");

        if (victoryTitleText == null)
            victoryTitleText = FindSceneComponent<TextMeshProUGUI>("WinText");

        if (victoryTimeText == null)
            victoryTimeText = FindSceneComponent<TextMeshProUGUI>("TimeScore");

        if (nextLevelButton == null)
            nextLevelButton = FindSceneGameObject("NextLevelButton");
    }

    private void UpdateNextLevelButtonVisibility()
    {
        if (nextLevelButton == null || nextLevelButton.name == "VictoryUI")
            return;

        nextLevelButton.SetActive(!string.IsNullOrEmpty(NextLevelScene));
    }

    private void UpdateConditionTexts()
    {
        if (star2ConditionText != null)
            star2ConditionText.text = (maxPipesForStar2 > 1) ? maxPipesForStar2 + " Pipes  Used" : maxPipesForStar2 + " Pipe  Used";

        if (star3ConditionText != null)
            star3ConditionText.text = "Under " + FormatTime(maxTimeForStar3);
    }

    private void UpdateTimeText()
    {
        if (timeText != null)
            timeText.text = FormatTime(timePassed);
    }

    private void SpawnStar(Transform position, bool earned)
    {
        if (position == null) return;

        GameObject prefab = earned ? starEarnedPrefab : starUnearnedPrefab;
        if (prefab == null) return;

        GameObject star = Instantiate(prefab, position.position, position.rotation, position);
        spawnedStars.Add(star);
    }

    private void ClearStars()
    {
        for (int i = 0; i < spawnedStars.Count; i++)
        {
            if (spawnedStars[i] != null)
                Destroy(spawnedStars[i]);
        }
        spawnedStars.Clear();
    }

    private string InferNextLevelScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        const string prefix = "Level ";

        if (!activeSceneName.StartsWith(prefix))
            return "";

        string numberPart = activeSceneName.Substring(prefix.Length);
        if (!int.TryParse(numberPart, out int levelNumber))
            return "";

        return prefix + (levelNumber + 1);
    }

    private static string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return mins.ToString("00") + ":" + secs.ToString("00");
    }

    private static T FindSceneComponent<T>(string objectName) where T : Component
    {
        GameObject go = FindSceneGameObject(objectName);
        return go != null ? go.GetComponent<T>() : null;
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
