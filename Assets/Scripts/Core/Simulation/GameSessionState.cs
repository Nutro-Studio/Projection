using System;
using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Crisis;

namespace Core.Simulation
{
    internal sealed class GameSessionState
    {
        public readonly PlayerProfile[] Players;
        public readonly ProvinceState[] Provinces;
        public readonly HashSet<(int EmbargoerId, int GuildId)> ActiveEmbargoes = new();
        public readonly bool CrisisEnabled;
        public readonly GameBalanceConfig Config;
        public readonly Dictionary<int, ActiveCaravanRoute> ActiveCaravans = new();

        public int CurrentPlayerIndex;
        public int NextUnitId;
        public int QualifyingCaravanRoutesThisTurn;
        public int TurnNumber = 1;
        public int PendingGuildCapitalPlayerId = -1;
        public bool IsGameOver;

        public event Action StateChanged;
        public event Action<CombatReport> BattleResolved;
        public event Action<ProvinceState> ProvinceStateChanged;

        public PlayerProfile CurrentPlayer => Players[CurrentPlayerIndex];

        public GameSessionState(
            IReadOnlyList<PlayerProfile> players,
            IReadOnlyList<ProvinceState> provinces,
            bool crisisEnabled,
            GameBalanceConfig config = null)
        {
            CrisisEnabled = crisisEnabled;
            Config = config ?? GameBalanceConfig.Default;
            Players = new PlayerProfile[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                Players[i] = players[i];
            }

            Provinces = new ProvinceState[provinces.Count];
            for (int i = 0; i < provinces.Count; i++)
            {
                Provinces[i] = BoardHegemonyEvaluator.Evaluate(provinces[i], Players);
                for (int u = 0; u < provinces[i].Units.Count; u++)
                {
                    NextUnitId = Math.Max(NextUnitId, provinces[i].Units[u].Id + 1);
                }
            }
        }

        public void SetProvince(ProvinceState province)
        {
            var evaluated = BoardHegemonyEvaluator.Evaluate(province, Players);
            Provinces[evaluated.Id] = evaluated;
            ProvinceStateChanged?.Invoke(evaluated);
        }

        public void SetProvinceDirect(ProvinceState province)
        {
            Provinces[province.Id] = province;
            ProvinceStateChanged?.Invoke(province);
        }

        public void AddGold(int playerIndex, int amount)
        {
            var player = Players[playerIndex];
            Players[playerIndex] = player with { Gold = player.Gold + amount };
        }

        public void AddVictoryPoints(int playerIndex, int amount)
        {
            var player = Players[playerIndex];
            Players[playerIndex] = player with { VictoryPoints = Math.Max(0, player.VictoryPoints + amount) };
            if (Players[playerIndex].VictoryPoints >= Config.VictoryPointsThreshold)
            {
                IsGameOver = true;
            }
        }

        public void SpendActionPoint()
        {
            var player = CurrentPlayer;
            Players[CurrentPlayerIndex] = player with { ActionPoints = player.ActionPoints - 1 };
        }

        public int IndexOfPlayer(int playerId)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].PlayerId == playerId) return i;
            }

            return -1;
        }

        public PlayerProfile GetPlayer(int playerId)
        {
            int idx = IndexOfPlayer(playerId);
            return idx >= 0 ? Players[idx] : null;
        }

        public int CountProvinces(int playerId)
        {
            int count = 0;
            for (int i = 0; i < Provinces.Length; i++)
            {
                if (Provinces[i].OwnerId == playerId) count++;
            }

            return count;
        }

        public sealed class ActiveCaravanRoute
        {
            public readonly int TraderUnitId;
            public readonly List<int> Path;
            public int CurrentStepIndex;

            public ActiveCaravanRoute(int traderUnitId, List<int> path, int currentStepIndex = 0)
            {
                TraderUnitId = traderUnitId;
                Path = path;
                CurrentStepIndex = currentStepIndex;
            }
        }

        public void NotifyStateChanged() => StateChanged?.Invoke();
        public void NotifyBattleResolved(CombatReport report) => BattleResolved?.Invoke(report);
    }
}