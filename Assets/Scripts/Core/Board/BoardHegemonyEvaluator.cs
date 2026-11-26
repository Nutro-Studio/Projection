using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Core.Board
{
    public static class BoardHegemonyEvaluator
    {
        /// <summary>
        /// Calculates total Root-style strength points for a player in a province:
        /// each RegularMilitary unit = 1, Fort (if owned by player) = 1, Church (if owned by player) = 1.
        /// </summary>
        public static int CalculateStrength(ProvinceState province, int playerId)
        {
            int strength = 0;
            var units = province.Units;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.OwnerPlayerId == playerId && unit.Type == UnitType.RegularMilitary)
                {
                    strength++;
                }
            }

            if (province.HasFort && province.OwnerId == playerId)
            {
                strength++;
            }

            if (province.HasChurch && province.ChurchOwnerId == playerId)
            {
                strength++;
            }

            return strength;
        }

        /// <summary>
        /// Dynamically evaluates Province Hegemony via strict majority of total strength points.
        /// Ties yield NeutralOwner; zero strength preserves existing ownership.
        /// </summary>
        public static ProvinceState Evaluate(ProvinceState province, IReadOnlyList<PlayerProfile> players)
        {
            int bestPlayerId = ProvinceState.NeutralOwner;
            int bestStrength = 0;
            bool isTie = false;

            for (int p = 0; p < players.Count; p++)
            {
                int playerId = players[p].PlayerId;
                int strength = CalculateStrength(province, playerId);

                if (strength > bestStrength)
                {
                    bestStrength = strength;
                    bestPlayerId = playerId;
                    isTie = false;
                }
                else if (strength > 0 && strength == bestStrength)
                {
                    isTie = true;
                }
            }

            if (bestStrength == 0)
            {
                return province;
            }

            int newOwnerId = isTie ? ProvinceState.NeutralOwner : bestPlayerId;
            return newOwnerId == province.OwnerId
                ? province
                : province.WithOccupation(newOwnerId, province.Units);
        }
    }
}