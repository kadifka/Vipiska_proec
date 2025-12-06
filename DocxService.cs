using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xceed.Document.NET;
using Xceed.Words.NET;
using VupisjkaWpf.Models;

namespace VupisjkaWpf.Services
{
    public static class DocxService
    {
        public static List<string> ReadAllLines(string docxPath)
        {
            var lines = new List<string>(256);
            using var doc = DocX.Load(docxPath);

            foreach (var p in doc.Paragraphs)
                lines.Add(p.Text ?? string.Empty);

            foreach (var t in doc.Tables)
            {
                foreach (var row in t.Rows)
                {
                    var cells = row.Cells
                                   .SelectMany(c => c.Paragraphs)
                                   .Select(p => (p.Text ?? string.Empty).Trim())
                                   .Where(x => !string.IsNullOrWhiteSpace(x));

                    var joined = string.Join(" | ", cells);
                    if (!string.IsNullOrWhiteSpace(joined))
                        lines.Add(joined);
                }
            }

            return lines;
        }

        private static Paragraph FormatTightParagraph(Paragraph p, double fontSize, Alignment alignment)
        {
            p.Font("Times New Roman");
            p.FontSize(fontSize);
            p.Alignment = alignment;
            p.LineSpacing(0.9);
            p.LineSpacingRule = LineSpacingRule.Multiple;
            p.SpacingAfter(0);
            p.SpacingBefore(0);
            return p;
        }

