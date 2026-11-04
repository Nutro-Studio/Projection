using System;
using Core.Combat;

namespace Core.Combat
{
    public class SystemRandomDiceRoller : IDiceRoller
    {
        private readonly Random _rng = new();

        public int RollD4() => _rng.Next(1, 5);

        public (int roll1, int roll2, int roll3) Roll3D4()
        {
            return (RollD4(), RollD4(), RollD4());
        }
    }
}