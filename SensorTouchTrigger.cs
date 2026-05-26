// using UnityEngine;
// using System.Collections.Generic; // ← これが足りなかったためにエラーが出ていました

// public class SensorTouchTrigger : MonoBehaviour
// {
//     // MoveSceneFunctionsが入っているオブジェクトを指定します
//     public MoveSceneFunctions moveSceneFunctions;

//     [Header("ボタンの反応範囲 (x, y, width, height)")]
//     public Rect touchArea = new Rect(700, 400, 500, 300);

//     private List<PointData> points = new List<PointData>(); // エラーが出ていた箇所

//     void Update()
//     {
//         // センサーが動いていない、または遷移スクリプトが設定されていないなら何もしない
//         if (SensorReceiver.Instance == null || moveSceneFunctions == null) return;

//         // センサーの検知データを取得
//         SensorReceiver.Instance.getPointList(ref points);

//         foreach (PointData p in points)
//         {
//             // 検知した位置(p.position)が、設定した範囲内か判定
//             if (touchArea.Contains(p.position))
//             {
//                 Debug.Log("センサーがボタン範囲内で反応しました");
//                 moveSceneFunctions.GameStartFunction();
//                 break;
//             }
//         }
//     }

//     // 範囲を画面上で確認するためのデバッグ表示
//     void OnDrawGizmos()
//     {
//         Gizmos.color = Color.red;
//         Gizmos.DrawWireCube(new Vector3(touchArea.center.x, touchArea.center.y, 0), 
//                             new Vector3(touchArea.width, touchArea.height, 0));
//     }
// }
using UnityEngine;
using System.Collections.Generic; // ← これが足りなかったためにエラーが出ていました
using UnityEngine.Events;

public class SensorTouchTrigger : MonoBehaviour
{
    // MoveSceneFunctionsが入っているオブジェクトを指定します
    public MoveSceneFunctions moveSceneFunctions;

    [Header("ボタンが反応した時に実行するイベント")]
    public UnityEvent OnTouchTriggered;

    [Header("ボタンの反応範囲 (x, y, width, height)")]
    public Rect touchArea = new Rect(700, 400, 500, 300);

    [Header("表示色")]
    [SerializeField] private Color touchAreaFillColor = new Color(1f, 0f, 0f, 0.2f);
    [SerializeField] private Color touchAreaOutlineColor = Color.red;
    [Header("検知マーカー")]
    [SerializeField] private float hitDotSize = 16f;
    [SerializeField] private Color hitDotColor = Color.red;

    private Texture2D hitDotTexture;

    private List<PointData> points = new List<PointData>(); // エラーが出ていた箇所

    void Update()
    {
        // センサーが動いていない、または遷移スクリプトが設定されていないなら何もしない
        if (SensorReceiver.Instance == null)
        {
            Debug.LogWarning("SensorReceiver.Instance が初期化されていません");
            return;
        }
        if (moveSceneFunctions == null)
        {
            Debug.LogWarning("moveSceneFunctions が設定されていません");
            return;
        }

        // センサーの検知データを取得
        SensorReceiver.Instance.getPointList(ref points);

        if (points.Count == 0)
        {
            // センサーからデータが入ってこない場合のデバッグ
            // Debug.Log("センサーからデータがありません");
        }

        foreach (PointData p in points)
        {
            //Debug.Log($"センサー検知位置: {p.position}, 判定範囲: {touchArea}");

            // 検知した位置(p.position)が、設定した範囲内か判定
            if (touchArea.Contains(p.position))
            {
                Debug.Log("センサーがボタン範囲内で反応しました");
                OnTouchTriggered?.Invoke();
                break;
            }
        }
    }

    void OnGUI()
    {
        if (hitDotTexture == null)
        {
            hitDotTexture = new Texture2D(1, 1);
            hitDotTexture.SetPixel(0, 0, hitDotColor);
            hitDotTexture.Apply();
        }

        foreach (PointData p in points)
        {
            float x = p.position.x - (hitDotSize / 2f);
            float y = (Screen.height - p.position.y) - (hitDotSize / 2f);
            GUI.DrawTexture(new Rect(x, y, hitDotSize, hitDotSize), hitDotTexture);
        }
    }

    // 範囲を画面上で確認するためのデバッグ表示
    void OnDrawGizmos()
    {
        Gizmos.color = touchAreaFillColor;
        Gizmos.DrawCube(new Vector3(touchArea.center.x, touchArea.center.y, 0),
                        new Vector3(touchArea.width, touchArea.height, 0));

        Gizmos.color = touchAreaOutlineColor;
        Gizmos.DrawWireCube(new Vector3(touchArea.center.x, touchArea.center.y, 0),
                            new Vector3(touchArea.width, touchArea.height, 0));
    }
}