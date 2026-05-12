using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointData
{
    public Vector2 position;
    public int count;

    public bool update;

    public PointData(Vector2 _position)
    {
        position = _position;
        update = true;
        count = 0;
    }

    public void average(Vector2 _v)
    {
        position = (position +_v) / 2;
        count++;
        update = true;
    }
}
