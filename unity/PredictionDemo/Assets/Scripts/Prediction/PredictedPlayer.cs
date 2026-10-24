using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PredictionDemo
{
    public sealed class PredictedPlayer : NetworkBehaviour
    {
        private const uint ClientToServerSalt = 0xA17E51u;
        private const uint ServerToClientSalt = 0x5E2A11u;

        private readonly List<PredictionInput> _pending = new List<PredictionInput>(128);

        private uint _nextSequence = 1;
        private uint _lastSentSequence;
        private uint _serverLastSequence;
        private uint _remoteLastSequence;
        private Vector3 _remoteTarget;
        private float _serverNextDashTime;
        private float _localNextDashTime;
        private float _lastCorrectionMeters;
        private float _maxCorrectionMeters;
        private uint _lastAckSequence;
        private int _correctionCount;
        private float _lastDashReactionMs = -1.0f;
        private uint _trackedDashSequence;
        private float _trackedDashStart;
        private float _automationSpawnTime;
        private bool _automationDashSent;
        private bool _automationSecondLegalDashSent;
        private bool _automationRejectedDashSent;

        public override void OnNetworkSpawn()
        {
            _remoteTarget = transform.position;

            if (IsOwner)
            {
                _automationSpawnTime = Time.realtimeSinceStartup;
                var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
                if (follow != null)
                {
                    follow.Target = transform;
                }

                SetVisualColor(new Color(0.19f, 0.37f, 0.31f)); // Green Tank (Client / Local)
            }
            else
            {
                SetVisualColor(new Color(0.58f, 0.28f, 0.14f)); // Desert / Orange Tank (Remote)
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Vector2 move;
            bool normalDash;
            bool cheatDash;

            if (NetworkLabSettings.AutomationEnabled && !IsServer)
            {
                move = Vector2.right;
                var elapsed = Time.realtimeSinceStartup - _automationSpawnTime;
                // Trigger the intentional divergence earlier so the client-only 2x dash
                // stays clear of the right wall and produces an observable reconciliation.
                var dashAt = NetworkLabSettings.AutomationDivergence ? 0.08f : 0.8f;
                var triggerFirstDash = !_automationDashSent && elapsed >= dashAt;
                var triggerRejectedDash = NetworkLabSettings.AutomationDivergence
                    && _automationDashSent
                    && !_automationRejectedDashSent
                    && elapsed >= dashAt + 0.20f;
                var triggerSecondLegalDash = !NetworkLabSettings.AutomationDivergence
                    && NetworkLabSettings.ActiveProfile == LabProfile.Lag175Loss5
                    && _automationDashSent
                    && !_automationSecondLegalDashSent
                    && elapsed >= dashAt + 1.60f;
                normalDash = (triggerFirstDash && !NetworkLabSettings.AutomationDivergence) || triggerSecondLegalDash;
                cheatDash = (triggerFirstDash && NetworkLabSettings.AutomationDivergence) || triggerRejectedDash;
                if (triggerFirstDash)
                {
                    _automationDashSent = true;
                }
                if (triggerSecondLegalDash)
                {
                    _automationSecondLegalDashSent = true;
                    Debug.Log("[PR4][Evidence] T4 second legal dash requested after cooldown");
                }
                if (triggerRejectedDash)
                {
                    _automationRejectedDashSent = true;
                    Debug.Log("[PR4][Evidence] T5 invalid second dash intentionally predicted during server cooldown");
                }
            }
            else
            {
                var keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return;
                }

                move = Vector2.zero;
                if (keyboard.aKey.isPressed) move.x -= 1.0f;
                if (keyboard.dKey.isPressed) move.x += 1.0f;
                if (keyboard.sKey.isPressed) move.y -= 1.0f;
                if (keyboard.wKey.isPressed) move.y += 1.0f;
                move = PredictionRules.SanitizeMove(move);
                normalDash = keyboard.spaceKey.wasPressedThisFrame;
                cheatDash = keyboard.bKey.wasPressedThisFrame;
            }
            var dashRequested = normalDash || cheatDash;
            var localDashMultiplier = cheatDash ? 2.0f : 1.0f;

            if (move.sqrMagnitude < 0.0001f && !dashRequested)
            {
                return;
            }

            var input = new PredictionInput(_nextSequence++, move, dashRequested, localDashMultiplier);
            var profile = NetworkLabSettings.ActiveProfile;

            if (dashRequested)
            {
                _trackedDashSequence = input.Sequence;
                _trackedDashStart = Time.realtimeSinceStartup;
                Debug.Log($"[PR4][Evidence] dash-input seq={input.Sequence} prediction={NetworkLabSettings.PredictionEnabled} divergence={cheatDash} profile={NetworkLabSettings.Describe(profile)}");
            }

            // Host player already lives in the authoritative server process; applying local
            // prediction first would move the same Transform twice.
            if (IsServer)
            {
                ProcessServerInput(input.Sequence, input.Move, input.DashRequested, profile);
                return;
            }

            _pending.Add(input);
            if (_pending.Count > 256)
            {
                _pending.RemoveAt(0);
            }

            if (NetworkLabSettings.PredictionEnabled)
            {
                ApplyPredictedInput(input, true);
                if (dashRequested)
                {
                    _lastDashReactionMs = 0.0f;
                    Debug.Log($"[PR4][Evidence] dash-local-reaction-ms={_lastDashReactionMs:F1} seq={input.Sequence} prediction=true");
                }
            }

            StartCoroutine(SendAfterNetworkDelay(input, profile));
        }

        private void Update()
        {
            if (!IsSpawned || IsOwner || IsServer)
            {
                return;
            }

            transform.position = Vector3.Lerp(transform.position, _remoteTarget, 1.0f - Mathf.Exp(-14.0f * Time.deltaTime));
            var delta = _remoteTarget - transform.position;
            if (delta.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1.0f - Mathf.Exp(-10.0f * Time.deltaTime));
            }
        }

        private IEnumerator SendAfterNetworkDelay(PredictionInput input, LabProfile profile)
        {
            var spec = NetworkLabSettings.GetProfile(profile);
            if (spec.OneWayDelayMs > 0)
            {
                yield return new WaitForSecondsRealtime(spec.OneWayDelayMs / 1000.0f);
            }

            // Independent delay coroutines can resume in a different order on adjacent
            // frames. Preserve the generated input sequence before applying logical loss
            // or invoking NGO so a later movement intent cannot overtake a dash intent.
            yield return new WaitUntil(() => input.Sequence == _lastSentSequence + 1);
            _lastSentSequence = input.Sequence;

            if (NetworkLabSettings.ShouldDrop(spec.LossPercent, input.Sequence, ClientToServerSalt))
            {
                Debug.Log($"[PR4][NetSim] DROP C->S seq={input.Sequence} profile={profile}");
                yield break;
            }

            SubmitInputRpc(input.Sequence, input.Move, input.DashRequested, (byte)profile, spec.OneWayDelayMs, spec.LossPercent);
        }

        // Keep client intents ordered at the NGO transport layer. The lab's packet-loss
        // profile is applied explicitly in SendAfterNetworkDelay, so a 0% profile must
        // never lose a dash only because a later movement RPC overtook it in transit.
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(uint sequence, Vector2 move, bool dashRequested, byte profileId, int delayMs, int lossPercent)
        {
            var profile = (LabProfile)Mathf.Clamp(profileId, 0, 3);
            ProcessServerInput(sequence, move, dashRequested, profile, delayMs, lossPercent);
        }

        private void ProcessServerInput(uint sequence, Vector2 move, bool dashRequested, LabProfile profile, int delayMs = -1, int lossPercent = -1)
        {
            if (!IsServer || sequence <= _serverLastSequence)
            {
                return;
            }

            _serverLastSequence = sequence;
            move = PredictionRules.SanitizeMove(move);

            var now = Time.unscaledTime;
            var dashAccepted = dashRequested && PredictionRules.CanDash(now, _serverNextDashTime, move);
            if (dashAccepted)
            {
                _serverNextDashTime = now + PredictionRules.DashCooldownSeconds;
            }

            var displacement = PredictionRules.ComputeDisplacement(move, dashAccepted, 1.0f);
            transform.position = ResolveCollision(transform.position, displacement);
            if (move.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(new Vector3(move.x, 0f, move.y));
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1.0f - Mathf.Exp(-12.0f * Time.deltaTime));
            }

            var cooldownRemaining = Mathf.Max(0.0f, _serverNextDashTime - now);
            if (dashRequested)
            {
                var validation = dashAccepted
                    ? "accepted"
                    : now < _serverNextDashTime ? "rejected:cooldown" : "rejected:no-direction";
                Debug.Log($"[PR4][Server] seq={sequence} dash={(dashAccepted ? "ACCEPT" : "REJECT")} validation={validation} cooldown={cooldownRemaining:F3}s pos={transform.position:F3}");
            }

            var spec = delayMs < 0 ? NetworkLabSettings.GetProfile(profile) : new LabProfileSpec(Mathf.Clamp(delayMs, 0, 500), Mathf.Clamp(lossPercent, 0, 100));
            StartCoroutine(SendStateAfterNetworkDelay(sequence, transform.position, dashAccepted, cooldownRemaining, spec));
        }

        private IEnumerator SendStateAfterNetworkDelay(uint sequence, Vector3 authoritativePosition, bool dashAccepted, float cooldownRemaining, LabProfileSpec spec)
        {
            if (spec.OneWayDelayMs > 0)
            {
                yield return new WaitForSecondsRealtime(spec.OneWayDelayMs / 1000.0f);
            }

            if (NetworkLabSettings.ShouldDrop(spec.LossPercent, sequence, ServerToClientSalt))
            {
                Debug.Log($"[PR4][NetSim] DROP S->C seq={sequence} loss={spec.LossPercent}%");
                yield break;
            }

            BroadcastStateRpc(sequence, authoritativePosition, dashAccepted, cooldownRemaining);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void BroadcastStateRpc(uint sequence, Vector3 authoritativePosition, bool dashAccepted, float cooldownRemaining)
        {
            if (IsOwner)
            {
                ReconcileOwner(sequence, authoritativePosition, dashAccepted, cooldownRemaining);
            }
            else if (sequence > _remoteLastSequence)
            {
                _remoteLastSequence = sequence;
                _remoteTarget = authoritativePosition;
            }
        }

        private void ReconcileOwner(uint sequence, Vector3 authoritativePosition, bool dashAccepted, float cooldownRemaining)
        {
            if (sequence <= _lastAckSequence)
            {
                return;
            }

            _lastAckSequence = sequence;
            _localNextDashTime = Time.unscaledTime + cooldownRemaining;

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Sequence <= sequence)
                {
                    _pending.RemoveAt(i);
                }
            }

            var before = transform.position;
            transform.position = authoritativePosition;

            if (NetworkLabSettings.PredictionEnabled)
            {
                foreach (var pending in _pending)
                {
                    ApplyPredictedInput(pending, false);
                }
            }

            _lastCorrectionMeters = Vector3.Distance(before, transform.position);
            _maxCorrectionMeters = Mathf.Max(_maxCorrectionMeters, _lastCorrectionMeters);
            if (_lastCorrectionMeters > 0.02f)
            {
                _correctionCount++;
                Debug.Log($"[PR4][Reconcile] ack={sequence} error={_lastCorrectionMeters:F3}m pending={_pending.Count} authoritative={authoritativePosition:F3}");
            }

            if (!NetworkLabSettings.PredictionEnabled && _trackedDashSequence != 0 && sequence >= _trackedDashSequence && dashAccepted)
            {
                _lastDashReactionMs = (Time.realtimeSinceStartup - _trackedDashStart) * 1000.0f;
                Debug.Log($"[PR4][Evidence] dash-authoritative-reaction-ms={_lastDashReactionMs:F1} ack={sequence} prediction=false");
                _trackedDashSequence = 0;
            }
            else if (NetworkLabSettings.PredictionEnabled && _trackedDashSequence != 0 && sequence >= _trackedDashSequence)
            {
                Debug.Log($"[PR4][Evidence] dash-authoritative-ack={sequence} local-reaction-ms={_lastDashReactionMs:F1} prediction=true");
                _trackedDashSequence = 0;
            }
        }

        private void ApplyPredictedInput(PredictionInput input, bool updateCooldown)
        {
            var dash = input.DashRequested;
            if (dash && input.LocalDashMultiplier <= 1.0f && !PredictionRules.CanDash(Time.unscaledTime, _localNextDashTime, input.Move))
            {
                dash = false;
            }

            if (dash && updateCooldown)
            {
                _localNextDashTime = Time.unscaledTime + PredictionRules.DashCooldownSeconds;
            }

            var displacement = PredictionRules.ComputeDisplacement(input.Move, dash, input.LocalDashMultiplier);
            transform.position = ResolveCollision(transform.position, displacement);
            if (input.Move.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(new Vector3(input.Move.x, 0f, input.Move.y));
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1.0f - Mathf.Exp(-12.0f * Time.deltaTime));
            }
        }

        private static Vector3 ResolveCollision(Vector3 position, Vector3 displacement)
        {
            var distance = displacement.magnitude;
            if (distance <= 0.0001f)
            {
                return position;
            }

            var direction = displacement / distance;
            // Sweep the chassis volume so low cover and side contacts block both
            // prediction and the authoritative simulation, including a whole dash.
            if (Physics.SphereCast(position, PredictionRules.CollisionRadius, direction, out var hit, distance + PredictionRules.ObstacleSkin, ~0, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0.0f, hit.distance - PredictionRules.ObstacleSkin);
            }

            var result = position + direction * distance;
            result.y = 1.0f;
            return result;
        }

        private void SetVisualColor(Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.name == "Player")
                    renderer.SetPropertyBlock(block);
            }
        }

        public float DashCooldownRemaining => Mathf.Clamp((IsServer ? _serverNextDashTime : _localNextDashTime) - Time.unscaledTime, 0, PredictionRules.DashCooldownSeconds);

        private void OnGUI()
        {
            if (!TankGameHud.DiagnosticsVisible) return;
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            var previousGuiMatrix = PredictionHudLayout.BeginScaledGui();
            try
            {
                GUILayout.BeginArea(new Rect(16, 365, 470, 145), GUI.skin.box);
                GUILayout.Label($"Local player: client {OwnerClientId}");
                GUILayout.Label($"Last ACK: {_lastAckSequence} | pending inputs: {_pending.Count}");
                GUILayout.Label($"Last reconciliation error: {_lastCorrectionMeters:F3} m | corrections: {_correctionCount}");
                GUILayout.Label($"Max reconciliation error: {_maxCorrectionMeters:F3} m");
                GUILayout.Label(_lastDashReactionMs < 0 ? "Last dash reaction: n/a" : $"Last dash reaction: {_lastDashReactionMs:F1} ms");
                GUILayout.Label($"Dash cooldown: {Mathf.Max(0.0f, _localNextDashTime - Time.unscaledTime):F2} s");
                GUILayout.EndArea();
            }
            finally
            {
                PredictionHudLayout.EndScaledGui(previousGuiMatrix);
            }
        }
    }
}
