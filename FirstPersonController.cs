using UnityEngine;
using System.Collections;
using System.Collections.Generic;
// using System.Numerics;
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Move")]
    public float walkSpeed = 3.5f;
    public float runSpeed  = 6.0f;
    public float jumpHeight = 1.2f;

    [Header("View (Mouse)")]
    public Transform cameraTransform;
    public float mouseSensitivity = 120f;  // 大きいほど速く回転
    public bool invertY = false;

    [Header("Physics")]
    public float gravity = -9.81f * 2f;    // 少し強めにすると気持ちよく落ちる
    public float groundedStick = -2f;      // 地面に張り付ける微小な下向き速度

    // === 物理エンジン関連 ===
    CharacterController controller;      // Unityの移動制御コンポーネント
    float verticalVelocity;               // 上下速度（ジャンプ・落下用）
    float cameraPitch;                    // カメラの上下角度（-90°～89°に制限)
    
    // === センサー入力 ===
    private List<PointData> list;         // フレーム毎のセンサー検出座標リスト
    
    // === フレーム内の集計値（毎フレーム初期化） ===
    float moveInput = 0f;                 // 前進(+1) ～ 後退(-1) の連続値 [-1, 1]
    bool jumpRequested = false;           // このフレームでジャンプ開始するか
    float pendingLookDx = 0f;             // 左右視点回転の集計値（複数点を1回で反映するため）
    public Transform warpTarget; // ワープ先のターゲット位置（Inspectorで指定）
    public Transform FirstWarpTarget; // ワープ先のターゲット位置
    public Transform SecondWarpTarget; // 2つ目のワープ先のターゲット位置
    public Transform ThirdWarpTarget; // 3つ目のワープ先のターゲット位置（Inspectorで指定）
    public Transform FourthWarpTarget; // 4つ目のワープ先のターゲット位置（Inspectorで指定）
    public Transform FifthWarpTarget; // 5つ目のワープ先のターゲット位置（Inspectorで指定）
    void Awake()
    {
        // === 初期化処理 ===
        
        // このGameObjectにアタッチされたCharacterControllerを取得
        // （Unityの移動・衝突判定コンポーネント）
        controller = GetComponent<CharacterController>();
        
        // カメラがインスペクタで指定されていない場合、子オブジェクトから自動検索
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cameraTransform = cam.transform;
        }
        
        // マウスカーソルをゲーム画面にロック＆非表示にする
        // （センサー映像表示中でもカーソルが見えないように）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // センサー座標リストを初期化
        list = new List<PointData>();
    }

    void Update()
    {
        // === フレーム毎の処理フロー ===
        
        // [ステップ1] センサーから現フレームの検出座標を取得
        SensorReceiver.Instance.getPointList(ref list);

        // [ステップ2] 集計値をリセット（前フレームのデータをクリア）
        // このフレームの新しいセンサー入力だけを処理するため
        moveInput = 0f;       // 前進/後退入力
        jumpRequested = false; // ジャンプ要求
        pendingLookDx = 0f;    // 左右視点回転

        // [ステップ3] センサー座標を処理して、moveInput, jumpRequested, pendingLookDx を更新
        HandleSensorInput();
        
        // [ステップ4] 重力・ジャンプ・移動を適用
        ApplyGravityAndMove();
    }
    void ApplyGravityAndMove()
    {
        // === 重力・物理処理 ===
        
        // [1] 地面張り付け
        // 地面に接地中で、下向き速度がマイナス（落下状態）なら、
        // わずかな下向き速度を持たせて地面に張り付ける
        // （段差で不意に落ちるのを防止）
        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = groundedStick;  // groundedStick = -2f

        // [2] 重力加速度を毎フレーム加算
        // gravity = -9.81 * 2f（リアルより強めで気持ちよい落下感）
        // Time.deltaTime をかけてフレームレート依存を排除
        verticalVelocity += gravity * Time.deltaTime;
        
        // === 視点回転（左右） ===
        
        // [3] 回転スピードを計算
        // mouseSensitivity が大きいほど、0.005の基本値を拡大
        float rotationSpeed = 0.005f * (mouseSensitivity / 120f);
        
        // [4] 横視点回転を適用（1フレーム1回のみ）
        // pendingLookDx には左側センサーの複数点が集計されているため、
        // この1行で全センサー点の回転を一度に反映する
        if (Mathf.Abs(pendingLookDx) > 0.001f)
            transform.Rotate(Vector3.up * pendingLookDx * rotationSpeed);

        // NOTE: センサー上下はカメラピッチではなく前進/後退にマップするため、
        //       カメラの上下角度は変更しない

        // === ジャンプ処理 ===
        
        // [5] ジャンプ判定（1フレーム更新されたジャンプフラグを使用）
        bool grounded = controller.isGrounded;  // 地面に接地しているか
        if (jumpRequested && grounded)
        {
            // ジャンプ初速度を計算：v = √(2*g*h)
            // h = jumpHeight = 1.2f
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // === 水平移動（前後） ===
        
        // [6] 移動速度を計算
        // transform.forward = プレイヤーが向いている方向
        // walkSpeed = 3.5f（基本歩行速度）
        // moveInput = -1～+1（後退～前進）
        Vector3 horizontal = transform.forward * walkSpeed * Mathf.Clamp(moveInput, -1f, 1f);

        // === 最終移動の適用 ===
        
        // [7] 水平移動と垂直速度を合成
        Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
        
        // [8] CharacterController.Move で実際に移動を適用
        // Time.deltaTime をかけてフレームレート依存を排除
        controller.Move(velocity * Time.deltaTime);
    }
    void LookHorizontal(float dx)
    {
        // 旧来の即時回転は使わない。dx は集計してから1回だけ回す。
        pendingLookDx += dx;
    }
    void HandleSensorInput()
    {
        // === センサー座標を左右2つの領域に分類・集計 ===
        
        // 複数の検出点がある場合、その平均を1回だけ処理する
        // （複数回処理すると、加速度が増してしまうため）
        float leftSumX = 0f, leftSumY = 0f;  int leftCount = 0;   // 左側点の合計・カウント
        float rightSumX = 0f, rightSumY = 0f; int rightCount = 0;  // 右側点の合計・カウント
        
        // [ステップ1] リストの全点を左右領域に分類
        for (int i = 0; i < list.Count; i++)
        {
            PointData p = list[i];
            float x = p.position.x;  // 横座標（0～1920）
            float y = p.position.y;  // 縦座標（0～450）

            // === 左側領域：視点回転＋前後移動 ===
            // 画面左側（x=0～800）に検出された点
            if (x >= 0 && x <= 800 && y >= 0 && y <= 450)
            {
                leftSumX += x; 
                leftSumY += y; 
                leftCount++;
            }

            // === 右側領域：ジャンプトリガー ===
            // 画面右側（x=1120～1920）に検出された点
            if (x >= 1120 && x <= 1920 && y >= 0 && y <= 450)
            {
                rightSumX += x; 
                rightSumY += y; 
                rightCount++;
            }
        }

        // === 左側処理：視点回転＋前後移動 ===
        
        if (leftCount > 0)
        {
            // [ステップ2] 複数点の平均を計算
            float avgX = leftSumX / leftCount;  // 平均X座標
            float avgY = leftSumY / leftCount;  // 平均Y座標
            
            // [ステップ3] 画面中央からの差分を計算
            Vector2 center = new Vector2(400f, 225f);  // 左側の中心（1920x450の左半分の中央）
            float dx = avgX - center.x;  // 横方向の差分（負=左、正=右）
            float dy = avgY - center.y;  // 縦方向の差分（負=上、正=下）

            float horizontalDead = 10f;    // 水平方向のデッドゾーン（±10pxは無視）
            float verticalScale = 80f;     // 垂直差を前後入力にマップする感度（小さいほど敏感）

            // === 左右の視点移動（回転＋前進寄与） ===
            if (Mathf.Abs(dx) > horizontalDead)
            {
                // [ステップ4] 左右回転を記録
                pendingLookDx += dx;  // ApplyGravityAndMove()で1回だけ適用される
                
                // [ステップ5] 左右に視点移動中は常に前進速度を追加
                // 左右どちらに動いても同じ速度で前進（Abs()を使用）
                moveInput += Mathf.Clamp01(Mathf.Abs(dx) / 200f);  // 最大+0.2程度の前進寄与
            }

            // === 上下の視点移動（前後移動） ===
            // 上を見（dy負）→ 前進（+moveInput）
            // 下を見（dy正）→ 後退（-moveInput）
            moveInput += dy / verticalScale;  // 80で割ることで感度を調整
            
            // [ステップ6] 前進/後退の最終値を -1～+1 に制限
            moveInput = Mathf.Clamp(moveInput, -1f, 1f);
        }

        // === 右側処理：ジャンプトリガー ===
        // 右側範囲にセンサー値が無いときにジャンプ要求
        if (rightCount == 0)
            jumpRequested = true;
    }


    // ゲームを抜けやすくするオプション
    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WarpTrigger"))
        {
            controller.enabled = false; // プレイヤーのコントローラーを無効化
            // if(other.name == "To1st")
            // {
            //     transform.position = FirstWarpTarget.position;
            // }
            // else if(other.name == "To2nd")
            // {
            //     transform.position = SecondWarpTarget.position;
            // }
            // else if(other.name == "To3rd")
            // {
            //     transform.position = ThirdWarpTarget.position;
            // }
            // else if(other.name == "To4th")
            // {
            //     transform.position = FourthWarpTarget.position;
            // }
            // else if(other.name == "To5th")
            // {
            //     transform.position = FifthWarpTarget.position;
            // }
            controller.enabled = true; // プレイヤーのコントローラーを再度有効化
            // ワープポイントに移動する
            controller.enabled = false; // プレイヤーのコントローラーを無効化
            transform.position = warpTarget.position;
            controller.enabled = true; // プレイヤーのコントローラーを再度有効化
            Debug.Log("ワープしました！"); // デバッグ用のログ
        }
    }
}