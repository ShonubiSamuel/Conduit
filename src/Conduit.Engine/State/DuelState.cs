using System.Collections.Generic;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.Rng;

namespace Conduit.Engine.State
{
    public sealed class CardInstance
    {
        public CardInstanceId Id;
        public CardDefId Def;
        public CardInstance(CardInstanceId id, CardDefId def) { Id = id; Def = def; }
    }

    public struct StatusInstance
    {
        public string Id;        // e.g. "charge.Ember", "hex.atk"
        public int Magnitude;
        public int UntilTick;    // int.MaxValue = permanent
    }

    public sealed class MonsterInstance
    {
        public EntityId Id;
        public PlayerId Owner;
        public CardDefId Def;
        public CardInstanceId Card;      // None for tokens
        public int Hp, MaxHp;
        public int WardPool;             // Ward keyword absorb pool
        public FieldZone Zone;
        public Directive Directive = Directive.AttackAuto;
        public bool FaceDown;            // Ambush
        public List<StatusInstance> Statuses = new List<StatusInstance>();

        // ── ability-driven combat runtime (combat-spec §5) ──
        public FxVec2 Pos;
        public FxVec2 Facing = new FxVec2(0, 1000);
        public FxVec2 Goal;
        public int MoveSpeed;
        public ActionState Action = ActionState.Idle;
        public int ActionUntilTick;      // end of WindingUp / Recovering / recall
        public int ActiveAbilityIndex = -1;
        public TargetRef CommittedTarget;    // target locked at commit; resolved after wind-up
        public int NextReevalTick;
        public bool Recalling;
        public int RecallReadyTick = -1;     // set on arrival at summoner; recall completes at this tick
        public int[] CooldownUntil = System.Array.Empty<int>();   // per ability
    }

    /// <summary>A creature pulled off the field via Recall — heals here, can be redeployed (combat-model §6).</summary>
    public sealed class ReserveInstance
    {
        public CardDefId Def;
        public CardInstanceId Card;
        public int Hp, MaxHp;
        public int RegenCounter;
    }

    public sealed class ChannelState
    {
        public CardInstanceId Card;
        public CardDefId Def;
        public CastTier Tier;
        public TargetRef Target;
        public int CostPaidMilli;
        public int CompleteTick;
    }

    public struct GuardState
    {
        public int ActiveFromTick;
        public int UntilTick;
    }

    public struct EchoRecord
    {
        public CardDefId Def;
        public int Tick;
    }

    public sealed class PlayerState
    {
        public PlayerId Id;
        public int Life;
        public int EnergyMilli;
        public int RegenAccumulator;     // Bresenham numerator (D2)
        public int DrawTickCounter;
        public int DrawCharges;
        public bool SurgeActive;
        public List<CardInstance> Deck = new List<CardInstance>();       // [0] = top
        public List<CardInstance> Hand = new List<CardInstance>();
        public List<CardInstance> Graveyard = new List<CardInstance>();
        public List<ReserveInstance> Reserve = new List<ReserveInstance>();  // recalled creatures, healing
        public ChannelState? Channel;
        public GuardState? Guard;
        public int[] SchoolCooldownUntil = new int[3];                   // per SpellSchool
        public List<EchoRecord> RecentCasts = new List<EchoRecord>();
        public bool Conceded;
    }

    public sealed record DuelResult(PlayerId? Winner, DuelEndReason Reason);

    /// <summary>The whole world. Copyable by design (§13 rollback); hashed canonically (§8).</summary>
    public sealed class DuelState
    {
        public int Tick;
        public DuelPhase Phase = DuelPhase.Created;
        public Pcg32 Rng;
        public PlayerState[] Players = { new PlayerState { Id = PlayerId.P0 }, new PlayerState { Id = PlayerId.P1 } };
        public List<MonsterInstance> Field = new List<MonsterInstance>();   // both sides, kept sorted by EntityId
        public int NextEntityId = 1;
        public int NextCardInstanceId = 1;
        public DuelResult? Result;

        public PlayerState P(PlayerId id) => Players[id.Value];
    }
}
