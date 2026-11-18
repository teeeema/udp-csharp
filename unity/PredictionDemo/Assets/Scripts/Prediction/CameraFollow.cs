using UnityEngine;

namespace PredictionDemo
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target { get; set; }

        [SerializeField] private Vector3 offset = new Vector3(8f, 12f, -12f);
        [SerializeField] private GameObject showcase;
        [SerializeField] private float playSize = 10f;
        private Camera _camera;
        private Vector3 _menuPosition;
        private Quaternion _menuRotation;
        private float _menuSize;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _menuPosition = transform.position;
            _menuRotation = transform.rotation;
            _menuSize = _camera.orthographicSize;
        }

        private void LateUpdate()
        {
            bool showingMenu = Target == null;
            if (showcase != null && showcase.activeSelf != showingMenu) showcase.SetActive(showingMenu);

            if (showingMenu)
            {
                if (_camera.orthographic)
                {
                    transform.SetPositionAndRotation(_menuPosition, _menuRotation);
                    _camera.orthographicSize = _menuSize;
                }
                return;
            }

            float blend = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, Target.position + offset, blend);
            if (!_camera.orthographic)
            {
                transform.LookAt(Target.position + Vector3.up * .5f);
                return;
            }
            transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, playSize, blend);
        }
    }
}
