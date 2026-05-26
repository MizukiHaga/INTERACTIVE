using UnityEngine;

public class JumpToLocation : MonoBehaviour
{
    public Transform warpTarget; // ワープ先のターゲット位置
    public float moveSpeed = 5f; // プレイヤーの移動速度
    

    void Update()
    {
        // キーボード入力でSphereを移動させる
        float moveX = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime;

        transform.Translate(new Vector3(moveX, 0, moveZ));
    }

    void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponent<CharacterController>();
        if (other.gameObject.CompareTag("WarpTrigger"))
        {
            // ワープポイントに移動する
            controller.enabled = false; // プレイヤーのコントローラーを無効化
            transform.position = warpTarget.position;
            controller.enabled = true; // プレイヤーのコントローラーを再度有効化
            Debug.Log("ワープしました！"); // デバッグ用のログ
        }
    }
}