using System;
using System.Collections.Generic;
using System.Text;

namespace ChuchuGames.ContentImport
{
    /// <summary>
    /// Minimal RFC 4180 CSV reader: quoted fields, doubled quotes, commas and newlines inside quotes,
    /// CRLF or LF line endings. Spreadsheet exports (Google Sheets, Excel) load as-is.
    /// </summary>
    public static class Csv
    {
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1; // BOM

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        rows.Add(row.ToArray()); row.Clear();
                        break;
                    default: field.Append(c); break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }

        /// <summary>Parses with the first row as headers; each record maps header → value ("" if missing).</summary>
        public static List<Dictionary<string, string>> ParseRecords(string text)
        {
            var rows = Parse(text);
            var records = new List<Dictionary<string, string>>();
            if (rows.Count == 0) return records;
            var headers = rows[0];
            for (int r = 1; r < rows.Count; r++)
            {
                if (rows[r].Length == 1 && rows[r][0].Length == 0) continue; // blank line
                var rec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int h = 0; h < headers.Length; h++)
                    rec[headers[h].Trim()] = h < rows[r].Length ? rows[r][h] : "";
                records.Add(rec);
            }
            return records;
        }
    }
}
