using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [SerializeField] private GameObject fluid;
    [SerializeField] private GameObject resetButton;
    [SerializeField] private GameObject clearButton;

    private GameObject[] pipesArray;
    private bool isPlaying = false;

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

    // START GAME
    public void onGreenButtonPress()
    {
        if (isPlaying) return;

        isPlaying = true;

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

            // Refresh pipe array
            pipesArray = new GameObject[0];
        }
    }
}