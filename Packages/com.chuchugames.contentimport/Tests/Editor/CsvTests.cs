using NUnit.Framework;

namespace ChuchuGames.ContentImport.Tests
{
    public class CsvTests
    {
        [Test]
        public void QuotesCommasAndNewlines()
        {
            var rows = Csv.Parse("id,line\r\nmorrow,\"Ahoy, \"\"friend\"\"\nsee you\"\r\npip,hi\n");
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("Ahoy, \"friend\"\nsee you", rows[1][1]);
            Assert.AreEqual("hi", rows[2][1]);
        }

        [Test]
        public void Records_ByHeader_CaseInsensitive_SkipBlank()
        {
            var recs = Csv.ParseRecords("﻿Id,Name\nmorrow,Captain Morrow\n\npip\n");
            Assert.AreEqual(2, recs.Count);
            Assert.AreEqual("Captain Morrow", recs[0]["name"]);
            Assert.AreEqual("", recs[1]["Name"]);
        }

        [Test]
        public void EmptyFields()
        {
            var rows = Csv.Parse("a,,c");
            CollectionAssert.AreEqual(new[] { "a", "", "c" }, rows[0]);
        }
    }
}
