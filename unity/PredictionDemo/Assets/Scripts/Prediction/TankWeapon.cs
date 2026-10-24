using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PredictionDemo
{
    // The owner sends only a numbered trigger. The server owns aim, cooldown and hits.
    public sealed class TankWeapon : NetworkBehaviour
    {
        private const float ReloadSeconds = .65f;
        private const float Range = 80f;
        private uint _nextShot, _lastServerShot;
        private float _nextLocalFire, _nextServerFire;
        public readonly NetworkVariable<int> ShotsFired = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Hits = new NetworkVariable<int>();

        private void Update()
        {
            if (!IsSpawned || !IsOwner || TankGameHud.DiagnosticsVisible) return;
            if (NetworkLabSettings.AutomationFire) { TryFire(); return; }
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if ((keyboard != null && keyboard.fKey.isPressed) ||
                (mouse != null && mouse.leftButton.isPressed && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())))
                TryFire();
        }

        public void TryFire()
        {
            if (!IsSpawned || !IsOwner || Time.unscaledTime < _nextLocalFire) return;
            _nextLocalFire = Time.unscaledTime + ReloadSeconds;
            var sequence = ++_nextShot;
            var spec = NetworkLabSettings.GetProfile(NetworkLabSettings.ActiveProfile);
            StartCoroutine(SendShot(sequence, spec));
        }

        private IEnumerator SendShot(uint sequence, LabProfileSpec spec)
        {
            if (!IsServer && spec.OneWayDelayMs > 0) yield return new WaitForSecondsRealtime(spec.OneWayDelayMs / 1000f);
            if (!IsSpawned) yield break;
            if (!IsServer && NetworkLabSettings.ShouldDrop(spec.LossPercent, sequence, 0x51A07u))
            {
                Debug.Log($"[PR4][Shoot] DROP C->S shot={sequence} loss={spec.LossPercent}%");
                yield break;
            }
            FireRpc(sequence, spec.OneWayDelayMs, spec.LossPercent);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable, InvokePermission = RpcInvokePermission.Owner)]
        private void FireRpc(uint sequence, int delayMs, int lossPercent)
        {
            if (sequence <= _lastServerShot) return;
            _lastServerShot = sequence;
            if (Time.unscaledTime < _nextServerFire)
            {
                Debug.Log($"[PR4][Shoot] REJECT cooldown owner={OwnerClientId} shot={sequence}");
                return;
            }
            _nextServerFire = Time.unscaledTime + ReloadSeconds;
            var origin = transform.position + Vector3.up * .2f;
            var direction = transform.forward;
            var ray = new Ray(origin, direction);
            float distance = Range;
            var hitName = "range";
            if (Physics.Raycast(ray, out var hit, Range, ~0, QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;
                hitName = hit.collider.name;
            }
            // Player visuals have no physics colliders: test their chassis bounds explicitly.
            foreach (var player in FindObjectsByType<PredictedPlayer>(FindObjectsSortMode.None))
            {
                if (player.gameObject == gameObject || !player.IsSpawned) continue;
                var bounds = new Bounds(player.transform.position, new Vector3(1.9f, 1.5f, 2.2f));
                if (bounds.IntersectRay(ray, out var tankDistance) && tankDistance < distance)
                {
                    distance = tankDistance;
                    hitName = $"tank:{player.OwnerClientId}";
                }
            }
            ShotsFired.Value++;
            if (distance < Range) Hits.Value++;
            var end = origin + direction * distance;
            Debug.Log($"[PR4][Shoot] ACCEPT owner={OwnerClientId} shot={sequence} hit={hitName} shots={ShotsFired.Value} hits={Hits.Value}");
            StartCoroutine(SendEffect(sequence, origin + direction * Mathf.Min(1.8f, distance), end,
                new LabProfileSpec(Mathf.Clamp(delayMs, 0, 500), Mathf.Clamp(lossPercent, 0, 100))));
        }

        private IEnumerator SendEffect(uint sequence, Vector3 muzzle, Vector3 end, LabProfileSpec spec)
        {
            // Host sees its authoritative effect immediately; remote effects use the lab path.
            DrawShot(muzzle, end);
            if (spec.OneWayDelayMs > 0) yield return new WaitForSecondsRealtime(spec.OneWayDelayMs / 1000f);
            if (!IsSpawned) yield break;
            if (NetworkLabSettings.ShouldDrop(spec.LossPercent, sequence, 0x51B07u))
            {
                Debug.Log($"[PR4][Shoot] DROP S->C shot={sequence} loss={spec.LossPercent}%");
                yield break;
            }
            ShotEffectRpc(muzzle, end);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void ShotEffectRpc(Vector3 muzzle, Vector3 end)
        {
            Debug.Log($"[PR4][Shoot] EFFECT owner={OwnerClientId} end={end:F2}");
            DrawShot(muzzle, end);
        }

        private void DrawShot(Vector3 muzzle, Vector3 end)
        {
            var lamp = transform.Find("Visual/Headlight");
            var renderer = lamp != null ? lamp.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            var tracer = new GameObject("Shell tracer").AddComponent<LineRenderer>();
            tracer.sharedMaterial = renderer.sharedMaterial;
            tracer.startWidth = .09f; tracer.endWidth = .035f;
            tracer.positionCount = 2;
            tracer.SetPosition(0, muzzle); tracer.SetPosition(1, end);
            Destroy(tracer.gameObject, .18f);
            foreach (var point in new[] { muzzle, end })
            {
                var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flash.name = "Shot flash";
                Destroy(flash.GetComponent<Collider>());
                flash.transform.position = point;
                flash.transform.localScale = Vector3.one * .3f;
                flash.GetComponent<Renderer>().sharedMaterial = renderer.sharedMaterial;
                Destroy(flash, .20f);
            }
        }
    }
}
