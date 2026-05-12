using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Button : MonoBehaviour
{
  [Header("Sensor Button Settings")]
  public Camera targetCamera;
  public float maxRayDistance = 100f;
  public LayerMask hitMask = ~0;
  public int activateFrames = 2;
  public float reTriggerDelay = 0.25f;

  [Header("Events")]
  public UnityEvent onPressed;

  private List<PointData> list = new List<PointData>();
  private Collider buttonCollider;
  private int insideFrameCount;
  private float lastPressTime = -999f;

  void Awake()
  {
    buttonCollider = GetComponent<Collider>();
    if (!targetCamera) targetCamera = Camera.main;
  }

  void Update()
  {
    if (!SensorReceiver.Instance || !buttonCollider)
    {
      insideFrameCount = 0;
      return;
    }

    if (!targetCamera) targetCamera = Camera.main;
    if (!targetCamera)
    {
      insideFrameCount = 0;
      return;
    }

    SensorReceiver.Instance.getPointList(ref list);

    bool touched = false;
    for (int i = 0; i < list.Count; i++)
    {
      if (IsSensorPointOnButton(list[i].position))
      {
        touched = true;
        break;
      }
    }

    if (touched)
    {
      insideFrameCount++;
      if (insideFrameCount >= Mathf.Max(1, activateFrames)
        && Time.time - lastPressTime >= reTriggerDelay)
      {
        lastPressTime = Time.time;
        onPressed?.Invoke();
      }
    }
    else
    {
      insideFrameCount = 0;
    }
  }

  bool IsSensorPointOnButton(Vector2 sensorScreenPos)
  {
    Ray ray = targetCamera.ScreenPointToRay(new Vector3(sensorScreenPos.x, sensorScreenPos.y, 0f));
    if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, hitMask, QueryTriggerInteraction.Collide))
    {
      return hit.collider == buttonCollider || hit.transform.IsChildOf(transform);
    }

    return false;
  }
}
