using NUnit.Framework;

namespace ChuchuGames.Dialogue.Tests
{
    public class DialogueScriptTests
    {
        [Test]
        public void Parse_SpeakersExpressionsChoicesComments()
        {
            var lines = DialogueScript.Parse(
                "# intro\nBartholomew[happy]: Welcome!\nYou look tired.\nCaptain Morrow: Ahoy.\n> Welcome aboard | Go away\n", "Narrator");
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("Bartholomew", lines[0].speaker);
            Assert.AreEqual("happy", lines[0].expression);
            Assert.AreEqual("Bartholomew", lines[1].speaker, "unnamed lines keep the previous speaker");
            Assert.AreEqual("Captain Morrow", lines[2].speaker);
            Assert.AreEqual("neutral", lines[2].expression);
            CollectionAssert.AreEqual(new[] { "Welcome aboard", "Go away" }, lines[2].choices);
        }

        [Test]
        public void Parse_ColonInsideSentenceIsNotASpeaker()
        {
            var lines = DialogueScript.Parse("Note this well, my dear friend: rooms are precious.", "Edith");
            Assert.AreEqual("Edith", lines[0].speaker);
            StringAssert.StartsWith("Note this", lines[0].text);
        }

        [Test]
        public void Parse_EmptyIsEmpty() => Assert.IsEmpty(DialogueScript.Parse("  \n "));
    }

    public class DialogueRunnerTests
    {
        [Test]
        public void Advance_BlocksOnChoice_ThenFinishes()
        {
            var r = new DialogueRunner(DialogueScript.Parse("A: one\nB: two\n> yes | no\nA: three"));
            bool finished = false;
            r.Finished += () => finished = true;
            r.Start();
            Assert.AreEqual("one", r.Current.text);
            r.Advance();
            Assert.IsTrue(r.AwaitingChoice);
            r.Advance(); // ignored
            Assert.AreEqual("two", r.Current.text);
            r.Choose(1);
            Assert.AreEqual("three", r.Current.text);
            r.Advance();
            Assert.IsTrue(finished);
            CollectionAssert.AreEqual(new[] { 1 }, r.Choices);
            Assert.AreEqual("  > no", r.Log[2]);
        }

        [Test]
        public void Skip_AnswersChoicesWithFirstOption()
        {
            var r = new DialogueRunner(DialogueScript.Parse("A: q\n> a | b\nA: end"));
            r.Start();
            r.Skip();
            Assert.IsTrue(r.IsDone);
            CollectionAssert.AreEqual(new[] { 0 }, r.Choices);
        }
    }
}
