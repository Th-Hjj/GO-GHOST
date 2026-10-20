using UnityEngine;

// FUNCTION SUMMARY
// LateUpdate() — moves the camera to match the target's x/y position every frame, after all other movement has happened, while keeping the camera's own z.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );
    }
}