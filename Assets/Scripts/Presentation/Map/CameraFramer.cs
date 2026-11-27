using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Presentation.Map
{
    public sealed class CameraFramer : MonoBehaviour
    {
        [SerializeField] private float _padding = 1.2f;
        [SerializeField] private float _zoomSpeed = 1.5f;
        [SerializeField] private float _panSpeed = 1.2f;
        [SerializeField] private float _minOrthoSize = 4f;
        [SerializeField] private float _maxOrthoSize = 22f;

        private Camera _camera;
        private Vector2 _minCenter;
        private Vector2 _maxCenter;
        private bool _hasBounds;

        public void Frame(Camera targetCamera, IReadOnlyList<ProvinceView> views)
        {
            _camera = targetCamera;
            if (_camera == null || views == null || views.Count == 0) return;

            var spriteBounds = new Bounds(views[0].transform.position, Vector3.zero);
            Vector2 minPos = views[0].transform.position;
            Vector2 maxPos = minPos;

            for (int i = 0; i < views.Count; i++)
            {
                Vector3 pos = views[i].transform.position;
                minPos = Vector2.Min(minPos, pos);
                maxPos = Vector2.Max(maxPos, pos);

                if (views[i].TryGetComponent<SpriteRenderer>(out var sr))
                {
                    spriteBounds.Encapsulate(sr.bounds);
                }
                else
                {
                    spriteBounds.Encapsulate(pos);
                }
            }

            _minCenter = minPos;
            _maxCenter = maxPos;
            _hasBounds = true;

            Vector3 camPos = _camera.transform.position;
            _camera.transform.position = new Vector3(spriteBounds.center.x, spriteBounds.center.y, camPos.z);

            float halfHeight = spriteBounds.extents.y * _padding;
            float halfWidth = (spriteBounds.extents.x * _padding) / Mathf.Max(0.01f, _camera.aspect);
            _camera.orthographicSize = Mathf.Clamp(Mathf.Max(halfHeight, halfWidth), _minOrthoSize, _maxOrthoSize);

            ClampCameraPosition();
        }

        private void Update()
        {
            if (_camera == null) return;

            HandlePan();
            HandleZoomAtCursor();
        }

        private void HandlePan()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 dir = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed) dir.x -= 1f;
            if (keyboard.rightArrowKey.isPressed) dir.x += 1f;
            if (keyboard.downArrowKey.isPressed) dir.y -= 1f;
            if (keyboard.upArrowKey.isPressed) dir.y += 1f;

            if (dir.sqrMagnitude <= 0.001f) return;

            // Scale pan velocity by orthographicSize so panning feels consistent when zoomed in or out
            float step = _panSpeed * _camera.orthographicSize * Time.deltaTime;
            Vector3 pos = _camera.transform.position;
            pos.x += dir.normalized.x * step;
            pos.y += dir.normalized.y * step;
            _camera.transform.position = pos;

            ClampCameraPosition();
        }

        private void HandleZoomAtCursor()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.01f) return;

            float prevSize = _camera.orthographicSize;
            float delta = -Mathf.Sign(scroll) * _zoomSpeed;
            float nextSize = Mathf.Clamp(prevSize + delta, _minOrthoSize, _maxOrthoSize);
            if (Mathf.Approximately(prevSize, nextSize)) return;

            // Anchor the world point under the cursor before and after changing orthographicSize
            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 worldBefore = _camera.ScreenToWorldPoint(screenPos);

            _camera.orthographicSize = nextSize;

            Vector3 worldAfter = _camera.ScreenToWorldPoint(screenPos);
            Vector3 offset = worldBefore - worldAfter;
            offset.z = 0f;

            _camera.transform.position += offset;
            ClampCameraPosition();
        }

        private void ClampCameraPosition()
        {
            if (!_hasBounds) return;

            Vector3 pos = _camera.transform.position;
            pos.x = Mathf.Clamp(pos.x, _minCenter.x, _maxCenter.x);
            pos.y = Mathf.Clamp(pos.y, _minCenter.y, _maxCenter.y);
            _camera.transform.position = pos;
        }
    }
}