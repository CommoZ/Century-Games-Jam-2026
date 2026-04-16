using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // remove if not using TMP

public class LevelSelector : MonoBehaviour
{
    [Header("Setup")]
    public GameObject buttonPrefab;
    public Transform buttonParent;

    [Header("Config")]
    public int numberOfLevels = 12;

    private int unlockedLevel;

    void Start()
    {
        // Load progress (default = 1)
        //unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);

        GenerateLevelButtons();
    }

    void GenerateLevelButtons()
    {
        for (int i = 1; i <= numberOfLevels; i++)
        {
            GameObject btn = Instantiate(buttonPrefab, buttonParent);

            // Set text
            TMP_Text text = btn.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = "Level " + i.ToString();

            int levelIndex = i;

            Button button = btn.GetComponent<Button>();

           
            button.interactable = true;

            button.onClick.AddListener(() =>
            {
                LoadLevel(levelIndex);
            });
        }
    }

    public void LoadLevel(int level)
    {
        Debug.Log("Loading Level " + level);

        // Example:
        SceneManager.LoadScene("Level " + level);
    }

    public void returnMenu()
    {
        Debug.Log("Returning to Main Menu");
        // Example:
        SceneManager.LoadScene("MainMenu");


    }

    public void goLevelMenu()
    {
        Debug.Log("Go level Selector");
        // Example:
        SceneManager.LoadScene("LevelSelector");
    }
}