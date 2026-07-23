// M2 desktop slice 1 — proves the headless-engine ↔ Unity seam (architecture rule R4:
// presentation is a dumb subscriber). Runs the deterministic Conduit engine inside Unity and
// renders a live duel with IMGUI. Content here is the same GENERIC ENGINE FIXTURES as the repo
// (not designed creatures), built in code to avoid a JSON dependency in this first slice.
// Next slice: mouse control for the human player, GameObject rendering, real content loading.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.State;

namespace Conduit.Desktop
{
    public sealed class DuelBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("ConduitDuel");
            DontDestroyOnLoad(go);
            go.AddComponent<DuelBootstrap>();
        }

        private DuelConfig _cfg;
        private ContentDb _content;
        private DuelEngine _engine;
        private ulong _seed = 42;
        private readonly ulong[] _seq = { 0, 0 };
        private float _acc;
        private float _speed = 4f;          // ticks-per-frame multiplier (visual pacing)
        private const int TickHz = 20;

        private void Start() => NewDuel(_seed);

        private void NewDuel(ulong seed)
        {
            _cfg = DuelConfig.Default;
            _content = FixtureGame.BuildContent();
            var decks = new[] { FixtureGame.Deck("aggro"), FixtureGame.Deck("wall") };
            _engine = DuelEngine.Create(_cfg, _content, decks, seed);
            _seq[0] = _seq[1] = 0;
            _acc = 0;
        }

        private void Update()
        {
            if (_engine == null || _engine.Phase == DuelPhase.Ended) return;
            _acc += Time.deltaTime * TickHz * _speed;
            int steps = Mathf.Min(20, Mathf.FloorToInt(_acc));
            _acc -= steps;
            for (int i = 0; i < steps && _engine.Phase != DuelPhase.Ended; i++)
            {
                if (_engine.CurrentTick % 10 == 0)
                {
                    FeedBot(PlayerId.P0, aggro: true);
                    FeedBot(PlayerId.P1, aggro: false);
                }
                _engine.AdvanceTick();
            }
        }

        private void FeedBot(PlayerId pid, bool aggro)
        {
            foreach (var intent in FixtureGame.Bot(_content, _engine.View(pid), aggro))
                _engine.Submit(new IntentEnvelope(++_seq[pid.Value], pid, _engine.CurrentTick + 1, intent, new IntentMeta("AI")));
        }

        // ── IMGUI render (debug view) ──
        private void OnGUI()
        {
            if (_engine == null) return;
            var v0 = _engine.View(PlayerId.P0);
            var v1 = _engine.View(PlayerId.P1);

            const float pad = 20f;
            float w = Mathf.Min(640f, Screen.width - 2 * pad);
            float h = Mathf.Min(520f, Screen.height - 140f);
            var arena = new Rect((Screen.width - w) / 2f, 90f, w, h);
            GUI.Box(arena, GUIContent.none);

            // HUD
            GUI.Label(new Rect(pad, 10, Screen.width, 24),
                $"CONDUIT — M2 desktop slice   tick {_engine.CurrentTick}  ({_engine.CurrentTick / (float)_cfg.TicksPerMinute:0.0} min)   " +
                (_engine.Phase == DuelPhase.Ended ? $"ENDED: winner {_engine.Result?.Winner?.ToString() ?? "draw"}" : "running"));
            GUI.color = new Color(0.5f, 0.7f, 1f);
            GUI.Label(new Rect(pad, 34, 400, 22), $"P0 (aggro)  life {v0.YourLife}  energy {v0.YourEnergyMilli / 1000f:0.0}  hand {v0.Hand.Count}  board {v0.Mine.Count()}  reserve {v0.Reserve.Count}");
            GUI.color = new Color(1f, 0.55f, 0.5f);
            GUI.Label(new Rect(pad, 56, 400, 22), $"P1 (wall)   life {v1.YourLife}  energy {v1.YourEnergyMilli / 1000f:0.0}  hand {v1.Hand.Count}  board {v1.Mine.Count()}  reserve {v1.Reserve.Count}");
            GUI.color = Color.white;

            // summoners at the arena ends
            DrawSummonerBar(arena, +1, v0.YourLife, _cfg.LifeMax, new Color(0.4f, 0.6f, 1f));
            DrawSummonerBar(arena, -1, v1.YourLife, _cfg.LifeMax, new Color(1f, 0.45f, 0.4f));

            // creatures
            foreach (var m in v0.Field)
                DrawCreature(arena, m, m.Owner == PlayerId.P0 ? new Color(0.3f, 0.55f, 1f) : new Color(1f, 0.4f, 0.35f));

            // controls
            if (GUI.Button(new Rect(pad, arena.yMax + 12, 90, 26), "Restart")) NewDuel(++_seed);
            GUI.Label(new Rect(pad + 100, arena.yMax + 14, 120, 24), $"speed {_speed:0}x");
            if (GUI.Button(new Rect(pad + 190, arena.yMax + 12, 30, 26), "-")) _speed = Mathf.Max(1, _speed - 1);
            if (GUI.Button(new Rect(pad + 224, arena.yMax + 12, 30, 26), "+")) _speed = Mathf.Min(20, _speed + 1);
        }

        private Vector2 ToScreen(Rect arena, FxVec2 pos)
        {
            // arena mu x[-2000,2000] y[-1700,1700] → screen rect (y flipped)
            float fx = Mathf.InverseLerp(-2000f, 2000f, pos.X);
            float fy = Mathf.InverseLerp(1700f, -1700f, pos.Y);
            return new Vector2(arena.x + fx * arena.width, arena.y + fy * arena.height);
        }

        private void DrawCreature(Rect arena, MonsterView m, Color c)
        {
            var p = ToScreen(arena, m.Pos);
            var box = new Rect(p.x - 9, p.y - 9, 18, 18);
            GUI.color = c;
            GUI.Box(box, GUIContent.none);
            GUI.color = Color.white;
            // hp bar
            float frac = m.MaxHp > 0 ? Mathf.Clamp01(m.Hp / (float)m.MaxHp) : 0f;
            GUI.color = Color.Lerp(Color.red, Color.green, frac);
            GUI.DrawTexture(new Rect(p.x - 10, p.y - 14, 20 * frac, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawSummonerBar(Rect arena, int sign, int life, int max, Color c)
        {
            float y = sign > 0 ? arena.yMax - 8 : arena.y + 2;
            float frac = Mathf.Clamp01(life / (float)max);
            GUI.color = c;
            GUI.DrawTexture(new Rect(arena.center.x - 60, y, 120 * frac, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
