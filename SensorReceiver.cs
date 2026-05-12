using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
public class SensorReceiver : MonoBehaviour
{
    // ==== 基本設定 ====
    public const int NumLines = 1081;            // UST-10LX 既定本数
    [Header("Protocol")]
    public int port = 5005;                      // 送信側と合わせる
    public bool littleEndian = true;             // 送信側と合わせる（既定: true）

    // ==== シングルトン（必要なら） ====
    public static SensorReceiver Instance { get; private set; }

    // ==== UDP ====
    private UdpClient udp;
    private Thread thread;
    private volatile bool loop;

    // 受信バッファ（最新値）
    private readonly ushort[] latestRanges = new ushort[NumLines];
    private readonly ushort[] tempRanges = new ushort[NumLines];
    private volatile bool dataReady = false;

    // 参照用コピー（メインスレッドで消費）
    private ushort[] copy;

    // ==== 可視化・検出パラメータ ====
    Color bgColor = Color.white;
    Color lineColor = Color.yellow;
    Color quadColor = Color.red;

    //マスク用フィールド　（あれば）
    [Header("Mask filter")]
    public Texture2D maskTexture;                 // スクリーン全体に対応するマスク画像
    [Range(0f, 1f)] public float maskBlackThreshold = 0.10f; // これ未満の輝度を「黒」と判定
    Color32[] maskPixels; int maskW, maskH;


    public float sensorRotation;    // rad
    public Vector2 sensorPosition;  // px
    public float sensorScale = 1.0f;
    public bool invert = false;

    public float pointThreshold = 20.0f;
    public float objectRadius = 100.0f;
    public float perMeter = 0.035f;
    public int minCount = 3;

    public Camera targetCamera;
    public bool editable;

    private float width;
    private float height;
    private Camera currentCamera;
    static Material lineMaterial;

    private readonly List<PointData> pointObjects = new List<PointData>();
    private readonly Vector3[] pointChain = new Vector3[NumLines];

    // 検出範囲トリミング（スクリーン座標）
    public float trimLeft = 0;
    public float trimRight = 1920;
    public float trimTop = 540;
    public float trimBottom = 0;

    // 内部
    private float deltaRad;
    private string filePath;

    private bool mousePressed;
    private Vector3 lastPos;

    void Awake()
    {
        // シングルトン：既存インスタンスがあれば、新しいものは破棄
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sensorRotation = 0.0f;
        sensorPosition = Vector2.zero;
        sensorScale = Mathf.Max(1e-3f, sensorScale);

        // 270度スキャンを本数で割った角度（ラジアン）
        deltaRad = Mathf.Deg2Rad * (270f / NumLines);

        // 設定ファイル
        filePath = Application.dataPath + this.name + ".txt";
        load();

        BuildMaskCache();

        // UDP受信開始（起動直後から待受）
        loop = true;
        thread = new Thread(ReceiveData) { IsBackground = true, Name = "UST10LX-UDP-Receiver" };
        thread.Start();

        editable = true; //センサーの表示を最初からオンにする
    }

    void Start()
    {
        if (!targetCamera) targetCamera = Camera.main;
        width = targetCamera ? targetCamera.pixelWidth : Screen.width;
        height = targetCamera ? targetCamera.pixelHeight : Screen.height;
        // Debug.Log(width + " " + height);
        GL.Viewport(new Rect(0, 0, width, height));
        CreateLineMaterial();
    }

    void Update()
    {
        if (dataReady)
        {
            List<Vector2> points = new List<Vector2>();

            searchPointByThreshold(ref points);

            // UI/ログ等に使う場合のサンプル（先頭10個）
            // string summary = string.Join(", ", copy.Take(10));
            // Debug.Log(summary);
        }

        if (Input.GetKeyDown(KeyCode.Tab)) editable = !editable;

        if (editable)
        {
            if (Input.GetMouseButtonDown(1))
            {
                mousePressed = true;
                lastPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1))
            {
                mousePressed = false;
                save();
            }

            if (mousePressed)
            {
                Vector3 newPos = Input.mousePosition;
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    sensorRotation += (newPos.x - lastPos.x) / 1000f;
                }
                else if (Input.GetKey(KeyCode.LeftControl))
                {
                    sensorScale = Mathf.Max(1e-3f, sensorScale + (newPos.x - lastPos.x) / 1000f);
                }
                else
                {
                    Vector3 vec = newPos - lastPos;
                    sensorPosition += new Vector2(vec.x, vec.y);
                }
                lastPos = newPos;
            }

