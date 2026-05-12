using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private List<PointData> list;
    public GameObject enemy;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        list = new List<PointData>();
        SensorReceiver.Instance.getPointList(ref list);
        Debug.Log(list);
        for(int i = 0;i < list.Count;i++)
        {
            PointData p = list[i];
            float x = p.position.x;
            float y = p.position.y;
            Vector2 v = p.position;
            Debug.Log("x:" + x + " y:" + y);
        }

    }
  public void AddEnemy(Vector3 _pos)
  {
      
    }
}
