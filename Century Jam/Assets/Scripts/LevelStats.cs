using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Star Images")]
    [SerializeField] private Image star1Image;
    [SerializeField] private Image star2Image;
    [SerializeField] private Image star3Image;
    [SerializeField] private Color starEarnedColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color starUnearnedColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);

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
        SetStarVisual(star1Image, false);
        SetStarVisual(star2Image, false);
        SetStarVisual(star3Image, false);
    }

    public void Reset()
    {
        ResolveReferences();

        isTracking = false;
        timePassed = 0f;
        starsEarned = 0;

        UpdateTimeText();
        SetStarVisual(star1Image, false);
        SetStarVisual(star2Image, false);
        SetStarVisual(star3Image, false);

        if (nextLevelButton != null && nextLevelButton.name != "VictoryUI")
            nextLevelButton.SetActive(false);
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

        SetStarVisual(star1Image, earnedStar1);
        SetStarVisual(star2Image, earnedStar2);
        SetStarVisual(star3Image, earnedStar3);

        if (victoryTitleText != null)
            victoryTitleText.text = "YOU WIN";

        if (victoryTimeText != null)
            victoryTimeText.text = "Time: " + FormatTime(timePassed);

        if (victoryPipesText != null)
            victoryPipesText.text = "Pipes: " + pipeCount;

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

        if (star1Image == null)
            star1Image = FindSceneComponent<Image>("MainStar");

        if (star2Image == null)
            star2Image = FindSceneComponent<Image>("LeftStar");

        if (star3Image == null)
            star3Image = FindSceneComponent<Image>("RightStar");

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
            star2ConditionText.text = "Use " + maxPipesForStar2 + " or fewer pipes";

        if (star3ConditionText != null)
            star3ConditionText.text = "Complete within " + FormatTime(maxTimeForStar3);
    }

    private void UpdateTimeText()
    {
        if (timeText != null)
            timeText.text = FormatTime(timePassed);
    }

    private void SetStarVisual(Image starImage, bool earned)
    {
        if (starImage == null)
            return;

        starImage.color = earned ? starEarnedColor : starUnearnedColor;
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
