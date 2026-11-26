using Core.Board;
using Core.Setup;
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

        // The terrain keeps its own colours and only leans towards the owner, whose colour shows on the rim.
        private const float TerrainTint = 0.18f;

        // The banner sits on the owner's own hexagon, so it needs a darker shade to stay visible.
        private const float BannerDarken = 0.45f;

        private static readonly Color IdleOutlineColor = new(0.12f, 0.15f, 0.18f, 0.65f);
        private static readonly Color SelectedOutlineColor = new(1f, 0.92f, 0.45f, 1f);
        private static readonly Color TargetOutlineColor = new(0.45f, 0.95f, 1f, 1f);

        private static readonly Color ChurchTint = new(1f, 0.88f, 0.35f, 1f);
        private static readonly Color PresenceTint = new(0.45f, 0.85f, 1f, 1f);
        private static readonly Color UndercoverCellTint = new(0.25f, 0.75f, 0.35f, 0.55f);
        private static readonly Color OpenRebellionTint = new(0.95f, 0.25f, 0.2f, 1f);
        private static readonly Color TradingPostTint = new(0.35f, 0.72f, 0.95f, 1f);

        [SerializeField] private int _provinceId;
        [SerializeField] private string _displayName;
        [SerializeField] private int _startingOwnerId;
        [SerializeField] private int _startingUnits;
        [SerializeField] private int _startingMissionaries;
        [SerializeField] private bool _hasStartingChurch;
        [SerializeField] private int _startingRebellionCellOwnerId = ProvinceSeed.NoRebellionCell;
        [SerializeField] private bool _hasFort;
        [SerializeField] private int[] _neighbours;
        [SerializeField] private SpriteRenderer _outline;
        [SerializeField] private SpriteRenderer _terrain;
        [SerializeField] private TextMeshPro _nameLabel;
        [SerializeField] private SpriteRenderer _ownerBanner;
        [SerializeField] private TextMeshPro _unitCount;
        [SerializeField] private SpriteRenderer _fortBadge;
        [SerializeField] private SpriteRenderer _churchBadge;
        [SerializeField] private SpriteRenderer _presenceBadge;
        [SerializeField] private SpriteRenderer _rebellionBadge;
        [SerializeField] private int _startingTraders;
        [SerializeField] private int _tradingPostOwnerId = ProvinceState.NoTradingPost;
        [SerializeField] private SpriteRenderer _tradingPostBadge;
        [SerializeField] private bool _hasStartingPresence;
        [SerializeField] private int _startingRebellionUnits;

        private SpriteRenderer _renderer;
        private Vector3 _baseScale;
        private Color _ownerColor;
        private bool _isMoveTarget;

        public int ProvinceId => _provinceId;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        public void ConfigureFromSeed(ProvinceSeed seed)
        {
            _provinceId = seed.Id;
            _displayName = string.Empty;
            _startingOwnerId = seed.OwnerId;
            _startingUnits = seed.RegularUnits;
            _startingMissionaries = seed.Missionaries;
            _hasStartingChurch = seed.HasChurch;
            _hasStartingPresence = seed.HasPresenceToken;
            _startingRebellionCellOwnerId = seed.RebellionCellOwnerId;
            _startingRebellionUnits = seed.RebellionUnits;
            _hasFort = seed.HasFort;
            _startingTraders = seed.Traders;
            _tradingPostOwnerId = seed.TradingPostOwnerId;

            var neighbours = new int[seed.Neighbours.Count];
            for (int i = 0; i < seed.Neighbours.Count; i++) neighbours[i] = seed.Neighbours[i];
            _neighbours = neighbours;
        }

        public void SetTerrain(Sprite terrain)
        {
            if (_terrain == null) return;

            _terrain.sprite = terrain;
            _terrain.enabled = terrain != null;
        }

        public ProvinceSeed ToSeed() =>
            new(_provinceId, string.Empty, _hasFort, _neighbours, _startingOwnerId, _startingUnits,
                _startingMissionaries, _hasStartingChurch, _startingRebellionCellOwnerId,
                _startingTraders, _tradingPostOwnerId, _hasStartingPresence, _startingRebellionUnits);

        public void ShowState(ProvinceState province, Color ownerColor, bool hasOwner)
        {
            _ownerColor = ownerColor;
            _unitCount.text = province.Units.Count.ToString();
            _fortBadge.enabled = province.HasFort;
            _ownerBanner.enabled = hasOwner;
            _ownerBanner.color = Color.Lerp(ownerColor, Color.black, BannerDarken);

            if (_churchBadge != null)
            {
                _churchBadge.enabled = province.HasChurch;
                _churchBadge.color = ChurchTint;
            }

            if (_presenceBadge != null)
            {
                _presenceBadge.enabled = province.PresenceToken != null;
                _presenceBadge.color = PresenceTint;
            }

            if (_rebellionBadge != null)
            {
                bool hasCell = province.RebellionCell != null;
                _rebellionBadge.enabled = hasCell;
                if (hasCell)
                {
                    _rebellionBadge.color = province.RebellionCell.IsOpenRebellion
                        ? OpenRebellionTint
                        : UndercoverCellTint;
                }
            }

            if (_tradingPostBadge != null)
            {
                _tradingPostBadge.enabled = province.HasTradingPost;
                _tradingPostBadge.color = TradingPostTint;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = $"#{province.Id}";
            }

            ApplyColor();
        }

        public void ShowHighlight(bool isSelected, bool isMoveTarget)
        {
            _isMoveTarget = isMoveTarget;
            float scale = isSelected ? SelectedScale : isMoveTarget ? TargetScale : 1f;
            transform.localScale = _baseScale * scale;

            if (_outline != null)
            {
                _outline.enabled = true;
                _outline.color = isSelected
                    ? SelectedOutlineColor
                    : isMoveTarget
                        ? TargetOutlineColor
                        : IdleOutlineColor;
            }

            ApplyColor();
        }

        private void ApplyColor()
        {
            _renderer.color = _isMoveTarget ? Color.Lerp(_ownerColor, Color.white, TargetLighten) : _ownerColor;
            if (_terrain != null) _terrain.color = Color.Lerp(Color.white, _ownerColor, TerrainTint);
        }
    }
}