using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.UI;
using HumanError.Data;
using HumanError.Model;
using HumanError.Model.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HumanError.View
{
    /// <summary>
    /// Grey-box flow: Shift Briefing → Reception Desk (⇄ Rulebook) → Shift Report → replay or next shift, using
    /// the shifts in the ContentCatalog (content.json). Debug flags for automated runs (with --screenshot):
    /// --he-shift N, --he-seed N, --he-screen briefing|desk|rulebook|report, --he-visitor N (auto-solve earlier
    /// visitors), --he-find KIND|TELL (jump to the first such visitor), --he-ask id,id, --he-uv,
    /// --he-decide letin|away|security, --he-autoplay best|letin.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] ArtLibrarySO art;
        [SerializeField] ContentCatalogSO catalog;

        RectTransform _root;
        GameObject _screen;
        List<ShiftPlan> _plans;
        int _shiftIndex;
        ulong _seed;

        public ArtLibrarySO Art { get => art; set => art = value; }
        public ContentCatalogSO Catalog { get => catalog; set => catalog = value; }

        void Start()
        {
            Application.targetFrameRate = 60;
            EnsureEventSystem();
            Theme.Init(art);
            var canvas = UiBuild.Canvas("GameCanvas", new Vector2(1920, 1080));
            canvas.transform.SetParent(transform, false);
            _root = (RectTransform)canvas.transform;

            List<string> errors = null;
            _plans = catalog != null ? catalog.Load(out errors) : null;
            if (_plans == null || _plans.Count == 0 || errors.Count > 0)
            {
                UiBuild.Label("Error", _root, catalog == null
                        ? "No ContentCatalog assigned. Run Tools/Human Error/Import Content, then Build Scenes."
                        : "Content errors:\n" + string.Join("\n", errors ?? new List<string>()),
                    30, Palette.Coral).rectTransform.Fill(80);
                return;
            }
            if (art == null)
                UiBuild.Label("Warning", _root, "No ArtLibrary assigned — run Tools/Human Error/Generate Placeholder Art, then Build Scenes.",
                    24, Palette.Coral, TextAnchor.LowerCenter).rectTransform.Fill(20);

            _seed = ulong.TryParse(LaunchScreenshot.Arg("--he-seed"), out var s) ? s : (ulong)DateTime.Now.Ticks;
            _shiftIndex = int.TryParse(LaunchScreenshot.Arg("--he-shift"), out var n) ? Mathf.Clamp(n - 1, 0, _plans.Count - 1) : 0;
            switch (LaunchScreenshot.Arg("--he-screen"))
            {
                case "desk": FindSeed(); StartDesk(); DebugDesk(); return;
                case "rulebook": StartDesk(); OpenRulebook(); return;
                case "report": DebugReport(); return;
            }
            ShowBriefing();
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        void Show(GameObject screen)
        {
            if (_screen != null) Destroy(_screen);
            _screen = screen;
        }

        ShiftPlan Plan => _plans[_shiftIndex];
        ShiftSession NewSession() => Plan.NewSession(_seed);

        void ShowBriefing()
        {
            var session = NewSession();
            Show(Screens.Briefing(_root, session.Context, () => StartDesk(session)));
        }

        DeskScreen StartDesk(ShiftSession session = null)
        {
            session = session ?? NewSession();
            DeskScreen desk = null;
            desk = DeskScreen.Create(_root, session, new DeskScreen.Actions
            {
                ShiftOver = () => ShowReport(desk.Session),
                OpenRulebook = OpenRulebook,
            });
            Show(desk.gameObject);
            return desk;
        }

        void OpenRulebook()
        {
            var desk = _screen != null ? _screen.GetComponent<DeskScreen>() : null;
            if (desk == null) return;
            Screens.Rulebook(_root, desk.Session.Context, null);
        }

        void ShowReport(ShiftSession session)
        {
            bool last = _shiftIndex >= _plans.Count - 1;
            Show(Screens.Report(_root, session,
                replay: ShowBriefing,
                next: () =>
                {
                    _shiftIndex = last ? 0 : _shiftIndex + 1;
                    _seed = (ulong)DateTime.Now.Ticks;
                    ShowBriefing();
                },
                nextLabel: last ? "BACK TO SHIFT 1" : $"NEXT: SHIFT {Plan.Data.number + 1}"));
        }

        // ---------------------------------------------------------------- debug (automated screenshots)

        /// <summary>--he-find: if tonight's seed has no such visitor, try the next seeds until one does.</summary>
        void FindSeed()
        {
            var find = LaunchScreenshot.Arg("--he-find");
            if (string.IsNullOrEmpty(find)) return;
            for (int i = 0; i < 500; i++, _seed++)
                if (NewSession().Queue.Any(v => Matches(v, find))) return;
            Debug.LogWarning($"[GameFlow] no visitor matching {find} in 500 seeds");
        }

        static bool Matches(VisitorInstance v, string find) =>
            v.Truth.Kind.ToString() == find || v.Truth.Tells.Exists(t => t.ToString() == find);

        void DebugDesk()
        {
            var desk = _screen.GetComponent<DeskScreen>();
            var session = desk.Session;
            int skip = int.TryParse(LaunchScreenshot.Arg("--he-visitor"), out var n) ? n - 1 : 0;
            var find = LaunchScreenshot.Arg("--he-find");
            if (!string.IsNullOrEmpty(find))
            {
                // First visitor of that kind or with that tell, e.g. --he-find OddCitizen or --he-find SuitFlicker.
                skip = 0;
                for (int i = 0; i < session.Queue.Count; i++)
                {
                    if (Matches(session.Queue[i], find)) { skip = i; break; }
                }
            }
            if (skip > 0)
            {
                // Solve earlier visitors in the model, then rebuild the desk so it shows the target fresh.
                for (int i = 0; i < skip && !session.IsOver; i++) session.Decide(Judge.Best(session.Current, session.Context));
                desk = StartDesk(session);
            }
            if (LaunchScreenshot.HasFlag("--he-uv")) desk.SendMessage("ToggleUv");
            var ask = LaunchScreenshot.Arg("--he-ask");
            if (!string.IsNullOrEmpty(ask)) foreach (var id in ask.Split(',')) desk.Ask(id.Trim());
            switch (LaunchScreenshot.Arg("--he-decide"))
            {
                case "letin": desk.Decide(Decision.LetIn); break;
                case "away": desk.Decide(Decision.TurnAway); break;
                case "security": desk.Decide(Decision.Security); break;
            }
        }

        void DebugReport()
        {
            var session = NewSession();
            bool best = LaunchScreenshot.Arg("--he-autoplay") != "letin";
            int i = 0;
            while (!session.IsOver)
            {
                // "best" still makes two mistakes so the report shows hints.
                var d = Judge.Best(session.Current, session.Context);
                if (!best || i == 1 || i == 4) d = d == Decision.LetIn ? Decision.TurnAway : Decision.LetIn;
                session.Decide(d);
                i++;
            }
            ShowReport(session);
        }
    }
}
