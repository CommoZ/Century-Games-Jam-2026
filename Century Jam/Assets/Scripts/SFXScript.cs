using UnityEngine;

public class SFXScript : MonoBehaviour
{
    public AudioClip clickSound;
    AudioSource audioSource;
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        Debug.Log("audioSource started");
    }

    public void PlaySound()
    {
        audioSource.PlayOneShot(clickSound);
    }
}
