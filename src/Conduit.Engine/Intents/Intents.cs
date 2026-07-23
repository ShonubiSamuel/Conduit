using Conduit.Engine.Core;

namespace Conduit.Engine.Intents
{
    public enum TargetKind { None, Monster, Summoner, Zone }

    /// <summary>Concrete by the time it reaches the engine — deixis ("that one") is resolved
    /// by multimodal fusion ABOVE this boundary (spec §4.1).</summary>
    public readonly record struct TargetRef(TargetKind Kind, EntityId Monster = default, PlayerId Player = default, FieldZone Zone = default)
    {
        public static TargetRef None => default;
        public static TargetRef OfMonster(EntityId id) => new TargetRef(TargetKind.Monster, id);
        public static TargetRef OfSummoner(PlayerId p) => new TargetRef(TargetKind.Summoner, Player: p);
        public static TargetRef OfZone(FieldZone z) => new TargetRef(TargetKind.Zone, Zone: z);
    }

    public enum DirectiveKind { Attack, Defend, Return, Sacrifice }

    public readonly record struct Directive(DirectiveKind Kind, TargetRef Target = default, FieldZone Zone = default)
    {
        public static Directive AttackAuto => new Directive(DirectiveKind.Attack);
        public static Directive Defend(FieldZone z) => new Directive(DirectiveKind.Defend, Zone: z);
    }

    // ── The closed intent union (R2). Isomorphic to the voice grammar's entity × verb × slot. ──
    public abstract record Intent;
    public sealed record SummonMonster(CardInstanceId HandCard, FieldZone Slot) : Intent;
    public sealed record CastSpell(CardInstanceId HandCard, CastTier Tier, TargetRef Target) : Intent;
    public sealed record CancelChannel : Intent;
    public sealed record CommandMonster(EntityId Monster, Directive D) : Intent;
    public sealed record Guard : Intent;      // the built-in danger-scaled ward (spec §6.7)
    public sealed record DrawCard : Intent;
    public sealed record Concede : Intent;

    /// <summary>Ignored by rules (R2); retained by replays for telemetry.</summary>
    public sealed record IntentMeta(string Modality, long ClientTimeMs = 0);

    public sealed record IntentEnvelope(
        ulong Seq,             // per-actor monotonic
        PlayerId Actor,
        int SubmittedTick,
        Intent Payload,
        IntentMeta? Meta = null);
}
