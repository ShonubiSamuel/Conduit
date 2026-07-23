using System;
using System.Collections.Generic;
using System.Linq;
using Conduit.Engine.Ai;
using Conduit.Engine.Core;
using Conduit.Engine.Events;
using Conduit.Engine.Hash;
using Conduit.Engine.Intents;
using Conduit.Engine.Replay;
using Conduit.Engine.Rng;
using Conduit.Engine.Spatial;
using Conduit.Engine.State;

namespace Conduit.Engine
{
    /// <summary>
    /// The headless deterministic rules kernel (R1). Consumes IntentEnvelopes, emits GameEvents.
    /// Same (config, content, decks, seed, intent stream) → bit-identical everything, always.
    /// Pipeline order per tick is spec §6.1 and is the determinism spine — do not reorder.
    /// </summary>
    public sealed class DuelEngine
    {
        private readonly DuelConfig _cfg;
        private readonly ContentDb _content;
        private readonly DuelState _s;
        private readonly List<IntentEnvelope> _pending = new List<IntentEnvelope>();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private readonly List<IntentEnvelope> _journal = new List<IntentEnvelope>();
        private readonly ulong[] _lastSeq = { 0, 0 };
        private readonly ulong _seed;

        public int CurrentTick => _s.Tick;
        public DuelPhase Phase => _s.Phase;
        public DuelResult? Result => _s.Result;

        private DuelEngine(DuelConfig cfg, ContentDb content, DuelState s, ulong seed)
        { _cfg = cfg; _content = content; _s = s; _seed = seed; }

        // ── Creation ──────────────────────────────────────────────────────────────

        public static DuelEngine Create(DuelConfig cfg, ContentDb content, DeckList[] decks, ulong seed)
        {
            if (decks.Length != 2) throw new ArgumentException("exactly two decks");
            var s = new DuelState { Rng = new Pcg32(seed) };
            var e = new DuelEngine(cfg, content, s, seed);

            for (int p = 0; p < 2; p++)
            {
                var ps = s.Players[p];
                ps.Life = cfg.LifeMax;
                ps.EnergyMilli = cfg.StartingEnergyMilli;
                foreach (var defId in decks[p].Cards)
                {
                    if (!content.TryGet(defId, out _)) throw new ArgumentException($"unknown card {defId}");
                    ps.Deck.Add(new CardInstance(new CardInstanceId(s.NextCardInstanceId++), defId));
                }
                e.Shuffle(ps.Deck);
                for (int i = 0; i < cfg.OpeningHandSize && ps.Deck.Count > 0; i++)
                    e.MoveTopOfDeckToHand(ps);
            }
            s.Phase = DuelPhase.Running;
            e._events.Add(new DuelStarted(0, seed));
            return e;
        }

