using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemSpawner3 : MonoBehaviour
{
    public GameObject[] spawnItems;
    public Vector3[] spawnPointList;

    void Start()
    {
        AddItems();
    }

    public void AddItems()
    {
        List<GameObject> items = new List<GameObject>(spawnItems);
        List<Vector3> allPoints = new List<Vector3>(spawnPointList);
        HashSet<Vector3> usedPoints = new HashSet<Vector3>();

        // 1. フロアグループ化（Y軸±3以内）
        List<List<Vector3>> floorGroups = new List<List<Vector3>>();
        foreach (Vector3 point in allPoints)
        {
            bool added = false;
            foreach (var group in floorGroups)
            {
                if (Mathf.Abs(group[0].y - point.y) <= 3f)
                {
                    group.Add(point);
                    added = true;
                    break;
                }
            }
            if (!added)
            {
                floorGroups.Add(new List<Vector3> { point });
            }
        }

        // 2. 各フロアから1地点ずつ選定してアイテムを配置
        foreach (var group in floorGroups)
        {
            if (items.Count == 0) break;

            int r = Random.Range(0, group.Count);
            Vector3 chosenPoint = group[r];

            int itemIndex = Random.Range(0, items.Count);
            GameObject item = items[itemIndex];

            Instantiate(item, chosenPoint, Quaternion.identity);
            usedPoints.Add(chosenPoint);
            items.RemoveAt(itemIndex);
        }

        // 3. 残りのアイテムを未使用の座標からランダムに配置
        List<Vector3> remainingPoints = allPoints.Where(p => !usedPoints.Contains(p)).ToList();
        while (items.Count > 0 && remainingPoints.Count > 0)
        {
            int r = Random.Range(0, remainingPoints.Count);
            Vector3 point = remainingPoints[r];

            int itemIndex = Random.Range(0, items.Count);
            GameObject item = items[itemIndex];

            Instantiate(item, point, Quaternion.identity);
            usedPoints.Add(point);
            items.RemoveAt(itemIndex);
            remainingPoints.RemoveAt(r);
        }
    }
}