using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarpPoint : MonoBehaviour
{
    [Header("Warp Settings")]
    public Vector3 pos;

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponentInParent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            controller.transform.position = pos;
            controller.enabled = true;
        }
        else if (other.attachedRigidbody != null)
        {
            other.attachedRigidbody.position = pos;
            other.attachedRigidbody.velocity = Vector3.zero;
            other.attachedRigidbody.angularVelocity = Vector3.zero;
        }
        else
        {
            other.transform.root.position = pos;
        }

        Debug.Log("Warp 発火");
    }
}
