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
    /// The screen flow (GDD §7): Title → Save Slots → Home → Shift Briefing → Reception Desk (⇄ Rulebook, pause)
    /// → Shift Report → Home. The save is written on reaching Home and on leaving it.
    ///
    /// Debug flags for automated runs (with --screenshot):
    ///   --he-save-dir PATH            keep saves out of the player's folder
    ///   --he-screen title|slots|home  campaign screens; slots shows the new-game version with --he-new
    ///   --he-home-after N [--he-slot S] [--he-skip-meds]  autoplay shifts 1..N (a couple of mistakes each) and show Home
    ///   --he-screen briefing|desk|rulebook|report  a single shift with no save: --he-shift N, --he-seed N,
    ///       --he-visitor N, --he-find KIND|TELL, --he-ask id,id, --he-uv, --he-decide letin|away|security, --he-autoplay best|letin,
    ///       --he-step N (shift 1: start the tutorial at step N)
    ///   --he-screen endless [--he-level N] | daily [--he-date yyyy-MM-dd]   score modes (add --he-autoplay best for their report)
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] ArtLibrarySO art;
        [SerializeField] ContentCatalogSO catalog;

        RectTransform _root;
        GameObject _screen, _pause;
        GameContent _game;
        SaveData _save;
        /// <summary>Save slot in use; 0 = don't persist (debug runs).</summary>
        int _slot;

        // Debug single-shift runs (no save).
        int _shiftIndex;
        ulong _seed;

        public ArtLibrarySO Art { get => art; set => art = value; }
        public ContentCatalogSO Catalog { get => catalog; set => catalog = value; }

        List<ShiftPlan> Plans => _game.Shifts;

        void Start()
        {
            Application.targetFrameRate = 60;
            EnsureEventSystem();
            Theme.Init(art);
            var canvas = UiBuild.Canvas("GameCanvas", new Vector2(1920, 1080));
            canvas.transform.SetParent(transform, false);
            _root = (RectTransform)canvas.transform;

            List<string> errors = null;
            _game = catalog != null ? catalog.LoadGame(out errors) : null;
            if (_game == null || _game.Shifts.Count == 0 || errors.Count > 0)
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
            _shiftIndex = int.TryParse(LaunchScreenshot.Arg("--he-shift"), out var n) ? Mathf.Clamp(n - 1, 0, Plans.Count - 1) : 0;
            if (LaunchScreenshot.Arg("--he-home-after") != null) { DebugHome(); return; }
            switch (LaunchScreenshot.Arg("--he-screen"))
            {
                case "briefing": PlayDebugShift(); return;
                case "desk": FindSeed(); StartDesk(NewDebugSession(), ShowDebugReport); DebugDesk(); return;
                case "rulebook": StartDesk(NewDebugSession(), ShowDebugReport); OpenRulebook(); return;
                case "report": DebugReport(); return;
                case "slots": ShowSlots(LaunchScreenshot.HasFlag("--he-new")); return;
                case "endless":
                    StartEndless(int.TryParse(LaunchScreenshot.Arg("--he-level"), out var lv) ? lv : 1);
                    return;
                case "daily":
                    StartDaily(DateTime.TryParse(LaunchScreenshot.Arg("--he-date"), out var day) ? day : DateTime.Now);
                    return;
            }
            ShowTitle();
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        void Show(GameObject screen)
        {
            if (_pause != null) Destroy(_pause);
            if (_screen != null) Destroy(_screen);
            _screen = screen;
        }

        void Persist()
        {
            if (_slot > 0 && _save != null) SaveStore.Slots.Save(_slot, _save);
        }

        // ---------------------------------------------------------------- title and slots

        void ShowTitle()
        {
            int recent = SaveStore.MostRecent();
            var latest = recent > 0 ? SaveStore.Slots.Load(recent) : null;
            var scores = SaveStore.LoadScores();
            Show(MenuScreens.Title(_root, new MenuScreens.TitleActions
            {
                Continue = latest != null ? () => Resume(recent) : (Action)null,
                ContinueInfo = latest != null ? $"Slot {recent} · shift {latest.nextShift} · {Theme.Balance(latest.shards)}" : null,
                NewGame = () => ShowSlots(newGame: true),
                LoadGame = () => ShowSlots(newGame: false),
                Endless = () => StartEndless(1),
                EndlessInfo = scores.endlessBest > 0 ? $"Best {scores.endlessBest} · level {scores.endlessBestLevel}" : "Survive shift after shift",
                Daily = () => StartDaily(DateTime.Now),
                DailyInfo = scores.DailyBestFor(Modes.DayKey(DateTime.Now)) is int today && today > 0
                    ? $"Today's best {today}" : "Same visitors for everyone today",
                Quit = Application.platform == RuntimePlatform.WebGLPlayer ? (Action)null : Application.Quit,
            }));
        }

        void ShowSlots(bool newGame)
        {
            var slots = Enumerable.Range(1, SaveStore.SlotCount).Select(i => new MenuScreens.SlotInfo
            {
                Slot = i,
                Save = SaveStore.Slots.Load(i),
                LastPlayed = SaveStore.Slots.LastWritten(i),
            }).ToArray();
            Show(MenuScreens.Slots(_root, newGame, slots, slot =>
            {
                if (newGame) NewGame(slot);
                else Resume(slot);
            }, ShowTitle));
        }

        void NewGame(int slot)
        {
            _slot = slot;
            _save = Campaign.NewGame(Environment.TickCount & 0x7FFFFFFF);
            Persist();
            ShowHome();
        }

        void Resume(int slot)
        {
            var save = SaveStore.Slots.Load(slot);
            if (save == null) { ShowSlots(newGame: false); return; }
            _slot = slot;
            _save = save;
            if (_save.atHome) ShowHome();
            else PlayCampaignShift();
        }

        // ---------------------------------------------------------------- campaign

        void ShowHome()
        {
            var home = new Home(_save, _game.Home);
            var next = _game.Shift(_save.nextShift);
            Show(HomeScreen.Create(_root, home, next?.ContextWith(Campaign.ShiftSeed(_save, _save.nextShift), _save.tools),
                new HomeScreen.Actions
                {
                    StartShift = () =>
                    {
                        home.Leave();
                        Persist();
                        PlayCampaignShift();
                    },
                    Menu = ShowTitle,
                }).gameObject);
        }

        void PlayCampaignShift()
        {
            var plan = _game.Shift(_save.nextShift);
            if (plan == null) { _save.atHome = true; ShowHome(); return; }
            var session = plan.NewSession(Campaign.ShiftSeed(_save, plan.Data.number), _save.tools);
            ShowBriefing(session, finished =>
            {
                Campaign.FinishShift(_save, finished);
                Persist();
                Show(Screens.Report(_root, finished, ShowHome, "GO HOME"));
            });
        }

        // ---------------------------------------------------------------- one shift

        /// <summary>Questions on this shift's list that no earlier shift had.</summary>
        List<string> NewQuestions(ShiftContext ctx)
        {
            var seen = new HashSet<string>(Plans.Where(p => p.Data.number < ctx.ShiftNumber).SelectMany(p => p.Data.questions));
            return ctx.Questions.Where(q => !seen.Contains(q.Id)).Select(q => q.Text).ToList();
        }

        void ShowBriefing(ShiftSession session, Action<ShiftSession> finished, ScoreRun score = null)
        {
            // Shift 1's questions are all new: the briefing already explains them. Score modes skip the list.
            var fresh = session.Context.ShiftNumber > 1 && !session.Context.IsMode ? NewQuestions(session.Context) : null;
            Show(Screens.Briefing(_root, session.Context, fresh, () => StartDesk(session, finished, score)));
        }

        DeskScreen StartDesk(ShiftSession session, Action<ShiftSession> finished, ScoreRun score = null)
        {
            DeskScreen desk = null;
            desk = DeskScreen.Create(_root, session, new DeskScreen.Actions
            {
                ShiftOver = () => finished(desk.Session),
                OpenRulebook = OpenRulebook,
                OpenMenu = OpenPause,
            }, score);
            Show(desk.gameObject);
            // The guided night (content.json "tutorial"): resumes at the visitor now at the window.
            var tutorial = session.Context.IsMode ? null : _game.Shift(session.Context.ShiftNumber)?.Tutorial;
            if (tutorial != null && !session.IsOver)
            {
                int start = int.TryParse(LaunchScreenshot.Arg("--he-step"), out var step) ? step - 1 : tutorial.FirstStepFrom(session.Index);
                TutorialRunner.Attach(desk, tutorial, _root, start);
            }
            return desk;
        }

        void OpenRulebook()
        {
            var desk = _screen != null ? _screen.GetComponent<DeskScreen>() : null;
            if (desk == null) return;
            Screens.Rulebook(_root, desk.Session.Context, null);
        }

        void OpenPause()
        {
            if (_pause != null) { Destroy(_pause); return; }
            _pause = MenuScreens.Pause(_root, null, OpenRulebook, () =>
            {
                // A campaign shift restarts from its briefing on Continue; debug runs just go to the title.
                ShowTitle();
            });
        }

        // ---------------------------------------------------------------- Endless and Daily (GDD §8)

        /// <summary>Endless: generated shifts, harder each level, until the third strike.</summary>
        void StartEndless(int level)
        {
            var run = new ScoreRun { EndsOnStrikes = true };
            ulong runSeed = LaunchScreenshot.Arg("--he-seed") != null ? _seed : (ulong)DateTime.Now.Ticks;
            PlayEndless(run, runSeed, Math.Max(1, level));
        }

        void PlayEndless(ScoreRun run, ulong runSeed, int level)
        {
            var plan = Modes.Endless(_game, level, runSeed, out var errors);
            if (errors.Count > 0) { ShowModeError(errors); return; }
            var session = plan.NewSession(Modes.EndlessSeed(runSeed, level));
            void Finished(ShiftSession s)
            {
                if (run.Over)
                {
                    var scores = SaveStore.LoadScores();
                    bool best = scores.RecordEndless(run.Score, level);
                    SaveStore.SaveScores(scores);
                    string note = best ? $"Three strikes. NEW BEST: {run.Score} on level {level}!"
                        : $"Three strikes. Run over on level {level}. Best: {scores.endlessBest}.";
                    Show(Screens.Report(_root, s, ShowTitle, "BACK TO TITLE", () => StartEndless(1), "NEW RUN", run, note));
                }
                else
                {
                    string note = $"Level {level} cleared. Score {run.Score}. Strikes {run.Strikes} of {ScoreRun.StrikesAllowed}.";
                    Show(Screens.Report(_root, s, () => PlayEndless(run, runSeed, level + 1), $"NEXT: LEVEL {level + 1}",
                        () => EndEndless(run, level), "END RUN", run, note));
                }
            }
            if (LaunchScreenshot.Arg("--he-autoplay") != null) { Autoplay(session, run); Finished(session); return; }
            if (LaunchScreenshot.HasFlag("--he-desk")) StartDesk(session, Finished, run);
            else ShowBriefing(session, Finished, run);
        }

        /// <summary>Quitting a run between levels still keeps its score.</summary>
        void EndEndless(ScoreRun run, int level)
        {
            var scores = SaveStore.LoadScores();
            scores.RecordEndless(run.Score, level);
            SaveStore.SaveScores(scores);
            ShowTitle();
        }

        /// <summary>Daily: one twelve-visitor shift from today's date; everyone gets the same visitors.</summary>
        void StartDaily(DateTime date)
        {
            var plan = Modes.Daily(_game, date, out var errors);
            if (errors.Count > 0) { ShowModeError(errors); return; }
            var run = new ScoreRun();
            var session = plan.NewSession(Modes.DailySeed(date));
            void Finished(ShiftSession s)
            {
                var scores = SaveStore.LoadScores();
                string day = Modes.DayKey(date);
                bool best = scores.RecordDaily(day, run.Score);
                SaveStore.SaveScores(scores);
                string note = best ? $"NEW BEST for today: {run.Score}!" : $"Score {run.Score}. Today's best: {scores.DailyBestFor(day)}.";
                Show(Screens.Report(_root, s, ShowTitle, "BACK TO TITLE", () => StartDaily(date), "PLAY AGAIN", run, note));
            }
            if (LaunchScreenshot.Arg("--he-autoplay") != null) { Autoplay(session, run); Finished(session); return; }
            if (LaunchScreenshot.HasFlag("--he-desk")) StartDesk(session, Finished, run);
            else ShowBriefing(session, Finished, run);
        }

        void ShowModeError(List<string> errors)
        {
            Debug.LogError("[Modes] " + string.Join("\n", errors));
            ShowTitle();
        }

        // ---------------------------------------------------------------- debug (automated screenshots)

        ShiftPlan DebugPlan => Plans[_shiftIndex];
        ShiftSession NewDebugSession() => DebugPlan.NewSession(_seed, LaunchScreenshot.HasFlag("--he-uv") ? new[] { Tools.UvLamp } : null);

        void PlayDebugShift() => ShowBriefing(NewDebugSession(), ShowDebugReport);

        void ShowDebugReport(ShiftSession session)
        {
            bool last = _shiftIndex >= Plans.Count - 1;
            Show(Screens.Report(_root, session,
                () =>
                {
                    _shiftIndex = last ? 0 : _shiftIndex + 1;
                    _seed = (ulong)DateTime.Now.Ticks;
                    PlayDebugShift();
                },
                last ? "BACK TO SHIFT 1" : $"NEXT: SHIFT {DebugPlan.Data.number + 1}",
                replay: PlayDebugShift));
        }

        /// <summary>--he-find: if tonight's seed has no such visitor, try the next seeds until one does.</summary>
        void FindSeed()
        {
            var find = LaunchScreenshot.Arg("--he-find");
            if (string.IsNullOrEmpty(find)) return;
            for (int i = 0; i < 500; i++, _seed++)
                if (NewDebugSession().Queue.Any(v => Matches(v, find))) return;
            Debug.LogWarning($"[GameFlow] no visitor matching {find} in 500 seeds");
        }

        static bool Matches(VisitorInstance v, string find) =>
            v.Truth.Kind.ToString() == find || v.Truth.Tells.Exists(t => t.ToString() == find);

        void DebugDesk()
        {
            var desk = _screen.GetComponent<DeskScreen>();
            var session = desk.Session;
            int skip = int.TryParse(LaunchScreenshot.Arg("--he-visitor"), out var n) ? n - 1 : 0;
            var tutorial = DebugPlan.Tutorial;
            if (tutorial != null && int.TryParse(LaunchScreenshot.Arg("--he-step"), out var step) && step >= 1 && step <= tutorial.Steps.Count)
                skip = tutorial.Steps[step - 1].Visitor - 1;
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
                desk = StartDesk(session, ShowDebugReport);
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

        /// <summary>Plays a whole session in the model: best decisions, except two mistakes so reports show hints.</summary>
        static void Autoplay(ShiftSession session, bool best = true) => Autoplay(session, null, best);

        static void Autoplay(ShiftSession session, ScoreRun score, bool best = true)
        {
            int i = 0;
            while (!session.IsOver && (score == null || !score.Over))
            {
                var d = Judge.Best(session.Current, session.Context);
                if (!best || i == 1 || i == 4) d = d == Decision.LetIn ? Decision.TurnAway : Decision.LetIn;
                var v = session.Decide(d);
                score?.Add(v);
                i++;
            }
        }

        void DebugReport()
        {
            var session = NewDebugSession();
            Autoplay(session, LaunchScreenshot.Arg("--he-autoplay") != "letin");
            ShowDebugReport(session);
        }

        /// <summary>--he-home-after N: a fresh campaign autoplayed through N shifts, then Home.</summary>
        void DebugHome()
        {
            int after = int.TryParse(LaunchScreenshot.Arg("--he-home-after"), out var a) ? Mathf.Clamp(a, 0, Plans.Count) : 0;
            _slot = int.TryParse(LaunchScreenshot.Arg("--he-slot"), out var slot) ? Mathf.Clamp(slot, 1, SaveStore.SlotCount) : 0;
            _save = Campaign.NewGame((int)(_seed & 0x7FFFFFFF));
            for (int shift = 1; shift <= after; shift++)
            {
                var home = new Home(_save, _game.Home);
                if (LaunchScreenshot.HasFlag("--he-skip-meds") && home.HasBills) home.Toggle(home.Bill("meds"));
                home.Leave();
                var session = _game.Shift(shift).NewSession(Campaign.ShiftSeed(_save, shift), _save.tools);
                Autoplay(session);
                Campaign.FinishShift(_save, session);
            }
            Persist();
            ShowHome();
        }
    }
}
