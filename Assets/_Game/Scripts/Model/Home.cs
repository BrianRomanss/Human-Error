using System;
using System.Collections.Generic;
using System.Linq;
using HumanError.Model.Content;

namespace HumanError.Model
{
    /// <summary>Home content from content.json: headlines, messages and the tool shop.</summary>
    public sealed class HomeContent
    {
        public const int NewsPerNight = 3;

        public List<NewsData> News = new List<NewsData>();
        public List<MessageData> Messages = new List<MessageData>();
        public List<UpgradeData> Upgrades = new List<UpgradeData>();

        /// <summary>Tonight's headlines: the first three whose conditions hold, in authored order.</summary>
        public List<NewsData> NewsFor(SaveData s) =>
            News.Where(n => n.after == s.last.shift && Conditions.All(n.when, s)).Take(NewsPerNight).ToList();

        /// <summary>Tonight's messages; <c>after: -1</c> ones (hearings, debts, the clinic) can arrive on any evening after a shift.</summary>
        public List<MessageData> MessagesFor(SaveData s) =>
            Messages.Where(m => (m.after == s.last.shift || m.after == AnyEvening && s.last.shift > 0) && Conditions.All(m.when, s)).ToList();

        public const int AnyEvening = -1;

        /// <summary>Tools for sale before <paramref name="shift"/> that the player doesn't own yet.</summary>
        public List<UpgradeData> ShopFor(SaveData s) =>
            Upgrades.Where(u => u.unlockShift <= s.nextShift && !s.tools.Contains(u.tool)).ToList();

        /// <summary>Tools a player could own by this shift if they bought everything (for the validator).</summary>
        public IEnumerable<string> ToolsBy(int shift) => Upgrades.Where(u => u.unlockShift <= shift).Select(u => u.tool);
    }

    public sealed class Bill
    {
        public string Id;
        public string Label;
        public int Cost;
        public bool Required;
        public bool Paid;
    }

    /// <summary>
    /// One evening at Home (GDD §4, §7.2): pay the bills, read the news and messages, buy tools, answer
    /// messages — then start the next shift. Nothing touches the save until <see cref="Leave"/>.
    /// </summary>
    public sealed class Home
    {
        public const int Rent = 40, Food = 15, Meds = 25, Hospital = 50;

        readonly Dictionary<string, int> _choices = new Dictionary<string, int>();
        readonly HashSet<string> _buying = new HashSet<string>();

        public SaveData Save { get; }
        public List<Bill> Bills { get; } = new List<Bill>();
        public List<NewsData> News { get; }
        public List<MessageData> Messages { get; }
        public List<UpgradeData> Shop { get; }

        /// <summary>The first evening of a new game has no bills yet.</summary>
        public bool HasBills => Bills.Count > 0;

        public Home(SaveData save, HomeContent content)
        {
            Save = save ?? throw new ArgumentNullException(nameof(save));
            content = content ?? new HomeContent();
            News = content.NewsFor(save);
            Messages = content.MessagesFor(save);
            Shop = content.ShopFor(save);
            if (save.last.shift > 0)
            {
                Bills.Add(new Bill { Id = "rent", Label = "Rent", Cost = Rent, Required = true, Paid = true });
                if (save.hospitalDue) Bills.Add(new Bill { Id = "hospital", Label = "Sel's hospital bill", Cost = Hospital, Required = true, Paid = true });
                Bills.Add(new Bill { Id = "food", Label = "Spore-meal", Cost = Food });
                Bills.Add(new Bill { Id = "meds", Label = "Sel's medicine", Cost = Meds });
                // Optional items start ticked while there is money for them.
                foreach (var b in Bills.Where(b => !b.Required)) b.Paid = Left >= b.Cost;
            }
        }

        public Bill Bill(string id) => Bills.FirstOrDefault(b => b.Id == id);

        public int Spent =>
            Bills.Where(b => b.Paid).Sum(b => b.Cost)
            + Shop.Where(u => _buying.Contains(u.id)).Sum(u => u.price)
            - Messages.Sum(m => ChoiceOf(m)?.shards ?? 0);

        /// <summary>Savings after tonight's ticked bills, purchases and choices (negative = in debt).</summary>
        public int Left => Save.shards - Spent;

        public bool CanToggle(Bill b) => !b.Required && (b.Paid || Left >= b.Cost);

        public bool Toggle(Bill b)
        {
            if (!CanToggle(b)) return false;
            b.Paid = !b.Paid;
            return true;
        }

        public bool IsBuying(UpgradeData u) => _buying.Contains(u.id);
        public bool CanBuy(UpgradeData u) => IsBuying(u) || Left >= u.price;

        public bool ToggleBuy(UpgradeData u)
        {
            if (!CanBuy(u)) return false;
            if (!_buying.Remove(u.id)) _buying.Add(u.id);
            return true;
        }

        public int? Choice(MessageData m) => _choices.TryGetValue(m.id, out var i) ? i : (int?)null;
        ChoiceData ChoiceOf(MessageData m) => Choice(m) is int i ? m.choices[i] : null;

        /// <summary>A choice that costs shards needs the money (counting a refund from the current choice).</summary>
        public bool CanChoose(MessageData m, int index)
        {
            if (index < 0 || index >= m.choices.Length) return false;
            int cost = -m.choices[index].shards;
            int refund = -(ChoiceOf(m)?.shards ?? 0);
            return cost <= 0 || Left + refund >= cost;
        }

        public bool Choose(MessageData m, int index)
        {
            if (!CanChoose(m, index)) return false;
            _choices[m.id] = index;
            return true;
        }

        /// <summary>Sel's health after tonight, given the ticked bills (0–3; 0 means the hospital).</summary>
        public int SelHealthTomorrow
        {
            get
            {
                if (!HasBills) return Save.selHealth;
                int h = Bill("hospital") != null ? 2 : Save.selHealth;
                bool meds = Bill("meds").Paid, food = Bill("food").Paid;
                if (!meds) h--;
                if (!food) h--;
                if (meds && food) h++;
                return Math.Max(0, Math.Min(Campaign.SelWell, h));
            }
        }

        /// <summary>Pays and applies everything, then moves the save on to the next shift.</summary>
        public void Leave()
        {
            var s = Save;
            int left = Left;
            if (HasBills)
            {
                int h = SelHealthTomorrow;
                s.medsMissed = !Bill("meds").Paid;
                s.foodMissed = !Bill("food").Paid;
                s.hospitalDue = false;
                if (h <= 0)
                {
                    // Sel collapses: tomorrow's bill includes the hospital, and Sel comes home weak.
                    s.hospitalDue = true;
                    s.Set("sel-hospitalised");
                    h = 1;
                }
                s.selHealth = h;
            }
            foreach (var u in Shop.Where(IsBuying)) if (!s.tools.Contains(u.tool)) s.tools.Add(u.tool);
            foreach (var m in Messages)
            {
                if (!(Choice(m) is int i)) continue;
                var c = m.choices[i];
                s.Set($"{m.id}:{i}");
                s.Set(c.flag);
                s.loyalty += c.loyalty;
                s.mercy += c.mercy;
            }
            s.shards = left;
            s.atHome = false;
        }
    }
}
