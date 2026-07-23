using System.Collections.Generic;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;

namespace Conduit.Engine.State
{
    /// <summary>
    /// Read-only, hidden-information-culled projection (spec §9): what one player may know.
    /// AI policies, the desktop UI, and (later) per-client network payloads all consume this.
    /// </summary>
    public sealed record MonsterView(EntityId Id, CardDefId Def, PlayerId Owner, int Hp, int MaxHp,
                                     FieldZone Zone, DirectiveKind Directive, bool AttackingSummoner,
                                     bool FaceDown, int WardPool, FxVec2 Pos, ActionState Action);
    public sealed record HandCardView(CardInstanceId Id, CardDefId Def);
    public sealed record ReserveView(int Slot, CardDefId Def, int Hp, int MaxHp);

    public sealed record DuelView(
        int Tick,
        DuelPhase Phase,
        DuelResult? Result,
        PlayerId You,
        int YourLife, int YourEnergyMilli, int YourDrawCharges,
        int OppLife, int OppEnergyMilli, int OppHandCount,
        IReadOnlyList<HandCardView> Hand,
        IReadOnlyList<ReserveView> Reserve,        // recalled creatures available to redeploy
        IReadOnlyList<MonsterView> Field,          // both sides; opponent FaceDown defs masked
        bool YouAreChanneling,
        bool CanGuard,
        bool OppChanneling)                        // the telegraph is public
    {
        public IEnumerable<MonsterView> Mine { get { foreach (var m in Field) if (m.Owner == You) yield return m; } }
        public IEnumerable<MonsterView> Theirs { get { foreach (var m in Field) if (m.Owner != You) yield return m; } }
    }
}
