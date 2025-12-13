using UnityEngine;

public class EffectTest : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip testSound;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            audioSource.PlayOneShot(testSound);
        }
    }
}