            if (Input.GetKeyDown(KeyCode.U)) pointThreshold -= 0.1f;
            else if (Input.GetKeyDown(KeyCode.I)) pointThreshold += 0.1f;
            else if (Input.GetKeyDown(KeyCode.J)) objectRadius -= 0.1f;
            else if (Input.GetKeyDown(KeyCode.K)) objectRadius += 0.1f;
        }
    }

    // ================= UDP受信 =================
    void ReceiveData()
    {
        try
        {
            udp = new UdpClient(port);
            // タイムアウトを設けると終了時に抜けやすい
            udp.Client.ReceiveTimeout = 1000;
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);

            while (loop)
            {
                byte[] data;
                try
                {
                    data = udp.Receive(ref remoteEP); // ブロッキング（タイムアウト付）
                }
                catch (SocketException se)
                {
                    // タイムアウトなど：ループ継続、終了時はCloseで例外→break
                    if (!loop) break;
                    if (se.SocketErrorCode == SocketError.TimedOut) continue;
                    Debug.LogWarning($"UDP受信警告: {se.SocketErrorCode}");
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break; // Close後
                }

                if (data == null || data.Length < 2 || (data.Length & 1) != 0)
                {
                    Debug.LogWarning($"受信サイズ不正: {data?.Length ?? 0} bytes");
                    continue;
                }

                // 2バイトで1点
                int pairs = data.Length >> 1;
                int m = Mathf.Min(pairs, NumLines);

                // まずゼロ埋め
                Array.Clear(tempRanges, 0, NumLines);

                int idx = 0;
                if (littleEndian)
                {
                    for (int i = 0; i < m; i++)
                    {
                        // LE: [lo][hi]
                        ushort v = (ushort)(data[idx] | (data[idx + 1] << 8));
                        tempRanges[i] = v;
                        idx += 2;
                    }
                }
                else
                {
                    for (int i = 0; i < m; i++)
                    {
                        // BE: [hi][lo]
                        ushort v = (ushort)((data[idx] << 8) | data[idx + 1]);
                        tempRanges[i] = v;
                        idx += 2;
                    }
                }

                // 固定1081配列へコピー
                lock (latestRanges)
                {
                    Array.Copy(tempRanges, latestRanges, NumLines);
                    dataReady = true;
                }

                if (pairs != NumLines)
                {
                    // 送信側の本数が想定と違う場合の参考ログ（騒がしいなら消してOK）
                    // Debug.Log($"受信本数: {pairs} (期待 {NumLines})");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("UDP受信初期化エラー: " + e.Message);
        }
    }

    // ================= 検出処理 =================
    // ================= 検出＋クラスタ＋紐付け（統合版） =================
    // ================= 検出＋クラスタ＋紐付け（統合・ローカル関数なし） =================
    private void searchPointByThreshold(ref List<Vector2> points)
    {
        if (!dataReady) return;

        // 最新値コピー（既存踏襲）
        lock (latestRanges)
        {
            if (latestRanges == null || latestRanges.Length == 0) return;
            copy = (ushort[])latestRanges.Clone();   // OnRenderObject 用も更新
        }

        int n = copy.Length;
        if (n == 0) return;

        // ---- パラメタ/定数 ----
        float startRad = sensorRotation - Mathf.Deg2Rad * 45f;
        float step = deltaRad;
        float cosStepAbs = Mathf.Cos(Mathf.Abs(step));
        bool fallbackD = Mathf.Abs(step) < 1e-6f; // 角度差0なら |r-prev| でフォールバック
        const int RMIN = 10, RMAX = 19800;          // 有効距離レンジ（従来互換）
        const int MIN_CLUSTER_POINTS = 3;           // 小片除去（お好みで 4〜6 に）

        // 1) 角度方向メディアン（窓=3、軽量）
        ushort[] filt = new ushort[n];
        for (int i = 0; i < n; i++)
        {
            ushort a = (i > 0) ? copy[i - 1] : copy[i];
            ushort b = copy[i];
            ushort c = (i + 1 < n) ? copy[i + 1] : copy[i];

            int cnt = 0;
            ushort v0 = 0, v1 = 0, v2 = 0;
            if (RMIN < a && a < RMAX) { v0 = a; cnt++; }
            if (RMIN < b && b < RMAX) { if (cnt == 0) v0 = b; else if (cnt == 1) v1 = b; else v2 = b; cnt++; }
            if (RMIN < c && c < RMAX) { if (cnt == 0) v0 = c; else if (cnt == 1) v1 = c; else v2 = c; cnt++; }

            if (cnt == 0) filt[i] = 0;
            else if (cnt == 1) filt[i] = v0;
            else if (cnt == 2) filt[i] = (ushort)((v0 + v1) >> 1);
            else
            {
                if (v0 > v1) { var t = v0; v0 = v1; v1 = t; }
                if (v1 > v2) { var t = v1; v1 = v2; v2 = t; }
                if (v0 > v1) { var t = v0; v0 = v1; v1 = t; }
                filt[i] = v1; // 中央値
            }
        }

        // 2) 幾何クラスタリング（距離依存で緩和）
        float baseEps = Mathf.Max(60f, pointThreshold); // 旧設定を尊重しつつ最低60mm
        float perMeter = 0.035f;                         // 1mごとに +35mm

        points.Clear();

        // 前フレームの生存管理（従来ロジック）
        for (int j = pointObjects.Count - 1; j >= 0; j--)
        {
            if (pointObjects[j].update) pointObjects[j].update = false;
            else pointObjects.RemoveAt(j);
        }

        int s = -1;          // 現クラスタ開始 index
        ushort prev = 0;

        // --- 走査 ---
        for (int i = 0; i < n; i++)
        {
            ushort r = filt[i];
            bool valid = (RMIN < r && r < RMAX);

            // クラスタを閉じる共通処理（インライン展開）
            if (!valid)
            {
                if (s >= 0)
                {
                    int a = s, b = i - 1;
                    int cnt = b - a + 1;
                    if (cnt >= MIN_CLUSTER_POINTS)
                    {
                        // XY(mm) → 中央値ピボット
                        float[] xs = new float[cnt];
                        float[] ys = new float[cnt];
                        for (int t = 0; t < cnt; t++)
                        {
                            int idx = a + t;
                            float rmm = filt[idx];
                            float ang = startRad + idx * step;
                            xs[t] = rmm * Mathf.Cos(ang);
                            ys[t] = rmm * Mathf.Sin(ang);
                        }
                        Array.Sort(xs); Array.Sort(ys);
                        float cx = xs[cnt / 2], cy = ys[cnt / 2];

                        // スクリーン変換
                        float sx = cx * sensorScale * (invert ? -1f : 1f) + sensorPosition.x;
                        float sy = cy * sensorScale + sensorPosition.y;

                        // 画面内 + トリムチェック
                        if (0 <= sx && sx < width && 0 <= sy && sy < height &&
                            trimLeft < sx && sx < trimRight && trimBottom < sy && sy < trimTop)
                        {
                            var pv = new Vector2(sx, sy);
                            points.Add(pv); // 可視用ポイントは常に追加

                            // ===== マスク判定：黒ならトラッキング対象外 =====
                            bool maskedOut = false;
                            if (maskTexture != null)
                            {
                                if (IsMasked(sx, sy)) maskedOut = true;
                            }

                            if (!maskedOut)
                            {
                                // 最近傍マッチ（従来 searchObjectByThreshold 相当）
                                float best = float.MaxValue;
                                PointData bestPd = null;
                                for (int k = 0; k < pointObjects.Count; k++)
                                {
                                    var pd = pointObjects[k];
                                    float d = Vector2.Distance(pd.position, pv);
                                    if (d < best) { best = d; bestPd = pd; }
                                }
                                if (bestPd != null && best < objectRadius) bestPd.average(pv);
                                else pointObjects.Add(new PointData(pv));
                            }
                        }
                    }
                }
                s = -1;
                prev = 0;
                continue;
            }

            if (s < 0)
            {
                s = i; // クラスタ開始
            }
            else if (prev > 0)
            {
                float d = fallbackD
                    ? Mathf.Abs(r - prev)
                    : Mathf.Sqrt(r * (float)r + prev * (float)prev - 2f * r * prev * cosStepAbs);

                float eps = baseEps + perMeter * Mathf.Min(r, prev); // mm
                if (d > eps)
                {
                    // ── 現クラスタを確定（インライン展開） ──
                    int a = s, b = i - 1;
                    int cnt = b - a + 1;
                    if (cnt >= MIN_CLUSTER_POINTS)
                    {
                        float[] xs = new float[cnt];
                        float[] ys = new float[cnt];
                        for (int t = 0; t < cnt; t++)
                        {
                            int idx = a + t;
                            float rmm = filt[idx];
                            float ang = startRad + idx * step;
                            xs[t] = rmm * Mathf.Cos(ang);
                            ys[t] = rmm * Mathf.Sin(ang);
                        }
                        Array.Sort(xs); Array.Sort(ys);
                        float cx = xs[cnt / 2], cy = ys[cnt / 2];

                        float sx = cx * sensorScale * (invert ? -1f : 1f) + sensorPosition.x;
                        float sy = cy * sensorScale + sensorPosition.y;

                        if (0 <= sx && sx < width && 0 <= sy && sy < height &&
                            trimLeft < sx && sx < trimRight && trimBottom < sy && sy < trimTop)
                        {
                            var pv = new Vector2(sx, sy);
                            points.Add(pv);

                            bool maskedOut = false;
                            if (maskTexture != null)
                            {
                                float u = Mathf.Clamp01(sx / Mathf.Max(1f, width));
                                float v = Mathf.Clamp01(sy / Mathf.Max(1f, height));
                                Color mc = maskTexture.GetPixelBilinear(u, v);
                                float lum = mc.r * 0.2126f + mc.g * 0.7152f + mc.b * 0.0722f;
                                if (lum < maskBlackThreshold) maskedOut = true;
                            }

                            if (!maskedOut)
                            {
                                float best = float.MaxValue;
                                PointData bestPd = null;
                                for (int k = 0; k < pointObjects.Count; k++)
                                {
                                    var pd = pointObjects[k];
                                    float dd = Vector2.Distance(pd.position, pv);
                                    if (dd < best) { best = dd; bestPd = pd; }
                                }
                                if (bestPd != null && best < objectRadius) bestPd.average(pv);
                                else pointObjects.Add(new PointData(pv));
                            }
                        }
                    }
                    s = i; // 新クラスタ開始
                }
            }

            prev = r;
        }

        // 3) 最後のクラスタ確定
        if (s >= 0)
        {
            int a = s, b = n - 1;
            int cnt = b - a + 1;
            if (cnt >= MIN_CLUSTER_POINTS)
            {
                float[] xs = new float[cnt];
                float[] ys = new float[cnt];
                for (int t = 0; t < cnt; t++)
                {
                    int idx = a + t;
                    float rmm = filt[idx];
                    float ang = startRad + idx * step;
                    xs[t] = rmm * Mathf.Cos(ang);
                    ys[t] = rmm * Mathf.Sin(ang);
                }
                Array.Sort(xs); Array.Sort(ys);
                float cx = xs[cnt / 2], cy = ys[cnt / 2];

                float sx = cx * sensorScale * (invert ? -1f : 1f) + sensorPosition.x;
                float sy = cy * sensorScale + sensorPosition.y;

                if (0 <= sx && sx < width && 0 <= sy && sy < height &&
                    trimLeft < sx && sx < trimRight && trimBottom < sy && sy < trimTop)
                {
                    var pv = new Vector2(sx, sy);
                    points.Add(pv);

                    bool maskedOut = false;
                    if (maskTexture != null)
                    {
                        float u = Mathf.Clamp01(sx / Mathf.Max(1f, width));
                        float v = Mathf.Clamp01(sy / Mathf.Max(1f, height));
                        Color mc = maskTexture.GetPixelBilinear(u, v);
                        float lum = mc.r * 0.2126f + mc.g * 0.7152f + mc.b * 0.0722f;
                        if (lum < maskBlackThreshold) maskedOut = true;
                    }

                    if (!maskedOut)
                    {
                        float best = float.MaxValue;
                        PointData bestPd = null;
                        for (int k = 0; k < pointObjects.Count; k++)
                        {
                            var pd = pointObjects[k];
                            float d = Vector2.Distance(pd.position, pv);
                            if (d < best) { best = d; bestPd = pd; }
                        }
                        if (bestPd != null && best < objectRadius) bestPd.average(pv);
                        else pointObjects.Add(new PointData(pv));
                    }
                }
            }
        }
    }

    //マスク軽量化
    void BuildMaskCache()
    {
        if (maskTexture == null) return;
        maskW = maskTexture.width; maskH = maskTexture.height;
        maskPixels = maskTexture.GetPixels32();
    }

    // 最近傍サンプル（true=黒っぽい）
    bool IsMasked(float sx, float sy)
    {
        if (maskPixels == null || maskPixels.Length == 0) return false;
        int x = Mathf.Clamp(Mathf.RoundToInt(sx / Mathf.Max(1f, width) * (maskW - 1)), 0, maskW - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(sy / Mathf.Max(1f, height) * (maskH - 1)), 0, maskH - 1);
        var c = maskPixels[y * maskW + x];
        float lum = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        return lum < maskBlackThreshold;
    }


    // ================= レンダリング =================
    void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering; }
    void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering; }
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) 
    { currentCamera = camera; }

    void OnRenderObject()
    {
        if (!editable) return;
        //if (currentCamera != targetCamera) return;
        if (lineMaterial == null) { CreateLineMaterial(); if (lineMaterial == null) return; }
        if (copy == null) return; // まだデータが無い

        lineMaterial.SetPass(0);

        float rad = sensorRotation - Mathf.Deg2Rad * 45f;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, width, 0, height);


        // レーザー線
        // for (int i = 0; i < copy.Length; i++)
        // {
        //     long d = copy[i];

        //     GL.Begin(GL.LINE_STRIP);
        //     GL.Color(lineColor);
        //     GL.Vertex(new Vector3(sensorPosition.x, sensorPosition.y, 0));
        //     GL.Vertex(new Vector3(Mathf.Cos(rad) * d * sensorScale * (invert ? -1f : 1f) + sensorPosition.x,
        //                           Mathf.Sin(rad) * d * sensorScale + sensorPosition.y,
        //                           0));
        //     GL.End();

        //     rad += deltaRad;
        // }
        // 点表示
        for (int j = 0; j < pointObjects.Count; j++)
        {
            Vector2 v = pointObjects[j].position;
            float s = 50f;

            GL.Begin(GL.QUADS);
            GL.Color(quadColor);
            GL.Vertex3(-s / 5 + v.x, -s / 5 + v.y, 0);
            GL.Vertex3(s / 5 + v.x, -s / 5 + v.y, 0);
            GL.Vertex3(s / 5 + v.x, s / 5 + v.y, 0);
            GL.Vertex3(-s / 5 + v.x, s / 5 + v.y, 0);
            GL.End();
        }

        GL.PopMatrix();


    }

    public void getPointList(ref List<PointData> _list)
    {
        _list = new List<PointData>(pointObjects);
    }

    static void CreateLineMaterial()
    {
        if (lineMaterial) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (!shader) { Debug.LogError("Hidden/Internal-Colored が見つかりません"); return; }

        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;

        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    // ================= 設定保存 =================
    public void save()
    {
        var data = new List<string>
        {
            sensorPosition.x.ToString(),
            sensorPosition.y.ToString(),
            sensorRotation.ToString(),
            sensorScale.ToString(),
            pointThreshold.ToString(),
            objectRadius.ToString(),
            perMeter.ToString(),
            minCount.ToString()
        };
        File.WriteAllLines(filePath, data);
    }

    public void load()
    {
        if (!File.Exists(filePath)) return;
        var data = File.ReadAllLines(filePath);
        if (data.Length >= 6)
        {
            sensorPosition = new Vector2(float.Parse(data[0]), float.Parse(data[1]));
            sensorRotation = float.Parse(data[2]);
            sensorScale = Mathf.Max(1e-3f, float.Parse(data[3]));
            pointThreshold = float.Parse(data[4]);
            objectRadius = float.Parse(data[5]);
            perMeter = float.Parse(data[6]);
            minCount = int.Parse(data[7]);
        }
    }

    void StopReceiver()
    {
        loop = false;
        try { udp?.Close(); } catch { }
        udp = null;

        if (thread != null)
        {
            try { thread.Join(500); } catch { }
            thread = null;
        }
    }

    // ================= 終了処理 =================
    void OnApplicationQuit()
    {
        StopReceiver();
    }

    void OnDestroy()
    {
        StopReceiver();
    }
}
// DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.
// UnityEngine.Object:DontDestroyOnLoad(UnityEngine.Object)
// SensorReceiver: Awake()(at Assets / script / SensorReceiver.cs:88)
