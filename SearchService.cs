using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VupisjkaWpf.Models;

namespace VupisjkaWpf.Services
{
    public static class SearchService
    {
        private const string RUS_OK = "АБВГДЕЖЗИКЛМНОПРСТУФХЦЧШЩЫЭЮЯ";

        private static readonly Regex RePersonalFull =
            new($@"^([{RUS_OK}]{{1,2}})-(\d{{6}})$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ReSection =
            new(@"§\s*(\d+)", RegexOptions.Compiled);

        private static readonly Regex RePoint =
            new(@"(?:(?:пункт|п\.)\s*(\d+)[\.\)]\s*)|^(?:\s*)(\d+)[\.\)]\s*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxDate =
            new(@"(\d{2}\.\d{2}\.\d{4})|(\d{4}[-_.](\d{1,2})[-_.](\d{1,2}))|(\d{1,2}[._-](\d{1,2})[._-](\d{2,4}))",
                RegexOptions.Compiled);

        private static readonly Regex RxNo =
            new(@"№\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string[] CustomKeywords { get; set; } = Array.Empty<string>();
        public static bool CustomCaseSensitive { get; set; } = false;

        public static bool TryNormalizePersonal(string raw, out string normalized)
        {
            raw = (raw ?? string.Empty).Trim();

            var m = RePersonalFull.Match(raw);
            if (m.Success)
            {
                normalized = $"{m.Groups[1].Value.ToUpperInvariant()}-{m.Groups[2].Value}";
                return true;
            }

            var mDigits = Regex.Match(raw, @"^(\d{6})$");
            if (mDigits.Success)
            {
                normalized = mDigits.Groups[1].Value;
                return true;
            }

            normalized = string.Empty;
            return false;
        }

        public static IEnumerable<int> FindPersonalHits(IReadOnlyList<string> lines, string normalizedCode)
        {
            if (string.IsNullOrWhiteSpace(normalizedCode))
                yield break;

            Regex patt;
            if (normalizedCode.Contains('-'))
            {
                var parts = normalizedCode.Split('-');
                string letters = parts[0];
                string digits = parts[1];

                patt = new Regex(@"" + Regex.Escape(letters) + @"[-–—]" + Regex.Escape(digits) + @"",
                                 RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            else
            {
                patt = new Regex(@"" + Regex.Escape(normalizedCode) + @"",
                                 RegexOptions.Compiled);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (patt.IsMatch(lines[i] ?? string.Empty))
                    yield return i;
            }
        }

        public static string? FindSectionNo(IReadOnlyList<string> lines, int fromIndex)
        {
            for (int i = fromIndex; i >= 0; i--)
            {
                var m = ReSection.Match(lines[i] ?? string.Empty);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        public static (string pointNo, List<string> block) ExtractPointBlock(IReadOnlyList<string> lines, int hitIndex)
        {
            int start = hitIndex;
            for (int i = hitIndex; i >= 0; i--)
            {
                var line = lines[i] ?? string.Empty;
                if (ReSection.IsMatch(line)) break;
                if (RePoint.IsMatch(line)) { start = i; break; }
            }

            int end = lines.Count;
            for (int j = hitIndex + 1; j < lines.Count; j++)
            {
                var line = lines[j] ?? string.Empty;
                if (ReSection.IsMatch(line) || RePoint.IsMatch(line))
                {
                    end = j;
                    break;
                }
            }

            var block = lines
                .Skip(start)
                .Take(end - start)
                .Select(s => s ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            string pointNo = string.Empty;

            if (block.Count > 0)
            {
                var first = block[0];
                var m = RePoint.Match(first);
                if (m.Success)
                {
                    var num = string.IsNullOrEmpty(m.Groups[1].Value)
                        ? m.Groups[2].Value
                        : m.Groups[1].Value;

                    pointNo = string.IsNullOrEmpty(num) ? string.Empty : num + ".";
                    first = RePoint.Replace(first, string.Empty, 1).Trim();
                    block[0] = first;
                }
            }

            return (pointNo, block);
        }

        public static bool MatchAdvancedCriteria(List<string> block, AdvancedCriteria? criteria)
        {
            if (criteria == null || !criteria.IsActive)
                return true;

            var text = string.Join(" ", block);
            var textUpper = text.ToUpperInvariant();

            bool anyMatch = false;

            bool MatchField(string value, bool required)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return true;

                string v = value.ToUpperInvariant();
                var rx = new Regex(@"" + Regex.Escape(v) + @"", RegexOptions.Compiled);
                bool contains = rx.IsMatch(textUpper);

                if (contains) anyMatch = true;

                return !required || contains;
            }

            bool okPersonal = MatchField(criteria.Personal, criteria.RequirePersonal);
            bool okLast = MatchField(criteria.LastName, criteria.RequireLastName);
            bool okFirst = MatchField(criteria.FirstName, criteria.RequireFirstName);
            bool okMiddle = MatchField(criteria.MiddleName, criteria.RequireMiddleName);

            if (!anyMatch) return false;

            return okPersonal && okLast && okFirst && okMiddle;
        }

        public static (bool found, List<string> keywords, List<string> modes) MatchKeywords(
            List<string> block,
            SearchModes modes)
        {
            if (!modes.Any)
                return (false, new(), new());

            var text = string.Join(" ", block);
            var textUpper = text.ToUpperInvariant();

            var kw = new List<string>();
            var md = new List<string>();

            if (modes.Mode200)
            {
                bool hasIskl = textUpper.Contains("ИСКЛЮЧИТЬ");
                bool hasDeath = textUpper.Contains("В СВЯЗИ СО СМЕРТЬЮ");
                if (hasIskl && hasDeath)
                {
                    kw.Add("ИСКЛЮЧИТЬ; В СВЯЗИ СО СМЕРТЬЮ");
                    md.Add("200");
                }
            }

            if (modes.ModeEnlist)
            {
                bool hasEnlist = textUpper.Contains("ЗАЧИСЛИТЬ");
                bool hasDispositionPhrase = textUpper.Contains("ЗАЧИСЛИТЬ В РАСПОРЯЖЕНИЕ");

                if (hasEnlist && !hasDispositionPhrase)
                {
                    kw.Add("ЗАЧИСЛИТЬ");
                    md.Add("enlist");
                }
            }

            if (modes.ModeDisposition)
            {
                if (textUpper.Contains("ЗАЧИСЛИТЬ В РАСПОРЯЖЕНИЕ"))
                {
                    kw.Add("ЗАЧИСЛИТЬ В РАСПОРЯЖЕНИЕ");
                    md.Add("disposition");
                }
            }

            if (modes.ModeAppointment)
            {
                if (textUpper.Contains("НАЗНАЧИТЬ") || textUpper.Contains("НАЗНАЧАЕТСЯ"))
                {
                    kw.Add("НАЗНАЧИТЬ/НАЗНАЧАЕТСЯ");
                    md.Add("appointment");
                }
            }

            if (modes.ModeDismissed)
            {
                if (textUpper.Contains("УВОЛИТЬ"))
                {
                    kw.Add("УВОЛИТЬ");
                    md.Add("dismissed");
                }
            }

            if (modes.UseKeywords && CustomKeywords.Length > 0)
            {
                foreach (var w in CustomKeywords.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    if (CustomCaseSensitive)
                    {
                        var rx = new Regex(@"" + Regex.Escape(w) + @"");
                        if (rx.IsMatch(text))
                        {
                            kw.Add(w);
                            if (!md.Contains("custom")) md.Add("custom");
                        }
                    }
                    else
                    {
                        var rx = new Regex(@"" + Regex.Escape(w.ToUpperInvariant()) + @"");
                        if (rx.IsMatch(textUpper))
                        {
                            kw.Add(w);
                            if (!md.Contains("custom")) md.Add("custom");
                        }
                    }
                }
            }

            return (kw.Count > 0, kw, md);
        }

        public static (string noText, string dateText) ExtractNoDateFromHeaderOrFile(string headerText, string fileName)
        {
            string no = "";
            string dt = "";

            var mNo = RxNo.Match(headerText);
            if (mNo.Success) no = mNo.Value;

            var mDt = RxDate.Match(headerText);
            if (mDt.Success)
                dt = mDt.Value.Replace('_', '-');

            if (string.IsNullOrWhiteSpace(no) || string.IsNullOrWhiteSpace(dt))
            {
                var fn = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var mNo2 = RxNo.Match(fn);
                if (string.IsNullOrWhiteSpace(no) && mNo2.Success)
                    no = mNo2.Value;

                var mDt2 = RxDate.Match(fn);
                if (string.IsNullOrWhiteSpace(dt) && mDt2.Success)
                    dt = mDt2.Value.Replace('_', '-');
            }

            return (no, dt);
        }
    }
}
