using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PredictionDemo
{
    // Presentation follows replicated displacement and never writes to the network root.
    public sealed class TankPresentation : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color OpponentArmor = new Color32(214, 104, 61, 255);
        private static readonly Color OpponentTrim = new Color32(255, 175, 81, 255);
        private readonly List<Transform> _wheels = new List<Transform>();
        private Transform _visual;
        private NetworkObject _networkObject;
        private Vector3 _previous;
        private bool _painted;

        private void Awake()
        {
            _visual = transform.Find("Visual");
            _networkObject = GetComponent<NetworkObject>();
            _previous = transform.position;
            foreach (var child in GetComponentsInChildren<Transform>())
                if (child.name.StartsWith("Wheel_", StringComparison.Ordinal)) _wheels.Add(child);
        }

        private void LateUpdate()
        {
            var delta = transform.position - _previous;
            _previous = transform.position;
            foreach (var wheel in _wheels)
                wheel.Rotate(Vector3.up, delta.magnitude * 190f, Space.Self);

            if (_visual == null) return;
            // Root yaw is also the heading of the authoritative shot.
            _visual.rotation = transform.rotation;
            if (_painted || _networkObject == null || !_networkObject.IsSpawned) return;
            _painted = true;
            if (_networkObject.IsOwner) return;

            var tint = new MaterialPropertyBlock();
            foreach (var part in _visual.GetComponentsInChildren<MeshRenderer>())
            {
                if (part.name is "Hull" or "Turret" or "SideSkirt")
                    tint.SetColor(BaseColor, OpponentArmor);
                else if (part.name is "TankMarker" or "ArmorStripe")
                    tint.SetColor(BaseColor, OpponentTrim);
                else
                    continue;
                part.SetPropertyBlock(tint);
            }
        }
    }
}
