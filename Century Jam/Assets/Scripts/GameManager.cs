using System.Data.SqlTypes;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [SerializeField] private GameObject fluid;
    [SerializeField] private GameObject resetButton;
    [SerializeField] private GameObject clearButton;
    [SerializeField] private TextMeshProUGUI time;

    private bool isPlaying = false;
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
        fluid = GameObject.Find("particleScript");
        //pipesArray = GameObject.FindGameObjectsWithTag("Pipe");
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

        timePassed = 0.0f;

        SPHFluid2D fluidScript = fluid.GetComponent<SPHFluid2D>();
        if (fluidScript != null)
        {
            fluidScript.enabled = true;
            fluidScript.startGame();
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
        SPHFluid2D fluidScript = fluid.GetComponent<SPHFluid2D>();

        // CASE 1: Game is running then STOP
        if (isPlaying)
        {
            isPlaying = false;

            if (fluidScript != null)
            {
                //fluidScript.stopGame();
                fluidScript.ClearParticles();
                fluidScript.enabled = false;
            }
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
        // CASE 2: Game NOT running then CLEAR
        else
        {
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

    public void onExitButtonPress()
    {
        SceneManager.LoadScene("MainMenu");
    }

}