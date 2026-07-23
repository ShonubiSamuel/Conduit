using System.Collections.Generic;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;

namespace Conduit.Engine.Events
{
    /// <summary>Presentation renders these, and only these (R4).</summary>
    public abstract record GameEvent(int Tick);

    // intake
    public sealed record IntentAccepted(int Tick, PlayerId Actor, ulong Seq) : GameEvent(Tick);
    public sealed record IntentRejected(int Tick, PlayerId Actor, ulong Seq, RejectReason Reason) : GameEvent(Tick);

    // zones
    public sealed record DrawResolved(int Tick, PlayerId Player, CardInstanceId Card, CardDefId Def) : GameEvent(Tick);
    public sealed record DrawChargeGained(int Tick, PlayerId Player, int Charges) : GameEvent(Tick);
    public sealed record CardSpent(int Tick, PlayerId Player, CardInstanceId Card) : GameEvent(Tick);   // → physical discard pouch

    // summoning
    public sealed record SummonResolved(int Tick, PlayerId Owner, EntityId Monster, CardDefId Def, FieldZone Zone, bool FaceDown, int Hp) : GameEvent(Tick);

    // casting
    public sealed record ChannelStarted(int Tick, PlayerId Player, CardDefId Def, CastTier Tier, int DurationTicks) : GameEvent(Tick);  // the telegraph
    public enum ChannelBreakReason { Cancelled, Interrupted }
    public sealed record ChannelBroken(int Tick, PlayerId Player, ChannelBreakReason Reason, int RefundMilli) : GameEvent(Tick);
    public sealed record CastResolved(int Tick, PlayerId Player, CardDefId Def, CastTier Tier, int MultiplierBp) : GameEvent(Tick);

    // combat — ability activation pipeline (combat-spec §8). No instant volley.
    public sealed record AbilityCommitted(int Tick, EntityId Creature, string AbilityId, TargetKind TargetKind, EntityId TargetMonster, PlayerId TargetPlayer, int WindupTicks) : GameEvent(Tick);  // the telegraph
    public sealed record HitRecord(EntityId Target, PlayerId TargetOwner, bool IsSummoner, int Dealt, int Overflow);
    public sealed record AbilityResolved(int Tick, EntityId Creature, string AbilityId, IReadOnlyList<HitRecord> Hits) : GameEvent(Tick);
    public sealed record CreatureRecalled(int Tick, PlayerId Owner, EntityId Creature, int ReserveSlot) : GameEvent(Tick);
    public sealed record CreatureDeployed(int Tick, PlayerId Owner, EntityId Creature, CardDefId Def, FieldZone Zone, int Hp) : GameEvent(Tick);
    public sealed record MonsterDied(int Tick, PlayerId Owner, EntityId Monster, CardDefId Def, EntityId Killer) : GameEvent(Tick);
    public sealed record SummonerDamaged(int Tick, PlayerId Player, int Amount, int NewLife, EntityId Source) : GameEvent(Tick);
    public sealed record SummonerHealed(int Tick, PlayerId Player, int Amount, int NewLife) : GameEvent(Tick);

    // economy
    public sealed record EnergyChanged(int Tick, PlayerId Player, int Delta, int NewMilli) : GameEvent(Tick);
    public sealed record SurgeStateChanged(int Tick, PlayerId Player, bool Active) : GameEvent(Tick);
    public sealed record GuardRaised(int Tick, PlayerId Player, int ActiveFromTick, int UntilTick) : GameEvent(Tick);
    public sealed record GuardExpired(int Tick, PlayerId Player) : GameEvent(Tick);

    // statuses
    public sealed record StatusApplied(int Tick, EntityId Entity, string StatusId, int Magnitude, int UntilTick) : GameEvent(Tick);
    public sealed record StatusExpired(int Tick, EntityId Entity, string StatusId) : GameEvent(Tick);

    // lifecycle & integrity
    public sealed record DuelStarted(int Tick, ulong Seed) : GameEvent(Tick);
    public sealed record DuelEnded(int Tick, PlayerId? Winner, DuelEndReason Reason) : GameEvent(Tick);
    public sealed record StateHashed(int Tick, ulong Hash) : GameEvent(Tick);
}
