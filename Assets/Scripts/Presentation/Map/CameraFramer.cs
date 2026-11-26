using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Presentation.Map
{
    public sealed class CameraFramer : MonoBehaviour
    {
        [SerializeField] private float _padding = 1.2f;
        [SerializeField] private float _zoomSpeed = 1.5f;
        [SerializeField] private float _minOrthoSize = 4f;
        [SerializeField] private float _maxOrthoSize = 22f;

        private Camera _camera;

        public void Frame(Camera targetCamera, IReadOnlyList<ProvinceView> views)
        {
            _camera = targetCamera;
            if (_camera == null || views == null || views.Count == 0) return;

            var bounds = new Bounds(views[0].transform.position, Vector3.zero);
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].TryGetComponent<SpriteRenderer>(out var sr))
                {
                    bounds.Encapsulate(sr.bounds);
                }
                else
                {
                    bounds.Encapsulate(views[i].transform.position);
                }
            }

            Vector3 camPos = _camera.transform.position;
            _camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, camPos.z);

            float halfHeight = bounds.extents.y * _padding;
            float halfWidth = (bounds.extents.x * _padding) / Mathf.Max(0.01f, _camera.aspect);
            _camera.orthographicSize = Mathf.Clamp(Mathf.Max(halfHeight, halfWidth), _minOrthoSize, _maxOrthoSize);
        }

        private void Update()
        {
            if (_camera == null || Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float delta = -Mathf.Sign(scroll) * _zoomSpeed;
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + delta, _minOrthoSize, _maxOrthoSize);
            }
        }
    }
}