using System.Collections.Generic;
using UnityEngine;

namespace Presentation.Map
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public class ProvinceView : MonoBehaviour
    {
        private const float SelectedScale = 1.12f;
        private const float TargetScale = 1.06f;
        private const float TargetLighten = 0.35f;

        [SerializeField] private int _provinceId;
        [SerializeField] private string _displayName;
        [SerializeField] private int _startingOwnerId;
        [SerializeField] private int _startingUnits;
        [SerializeField] private bool _hasFort;
        [SerializeField] private int[] _neighbours;
        [SerializeField] private SpriteRenderer _outline;

        private SpriteRenderer _renderer;
        private Vector3 _baseScale;

        public int ProvinceId => _provinceId;
        public string DisplayName => _displayName;
        public int StartingOwnerId => _startingOwnerId;
        public int StartingUnits => _startingUnits;
        public bool HasFort => _hasFort;
        public IReadOnlyList<int> Neighbours => _neighbours;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        public void Render(Color ownerColor, bool isSelected, bool isMoveTarget)
        {
            _renderer.color = isMoveTarget ? Color.Lerp(ownerColor, Color.white, TargetLighten) : ownerColor;

            float scale = isSelected ? SelectedScale : isMoveTarget ? TargetScale : 1f;
            transform.localScale = _baseScale * scale;
            _outline.enabled = isSelected || isMoveTarget;
        }
    }
}
