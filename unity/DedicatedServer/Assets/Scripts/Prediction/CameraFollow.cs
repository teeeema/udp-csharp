using UnityEngine;

namespace PredictionDemo
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target { get; set; }

        [SerializeField] private Vector3 offset = new Vector3(8.0f, 12.0f, -12.0f);

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            var desired = Target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1.0f - Mathf.Exp(-8.0f * Time.unscaledDeltaTime));
            transform.LookAt(Target.position + Vector3.up * 0.5f);
        }
    }
}
