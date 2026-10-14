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
    ///   --he-tools uv,gillscope,archive,q4  --he-silent  --he-scope  --he-lookup  --he-rulebook   desk tools in a debug shift
    ///   --he-screen settings | act [--he-act N] | ending [--he-ending 1-5]
    ///   --he-colourblind  --he-tell-assist   force those settings for a screenshot
    ///   --he-phone WxH   lay out as a phone of that size (e.g. 1080x2340), letterboxed in the window: portrait screenshots
    ///   --he-rotate      with --he-phone: turn the phone sideways after a second (tests the rebuild on rotation)
    ///   --he-store       store screenshots: hide debug-only buttons (the report's REPLAY SHIFT)
    ///   --he-demo        play as the demo edition (shifts 1–5, Endless locked); --he-screen credits
    ///
    /// Layout (M6): screens are built on a stage (see <see cref="Layout"/>) that is 1920×1080 in landscape and 1080×1920+
    /// in portrait, inside the safe area. Rotating or resizing rebuilds the current screen with its state.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] ArtLibrarySO art;
        [SerializeField] ContentCatalogSO catalog;

        RectTransform _root;
        StageCanvas _stage;
        /// <summary>The layout changed while the desk was mid-verdict: rebuild once it's free.</summary>
        bool _layoutDirty;
        GameObject _screen, _pause, _settings, _rulebook;
        /// <summary>Redraws the current screen (after a settings change that affects layout), keeping its state.</summary>
        Action _rebuild;
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
            BuildInfo.ApplyDebugFlags();
            Theme.Init(art);
            SettingsStore.Apply();
            var phone = LaunchScreenshot.Arg("--he-phone");
            if (phone != null && phone.Split('x') is var wh && wh.Length == 2 && int.TryParse(wh[0], out var pw) && int.TryParse(wh[1], out var ph))
                StageCanvas.SimulatedScreen = new Vector2Int(pw, ph);
            _stage = StageCanvas.Create("GameCanvas", transform, Palette.Ink);
            _root = _stage.Stage;
            Layout.From(_stage);
            _stage.LayoutChanged += () => _layoutDirty = true;
            // --he-rotate: turn the simulated phone sideways after a second, to exercise the relayout path.
            if (LaunchScreenshot.HasFlag("--he-rotate") && StageCanvas.SimulatedScreen.x > 0)
                Tween.Delay(this, 1f, () => StageCanvas.SimulatedScreen = new Vector2Int(StageCanvas.SimulatedScreen.y, StageCanvas.SimulatedScreen.x), "rotate");

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
                case "settings": ShowTitle(); OpenSettings(); return;
                case "credits": ShowCredits(); return;
                case "ending": DebugEnding(); return;
                case "act":
                    int debugAct = int.TryParse(LaunchScreenshot.Arg("--he-act"), out var act) ? act : 2;
                    void DebugAct() => Show(Screens.ActCard(_root, debugAct, ShowTitle), DebugAct);
                    DebugAct();
                    return;
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

        void Show(GameObject screen, Action rebuild = null)
        {
            // Destroy waits for the end of the frame: clear the fields too, so a relayout can reopen overlays at once.
            if (_pause != null) Destroy(_pause);
            if (_settings != null) Destroy(_settings);
            if (_rulebook != null) Destroy(_rulebook);
            if (_screen != null) Destroy(_screen);
            _pause = _settings = _rulebook = null;
            _screen = screen;
            _rebuild = rebuild;
        }

        void Update()
        {
            if (!_layoutDirty) return;
            // Don't pull the desk out from under a verdict animation: wait for the next visitor.
            var desk = _screen != null ? _screen.GetComponent<DeskScreen>() : null;
            if (desk != null && desk.Busy) return;
            _layoutDirty = false;
            Relayout();
        }

        /// <summary>The phone rotated or the window changed shape: redraw the current screen and any open overlay.</summary>
        void Relayout()
        {
            Layout.From(_stage);
            bool settings = _settings != null, pause = _pause != null, rulebook = _rulebook != null;
            _rebuild?.Invoke();
            if (pause) OpenPause();
            if (rulebook) OpenRulebook();
            if (settings) OpenSettings();
        }

        /// <summary>Crossfades to one music loop; the desk adds the tension layer on top (GDD §10).</summary>
        static void Music(string track, bool tension = false)
        {
            AudioPlayer.PlayLayer(track, Theme.Clip(track), 1f);
            if (tension) AudioPlayer.PlayLayer("tension", Theme.Clip("music_tension"), 0.15f);
            AudioPlayer.OnlyLayers(tension ? new[] { track, "tension" } : new[] { track });
        }

        void OpenSettings()
        {
            if (_settings != null) return;
            string layout = SettingsStore.Current.LayoutKey;
            _settings = MenuScreens.Settings(_root, SettingsStore.Current, s =>
            {
                string before = SettingsStore.Current.LayoutKey;
                SettingsStore.Save(s);
                if (before == s.LayoutKey) return;
                // Text size and palette are baked into widgets: redraw the settings card at once.
                Destroy(_settings);
                _settings = null;
                OpenSettings();
            }, () =>
            {
                _settings = null;
                if (SettingsStore.Current.LayoutKey != layout) _rebuild?.Invoke();
                var desk = _screen != null ? _screen.GetComponent<DeskScreen>() : null;
                if (desk != null) desk.TimerOff = SettingsStore.Current.timerOff;
            });
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
                ContinueInfo = latest == null ? null : latest.Finished ? $"Slot {recent} · {Endings.Title(latest.Ending)}"
                    : $"Slot {recent} · shift {latest.nextShift} · {Theme.Balance(latest.shards)}",
                NewGame = () => ShowSlots(newGame: true),
                LoadGame = () => ShowSlots(newGame: false),
                Endless = Edition.EndlessUnlocked ? () => StartEndless(1) : (Action)null,
                EndlessInfo = !Edition.EndlessUnlocked ? "In the full game"
                    : scores.endlessBest > 0 ? $"Best {scores.endlessBest} · level {scores.endlessBestLevel}" : "Survive shift after shift",
                Credits = ShowCredits,
                Daily = () => StartDaily(DateTime.Now),
                Settings = OpenSettings,
                BuildInfo = Edition.IsDemo ? $"DEMO · shifts 1–{Edition.DemoShifts} · {View.BuildInfo.Stamp}"
                    : $"Story shifts 1–{Plans.Count} · Endless · Daily · {View.BuildInfo.Stamp}",
                DailyInfo = scores.DailyBestFor(Modes.DayKey(DateTime.Now)) is int today && today > 0
                    ? $"Today's best {today}" : "Same visitors for everyone today",
                Quit = Application.platform == RuntimePlatform.WebGLPlayer || Application.isMobilePlatform ? (Action)null : Application.Quit,
            }), ShowTitle);
            Music("music_title");
        }

        static Edition Edition => View.BuildInfo.Edition;

        void ShowCredits()
        {
            Show(MenuScreens.Credits(_root, View.BuildInfo.Stamp, Edition.Name, ShowTitle), ShowCredits);
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
            }, ShowTitle), () => ShowSlots(newGame));
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
            if (_save.Finished) ShowEnding();
            else if (_save.atHome) ShowHome();
            else PlayCampaignShift();
        }

        // ---------------------------------------------------------------- campaign

        void ShowHome() => ShowHome(new Home(_save, _game.Home));

        /// <summary>Home keeps tonight's ticks and choices across a redraw (settings), so it's built from an existing evening.</summary>
        void ShowHome(Home home)
        {
            // The demo stops at Home after its last shift, with a note about the full game.
            var next = Edition.CanPlay(_save.nextShift) ? _game.Shift(_save.nextShift) : null;
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
                    EndLabel = Edition.DemoFinished(_save) ? "END OF THE DEMO" : null,
                    EndNote = Edition.DemoFinished(_save)
                        ? $"Thank you for playing! The full game has {LastShift} shifts, five endings, Endless and Daily."
                        : null,
                }).gameObject, () => ShowHome(home));
            Music("music_home");
        }

        void PlayCampaignShift()
        {
            var plan = Edition.CanPlay(_save.nextShift) ? _game.Shift(_save.nextShift) : null;
            if (plan == null) { _save.atHome = true; ShowHome(); return; }
            var session = plan.NewSession(Campaign.ShiftSeed(_save, plan.Data.number), _save.tools, _save);
            void Briefing() => ShowBriefing(session, finished =>
            {
                Campaign.FinishShift(_save, finished, LastShift);
                Persist();
                // The last shift, or an arrest at an Act 3 hearing, ends the story (GDD §3 endings).
                if (_save.Finished) ShowReport(finished, ShowEnding, "WHAT HAPPENED NEXT");
                else ShowReport(finished, ShowHome, "GO HOME");
            });
            // A new act opens with its title card (GDD §3 narrative arc).
            int n = plan.Data.number;
            if (n == 1 || Acts.For(n) != Acts.For(n - 1))
            {
                void Card() => Show(Screens.ActCard(_root, Acts.For(n), Briefing), Card);
                Card();
                Music("music_title");
            }
            else Briefing();
        }

        int LastShift => Plans.Max(p => p.Data.number);

        void ShowEnding()
        {
            Show(Screens.Ending(_root, _save, ShowTitle), ShowEnding);
            Music("music_title");
        }

        void ShowReport(ShiftSession s, Action next, string nextLabel, Action replay = null, string replayLabel = "REPLAY SHIFT",
            ScoreRun score = null, string note = null)
        {
            Show(Screens.Report(_root, s, next, nextLabel, replay, replayLabel, score, note),
                () => ShowReport(s, next, nextLabel, replay, replayLabel, score, note));
            Music("music_desk");
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
            Show(Screens.Briefing(_root, session.Context, fresh, () => StartDesk(session, finished, score)),
                () => ShowBriefing(session, finished, score));
            Music("music_desk");
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
            desk.ClockHeld = () => _pause != null || _settings != null;
            desk.TimerOff = SettingsStore.Current.timerOff;
            Show(desk.gameObject, () => StartDesk(desk.Session, finished, score));
            Music("music_desk", tension: true);
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
            var ctx = desk.Session.Context;
            // Last night's rulebook, so tonight's new lines can be flagged (not in score modes).
            var previous = ctx.IsMode ? null : _game.Shift(ctx.ShiftNumber - 1)?.ContextWith(1, ctx.Tools);
            if (_rulebook != null) Destroy(_rulebook);
            _rulebook = Screens.Rulebook(_root, ctx, null, previous);
        }

        void OpenPause()
        {
            if (_pause != null) { Destroy(_pause); return; }
            if (_screen == null || _screen.GetComponent<DeskScreen>() == null) return;
            _pause = MenuScreens.Pause(_root, null, OpenRulebook, OpenSettings, () =>
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
                    ShowReport(s, ShowTitle, "BACK TO TITLE", () => StartEndless(1), "NEW RUN", run, note);
                }
                else
                {
                    string note = $"Level {level} cleared. Score {run.Score}. Strikes {run.Strikes} of {ScoreRun.StrikesAllowed}.";
                    ShowReport(s, () => PlayEndless(run, runSeed, level + 1), $"NEXT: LEVEL {level + 1}",
                        () => EndEndless(run, level), "END RUN", run, note);
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
            var plan = Modes.Daily(_game, date, out var errors, Edition.LastDailyBase);
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
                ShowReport(s, ShowTitle, "BACK TO TITLE", () => StartDaily(date), "PLAY AGAIN", run, note);
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

        /// <summary>--he-tools uv,gillscope,q4 (or --he-uv) and --he-silent: a debug shift with those tools and the W3 button.</summary>
        ShiftSession NewDebugSession()
        {
            var tools = (LaunchScreenshot.Arg("--he-tools") ?? "").Split(',').Where(t => t.Length > 0).ToList();
            if (LaunchScreenshot.HasFlag("--he-uv")) tools.Add(Tools.UvLamp);
            SaveData save = null;
            if (LaunchScreenshot.HasFlag("--he-silent")) { save = new SaveData(); save.Set(Campaign.SilentFlag); }
            return DebugPlan.NewSession(_seed, tools, save);
        }

        void PlayDebugShift() => ShowBriefing(NewDebugSession(), ShowDebugReport);

        void ShowDebugReport(ShiftSession session)
        {
            bool last = _shiftIndex >= Plans.Count - 1;
            ShowReport(session,
                () =>
                {
                    _shiftIndex = last ? 0 : _shiftIndex + 1;
                    _seed = (ulong)DateTime.Now.Ticks;
                    PlayDebugShift();
                },
                last ? "BACK TO SHIFT 1" : $"NEXT: SHIFT {DebugPlan.Data.number + 1}",
                replay: LaunchScreenshot.HasFlag("--he-store") ? (Action)null : PlayDebugShift);
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
            if (LaunchScreenshot.HasFlag("--he-scope")) desk.SendMessage("ToggleScope");
            if (LaunchScreenshot.HasFlag("--he-lookup")) desk.SendMessage("LookUp");
            if (LaunchScreenshot.HasFlag("--he-rulebook")) OpenRulebook();
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

        /// <summary>--he-screen ending [--he-ending 1-5]: a finished campaign's ending screen.</summary>
        void DebugEnding()
        {
            _save = Campaign.NewGame(1);
            _save.nextShift = LastShift + 1;
            _save.humansCaught = 41;
            _save.humansLetIn = 6;
            _save.humansHidden = 9;
            _save.loyalty = 30;
            _save.mercy = 15;
            _save.Set("mira2:letin");
            _save.Set("mira3:letin");
            _save.ending = int.TryParse(LaunchScreenshot.Arg("--he-ending"), out var e) ? Mathf.Clamp(e, 1, 5) : 1;
            ShowEnding();
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
                var session = _game.Shift(shift).NewSession(Campaign.ShiftSeed(_save, shift), _save.tools, _save);
                Autoplay(session);
                Campaign.FinishShift(_save, session, LastShift);
                if (_save.Finished) break;
            }
            Persist();
            if (_save.Finished) ShowEnding();
            else ShowHome();
        }
    }
}
