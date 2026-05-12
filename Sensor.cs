using UnityEngine;
using System.Collections.Generic;
using System;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private List<PointData> list;
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
            // if(p.count == 0)
            // {
                
            // }
        }
    }
}