        private void Shuffle(List<CardInstance> deck)
        {
            // Fisher–Yates via the engine Rng (D3). Deck [0] is the top.
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = _s.Rng.NextInt(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        private void MoveTopOfDeckToHand(PlayerState ps)
        {
            var c = ps.Deck[0];
            ps.Deck.RemoveAt(0);
            ps.Hand.Add(c);
        }

        // ── Input ─────────────────────────────────────────────────────────────────

        public void Submit(IntentEnvelope e)
        {
            if (e.Seq <= _lastSeq[e.Actor.Value]) return;   // dedupe / replay-safety
            _lastSeq[e.Actor.Value] = e.Seq;
            _pending.Add(e);
            _journal.Add(e);
        }

        // ── The tick pipeline (spec §6.1) ─────────────────────────────────────────

        public IReadOnlyList<GameEvent> AdvanceTick()
        {
            var outEvents = new List<GameEvent>();
            if (_events.Count > 0) { outEvents.AddRange(_events); _events.Clear(); }  // creation events
            if (_s.Phase == DuelPhase.Ended) return outEvents;

            _s.Tick++;
            int t = _s.Tick;

            // 1. INGEST — order by (Actor, Seq); D5 canonical ordering
            var batch = _pending.Where(p => p.SubmittedTick <= t)
                                .OrderBy(p => p.Actor.Value).ThenBy(p => p.Seq).ToList();
            foreach (var b in batch) _pending.Remove(b);

            // 2. VALIDATE + apply accepted intents against start-of-tick state
            foreach (var env in batch)
            {
                var reason = Validate(env);
                if (reason is RejectReason r)
                    _events.Add(new IntentRejected(t, env.Actor, env.Seq, r));
                else
                {
                    _events.Add(new IntentAccepted(t, env.Actor, env.Seq));
                    ApplyAccepted(env);
                }
            }

            if (_s.Phase != DuelPhase.Ended)
            {
                Timers(t);        // 3
                CastCompletion(t);// 4
                CreatureAct(t);   // 5 — movement + AI + ability pipeline (combat-spec §8)
                Deaths(t);        // 6
                WinCheck(t);      // 7
            }

            // 8. EMIT (+ periodic hash)
            if (_s.Phase != DuelPhase.Ended && t % _cfg.HashIntervalTicks == 0)
                _events.Add(new StateHashed(t, StateHash()));

            outEvents.AddRange(_events);
            _events.Clear();
            return outEvents;
        }

        public void AdvanceTo(int tick, Action<GameEvent>? sink = null)
        {
            while (_s.Tick < tick && _s.Phase != DuelPhase.Ended)
                foreach (var ev in AdvanceTick()) sink?.Invoke(ev);
        }

        // ── Phase 2: validation & application ─────────────────────────────────────

        private RejectReason? Validate(IntentEnvelope env)
        {
            if (_s.Phase == DuelPhase.Ended) return RejectReason.DuelOver;
            var ps = _s.P(env.Actor);
            switch (env.Payload)
            {
                case SummonMonster sm:
                {
                    var card = ps.Hand.FirstOrDefault(c => c.Id == sm.HandCard);
                    if (card == null) return RejectReason.NotYourCard;
                    var def = _content.Get(card.Def);
                    if (def.Kind != CardKind.Monster) return RejectReason.WrongZone;
                    if (_s.Field.Count(m => m.Owner == env.Actor) >= _cfg.BoardCap) return RejectReason.BoardFull;
                    if (ps.EnergyMilli < def.CostMilliEnergy) return RejectReason.InsufficientEnergy;
                    return null;
                }
                case CastSpell cs:
                {
                    var card = ps.Hand.FirstOrDefault(c => c.Id == cs.HandCard);
                    if (card == null) return RejectReason.NotYourCard;
                    var def = _content.Get(card.Def);
                    if (def.Kind != CardKind.Spell) return RejectReason.WrongZone;
                    if (ps.Channel != null) return RejectReason.AlreadyChanneling;
                    var mask = (CastTierMask)(1 << (int)cs.Tier);
                    if ((def.AllowedTiers & mask) == 0) return RejectReason.TierNotAllowed;
                    // Conduit rule: no fielded monster → Script/Ritual blocked (canonical universal cost)
                    if (cs.Tier >= CastTier.Script && !_s.Field.Any(m => m.Owner == env.Actor))
                        return RejectReason.ConduitBroken;
                    if (_s.Tick < ps.SchoolCooldownUntil[(int)def.School]) return RejectReason.SchoolOnCooldown;
                    if (ps.EnergyMilli < def.CostMilliEnergy) return RejectReason.InsufficientEnergy;
                    if (!TargetLegal(cs.Target)) return RejectReason.IllegalTarget;
                    return null;
                }
                case CancelChannel:
                    return ps.Channel == null ? RejectReason.NotChanneling : (RejectReason?)null;
                case CommandMonster cm:
                {
                    var m = _s.Field.FirstOrDefault(x => x.Id == cm.Monster);
                    if (m == null) return RejectReason.UnknownEntity;
                    if (m.Owner != env.Actor) return RejectReason.NotYourCard;
                    return null;
                }
                case Recall rc:
                {
                    var m = _s.Field.FirstOrDefault(x => x.Id == rc.Monster);
                    if (m == null) return RejectReason.UnknownEntity;
                    if (m.Owner != env.Actor) return RejectReason.NotYourCard;
                    return null;
                }
                case DeployReserve dr:
                {
                    if (dr.ReserveSlot < 0 || dr.ReserveSlot >= ps.Reserve.Count) return RejectReason.UnknownEntity;
                    if (_s.Field.Count(m => m.Owner == env.Actor) >= _cfg.BoardCap) return RejectReason.BoardFull;
                    if (ps.EnergyMilli < _cfg.DeployCostMilli) return RejectReason.InsufficientEnergy;
                    return null;
                }
                case Intents.Guard:
                    if (ps.Guard != null) return RejectReason.GuardActive;
                    if (_s.Tick < ps.SchoolCooldownUntil[(int)SpellSchool.Defense]) return RejectReason.SchoolOnCooldown;
                    if (ps.EnergyMilli < _cfg.GuardCostMilli) return RejectReason.InsufficientEnergy;
                    return null;
                case DrawCard:
                    if (ps.DrawCharges <= 0) return RejectReason.NoDrawCharge;
                    if (ps.Deck.Count == 0) return RejectReason.WrongZone;
                    return null;
                case Concede:
                    return null;
                default:
                    return RejectReason.UnknownEntity;
            }
        }

        private bool TargetLegal(TargetRef tr)
        {
            switch (tr.Kind)
            {
                case TargetKind.None:
                case TargetKind.Zone:
                case TargetKind.Summoner:
                    return true;
                case TargetKind.Monster:
                    return _s.Field.Any(m => m.Id == tr.Monster);
                default: return false;
            }
        }

        private void ApplyAccepted(IntentEnvelope env)
        {
            int t = _s.Tick;
            var ps = _s.P(env.Actor);
            switch (env.Payload)
            {
                case SummonMonster sm:
                {
                    var card = ps.Hand.First(c => c.Id == sm.HandCard);
                    var def = _content.Get(card.Def);
                    ps.Hand.Remove(card);
                    ps.EnergyMilli -= def.CostMilliEnergy;
                    _events.Add(new EnergyChanged(t, env.Actor, -def.CostMilliEnergy, ps.EnergyMilli));
                    SpawnMonster(env.Actor, def, card.Id, sm.Slot, t);
                    // Swarm X: tokens on entry
                    var swarm = def.Get(Keyword.Swarm);
                    if (swarm != null && swarm.Ref != null && _content.TryGet(new CardDefId(swarm.Ref), out var tokenDef))
                        for (int i = 0; i < swarm.X; i++)
                            SpawnMonster(env.Actor, tokenDef, CardInstanceId.None, sm.Slot, t);
                    break;
                }
                case CastSpell cs:
                {
                    var card = ps.Hand.First(c => c.Id == cs.HandCard);
                    var def = _content.Get(card.Def);
                    ps.Hand.Remove(card);
                    ps.EnergyMilli -= def.CostMilliEnergy;
                    _events.Add(new EnergyChanged(t, env.Actor, -def.CostMilliEnergy, ps.EnergyMilli));
                    int dur = _cfg.CastDurationTicks[(int)cs.Tier];
                    ps.Channel = new ChannelState
                    {
                        Card = card.Id, Def = def.Id, Tier = cs.Tier, Target = cs.Target,
                        CostPaidMilli = def.CostMilliEnergy, CompleteTick = t + dur
                    };
                    _events.Add(new ChannelStarted(t, env.Actor, def.Id, cs.Tier, dur));
                    break;
                }
                case CancelChannel:
                    BreakChannel(ps, ChannelBreakReason.Cancelled, t);
                    break;
                case CommandMonster cm:
                {
                    var m = _s.Field.First(x => x.Id == cm.Monster);
                    m.Directive = cm.D;
                    m.Action = ActionState.Idle;        // re-decide next tick under the new directive
                    m.NextReevalTick = t;
                    if (cm.D.Kind == DirectiveKind.Sacrifice) m.Hp = 0;   // resolved in phase 6
                    break;
                }
                case Recall rc:
                {
                    var m = _s.Field.First(x => x.Id == rc.Monster);
                    m.Directive = Directive.Retreat;    // move to summoner, then leave the field
                    m.Recalling = true;
                    m.Action = ActionState.Retreating;
                    m.Goal = SummonerPos(m.Owner);
                    break;
                }
                case DeployReserve dr:
                {
                    var r = ps.Reserve[dr.ReserveSlot];
                    ps.Reserve.RemoveAt(dr.ReserveSlot);
                    ps.EnergyMilli -= _cfg.DeployCostMilli;
                    if (_cfg.DeployCostMilli > 0) _events.Add(new EnergyChanged(t, env.Actor, -_cfg.DeployCostMilli, ps.EnergyMilli));
                    var def = _content.Get(r.Def);
                    var m = SpawnMonster(env.Actor, def, r.Card, dr.Slot, t, startHp: r.Hp);
                    _events.Add(new CreatureDeployed(t, env.Actor, m.Id, def.Id, dr.Slot, m.Hp));
                    break;
                }
                case Intents.Guard:
                {
                    ps.EnergyMilli -= _cfg.GuardCostMilli;
                    _events.Add(new EnergyChanged(t, env.Actor, -_cfg.GuardCostMilli, ps.EnergyMilli));
                    int from = t + _cfg.GuardChannelTicks;
                    int until = from + _cfg.GuardActiveTicks;
                    ps.Guard = new GuardState { ActiveFromTick = from, UntilTick = until };
                    ps.SchoolCooldownUntil[(int)SpellSchool.Defense] = t + _cfg.SchoolCooldownTicks;
                    _events.Add(new GuardRaised(t, env.Actor, from, until));
                    break;
                }
                case DrawCard:
                {
                    ps.DrawCharges--;
                    var c = ps.Deck[0];
                    MoveTopOfDeckToHand(ps);
                    _events.Add(new DrawResolved(t, env.Actor, c.Id, c.Def));
                    break;
                }
                case Concede:
                    ps.Conceded = true;
                    End(t, env.Actor.Opponent, DuelEndReason.Concede);
                    break;
            }
        }

        private MonsterInstance SpawnMonster(PlayerId owner, CardDef def, CardInstanceId card, FieldZone zone, int t, int startHp = -1)
        {
            int hp = startHp >= 0 ? startHp : def.Hp * _cfg.HpScale;
            int maxHp = def.Hp * _cfg.HpScale;
            var m = new MonsterInstance
            {
                Id = new EntityId(_s.NextEntityId++),
                Owner = owner, Def = def.Id, Card = card,
                Hp = hp, MaxHp = maxHp,
                WardPool = def.KeywordX(Keyword.Ward),
                Zone = zone,
                FaceDown = def.Has(Keyword.Ambush),
                MoveSpeed = def.MoveSpeedMuPerTick,
                CooldownUntil = new int[def.Abilities.Count],
            };
            m.Pos = SpawnPosition(owner, zone);
            int sign = owner.Value == 0 ? -1 : 1;
            m.Facing = new FxVec2(0, -sign * 1000);   // face the enemy end
            m.Goal = m.Pos;
            _s.Field.Add(m);   // NextEntityId is monotonic → list stays sorted by EntityId (D5)
            _events.Add(new SummonResolved(t, owner, m.Id, def.Id, zone, m.FaceDown, m.Hp));
            return m;
        }

        private FxVec2 SpawnPosition(PlayerId owner, FieldZone zone)
        {
            int sign = owner.Value == 0 ? -1 : 1;
            int forward = sign * (_cfg.SummonerOffsetMu - 500);   // just in front of the summoner
            int count = _s.Field.Count(x => x.Owner == owner);
            int spread = ((count % 4) - 1) * 300;                  // fan out so bodies don't stack
            int side = zone switch
            {
                FieldZone.LeftFlank => -_cfg.ArenaWidthMu / 4,
                FieldZone.RightFlank => _cfg.ArenaWidthMu / 4,
                _ => 0,
            };
            return new FxVec2(side + spread, forward);
        }

        private FxVec2 SummonerPos(PlayerId p) =>
            new FxVec2(0, (p.Value == 0 ? -1 : 1) * _cfg.SummonerOffsetMu);

        // ── Phase 3: timers ───────────────────────────────────────────────────────

        private void Timers(int t)
        {
            for (int p = 0; p < 2; p++)
            {
                var ps = _s.Players[p];
                var opp = _s.Players[1 - p];

                // energy: base + escalation ramp, ×surge while outnumbered — Bresenham accumulator (D2)
                int elapsedMin = t / _cfg.TicksPerMinute;
                int rate = Math.Min(_cfg.RampCapMilliPerMin, _cfg.BaseRegenMilliPerMin + _cfg.RampMilliPerMinPerMin * elapsedMin);
                int myCount = _s.Field.Count(m => m.Owner.Value == p);
                int oppCount = _s.Field.Count(m => m.Owner.Value != p);
                bool surge = myCount < oppCount;
                if (surge != ps.SurgeActive)
                {
                    ps.SurgeActive = surge;
                    _events.Add(new SurgeStateChanged(t, ps.Id, surge));
                }
                if (surge) rate = (int)((long)rate * _cfg.SurgeBp / 10000);
                ps.RegenAccumulator += rate;
                int gain = ps.RegenAccumulator / _cfg.TicksPerMinute;
                ps.RegenAccumulator %= _cfg.TicksPerMinute;
                if (gain > 0 && ps.EnergyMilli < _cfg.EnergyCapMilli)
                {
                    int newVal = Math.Min(_cfg.EnergyCapMilli, ps.EnergyMilli + gain);
                    ps.EnergyMilli = newVal;   // capped pool forces spending (balance §1); no event spam for regen
                }

                // draw charges
                ps.DrawTickCounter++;
                if (ps.DrawTickCounter >= _cfg.DrawIntervalTicks)
                {
                    ps.DrawTickCounter = 0;
                    if (ps.DrawCharges < _cfg.DrawChargeCap)
                    {
                        ps.DrawCharges++;
                        _events.Add(new DrawChargeGained(t, ps.Id, ps.DrawCharges));
                    }
                }

                // guard expiry
                if (ps.Guard is GuardState g && t >= g.UntilTick)
                {
                    ps.Guard = null;
                    _events.Add(new GuardExpired(t, ps.Id));
                }

                // echo window pruning
                ps.RecentCasts.RemoveAll(rc => t - rc.Tick > _cfg.EchoWindowTicks);

                // reserve (recalled) creatures heal while off the field (combat-model §6)
                if (t % _cfg.ReserveRegenIntervalTicks == 0)
                    foreach (var r in ps.Reserve)
                        if (r.Hp < r.MaxHp) r.Hp = Math.Min(r.MaxHp, r.Hp + _cfg.ReserveRegenPerInterval);
            }

            // statuses + Regrow (per monster, sorted order is inherent — Field is EntityId-sorted)
            foreach (var m in _s.Field)
            {
                for (int i = m.Statuses.Count - 1; i >= 0; i--)
                {
                    if (t >= m.Statuses[i].UntilTick)
                    {
                        _events.Add(new StatusExpired(t, m.Id, m.Statuses[i].Id));
                        m.Statuses.RemoveAt(i);
                    }
                }
                if (t % _cfg.RegrowIntervalTicks == 0)
                {
                    var def = _content.Get(m.Def);
                    int x = def.KeywordX(Keyword.Regrow);
                    if (x > 0 && m.Hp < m.MaxHp) m.Hp = Math.Min(m.MaxHp, m.Hp + x);
                }
            }
        }

        // ── Phase 4: cast completion ──────────────────────────────────────────────

        private void CastCompletion(int t)
        {
            for (int p = 0; p < 2; p++)
            {
                var ps = _s.Players[p];
                var ch = ps.Channel;
                if (ch == null || t < ch.CompleteTick) continue;
                ps.Channel = null;

                var def = _content.Get(ch.Def);
                int tierBp = _cfg.CastTierBp[(int)ch.Tier];
                int priorCasts = ps.RecentCasts.Count(rc => rc.Def == ch.Def);
                int echoBp = priorCasts == 0 ? 10000 : priorCasts == 1 ? _cfg.EchoFirstBp : _cfg.EchoSecondBp;
                int bp = (int)((long)tierBp * echoBp / 10000);

                foreach (var op in def.Effects)
                    ApplyEffect(ps.Id, op, ch.Target, bp, t);

                ps.Graveyard.Add(new CardInstance(ch.Card, ch.Def));
                _events.Add(new CardSpent(t, ps.Id, ch.Card));
                ps.SchoolCooldownUntil[(int)def.School] = t + _cfg.SchoolCooldownTicks;
                ps.RecentCasts.Add(new EchoRecord { Def = ch.Def, Tick = t });
                _events.Add(new CastResolved(t, ps.Id, def.Id, ch.Tier, bp));
            }
        }

        private void ApplyEffect(PlayerId caster, EffectOp op, TargetRef target, int bp, int t)
        {
            int amount = (int)((long)op.Amount * bp / 10000);
            switch (op.Type)
            {
                case EffectOpType.Damage:
                {
                    var m = ResolveMonster(target);
                    if (m != null) { DamageMonster(m, amount, EntityId.None, t, out _); }
                    else if (target.Kind == TargetKind.Summoner) DamageSummoner(target.Player, amount, EntityId.None, t);
                    break;
                }
                case EffectOpType.Heal:
                {
                    var m = ResolveMonster(target);
                    if (m != null) m.Hp = Math.Min(m.MaxHp, m.Hp + amount);
                    else HealSummoner(op.Target == EffectTargetKind.EnemySummoner ? caster.Opponent : caster, amount, t);
                    break;
                }
                case EffectOpType.ModifyStats:
                case EffectOpType.Hex:
                {
                    // In the ability model a creature has no single ATK; stat modifiers attach as a status
                    // (a generic hook — how it modifies a creature's ability output is content/rework territory).
                    var m = ResolveMonster(target);
                    if (m != null && op.DurationTicks > 0)
                    {
                        var st = new StatusInstance { Id = "mod.dmg", Magnitude = amount, UntilTick = t + op.DurationTicks };
                        m.Statuses.Add(st);
                        _events.Add(new StatusApplied(t, m.Id, st.Id, amount, st.UntilTick));
                    }
                    break;
                }
                case EffectOpType.DrainLife:
                {
                    var m = ResolveMonster(target);
                    if (m != null)
                    {
                        DamageMonster(m, amount, EntityId.None, t, out int dealt);
                        HealSummoner(caster, dealt / 2, t);
                    }
                    break;
                }
                case EffectOpType.SummonToken:
                {
                    if (op.Ref != null && _content.TryGet(new CardDefId(op.Ref), out var tokenDef))
                        for (int i = 0; i < Math.Max(1, op.Amount); i++)
                            if (_s.Field.Count(x => x.Owner == caster) < _cfg.BoardCap)
                                SpawnMonster(caster, tokenDef, CardInstanceId.None, FieldZone.Vanguard, t);
                    break;
                }
                case EffectOpType.GrantStatus:
                    break; // reserved (Reactions land in M8)
            }
        }

        private MonsterInstance? ResolveMonster(TargetRef tr) =>
            tr.Kind == TargetKind.Monster ? _s.Field.FirstOrDefault(m => m.Id == tr.Monster) : null;

        // ── Phase 5: Creature Act — movement + AI + ability pipeline (combat-spec §8) ─────

        private SpatialGrid BuildGrid()
        {
            int maxRange = 0;
            foreach (var m in _s.Field)
                maxRange = Math.Max(maxRange, _content.Get(m.Def).MaxAbilityRange);
            int cell = Math.Max(300, maxRange);
            var grid = new SpatialGrid(-_cfg.ArenaWidthMu, -_cfg.ArenaHeightMu - _cfg.SummonerOffsetMu,
                                       _cfg.ArenaWidthMu * 2, (_cfg.ArenaHeightMu + _cfg.SummonerOffsetMu) * 2, cell);
            foreach (var m in _s.Field) if (m.Hp > 0) grid.Add(m.Id, m.Pos);   // Field is EntityId-sorted
            return grid;
        }

        private void CreatureAct(int t)
        {
            var grid = BuildGrid();
            var world = new AiWorld(_cfg, _content, t, _s.Field, grid, SummonerPos(PlayerId.P0), SummonerPos(PlayerId.P1));

            // snapshot the field: recall/death may mutate it; operate on live objects, skip removed ones
            foreach (var c in _s.Field.ToArray())
            {
                if (c.Hp <= 0 || !_s.Field.Contains(c)) continue;

                if (c.Recalling) { StepRecall(c, t); continue; }

                switch (c.Action)
                {
                    case ActionState.Recovering:
                        if (t >= c.ActionUntilTick) { c.Action = ActionState.Idle; goto case ActionState.Idle; }
                        break;

                    case ActionState.WindingUp:
                        if (t >= c.ActionUntilTick) ResolveAbility(c, t);   // → sets Recovering
                        break;

                    case ActionState.Moving:
                        StepMove(c);
                        if (Arrived(c)) { c.Action = ActionState.Idle; goto case ActionState.Idle; }
                        break;

                    case ActionState.Idle:
                        if (t < c.NextReevalTick) break;
                        ApplyAiDecision(c, CreatureAi.Decide(world, c), t);
                        break;
                }
            }
        }

        private void ApplyAiDecision(MonsterInstance c, AiDecision d, int t)
        {
            switch (d.Kind)
            {
                case AiKind.Commit:
                {
                    var def = _content.Get(c.Def);
                    var ab = def.Abilities[d.AbilityIndex];
                    c.Action = ActionState.WindingUp;
                    c.ActiveAbilityIndex = d.AbilityIndex;
                    c.CommittedTarget = d.Target;
                    c.ActionUntilTick = t + ab.WindupTicks;
                    FaceToward(c, TargetPoint(d.Target, c));
                    var (tk, tm, tp) = TargetTriple(d.Target, c);
                    _events.Add(new AbilityCommitted(t, c.Id, ab.Id, tk, tm, tp, ab.WindupTicks));   // the telegraph
                    break;
                }
                case AiKind.MoveTo:
                    c.Action = ActionState.Moving;
                    c.Goal = d.Goal;
                    c.NextReevalTick = t + _cfg.AiReevalTicks;
                    break;
                default:
                    c.NextReevalTick = t + _cfg.AiReevalTicks;
                    break;
            }
        }

        private void ResolveAbility(MonsterInstance c, int t)
        {
            var def = _content.Get(c.Def);
            var ab = def.Abilities[c.ActiveAbilityIndex];
            int dmg = ab.Damage;
            if (c.FaceDown) { dmg = (int)((long)dmg * _cfg.AmbushRevealBp / 10000); c.FaceDown = false; }   // Ambush first strike

            var hits = new List<HitRecord>();
            var tgt = c.CommittedTarget;

            if (tgt.Kind == TargetKind.Summoner)
            {
                int dealt = DamageSummonerWithGuard(tgt.Player, dmg, c.Id, t);
                if (def.Has(Keyword.Drain) && dealt > 0) HealSummoner(c.Owner, dealt / 2, t);
                hits.Add(new HitRecord(EntityId.None, tgt.Player, true, dealt, 0));
            }
            else
            {
                // primary point = committed monster's current pos (may have moved/died) else attacker forward
                var primary = tgt.Kind == TargetKind.Monster ? _s.Field.FirstOrDefault(m => m.Id == tgt.Monster) : null;
                FxVec2 impact = primary?.Pos ?? c.Pos;

                var victims = new List<MonsterInstance>();
                if (ab.Shape == AoeShape.Single)
                { if (primary != null) victims.Add(primary); }
                else
                {
                    int radius = ab.Shape == AoeShape.Circle ? ab.ShapeParam : ab.RangeMaxMu;
                    foreach (var id in BuildGrid().WithinRadius(impact, radius, id => IsEnemyOf(c, id)))
                    {
                        var m = _s.Field.First(x => x.Id == id);
                        if (ab.Shape == AoeShape.Cone && !InCone(c, m.Pos, ab)) continue;
                        victims.Add(m);
                    }
                }

                foreach (var v in victims)   // EntityId order (grid returns sorted; Single is one)
                {
                    DamageMonster(v, dmg, c.Id, t, out int dealt);
                    int overflow = 0;
                    if (v.Hp < 0 && def.Has(Keyword.Pierce))
                        overflow = DamageSummonerWithGuard(v.Owner, -v.Hp, c.Id, t);
                    if (def.Has(Keyword.Drain) && dealt > 0) HealSummoner(c.Owner, dealt / 2, t);
                    hits.Add(new HitRecord(v.Id, v.Owner, false, dealt, overflow));
                }
            }

            _events.Add(new AbilityResolved(t, c.Id, ab.Id, hits));
            c.CooldownUntil[c.ActiveAbilityIndex] = t + ab.CooldownTicks;
            c.Action = ActionState.Recovering;
            c.ActionUntilTick = t + ab.RecoveryTicks;
            c.ActiveAbilityIndex = -1;
        }

        private void StepMove(MonsterInstance c)
        {
            var delta = c.Goal - c.Pos;
            long distSq = delta.LenSq();
            long tol = (long)_cfg.ArriveToleranceMu * _cfg.ArriveToleranceMu;
            if (distSq <= tol) return;
            int dist = FxMath.ISqrt(distSq);
            int step = Math.Min(c.MoveSpeed, dist);
            c.Pos += delta.ScaledTo(step);
            c.Facing = delta.ScaledTo(1000);
        }

        private bool Arrived(MonsterInstance c)
        {
            long tol = (long)_cfg.ArriveToleranceMu * _cfg.ArriveToleranceMu;
            return c.Pos.DistSq(c.Goal) <= tol;
        }

        private void StepRecall(MonsterInstance c, int t)
        {
            c.Goal = SummonerPos(c.Owner);
            StepMove(c);
            if (!Arrived(c)) return;
            if (c.RecallReadyTick < 0) { c.RecallReadyTick = t + _cfg.RecallWindupTicks; return; }  // arrival → windup
            if (t < c.RecallReadyTick) return;                                                       // still winding up (killable)
            // complete: field → reserve (heals there)
            _s.Field.Remove(c);
            var ps = _s.P(c.Owner);
            ps.Reserve.Add(new ReserveInstance { Def = c.Def, Card = c.Card, Hp = Math.Max(1, c.Hp), MaxHp = c.MaxHp });
            _events.Add(new CreatureRecalled(t, c.Owner, c.Id, ps.Reserve.Count - 1));
        }

        // ── combat helpers ──
        private bool IsEnemyOf(MonsterInstance c, EntityId id)
        {
            var m = _s.Field.FirstOrDefault(x => x.Id == id);
            return m != null && m.Owner != c.Owner && m.Hp > 0;
        }

        private bool InCone(MonsterInstance c, FxVec2 point, AbilityDef ab)
        {
            var toPoint = point - c.Pos;
            if (toPoint.LenSq() == 0) return true;
            long dot = c.Facing.ScaledTo(1000).Dot(toPoint.ScaledTo(1000));   // both ~1000-scaled
            return dot >= (long)ab.ShapeParam * 1000;   // ShapeParam = cos(halfAngle)×1000 threshold
        }

        private void FaceToward(MonsterInstance c, FxVec2 point)
        {
            var d = point - c.Pos;
            if (d.LenSq() != 0) c.Facing = d.ScaledTo(1000);
        }

        private FxVec2 TargetPoint(TargetRef tr, MonsterInstance c) => tr.Kind switch
        {
            TargetKind.Monster => _s.Field.FirstOrDefault(m => m.Id == tr.Monster)?.Pos ?? c.Pos,
            TargetKind.Summoner => SummonerPos(tr.Player),
            _ => c.Pos,
        };

        private (TargetKind, EntityId, PlayerId) TargetTriple(TargetRef tr, MonsterInstance c) => tr.Kind switch
        {
            TargetKind.Monster => (TargetKind.Monster, tr.Monster, c.Owner.Opponent),
            TargetKind.Summoner => (TargetKind.Summoner, EntityId.None, tr.Player),
            _ => (TargetKind.None, EntityId.None, c.Owner.Opponent),
        };

        /// <summary>Face damage, reduced by an active Guard scaled to CURRENT life (danger-scaled defense).
        /// Abilities resolve sequentially in the ability pipeline, so current life is the correct basis.</summary>
        private int DamageSummonerWithGuard(PlayerId target, int raw, EntityId source, int t)
        {
            var ps = _s.P(target);
            int dealt = raw;
            if (ps.Guard is GuardState g && t >= g.ActiveFromTick && t < g.UntilTick)
            {
                int missing = _cfg.LifeMax - ps.Life;
                int blockBp = Math.Min(_cfg.GuardCapBp, _cfg.GuardBaseBp + missing * _cfg.GuardBpPerMissingLife);
                dealt = (int)((long)raw * (10000 - blockBp) / 10000);
            }
            DamageSummoner(target, dealt, source, t);
            // channel interruption: summoner damage breaks Script/Ritual (spec §6.5, ⚑ approved)
            if (dealt > 0 && ps.Channel != null && ps.Channel.Tier >= CastTier.Script)
                BreakChannel(ps, ChannelBreakReason.Interrupted, t);
            return dealt;
        }

        private void DamageSummoner(PlayerId target, int amount, EntityId source, int t)
        {
            if (amount <= 0) return;
            var ps = _s.P(target);
            ps.Life -= amount;
            _events.Add(new SummonerDamaged(t, target, amount, ps.Life, source));
        }

        private void HealSummoner(PlayerId target, int amount, int t)
        {
            if (amount <= 0) return;
            var ps = _s.P(target);
            int before = ps.Life;
            ps.Life = Math.Min(_cfg.LifeMax, ps.Life + amount);
            if (ps.Life != before) _events.Add(new SummonerHealed(t, target, ps.Life - before, ps.Life));
        }

        private void DamageMonster(MonsterInstance m, int amount, EntityId source, int t, out int dealt)
        {
            int ward = m.WardPool;
            int afterWard = Math.Max(0, amount - ward);
            m.WardPool = Math.Max(0, ward - amount);
            int hpBefore = m.Hp;
            m.Hp -= afterWard;
            dealt = Math.Min(afterWard, hpBefore);
        }

        private void BreakChannel(PlayerState ps, ChannelBreakReason reason, int t)
        {
            var ch = ps.Channel;
            if (ch == null) return;
            ps.Channel = null;
            int refund = (int)((long)ch.CostPaidMilli * _cfg.ChannelRefundBp / 10000);
            ps.EnergyMilli = Math.Min(_cfg.EnergyCapMilli, ps.EnergyMilli + refund);
            ps.Hand.Add(new CardInstance(ch.Card, ch.Def));   // interrupted spell returns to hand
            _events.Add(new ChannelBroken(t, ps.Id, reason, refund));
        }

        // ── Phase 6: deaths ───────────────────────────────────────────────────────

        private void Deaths(int t)
        {
            List<MonsterInstance>? dead = null;
            foreach (var m in _s.Field)
                if (m.Hp <= 0) (dead ??= new List<MonsterInstance>()).Add(m);
            if (dead == null) return;

            foreach (var m in dead)   // EntityId order (Field is sorted)
            {
                _s.Field.Remove(m);
                var ps = _s.P(m.Owner);
                if (!m.Card.IsNone)
                {
                    ps.Graveyard.Add(new CardInstance(m.Card, m.Def));
                    _events.Add(new CardSpent(t, m.Owner, m.Card));
                }
                _events.Add(new MonsterDied(t, m.Owner, m.Id, m.Def, EntityId.None));
                // Torrent: stored Aspect charge on... (killer attribution lands with the targeting rework;
                // charges are consumed only by M8 Reactions, so v0 defers attribution safely)
            }
        }

        // ── Phase 7: win check ────────────────────────────────────────────────────

        private void WinCheck(int t)
        {
            bool p0Dead = _s.Players[0].Life <= 0;
            bool p1Dead = _s.Players[1].Life <= 0;
            if (p0Dead && p1Dead) End(t, null, DuelEndReason.Draw);
            else if (p0Dead) End(t, PlayerId.P1, DuelEndReason.LifeZero);
            else if (p1Dead) End(t, PlayerId.P0, DuelEndReason.LifeZero);
        }

        private void End(int t, PlayerId? winner, DuelEndReason reason)
        {
            if (_s.Phase == DuelPhase.Ended) return;
            _s.Phase = DuelPhase.Ended;
            _s.Result = new DuelResult(winner, reason);
            _events.Add(new DuelEnded(t, winner, reason));
        }

        // ── State hash (spec §8) — canonical field order is the versioned contract ──

        public ulong StateHash()
        {
            var w = new HashWriter();
            w.I32(_s.Tick);
            w.I32((int)_s.Phase);
            var (rs, ri) = _s.Rng.Snapshot();
            w.U64(rs); w.U64(ri);
            w.I32(_s.NextEntityId);
            w.I32(_s.NextCardInstanceId);
            for (int p = 0; p < 2; p++)
            {
                var ps = _s.Players[p];
                w.I32(ps.Life); w.I32(ps.EnergyMilli); w.I32(ps.RegenAccumulator);
                w.I32(ps.DrawTickCounter); w.I32(ps.DrawCharges); w.B(ps.SurgeActive);
                foreach (var zone in new[] { ps.Deck, ps.Hand, ps.Graveyard })
                {
                    w.I32(zone.Count);
                    foreach (var c in zone) { w.I32(c.Id.Value); w.S(c.Def.Value); }
                }
                w.B(ps.Channel != null);
                if (ps.Channel != null)
                {
                    w.I32(ps.Channel.Card.Value); w.S(ps.Channel.Def.Value);
                    w.I32((int)ps.Channel.Tier); w.I32(ps.Channel.CompleteTick); w.I32(ps.Channel.CostPaidMilli);
                }
                w.B(ps.Guard != null);
                if (ps.Guard is GuardState g) { w.I32(g.ActiveFromTick); w.I32(g.UntilTick); }
                foreach (var cd in ps.SchoolCooldownUntil) w.I32(cd);
                w.I32(ps.RecentCasts.Count);
                foreach (var rc in ps.RecentCasts) { w.S(rc.Def.Value); w.I32(rc.Tick); }
                w.I32(ps.Reserve.Count);
                foreach (var r in ps.Reserve) { w.S(r.Def.Value); w.I32(r.Card.Value); w.I32(r.Hp); w.I32(r.MaxHp); w.I32(r.RegenCounter); }
            }
            w.I32(_s.Field.Count);
            foreach (var m in _s.Field)
            {
                w.I32(m.Id.Value); w.I32(m.Owner.Value); w.S(m.Def.Value); w.I32(m.Card.Value);
                w.I32(m.Hp); w.I32(m.MaxHp); w.I32(m.WardPool);
                w.I32((int)m.Zone); w.I32((int)m.Directive.Kind); w.B(m.FaceDown);
                w.I32(m.Pos.X); w.I32(m.Pos.Y); w.I32(m.Facing.X); w.I32(m.Facing.Y);
                w.I32(m.Goal.X); w.I32(m.Goal.Y); w.I32(m.MoveSpeed);
                w.I32((int)m.Action); w.I32(m.ActionUntilTick); w.I32(m.ActiveAbilityIndex);
                w.I32(m.NextReevalTick); w.B(m.Recalling); w.I32(m.RecallReadyTick);
                w.I32(m.CooldownUntil.Length);
                foreach (var cd in m.CooldownUntil) w.I32(cd);
                w.I32(m.Statuses.Count);
                foreach (var st in m.Statuses) { w.S(st.Id); w.I32(st.Magnitude); w.I32(st.UntilTick); }
            }
            return w.Finish();
        }

        // ── View projection (spec §9) — hidden info culled here, not downstream ────

        public DuelView View(PlayerId asSeenBy)
        {
            var me = _s.P(asSeenBy);
            var opp = _s.P(asSeenBy.Opponent);
            var hand = me.Hand.Select(c => new HandCardView(c.Id, c.Def)).ToList();
            var reserve = me.Reserve.Select((r, i) => new ReserveView(i, r.Def, r.Hp, r.MaxHp)).ToList();
            var field = _s.Field.Select(m => new MonsterView(
                m.Id, m.Owner == asSeenBy || !m.FaceDown ? m.Def : new CardDefId("hidden"),
                m.Owner, m.Hp, m.MaxHp, m.Zone, m.Directive.Kind,
                m.Directive.Kind == DirectiveKind.Attack && m.Directive.Target.Kind == TargetKind.Summoner,
                m.FaceDown, m.WardPool, m.Pos, m.Action)).ToList();
            bool canGuard = me.Guard == null &&
                            _s.Tick >= me.SchoolCooldownUntil[(int)SpellSchool.Defense] &&
                            me.EnergyMilli >= _cfg.GuardCostMilli;
            return new DuelView(_s.Tick, _s.Phase, _s.Result, asSeenBy,
                me.Life, me.EnergyMilli, me.DrawCharges,
                opp.Life, opp.EnergyMilli, opp.Hand.Count,
                hand, reserve, field, me.Channel != null, canGuard, opp.Channel != null);
        }

        // ── Replay export (spec §8; in-memory here, file IO lives outside Engine) ──

        public ReplayFile ExportReplay(DeckList[] decks) =>
            new ReplayFile(ReplayFile.CurrentSchemaVersion, _seed, decks, _journal.ToList(), _s.Tick, StateHash());
    }
}
