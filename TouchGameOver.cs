using UnityEngine;
using UnityEngine.SceneManagement;

public class TouchGameOver : MonoBehaviour
{
    public static bool Caught;
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "skk_horror")
        {
            Caught = true;
            SceneManager.LoadScene("GameOver");
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
}
