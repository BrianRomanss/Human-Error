using System.Collections.Generic;
using System.Linq;
using HumanError.Model.Content;
using UnityEngine;

namespace HumanError.Data
{
    /// <summary>All questions, the ordered shifts and the Home content: the game's entry point to content.</summary>
    [CreateAssetMenu(menuName = "Human Error/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalogSO : ScriptableObject
    {
        public QuestionSO[] questions = new QuestionSO[0];
        public ShiftSO[] shifts = new ShiftSO[0];
        /// <summary>Home content (GDD §7.2): headlines, messages and the tool shop.</summary>
        public NewsData[] news = new NewsData[0];
        public MessageData[] messages = new MessageData[0];
        public UpgradeData[] upgrades = new UpgradeData[0];
        /// <summary>The guided first night: scripted visitors and spotlight tips.</summary>
        public TutorialData tutorial = new TutorialData();
        /// <summary>Recurring story characters and their visits (GDD §6).</summary>
        public StoryData[] story = new StoryData[0];

        public ContentFile ToFile() => new ContentFile
        {
            questions = questions.Where(q => q != null).Select(q => q.data).ToArray(),
            shifts = shifts.Where(s => s != null).Select(s => s.data).ToArray(),
            news = news,
            messages = messages,
            upgrades = upgrades,
            tutorial = tutorial,
            story = story,
        };

        /// <summary>Playable shifts in order. Content errors are logged and also returned.</summary>
        public List<ShiftPlan> Load(out List<string> errors) => LoadGame(out errors).Shifts;

        /// <summary>Shifts and Home content. Content errors are logged and also returned.</summary>
        public GameContent LoadGame(out List<string> errors)
        {
            var game = ContentFactory.LoadGame(ToFile(), out errors);
            foreach (var e in errors) Debug.LogError($"[Content] {e}", this);
            return game;
        }
    }
}
