using UnityEngine;

public class MoveSceneFunctions : MonoBehaviour
{
  public void GoToEasy()
  {
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
    UnityEngine.SceneManagement.SceneManager.LoadScene("DifficultMode");
  }
}
