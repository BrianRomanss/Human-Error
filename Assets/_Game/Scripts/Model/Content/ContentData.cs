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
        /// <summary>Show the truth after every decision (the training night); otherwise only the Shift Report does (GDD §7.2).</summary>
        public bool revealEach;
        /// <summary>The clock only moves when you act (the training night): no early closing.</summary>
        public bool untimed;
        /// <summary>Clan name for "invaders seen wearing X suits" on tonight's bulletin; empty = none.</summary>
        public string wanted;
    }

    /// <summary>A news headline at Home (GDD §8: 3 per shift, reacting to your choices).</summary>
    [Serializable]
    public class NewsData
    {
        /// <summary>Shown at Home after this shift (0 = the first evening of a new game).</summary>
        public int after;
        public string text;
        /// <summary>Condition tokens that must all hold (see Conditions); empty = always. Earlier entries win.</summary>
        public string[] when = new string[0];
    }

    /// <summary>A reply to a message: a small story choice (GDD §7.2 Home "story choices").</summary>
    [Serializable]
    public class ChoiceData
    {
        public string text;
        /// <summary>What the sender says back once you choose.</summary>
        public string reply;
        /// <summary>Story flag set by this choice (optional).</summary>
        public string flag;
        public int loyalty;
        public int mercy;
        /// <summary>How much more closely the Lattice watches you after this choice.</summary>
        public int suspicion;
        /// <summary>Shards gained (or spent, if negative) tonight.</summary>
        public int shards;
    }

    /// <summary>A message waiting at Home: from Sel, Supervisor Graav, the landlord, or someone unknown.</summary>
    [Serializable]
    public class MessageData
    {
        public string id;
        /// <summary>Shown at Home after this shift; -1 = any evening (when its conditions hold).</summary>
        public int after;
        public string from;
        public string text;
        public string[] when = new string[0];
        public ChoiceData[] choices = new ChoiceData[0];
    }

    /// <summary>A tool sold at Home (GDD §5.6), e.g. the UV lamp from shift 6.</summary>
    [Serializable]
    public class UpgradeData
    {
        public string id;
        public string name;
        public string text;
        public int price;
        /// <summary>First shift it can be used on: on sale at Home before that shift.</summary>
        public int unlockShift;
        /// <summary>Desk tool id it unlocks (Tools.All).</summary>
        public string tool;
    }

    /// <summary>A scripted tutorial visitor, e.g. a clumsy human who must show round pupils and say "fine".</summary>
    [Serializable]
    public class TutorialVisitorData
    {
        /// <summary>VisitorKind name.</summary>
        public string kind;
        /// <summary>Tell names the visitor must show.</summary>
        public string[] tells = new string[0];
        /// <summary>Optional Violation name for a rule-breaker, e.g. "Expired".</summary>
        public string violation;
    }

    /// <summary>One tutorial tip (see Tutorial.Targets and Tutorial.Waits).</summary>
    [Serializable]
    public class TutorialStepData
    {
        public int visitor;
        public string target;
        /// <summary>tap, ask, letin, away or security.</summary>
        public string wait;
        public string text;
    }

    /// <summary>The guided first night. shift 0 = no tutorial.</summary>
    [Serializable]
    public class TutorialData
    {
        public int shift;
        public TutorialVisitorData[] visitors = new TutorialVisitorData[0];
        public TutorialStepData[] steps = new TutorialStepData[0];
    }

    /// <summary>A fixed face from the kit (free slots 0–7, tone 0–1).</summary>
    [Serializable]
    public class LookData
    {
        public string clan;
        public int head;
        public int mouth;
        public int pattern;
        public int outfit;
        public int tone;
    }

    /// <summary>One appearance of a story character at the window.</summary>
    [Serializable]
    public class StoryVisitData
    {
        /// <summary>Visit number for flags ("mira2:letin"); 0 = the next one. Alternatives for the same visit share it.</summary>
        public int visit;
        public int shift;
        /// <summary>1-based place in the queue.</summary>
        public int position;
        /// <summary>VisitorKind name; the visitor replaces a generated one of this kind.</summary>
        public string kind;
        /// <summary>Name on tonight's papers; empty = the character's name.</summary>
        public string alias;
        /// <summary>Tonight's suit (or face); null = rolled.</summary>
        public LookData look;
        /// <summary>Humans: exactly these tells; empty = rolled by kind.</summary>
        public string[] tells = new string[0];
        public string violation;
        public string quirk;
        public string motive;
        /// <summary>Arrival line. States a mood ("I am 60% …") unless the visitor greets with small talk.</summary>
        public string greeting;
        /// <summary>What they say as you let them in, turn them away, call security or press the silent button.</summary>
        public string letin;
        public string away;
        public string security;
        public string silent;
        /// <summary>Condition tokens that must hold (e.g. "!flag:mira1:security").</summary>
        public string[] when = new string[0];
        /// <summary>Who they really are, for the reveal and the Shift Report.</summary>
        public string note;
    }

    /// <summary>A recurring character (GDD §6): Mira, Duun, the Inspector, the Twins, Window 3's couriers.</summary>
    [Serializable]
    public class StoryData
    {
        public string id;
        public string name;
        public StoryVisitData[] visits = new StoryVisitData[0];
    }

    [Serializable]
    public class ContentFile
    {
        public StoryData[] story = new StoryData[0];
        public QuestionData[] questions = new QuestionData[0];
        public ShiftData[] shifts = new ShiftData[0];
        public NewsData[] news = new NewsData[0];
        public MessageData[] messages = new MessageData[0];
        public UpgradeData[] upgrades = new UpgradeData[0];
        public TutorialData tutorial = new TutorialData();
    }
}
