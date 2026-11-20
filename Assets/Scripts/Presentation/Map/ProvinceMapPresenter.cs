using System;
using Presentation.Hud;
using Presentation.Prototype;

namespace Presentation.Map
{
    /// <summary>
    /// Keeps the world-space province views in sync: ownership and garrison come from
    /// <see cref="PrototypeGameSession.OnProvinceStateChanged"/>, selection from <see cref="HudInteraction.Changed"/>.
    /// </summary>
    public sealed class ProvinceMapPresenter : IDisposable
    {
        private readonly PrototypeGameSession _session;
        private readonly HudInteraction _interaction;
        private readonly ProvinceView[] _views;

        public ProvinceMapPresenter(PrototypeGameSession session, HudInteraction interaction, ProvinceView[] views)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            _views = views ?? throw new ArgumentNullException(nameof(views));

            for (int i = 0; i < _session.Provinces.Count; i++)
            {
                ShowProvince(_session.Provinces[i]);
            }

            ShowHighlights();

            _session.OnProvinceStateChanged += ShowProvince;
            _interaction.Changed += ShowHighlights;
        }

        public void Dispose()
        {
            _session.OnProvinceStateChanged -= ShowProvince;
            _interaction.Changed -= ShowHighlights;
        }

        private void ShowProvince(ProvinceState province)
        {
            var owner = _session.GetPlayer(province.OwnerId);
            var color = owner == null ? FactionPalette.Neutral : FactionPalette.For(owner.Faction);
            _views[province.Id].ShowState(province, color, owner != null);
        }

        private void ShowHighlights()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i].ShowHighlight(_interaction.SelectedProvinceId == i, _interaction.IsValidMoveTarget(i));
            }
        }
    }
}
