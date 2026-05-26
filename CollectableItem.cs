using UnityEngine;

public class CollectableItem : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーオブジェクトに特定のタグが付いているか確認
        if (other.CompareTag("Player"))
        {
            // GameManagerにアイテムが収集されたことを通知
            GameManager.Instance.CollectItem();

            // アイテムをシーンから削除
            Destroy(gameObject);
        }
    }
}