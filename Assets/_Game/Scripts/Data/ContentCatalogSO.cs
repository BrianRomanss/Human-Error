using System.Collections.Generic;
using System.Linq;
using HumanError.Model.Content;
using UnityEngine;

namespace HumanError.Data
{
    /// <summary>All questions and the ordered shifts: the game's entry point to content.</summary>
    [CreateAssetMenu(menuName = "Human Error/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalogSO : ScriptableObject
    {
        public QuestionSO[] questions = new QuestionSO[0];
        public ShiftSO[] shifts = new ShiftSO[0];

        public ContentFile ToFile() => new ContentFile
        {
            questions = questions.Where(q => q != null).Select(q => q.data).ToArray(),
            shifts = shifts.Where(s => s != null).Select(s => s.data).ToArray(),
        };

        /// <summary>Playable shifts in order. Content errors are logged and also returned.</summary>
        public List<ShiftPlan> Load(out List<string> errors)
        {
            var plans = ContentFactory.Load(ToFile(), out errors);
            foreach (var e in errors) Debug.LogError($"[Content] {e}", this);
            return plans;
        }
    }
}
