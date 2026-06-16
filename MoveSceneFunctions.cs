using UnityEngine;
using UnityEngine.SceneManagement;


public class MoveSceneFunctions : MonoBehaviour
{
  public static bool Caught;
  public static string difficulty = "";
  public void GoToEasy()
  {
    difficulty = "Easy";
    UnityEngine.SceneManagement.SceneManager.LoadScene("PlayDisplay");
  }
  public void GoToTitle()
  {
    UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
  }
  public void GoToDifficultySelection()
  {
    UnityEngine.SceneManagement.SceneManager.LoadScene("DifficultySelection");
  }
  public void GoToHard()
  {
    difficulty = "Hard";
    UnityEngine.SceneManagement.SceneManager.LoadScene("DifficultMode");
  }
  void OnTriggerEnter(Collider other)
  {
    if (other.gameObject.name == "skk_horror")
    {
        Caught = true;
        SceneManager.LoadScene("GameOver");
    }
  }
  public void Restart()
  {
    Debug.Log("difficulty:" + difficulty);
    if (difficulty == "Easy")
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("PlayDisplay");
    }
    else if (difficulty == "Hard")
    {
      SceneManager.LoadScene("DifficultMode");
    }
  }
}
