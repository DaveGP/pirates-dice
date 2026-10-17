namespace PiratesDice.Core
{
    public enum DecisionKind { Bid, Liar, SpotOn }

    /// <summary>One move by one seat. Local players, AI pirates and (later) network clients all produce these.</summary>
    public readonly struct SeatDecision
    {
        public readonly DecisionKind Kind;
        public readonly Bid Bid;

        SeatDecision(DecisionKind kind, Bid bid)
        {
            Kind = kind;
            Bid = bid;
        }

        public static SeatDecision Raise(Bid bid) => new SeatDecision(DecisionKind.Bid, bid);
        public static SeatDecision Liar() => new SeatDecision(DecisionKind.Liar, default);
        public static SeatDecision SpotOn() => new SeatDecision(DecisionKind.SpotOn, default);

        public override string ToString() => Kind == DecisionKind.Bid ? $"Bid {Bid}" : Kind.ToString();
    }

    public static class GameEngineExtensions
    {
        /// <summary>Applies a decision for the seat whose turn it is. Returns the reveal if the decision was a challenge.</summary>
        public static RoundResult Apply(this GameEngine engine, int seat, SeatDecision decision)
        {
            switch (decision.Kind)
            {
                case DecisionKind.Liar:
                    return engine.CallLiar(seat);
                case DecisionKind.SpotOn:
                    return engine.CallSpotOn(seat);
                default:
                    if (!engine.TryBid(seat, decision.Bid, out string error))
                        throw new System.InvalidOperationException(error);
                    return null;
            }
        }
    }
}