        public static void AppendExtract(DocX outDoc,
                                         List<string> sourceLines,
                                         string sourceFileName,
                                         string sectionNo,
                                         string pointNo,
                                         List<string> pointBlock,
                                         ExtractOptions opt)
        {
            var rxVypiska = new Regex(@"выписка\s+из\s+приказа", RegexOptions.IgnoreCase);
            var rxSpacedPrikaz = new Regex(@"(?i)П\s*Р\s*И\s*К\s*А\s*З", RegexOptions.Compiled);
            var rxWordPrikaz = new Regex(@"приказ", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            int idxOrder = -1;
            for (int i = 0; i < sourceLines.Count; i++)
            {
                string line = sourceLines[i] ?? string.Empty;
                if (rxVypiska.IsMatch(line) || rxSpacedPrikaz.IsMatch(line) || rxWordPrikaz.IsMatch(line))
                {
                    idxOrder = i;
                    break;
                }
            }

            if (idxOrder < 0)
                idxOrder = 0;

            int idxSection = -1;
            for (int i = idxOrder + 1; i < sourceLines.Count; i++)
            {
                if ((sourceLines[i] ?? string.Empty).Contains('§'))
                {
                    idxSection = i;
                    break;
                }
            }

            if (idxSection < 0 || idxSection <= idxOrder)
                idxSection = sourceLines.Count;

            var headerBodyLines = sourceLines
                .Skip(idxOrder + 1)
                .Take(idxSection - (idxOrder + 1))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            string headerTextForMeta = string.Join("\n", headerBodyLines);
            var (noText, dateText) = SearchService.ExtractNoDateFromHeaderOrFile(headerTextForMeta, sourceFileName);

            var headerOutLines = new List<string>
            {
                "ВЫПИСКА ИЗ ПРИКАЗА"
            };

            bool bodyHasNoOrDate = headerBodyLines.Any(
                l => (l ?? string.Empty).Contains("№") || (l ?? string.Empty).ToLowerInvariant().Contains(" от "));

            if (!bodyHasNoOrDate && (!string.IsNullOrWhiteSpace(noText) || !string.IsNullOrWhiteSpace(dateText)))
            {
                string tail = (noText +
                               ((string.IsNullOrWhiteSpace(noText) || string.IsNullOrWhiteSpace(dateText)) ? "" : " ") +
                               (string.IsNullOrWhiteSpace(dateText) ? "" : "от " + dateText))
                              .Trim();

                if (!string.IsNullOrWhiteSpace(tail))
                    headerBodyLines.Insert(0, tail);
            }

            headerOutLines.AddRange(headerBodyLines);

            foreach (var line in headerOutLines)
            {
                var p = outDoc.InsertParagraph(line);
                FormatTightParagraph(p, 15, Alignment.center);
            }

            if (!string.IsNullOrWhiteSpace(sectionNo))
            {
                var pSec = outDoc.InsertParagraph($"§ {sectionNo}");
                FormatTightParagraph(pSec, 13, Alignment.center).Bold();
            }

            if (pointBlock is not null && pointBlock.Count > 0)
            {
                string first = pointBlock[0];
                string head = string.IsNullOrWhiteSpace(pointNo) ? first : $"{pointNo} {first}";

                var pHead = outDoc.InsertParagraph(head);
                FormatTightParagraph(pHead, 13, Alignment.both);

                foreach (var s in pointBlock.Skip(1))
                {
                    var p = outDoc.InsertParagraph(s);
                    FormatTightParagraph(p, 13, Alignment.both);
                }
            }

            AppendSigners(outDoc, sourceLines);
            AppendVerno(outDoc, opt);

            outDoc.InsertSectionPageBreak();
        }

        private static void AppendSigners(DocX outDoc, List<string> sourceLines)
        {
            if (sourceLines == null || sourceLines.Count == 0)
                return;

            var lower = sourceLines
                .Select(s => (s ?? string.Empty).ToLowerInvariant())
                .ToList();

            int total = sourceLines.Count;
            int startSearch = System.Math.Max(0, total - 200);

            var rxCmd = new Regex(@"командир|командующий|статс-секретарь|министр обороны",
                                  RegexOptions.IgnoreCase);
            var rxChief = new Regex(@"начальник штаба", RegexOptions.IgnoreCase);

            var signerBlocks = new List<List<string>>();

            int idxCmd = -1;
            for (int i = startSearch; i < lower.Count; i++)
            {
                if (rxCmd.IsMatch(lower[i]))
                {
                    idxCmd = i;
                    break;
                }
            }

            if (idxCmd >= 0)
            {
                var blockCmd = CollectSignerBlock(sourceLines, idxCmd);
                if (blockCmd.Count > 0)
                    signerBlocks.Add(blockCmd);
            }

            int searchChiefFrom = idxCmd >= 0 ? idxCmd + 1 : startSearch;
            int idxChief = -1;
            for (int i = searchChiefFrom; i < lower.Count; i++)
            {
                if (rxChief.IsMatch(lower[i]))
                {
                    idxChief = i;
                    break;
                }
            }

            if (idxChief >= 0)
            {
                var blockChief = CollectSignerBlock(sourceLines, idxChief);
                if (blockChief.Count > 0)
                    signerBlocks.Add(blockChief);
            }

            if (signerBlocks.Count == 0)
                return;

            outDoc.InsertParagraph("");

            foreach (var block in signerBlocks)
            {
                if (block.Count > 0)
                {
                    var p1 = outDoc.InsertParagraph(block[0]);
                    FormatTightParagraph(p1, 13, Alignment.center);
                }

                if (block.Count > 1)
                {
                    var p2 = outDoc.InsertParagraph(block[1]);
                    FormatTightParagraph(p2, 13, Alignment.center);
                }

                if (block.Count > 2)
                {
                    var p3 = outDoc.InsertParagraph(block[2]);
                    FormatTightParagraph(p3, 13, Alignment.right);
                }
            }
        }

        private static List<string> CollectSignerBlock(List<string> sourceLines, int startIndex)
        {
            var res = new List<string>(3);

            for (int i = startIndex; i < sourceLines.Count && res.Count < 3; i++)
            {
                var line = (sourceLines[i] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                res.Add(line);
            }

            return res;
        }

        private static void AppendVerno(DocX outDoc, ExtractOptions opt)
        {
            var pV = outDoc.InsertParagraph($"ВЕРНО: {opt.ApproverPosition}");
            FormatTightParagraph(pV, 13, Alignment.left);

            var pR = outDoc.InsertParagraph(opt.ApproverRank);
            FormatTightParagraph(pR, 13, Alignment.center);

            var pN = outDoc.InsertParagraph(opt.ApproverNameShort);
            FormatTightParagraph(pN, 13, Alignment.right);
        }

        public static DocX CreateOrOpen(string path)
            => System.IO.File.Exists(path) ? DocX.Load(path) : DocX.Create(path);
    }
}
