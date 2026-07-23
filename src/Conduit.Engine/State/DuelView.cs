using System.Collections.Generic;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;

namespace Conduit.Engine.State
{
    /// <summary>
    /// Read-only, hidden-information-culled projection (spec §9): what one player may know.
    /// AI policies, the desktop UI, and (later) per-client network payloads all consume this.
    /// </summary>
    public sealed record MonsterView(EntityId Id, CardDefId Def, PlayerId Owner, int Atk, int Hp, int MaxHp,
                                     FieldZone Zone, DirectiveKind Directive, bool FaceDown, int WardPool);
    public sealed record HandCardView(CardInstanceId Id, CardDefId Def);

    public sealed record DuelView(
        int Tick,
        DuelPhase Phase,
        DuelResult? Result,
        PlayerId You,
        int YourLife, int YourEnergyMilli, int YourDrawCharges,
        int OppLife, int OppEnergyMilli, int OppHandCount,
        IReadOnlyList<HandCardView> Hand,
        IReadOnlyList<MonsterView> Field,          // both sides; opponent FaceDown defs masked
        bool YouAreChanneling,
        bool CanGuard,
        bool OppChanneling)                        // the telegraph is public
    {
        public IEnumerable<MonsterView> Mine { get { foreach (var m in Field) if (m.Owner == You) yield return m; } }
        public IEnumerable<MonsterView> Theirs { get { foreach (var m in Field) if (m.Owner != You) yield return m; } }
    }
}
