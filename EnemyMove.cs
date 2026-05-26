using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMove : MonoBehaviour
{
    NavMeshAgent agent; // NavMeshAgentコンポーネントを格納する変数
    public GameObject target; // 追尾対象のオブジェクト

    void Start()
    {
        // NavMeshAgentコンポーネントを取得
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // 追尾対象（target）の位置を目的地に設定
        agent.destination = target.transform.position;
    }
}