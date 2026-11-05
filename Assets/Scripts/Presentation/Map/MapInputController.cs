using Presentation.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Presentation.Map
{
    /// <summary>
    /// Forwards mouse and keyboard input to the HUD interaction state.
    /// </summary>
    public class MapInputController : MonoBehaviour
    {
        private HudInteraction _interaction;
        private HudController _hud;
        private Camera _camera;

        public void Initialize(HudInteraction interaction, HudController hud, Camera mapCamera)
        {
            _interaction = interaction;
            _hud = hud;
            _camera = mapCamera;
        }

        private void Update()
        {
            if (_interaction == null) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                HandleClick(mouse.position.ReadValue());
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _hud.ConfirmOrEndTurn();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _interaction.CancelTargeting();
            }
        }

        private void HandleClick(Vector2 screenPosition)
        {
            if (_hud.IsPointerOverUi(screenPosition)) return;

            Vector2 worldPosition = _camera.ScreenToWorldPoint(screenPosition);
            var hit = Physics2D.OverlapPoint(worldPosition);
            if (hit != null && hit.TryGetComponent(out ProvinceView province))
            {
                _interaction.ClickProvince(province.ProvinceId);
                return;
            }

            _interaction.ClickEmptySpace();
        }
    }
}
