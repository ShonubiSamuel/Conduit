namespace Conduit.Engine.Core
{
    /// <summary>0 or 1. Never any other value in a duel.</summary>
    public readonly record struct PlayerId(byte Value)
    {
        public static readonly PlayerId P0 = new PlayerId(0);
        public static readonly PlayerId P1 = new PlayerId(1);
        public PlayerId Opponent => new PlayerId((byte)(1 - Value));
        public override string ToString() => $"P{Value}";
    }

    /// <summary>Engine-assigned, monotonic, never reused. Canonical sort key (D5).</summary>
    public readonly record struct EntityId(int Value)
    {
        public static readonly EntityId None = new EntityId(0);
        public bool IsNone => Value == 0;
        public override string ToString() => $"E{Value}";
    }

    /// <summary>Content key, e.g. "ember.emberdrake".</summary>
    public readonly record struct CardDefId(string Value)
    {
        public override string ToString() => Value ?? "?";
    }

    /// <summary>A specific physical/instance copy of a card within a duel (deck slot).</summary>
    public readonly record struct CardInstanceId(int Value)
    {
        public static readonly CardInstanceId None = new CardInstanceId(0);
        public bool IsNone => Value == 0;
        public override string ToString() => $"C{Value}";
    }
}
