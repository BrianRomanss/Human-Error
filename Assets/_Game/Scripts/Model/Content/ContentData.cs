using System;

namespace HumanError.Model.Content
{
    // Authoring data: plain serializable fields so the same classes load from JSON (JsonUtility in Unity,
    // System.Text.Json in tests), live inside ScriptableObjects and show in the Inspector. Enums are strings
    // on purpose: readable in JSON written by hand or by an AI, and validated by ContentFactory with clear
    // error messages. Zero / empty means "use the default" (JsonUtility doesn't run field initialisers).

    /// <summary>One question (GDD §5.3, §11 QuestionSO).</summary>
    [Serializable]
    public class QuestionData
    {
        public string id;
        public string text;
        /// <summary>Greeting, Culture, Paperwork, Action, Trap, Pressure, Personal.</summary>
        public string kind;
        /// <summary>Optional: ShowHand or BreatheOnGlass.</summary>
        public string effect;
        /// <summary>Optional: the Tell a human gives away (e.g. "WrongKesh").</summary>
        public string reveals;
        public string[] citizen = new string[0];
        public string[] human = new string[0];
        public float slipChance;
        public float stress;
        public int unlockShift;
    }

    /// <summary>One shift (GDD §8, §11 ShiftSO + BulletinSO): queue mix, difficulty levers and tonight's bulletin.</summary>
    [Serializable]
    public class ShiftData
    {
        public int number;
        public string title;
        public int cycle;
        public int day;
        /// <summary>Tonight's Kesh colour; empty = picked from the seed.</summary>
        public string kesh;
        public int visitors;
        public int humans;
        public int ruleBreakers;
        public int oddCitizens;
        public float skilledShare;
        public int clumsyMinTells;
        public int clumsyMaxTells;
        public int skilledTells;
        /// <summary>0 = default (3).</summary>
        public int maxForgedFields;
        public int questionBudget;
        /// <summary>Question ids on tonight's list (at most 8).</summary>
        public string[] questions = new string[0];
        /// <summary>Tell names introduced so far; empty = all.</summary>
        public string[] tells = new string[0];
        public int[] closedFloors = new int[0];
        /// <summary>Bulletin rule ids on top of the permanent ones, e.g. "clan-stamp".</summary>
        public string[] rules = new string[0];
        /// <summary>Tool ids, e.g. "uv".</summary>
        public string[] tools = new string[0];
        public string briefing;
        public string supervisor;
    }

    [Serializable]
    public class ContentFile
    {
        public QuestionData[] questions = new QuestionData[0];
        public ShiftData[] shifts = new ShiftData[0];
    }
}
