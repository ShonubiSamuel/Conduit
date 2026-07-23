// M2 desktop slice 2 — mouse/human control. The human commands P0 with the mouse; P1 is the AI wall.
// Interaction follows the game's COMMANDER model (combat-model §2): you summon and issue high-level
// DIRECTIVES (attack / defend / hold / retreat / recall) — the creature's own AI handles tactics.
// Legacy Input Manager + IMGUI (verified: activeInputHandler=0). Content = generic engine fixtures.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.State;
// Unity 6 ships UnityEngine.EntityId (ECS); disambiguate to the engine's type.
using EntityId = Conduit.Engine.Core.EntityId;
using CardInstanceId = Conduit.Engine.Core.CardInstanceId;

namespace Conduit.Desktop
{
    // Runs two ways: (1) it's a component on the "ConduitDuel" GameObject in Assets/Scenes/Duel.unity,
    // or (2) if you press Play in any scene without it, the guarded Boot below spawns one. Either way
    // there is exactly one instance.
    public sealed class DuelBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<DuelBootstrap>() != null) return;   // scene already has one
            new GameObject("ConduitDuel").AddComponent<DuelBootstrap>();
        }

        private DuelConfig _cfg;
        private ContentDb _content;
        private DuelEngine _engine;
        private ulong _seed = 42;
        private readonly ulong[] _seq = { 0, 0 };
        private float _acc;
        private float _speed = 2f;
        private bool _paused;
        private const int TickHz = 20;
        private static readonly PlayerId Human = PlayerId.P0;
        private static readonly PlayerId Ai = PlayerId.P1;

        // selection state
        private EntityId _selCreature = EntityId.None;
        private CardInstanceId _selCard = CardInstanceId.None;
        private bool _selCardIsMonster;
        private string _flash = "";
        private float _flashUntil;

        private void Start() => NewDuel(_seed);

        private void NewDuel(ulong seed)
        {
            _cfg = DuelConfig.Default;
            _content = FixtureGame.BuildContent();
            var decks = new[] { FixtureGame.Deck("aggro"), FixtureGame.Deck("wall") };
            _engine = DuelEngine.Create(_cfg, _content, decks, seed);
            _seq[0] = _seq[1] = 0;
            _acc = 0; _paused = false;
            _selCreature = EntityId.None; _selCard = CardInstanceId.None;
        }

        private void Update()
        {
            if (_engine == null || _engine.Phase == DuelPhase.Ended || _paused) return;
            _acc += Time.deltaTime * TickHz * _speed;
            int steps = Mathf.Min(20, Mathf.FloorToInt(_acc));
            _acc -= steps;
            for (int i = 0; i < steps && _engine.Phase != DuelPhase.Ended; i++)
            {
                if (_engine.CurrentTick % 10 == 0)   // AI opponent thinks; the human acts via mouse
                    FeedAi();
                _engine.AdvanceTick();
            }
            // clear a stale selection if the creature died
            if (!_selCreature.IsNone && !_engine.View(Human).Mine.Any(m => m.Id.Value == _selCreature.Value))
                _selCreature = EntityId.None;
        }

        private void FeedAi()
        {
            foreach (var intent in FixtureGame.Bot(_content, _engine.View(Ai), aggro: false))
                _engine.Submit(new IntentEnvelope(++_seq[Ai.Value], Ai, _engine.CurrentTick + 1, intent, new IntentMeta("AI")));
        }

        private void Submit(Intent intent)
            => _engine.Submit(new IntentEnvelope(++_seq[Human.Value], Human, _engine.CurrentTick + 1, intent, new IntentMeta("Human")));

        private void Flash(string msg) { _flash = msg; _flashUntil = Time.time + 2.5f; }

        // ─────────────────────────────── UI ───────────────────────────────
        private void OnGUI()
        {
            if (_engine == null) return;
            var vh = _engine.View(Human);
            var va = _engine.View(Ai);

            float top = 78f, handH = 92f, toolH = 40f, pad = 16f;
            float aw = Mathf.Min(720f, Screen.width - 2 * pad);
            float ay = top + 44f;
            float ah = Mathf.Max(240f, Screen.height - ay - handH - toolH - 24f);
            var arena = new Rect((Screen.width - aw) / 2f, ay, aw, ah);

            DrawTopHud(vh, va, arena);
            GUI.Box(arena, GUIContent.none);

            // enemy summoner strip (top of arena) — click while a creature is selected → attack face
            var enemyStrip = new Rect(arena.x, arena.y, arena.width, 26f);
            DrawSummonerStrip(enemyStrip, va.YourLife, _cfg.LifeMax, new Color(1f, 0.45f, 0.4f), "ENEMY SUMMONER");
            if (GUI.Button(enemyStrip, GUIContent.none, GUIStyle.none) && !_selCreature.IsNone)
            { Submit(new CommandMonster(_selCreature, Directive.AttackOf(TargetRef.OfSummoner(Ai)))); Flash("→ attack enemy summoner"); }

            // own summoner strip (bottom of arena) — click while a creature is selected → defend me
            var ownStrip = new Rect(arena.x, arena.yMax - 26f, arena.width, 26f);
            DrawSummonerStrip(ownStrip, vh.YourLife, _cfg.LifeMax, new Color(0.45f, 0.65f, 1f), "YOUR SUMMONER");
            if (GUI.Button(ownStrip, GUIContent.none, GUIStyle.none) && !_selCreature.IsNone)
            { Submit(new CommandMonster(_selCreature, Directive.Defend(TargetRef.OfSummoner(Human)))); Flash("→ defend you"); }

            // summon zone bands (only while a monster card is selected)
            if (!_selCard.IsNone && _selCardIsMonster) DrawZoneBands(arena);

            // creatures (clickable)
            foreach (var m in vh.Field) DrawCreatureButton(arena, m);

            DrawToolbar(vh, new Rect(arena.x, arena.yMax + 8f, arena.width, toolH));
            DrawHand(vh, new Rect(arena.x, Screen.height - handH - 6f, arena.width, handH));

            if (_engine.Phase == DuelPhase.Ended)
                DrawCenterBanner(_engine.Result?.Winner is PlayerId w
                    ? (w.Value == Human.Value ? "YOU WIN" : "YOU LOSE")
                    : "DRAW");
        }

        private void DrawTopHud(DuelView vh, DuelView va, Rect arena)
        {
            GUI.Label(new Rect(16, 8, Screen.width, 22),
                $"CONDUIT — M2 human control    tick {_engine.CurrentTick} ({_engine.CurrentTick / (float)_cfg.TicksPerMinute:0.0} min)    " +
                (_paused ? "PAUSED" : "running"));
            GUI.color = new Color(0.6f, 0.75f, 1f);
            GUI.Label(new Rect(16, 30, 460, 20), $"YOU  life {vh.YourLife}  energy {vh.YourEnergyMilli / 1000f:0.0}  draws {vh.YourDrawCharges}  board {vh.Mine.Count()}  reserve {vh.Reserve.Count}");
            GUI.color = new Color(1f, 0.6f, 0.55f);
            GUI.Label(new Rect(16, 50, 460, 20), $"AI   life {va.YourLife}  board {va.Mine.Count()}");
            GUI.color = Color.white;

            // right-aligned controls
            float x = arena.xMax - 300;
            if (GUI.Button(new Rect(x, 30, 70, 24), _paused ? "Play" : "Pause")) _paused = !_paused;
            GUI.Label(new Rect(x + 78, 32, 60, 22), $"{_speed:0}x");
            if (GUI.Button(new Rect(x + 120, 30, 26, 24), "-")) _speed = Mathf.Max(1, _speed - 1);
            if (GUI.Button(new Rect(x + 148, 30, 26, 24), "+")) _speed = Mathf.Min(16, _speed + 1);
            if (GUI.Button(new Rect(x + 182, 30, 74, 24), "Restart")) NewDuel(++_seed);

            if (Time.time < _flashUntil)
            { GUI.color = new Color(1f, 0.95f, 0.6f); GUI.Label(new Rect(arena.x, arena.yMax + 46, arena.width, 20), _flash); GUI.color = Color.white; }
        }

        private void DrawToolbar(DuelView vh, Rect r)
        {
            if (!_selCreature.IsNone)
            {
                var m = vh.Mine.FirstOrDefault(x => x.Id.Value == _selCreature.Value);
                if (m != null)
                {
                    GUI.Label(new Rect(r.x, r.y, 220, r.height), $"Selected {Short(m.Def)}  hp {m.Hp}/{m.MaxHp}  [{m.Directive}]");
                    float bx = r.x + 230;
                    if (Btn(ref bx, r.y, 78, "Attack")) { Submit(new CommandMonster(_selCreature, Directive.AttackOf(TargetRef.OfSummoner(Ai)))); Flash("→ push enemy summoner"); }
                    if (Btn(ref bx, r.y, 74, "Defend")) { Submit(new CommandMonster(_selCreature, Directive.Defend(TargetRef.OfSummoner(Human)))); Flash("→ defend you"); }
                    if (Btn(ref bx, r.y, 64, "Hold"))   { Submit(new CommandMonster(_selCreature, Directive.Hold(FieldZone.Vanguard))); Flash("→ hold vanguard"); }
                    if (Btn(ref bx, r.y, 74, "Retreat")){ Submit(new CommandMonster(_selCreature, Directive.Retreat)); Flash("→ retreat"); }
                    if (Btn(ref bx, r.y, 70, "Recall")) { Submit(new Recall(_selCreature)); Flash("→ recalling (heals in reserve)"); _selCreature = EntityId.None; }
                    GUI.Label(new Rect(bx + 6, r.y, 260, r.height), "…or click an enemy creature to focus it");
                }
            }
            else if (!_selCard.IsNone && !_selCardIsMonster)
            {
                GUI.Label(new Rect(r.x, r.y, 200, r.height), "Spell selected");
                float bx = r.x + 150;
                if (Btn(ref bx, r.y, 90, "Cast")) { Submit(new CastSpell(_selCard, CastTier.Sign, TargetRef.None)); Flash("cast"); _selCard = CardInstanceId.None; }
            }
            else GUI.Label(new Rect(r.x, r.y, 620, r.height), "Click a hand card to summon/cast, or click your creature to command it.");

            // always-available support actions
            float sx = r.xMax - 260;
            GUI.enabled = vh.CanGuard;
            if (Btn(ref sx, r.y, 70, "Guard")) { Submit(new Guard()); Flash("guard raised"); }
            GUI.enabled = vh.YourDrawCharges > 0;
            if (Btn(ref sx, r.y, 64, "Draw")) { Submit(new DrawCard()); Flash("drew a card"); }
            GUI.enabled = true;
            // reserve redeploy
            for (int i = 0; i < vh.Reserve.Count && i < 2; i++)
            {
                var rv = vh.Reserve[i]; int slot = rv.Slot;
                if (Btn(ref sx, r.y, 88, $"Deploy {rv.Hp}")) { Submit(new DeployReserve(slot, FieldZone.Vanguard)); Flash("redeployed from reserve"); }
            }
        }

        private void DrawHand(DuelView vh, Rect r)
        {
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 6, r.y + 2, 200, 18), "HAND");
            float x = r.x + 8, y = r.y + 22, cw = 96, ch = 60;
            foreach (var h in vh.Hand)
            {
                var def = _content.Get(h.Def);
                bool sel = _selCard.Value == h.Id.Value;
                GUI.color = sel ? new Color(1f, 0.95f, 0.6f) : (def.Kind == CardKind.Monster ? new Color(0.55f, 0.7f, 1f) : new Color(0.7f, 1f, 0.7f));
                if (GUI.Button(new Rect(x, y, cw, ch), $"{Short(h.Def)}\ncost {def.CostMilliEnergy / 1000}\n{(def.Kind == CardKind.Monster ? $"hp {def.Hp}" : "spell")}"))
                {
                    _selCard = h.Id; _selCardIsMonster = def.Kind == CardKind.Monster; _selCreature = EntityId.None;
                    Flash(_selCardIsMonster ? "click a zone band to summon" : "click Cast in the toolbar");
                }
                GUI.color = Color.white;
                x += cw + 6;
                if (x + cw > r.xMax) break;
            }
        }

        private void DrawZoneBands(Rect arena)
        {
            var zones = new[] { (FieldZone.LeftFlank, "LEFT"), (FieldZone.Vanguard, "VANGUARD"), (FieldZone.RightFlank, "RIGHT") };
            float bw = arena.width / 3f;
            for (int i = 0; i < 3; i++)
            {
                var band = new Rect(arena.x + i * bw, arena.y + 28, bw - 2, arena.height - 56);
                GUI.color = new Color(1f, 1f, 0.5f, 0.10f);
                GUI.DrawTexture(band, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 0.6f, 0.8f);
                GUI.Label(new Rect(band.x, band.center.y, band.width, 20), zones[i].Item2);
                GUI.color = Color.white;
                if (GUI.Button(band, GUIContent.none, GUIStyle.none))
                {
                    Submit(new SummonMonster(_selCard, zones[i].Item1));
                    Flash($"summon → {zones[i].Item2}");
                    _selCard = CardInstanceId.None;
                }
            }
        }

        private void DrawCreatureButton(Rect arena, MonsterView m)
        {
            var p = ToScreen(arena, m.Pos);
            var rect = new Rect(p.x - 10, p.y - 10, 20, 20);
            bool mine = m.Owner.Value == Human.Value;
            bool selected = mine && m.Id.Value == _selCreature.Value;

            if (selected) { GUI.color = new Color(1f, 0.95f, 0.4f); GUI.DrawTexture(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), Texture2D.whiteTexture); }
            GUI.color = mine ? new Color(0.3f, 0.55f, 1f) : new Color(1f, 0.4f, 0.35f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float frac = m.MaxHp > 0 ? Mathf.Clamp01(m.Hp / (float)m.MaxHp) : 0f;
            GUI.color = Color.Lerp(Color.red, Color.green, frac);
            GUI.DrawTexture(new Rect(rect.x, rect.y - 5, rect.width * frac, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                if (mine) { _selCreature = m.Id; _selCard = CardInstanceId.None; Flash($"selected {Short(m.Def)}"); }
                else if (!_selCreature.IsNone) { Submit(new CommandMonster(_selCreature, Directive.AttackOf(TargetRef.OfMonster(m.Id)))); Flash("→ attack that creature"); }
            }
        }

        private Vector2 ToScreen(Rect arena, FxVec2 pos)
        {
            float fx = Mathf.InverseLerp(-2000f, 2000f, pos.X);
            float fy = Mathf.InverseLerp(1700f, -1700f, pos.Y);   // P1 (enemy, +y) at top; you (-y) at bottom
            return new Vector2(arena.x + fx * arena.width, arena.y + fy * arena.height);
        }

        private void DrawSummonerStrip(Rect r, int life, int max, Color c, string label)
        {
            GUI.color = new Color(c.r, c.g, c.b, 0.25f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            float frac = Mathf.Clamp01(life / (float)max);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * frac, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.Label(new Rect(r.x + 6, r.y + 4, r.width, 20), $"{label}   {life}/{max}");
        }

        private void DrawCenterBanner(string text)
        {
            var r = new Rect(Screen.width / 2f - 120, Screen.height / 2f - 30, 240, 60);
            GUI.color = new Color(0, 0, 0, 0.7f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
            var s = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            GUI.Label(r, text, s);
        }

        private static bool Btn(ref float x, float y, float w, string label)
        { bool clicked = GUI.Button(new Rect(x, y, w, 26), label); x += w + 6; return clicked; }

        private static string Short(CardDefId id) => id.Value != null && id.Value.StartsWith("test.") ? id.Value.Substring(5) : id.Value;
    }
}
