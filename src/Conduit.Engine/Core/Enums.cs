using System;

namespace Conduit.Engine.Core
{
    public enum Aspect { Ember, Tide, Bastion, Gale, Verdant, Umbra }

    public enum Rarity { Token, Common, Rare, Legendary }

    public enum CardKind { Monster, Spell }

    /// <summary>The casting ladder (casting-and-cards §3). Order matters: higher = slower + stronger.</summary>
    public enum CastTier { Sign = 0, Word = 1, Script = 2, Ritual = 3 }

    [Flags]
    public enum CastTierMask { None = 0, Sign = 1, Word = 2, Script = 4, Ritual = 8, All = 15 }

    /// <summary>Named field zones — diegetic (voice grammar parse targets) and mechanical.</summary>
    public enum FieldZone { Vanguard = 0, LeftFlank = 1, RightFlank = 2, Rear = 3 }

    public enum SpellSchool { Offense = 0, Defense = 1, Hex = 2 }

    /// <summary>The ten launch keywords (casting-and-cards §2).</summary>
    public enum Keyword { Bulwark, Swift, Pierce, Ward, Regrow, Ambush, Drain, Torrent, Swarm, SoulLink }

    // ── Ability-driven combat model (combat-spec.md) ──

    /// <summary>A creature's current combat action (spec §5).</summary>
    public enum ActionState { Idle, Moving, WindingUp, Recovering, Retreating }

    /// <summary>Area shape of an ability. v0: Single / Circle / Cone (Line deferred).</summary>
    public enum AoeShape { Single, Circle, Cone }

    /// <summary>How a creature's AI prefers to pick among valid targets (spec §7). Generic — no
    /// element/creature semantics; content assigns these to abilities later.</summary>
    public enum TargetPref { Any, Swarm, Armored, Summoner, LowestHp, HighestThreat }

    /// <summary>Damage classification. Placeholder single value in v0 — element/type interactions are
    /// unspecified content design, not engine design. Reserved so content can extend without engine change.</summary>
    public enum DamageType { Generic }

    /// <summary>The closed effect vocabulary (spec §3.3). New ops are a spec change.</summary>
    public enum EffectOpType { Damage, Heal, ModifyStats, GrantStatus, SummonToken, DrainLife, Hex }

    public enum EffectTargetKind { FromIntent, AllySummoner, EnemySummoner, Self }

    public enum DuelPhase { Created, Running, Ended }

    public enum DuelEndReason { LifeZero, Concede, Draw }

    /// <summary>Every validation rule = one reason (spec §4.4).</summary>
    public enum RejectReason
    {
        UnknownEntity, NotYourCard, WrongZone, InsufficientEnergy, BoardFull,
        SchoolOnCooldown, AlreadyChanneling, ConduitBroken, TierNotAllowed,
        IllegalTarget, NoDrawCharge, DuelOver, NotChanneling, GuardActive
    }
}
