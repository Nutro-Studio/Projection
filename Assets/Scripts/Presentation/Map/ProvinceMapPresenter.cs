using System;
using Presentation.Hud;
using Core.Board;
using Core.Simulation;

namespace Presentation.Map
{
    /// <summary>
    /// Keeps the world-space province views in sync: ownership and garrison come from
    /// <see cref="GameSession.OnProvinceStateChanged"/>, selection from <see cref="HudInteraction.Changed"/>.
    /// </summary>
    public sealed class ProvinceMapPresenter : IDisposable
    {
        private readonly GameSession _session;
        private readonly HudInteraction _interaction;
        private readonly ProvinceView[] _views;

        public ProvinceMapPresenter(GameSession session, HudInteraction interaction, ProvinceView[] views)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            _views = views ?? throw new ArgumentNullException(nameof(views));

            RefreshAllProvinces();
            ShowHighlights();

            _session.OnProvinceStateChanged += ShowProvince;
            _session.StateChanged += RefreshAllProvinces;
            _interaction.Changed += ShowHighlights;
        }

        public void Dispose()
        {
            _session.OnProvinceStateChanged -= ShowProvince;
            _session.StateChanged -= RefreshAllProvinces;
            _interaction.Changed -= ShowHighlights;
        }

        private void RefreshAllProvinces()
        {
            for (int i = 0; i < _session.Provinces.Count; i++)
            {
                ShowProvince(_session.Provinces[i]);
            }
        }

        private void ShowProvince(ProvinceState province)
        {
            var owner = _session.GetPlayer(province.OwnerId);
            var color = owner == null ? PlayerPalette.Neutral : PlayerPalette.ForPlayerId(owner.PlayerId);
            _views[province.Id].ShowState(province, color, owner != null, _session.CurrentPlayer.PlayerId);
        }

        private void ShowHighlights()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                bool isSelected = _interaction.SelectedProvinceId == i;
                bool isTargetOrWaypoint = _interaction.IsValidMoveTarget(i) || _interaction.IsCaravanWaypoint(i);
                _views[i].ShowHighlight(isSelected, isTargetOrWaypoint);
            }
        }
    }
}