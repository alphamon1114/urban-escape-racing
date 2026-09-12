using UnityEngine;

namespace UrbanEscape
{
    public sealed class ChaseCamera : MonoBehaviour
    {
        public SedanController target;
        Vector3 velocity;
        Camera lens;
        void Awake() { lens = GetComponent<Camera>(); }
        void LateUpdate()
        {
            if (!target) return;
            var heading = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (heading.sqrMagnitude < 0.1f) heading = Vector3.forward;
            float speed = Mathf.Clamp01(target.SpeedKph / 170f);
            var desired = target.transform.position - heading * Mathf.Lerp(7.5f, 9.5f, speed) + Vector3.up * 3.6f;
            if (Vector3.Distance(transform.position, desired) > 30f) { transform.position = desired; velocity = Vector3.zero; }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.16f);
            transform.LookAt(target.transform.position + Vector3.up * 0.8f + heading * 3f);
            lens.fieldOfView = Mathf.Lerp(lens.fieldOfView, Mathf.Lerp(60f, 73f, speed), 1f - Mathf.Exp(-3f * Time.deltaTime));
        }
    }
}
