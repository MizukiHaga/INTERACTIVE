using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // シーン遷移などで使用
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    AudioSource[] audioSource;
    // Singletonパターン
    public static GameManager Instance { get; private set; }
    public static bool Clear;
    [SerializeField ]private Text pickedItems;

    [Header("ゲーム設定")]
    [SerializeField] private int requiredItemsToWin = 6; // クリアに必要なアイテム数
    private int collectedItems = 0; // 収集したアイテムの現在数

    void Start()
    {
        TouchGameOver.Caught = false;
        GameManager.Clear = false;
        audioSource = GetComponents<AudioSource>();
        audioSource[1].loop = true;
        audioSource[1].Play();
        if (Instance == null)
        {
            Instance = this;
            // シーンを跨いで保持したい場合は DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // アイテム収集時に呼び出されるメソッド
    public void CollectItem()
    {
        collectedItems += 1;
        Debug.Log("アイテムを拾いました。現在の収集数: " + collectedItems + " / " + requiredItemsToWin);
        audioSource[0].Play();
        // クリア条件のチェック
        if (collectedItems >= requiredItemsToWin)
        {
            GameClear();
        }
        pickedItems.text = collectedItems.ToString() + "/6";
        pickedItems.gameObject.SetActive(true);
    }

    // ゲームクリア時の処理
    private void GameClear()
    {
        Debug.Log("ゲームクリア！");
        Clear = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
        // その他のクリア処理（時間停止、入力無効化など）
        // Time.timeScale = 0f; 
    }

    // 収集数の表示（デバッグ用またはUI更新用）
    public int GetCollectedItemsCount()
    {
        return collectedItems;
    }
}