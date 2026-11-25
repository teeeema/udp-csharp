using UnityEngine;

namespace PredictionDemo
{
    // Presentation follows replicated displacement; it never writes the network root.
    public sealed class TankPresentation : MonoBehaviour
    {
        private Transform[] _wheels;
        private Transform _visual;
        private Vector3 _previous;

        private void Awake()
        {
            _visual = transform.Find("Visual");
            var wheels = new System.Collections.Generic.List<Transform>();
            foreach (var child in GetComponentsInChildren<Transform>())
                if (child.name.StartsWith("Wheel_")) wheels.Add(child);
            _wheels = wheels.ToArray();
            _previous = transform.position;
        }

        private void LateUpdate()
        {
            var delta = transform.position - _previous;
            _previous = transform.position;
            foreach (var wheel in _wheels) wheel.Rotate(Vector3.up, delta.magnitude * 190, Space.Self);
            // Root yaw is the gameplay heading used by server-authoritative shooting.
            // Displacement can include spawn teleports or reconciliation corrections.
            if (_visual != null) _visual.rotation = transform.rotation;

        }
    }
}
