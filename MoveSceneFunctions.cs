using UnityEngine;

public class MoveSceneFunctions : MonoBehaviour
{
  public void GameStartFunction()
  {
    UnityEngine.SceneManagement.SceneManager.LoadScene("PlayDisplay");
  }
  public void MoveTitle()
  {
    UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
  }
}
