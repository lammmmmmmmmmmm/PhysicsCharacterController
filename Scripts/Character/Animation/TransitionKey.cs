using System;

namespace PhysicsCharacterController
{
    public readonly struct TransitionKey : IEquatable<TransitionKey>
    {
        public readonly StateSO From;
        public readonly StateSO To;

        public TransitionKey(StateSO from, StateSO to)
        {
            From = from;
            To = to;
        }

        public bool Equals(TransitionKey other) => From == other.From && To == other.To;
        public override bool Equals(object obj) => obj is TransitionKey other && Equals(other);
        public override int GetHashCode() => (From.GetHashCode() * 397) ^ To.GetHashCode();
    }
}