using UnityEngine;

namespace BuildCrew.Sorting
{
    /// <summary>Turns a world-space canvas to face the main camera (yaw only).</summary>
    public class WorldLabel : MonoBehaviour
    {
        Camera _cam;

        void LateUpdate()
        {
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            if (_cam == null) return;
            Vector3 d = transform.position - _cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
