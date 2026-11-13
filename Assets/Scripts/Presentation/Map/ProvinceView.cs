using System.Collections.Generic;
using Presentation.Prototype;
using TMPro;
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
        [SerializeField] private TextMeshPro _nameLabel;
        [SerializeField] private SpriteRenderer _ownerBanner;
        [SerializeField] private TextMeshPro _unitCount;
        [SerializeField] private SpriteRenderer _fortBadge;

        private SpriteRenderer _renderer;
        private Vector3 _baseScale;
        private Color _ownerColor;
        private bool _isMoveTarget;

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

        public void ShowState(ProvinceState province, Color ownerColor, bool hasOwner)
        {
            _ownerColor = ownerColor;
            _nameLabel.text = province.Name;
            _unitCount.text = province.Units.Count.ToString();
            _fortBadge.enabled = province.HasFort;
            _ownerBanner.enabled = hasOwner;
            _ownerBanner.color = ownerColor;
            ApplyColor();
        }

        public void ShowHighlight(bool isSelected, bool isMoveTarget)
        {
            _isMoveTarget = isMoveTarget;
            float scale = isSelected ? SelectedScale : isMoveTarget ? TargetScale : 1f;
            transform.localScale = _baseScale * scale;
            _outline.enabled = isSelected || isMoveTarget;
            ApplyColor();
        }

        private void ApplyColor()
        {
            _renderer.color = _isMoveTarget ? Color.Lerp(_ownerColor, Color.white, TargetLighten) : _ownerColor;
        }
    }
}
