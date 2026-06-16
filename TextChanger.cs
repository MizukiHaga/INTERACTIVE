using UnityEngine;
using UnityEngine.UI;

public class TextChanger : MonoBehaviour
{
    [SerializeField] private Text TextField;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (GameManager.Clear == true && MoveSceneFunctions.Caught == false)
        {
            TextField.text = "GameClear";
            TextField.gameObject.SetActive(true);
            Debug.Log("GameClear");
        }
        else if (GameManager.Clear == false && MoveSceneFunctions.Caught == true)
        {
            TextField.text = "GameOver";
            TextField.gameObject.SetActive(true);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
