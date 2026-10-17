using System;

namespace PiratesDice.Core
{
    /// <summary>A claim about every die at the table, such as "four 3s".</summary>
    public readonly struct Bid : IEquatable<Bid>
    {
        public readonly int Quantity;
        public readonly int Face;

        public Bid(int quantity, int face)
        {
            Quantity = quantity;
            Face = face;
        }

        public bool Equals(Bid other) => Quantity == other.Quantity && Face == other.Face;
        public override bool Equals(object obj) => obj is Bid other && Equals(other);
        public override int GetHashCode() => Quantity * 7 + Face;
        public static bool operator ==(Bid a, Bid b) => a.Equals(b);
        public static bool operator !=(Bid a, Bid b) => !a.Equals(b);

        public override string ToString()
        {
            string face = Face == 1 ? "1" : Face.ToString();
            return Quantity == 1 ? $"one {face}" : $"{Quantity} {face}s";
        }
    }
}
