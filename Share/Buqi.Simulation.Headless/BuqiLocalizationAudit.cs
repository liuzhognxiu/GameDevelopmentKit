using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BattleText = Game.Hot.Buqi.Battle.BuqiText;

namespace Buqi.Simulation.Headless
{
    internal static class BuqiLocalizationAudit
    {
        private static readonly string[] BannedTerms =
        {
            "<NoKey>", "器物", "坊市", "八方周天", "周天", "蕴灵", "炼养", "精炼", "回响",
            "时段", "结算印记", "供给角色", "候选冻结", "PVE", "PVP", "tick", "DEMO", "INFO",
            "灵石", "护体", "失衡", "改良", "定型", "道印",
            "LOCKED", "SIZE", "ITEM", "COST", "UNAVAILABLE", "CHOICE", "SOLD", "PRICE", "OFFER",
            "BUY", "DETAILS", "coins", "Battle", "Summary",
        };

        public static int Run()
        {
            string root = FindRepositoryRoot();
            var failures = new List<string>();
            Dictionary<string, string[]> localization = ReadLocalization(root, failures);
            ValidateLanguageColumns(localization, failures);
            ValidateBuqiLocalizationKeys(root, localization, failures);
            ValidateGeneratedArtifacts(root, localization, failures);
            ValidatePrefabs(root, localization, failures);
            ValidateGeneratedUiBuilders(root, failures);
            ValidatePlayerFacingCode(root, failures);
            ValidateDynamicText(failures);

            if (failures.Count == 0)
            {
                Console.WriteLine(BattleText.Format(
                    "不器中文审计通过：{0} 个本地化 Key。", localization.Count));
                return 0;
            }

            Console.Error.WriteLine(BattleText.Format(
                "不器中文审计失败，共 {0} 项：", failures.Count));
            foreach (string failure in failures.Take(80))
                Console.Error.WriteLine(BattleText.Format("- {0}", failure));
            if (failures.Count > 80)
            {
                Console.Error.WriteLine(BattleText.Format(
                    "- 其余 {0} 项已省略。", failures.Count - 80));
            }
            return 1;
        }

        private static Dictionary<string, string[]> ReadLocalization(
            string root,
            ICollection<string> failures)
        {
            string path = Path.Combine(root, "Design", "Excel", "Localization.xlsx");
            string[][] rows = ReadFirstWorksheet(path);
            if (rows.Length == 0 || rows[0].Length < 6 || rows[0][1] != "key")
                throw new InvalidDataException("Localization.xlsx 表头无效。");

            string[] expectedLanguages = { "ChineseSimplified", "English", "ChineseTraditional", "Korean" };
            for (int index = 0; index < expectedLanguages.Length; index++)
            {
                if (rows[0][index + 2] != expectedLanguages[index])
                    failures.Add(BattleText.Format(
                        "Localization.xlsx 缺少语言列 {0}。", expectedLanguages[index]));
            }

            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            for (int rowIndex = 1; rowIndex < rows.Length; rowIndex++)
            {
                string key = Cell(rows[rowIndex], 1);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                string[] values = Enumerable.Range(2, 4)
                    .Select(index => Cell(rows[rowIndex], index))
                    .ToArray();
                if (!result.TryAdd(key, values))
                    failures.Add(BattleText.Format("Localization.xlsx Key 重复：{0}。", key));
            }
            return result;
        }

        private static void ValidateLanguageColumns(
            IReadOnlyDictionary<string, string[]> localization,
            ICollection<string> failures)
        {
            foreach (KeyValuePair<string, string[]> pair in localization)
            {
                string key = pair.Key;
                string[] values = pair.Value;
                if (values.Any(string.IsNullOrWhiteSpace))
                {
                    failures.Add(BattleText.Format("本地化值为空：{0}。", key));
                    continue;
                }
                if (values.Skip(1).Any(value => !string.Equals(value, values[0], StringComparison.Ordinal)))
                    failures.Add(BattleText.Format("四语言未统一为简体中文：{0}。", key));

                if (key.EndsWith(".Website", StringComparison.Ordinal))
                    continue;
                if (Regex.IsMatch(values[0], "[A-Za-z]"))
                {
                    failures.Add(BattleText.Format(
                        "本地化仍含英文字母：{0} = {1}。", key, values[0]));
                }
                foreach (string term in BannedTerms)
                {
                    if (values[0].IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    failures.Add(BattleText.Format(
                        "本地化含禁用词“{0}”：{1} = {2}。", term, key, values[0]));
                    break;
                }
            }
        }

        private static void ValidateBuqiLocalizationKeys(
            string root,
            IReadOnlyDictionary<string, string[]> localization,
            ICollection<string> failures)
        {
            string buqiDir = Path.Combine(root, "Design", "Excel", "GameHot", "Datas", "Buqi");
            foreach (string path in Directory.EnumerateFiles(buqiDir, "*.xlsx"))
            {
                string[][] rows = ReadFirstWorksheet(path);
                if (rows.Length == 0)
                    continue;
                for (int column = 0; column < rows[0].Length; column++)
                {
                    string header = Cell(rows[0], column);
                    if (header.EndsWith("LocalizationKey", StringComparison.Ordinal))
                    {
                        for (int row = 3; row < rows.Length; row++)
                        {
                            string key = Cell(rows[row], column);
                            if (string.IsNullOrWhiteSpace(key))
                            {
                                failures.Add(BattleText.Format(
                                    "{0} 第 {1} 行未赋值本地化 Key。", Path.GetFileName(path), row + 1));
                            }
                            else if (!localization.ContainsKey(key))
                            {
                                failures.Add(BattleText.Format(
                                    "{0} 引用了不存在的 Key：{1}。", Path.GetFileName(path), key));
                            }
                        }
                    }
                    if (header != "DisplayName" && header != "Summary" && header != "UpgradeSummary")
                        continue;
                    for (int row = 3; row < rows.Length; row++)
                    {
                        string value = Cell(rows[row], column);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            failures.Add(BattleText.Format(
                                "{0} 第 {1} 行的 {2} 为空。", Path.GetFileName(path), row + 1, header));
                            continue;
                        }
                        if (Regex.IsMatch(value, "[A-Za-z]"))
                        {
                            failures.Add(BattleText.Format(
                                "{0} 第 {1} 行的 {2} 含英文：{3}。", Path.GetFileName(path), row + 1, header, value));
                        }
                        foreach (string term in BannedTerms)
                        {
                            if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            failures.Add(BattleText.Format(
                                "{0} 第 {1} 行的 {2} 含禁用词“{3}”。", Path.GetFileName(path), row + 1, header, term));
                            break;
                        }
                        if (header == "DisplayName"
                            && Path.GetFileName(path) == "BuqiEventOption.xlsx"
                            && !Regex.IsMatch(
                                value,
                                "购买|获得|强化|过载|升级|生命|结清|支付|拒付|归还|卖出|投资|清除|更换|构筑结算|移除|借用|延后"))
                        {
                            failures.Add(BattleText.Format(
                                "{0} 第 {1} 行的选项按钮未以功能动作命名：{2}。",
                                Path.GetFileName(path), row + 1, value));
                        }
                    }
                }
            }
        }

        private static void ValidateGeneratedArtifacts(
            string root,
            IReadOnlyDictionary<string, string[]> localization,
            ICollection<string> failures)
        {
            string localizationDir = Path.Combine(root, "Unity", "Assets", "Res", "Localization");
            string[] languages = { "ChineseSimplified", "English", "ChineseTraditional", "Korean" };
            byte[][] payloads = languages
                .Select(language => File.ReadAllBytes(Path.Combine(localizationDir, language, "Localization.bytes")))
                .ToArray();
            for (int index = 1; index < payloads.Length; index++)
            {
                if (!payloads[0].SequenceEqual(payloads[index]))
                {
                    failures.Add(BattleText.Format(
                        "{0} Localization.bytes 与简体中文导出不一致。", languages[index]));
                }
            }

            string keyCode = File.ReadAllText(Path.Combine(
                root, "Unity", "Assets", "Scripts", "Game", "Hot", "Code", "Generate", "LocalizationKey.cs"));
            foreach (string key in localization.Keys.Where(value => value.StartsWith("Buqi.", StringComparison.Ordinal)))
            {
                if (!keyCode.Contains(BattleText.Format("\"{0}\"", key)))
                    failures.Add(BattleText.Format("LocalizationKey.cs 缺少 {0}。", key));
            }
        }

        private static void ValidatePrefabs(
            string root,
            IReadOnlyDictionary<string, string[]> localization,
            ICollection<string> failures)
        {
            string uiRoot = Path.Combine(root, "Unity", "Assets", "Res", "UI");
            foreach (string path in Directory.EnumerateFiles(uiRoot, "*.prefab", SearchOption.AllDirectories)
                         .Where(value => value.IndexOf("Buqi", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                int lineNumber = 0;
                foreach (string line in File.ReadLines(path))
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (!trimmed.StartsWith("m_Text:", StringComparison.Ordinal))
                        continue;
                    string value = trimmed.Substring("m_Text:".Length).Trim();
                    if (value.Contains("<NoKey>"))
                    {
                        failures.Add(BattleText.Format(
                            "{0}:{1} 含 <NoKey>。", Relative(root, path), lineNumber));
                    }
                    if (string.IsNullOrWhiteSpace(value))
                        continue;
                    if (!value.StartsWith("Buqi.", StringComparison.Ordinal))
                    {
                        failures.Add(BattleText.Format(
                            "{0}:{1} 静态文字未使用本地化 Key：{2}。",
                            Relative(root, path), lineNumber, value));
                    }
                    else if (!localization.ContainsKey(value))
                    {
                        failures.Add(BattleText.Format(
                            "{0}:{1} 引用了不存在的 Key：{2}。", Relative(root, path), lineNumber, value));
                    }
                }
            }
        }

        private static void ValidatePlayerFacingCode(string root, ICollection<string> failures)
        {
            string codeRoot = Path.Combine(root, "Unity", "Assets", "Scripts", "Game", "Hot", "Code", "Buqi");
            string[] visibleRoots =
            {
                Path.Combine(codeRoot, "UI"),
                Path.Combine(codeRoot, "DemoUI"),
            };
            var stringLiteral = new Regex("(?:\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"|@\"(?:\"\"|[^\"])*\")");
            foreach (string path in visibleRoots.SelectMany(directory =>
                         Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)))
            {
                string text = File.ReadAllText(path);
                bool isSanitizer = path.EndsWith("BuqiPlayerText.cs", StringComparison.Ordinal);
                if (!isSanitizer && text.Contains("<NoKey>"))
                    failures.Add(BattleText.Format("{0} 仍处理 <NoKey> 回退。", Relative(root, path)));

                foreach (Match match in stringLiteral.Matches(text))
                {
                    string value = LiteralContent(match.Value);
                    if (isSanitizer && value.Contains("<NoKey>"))
                        continue;
                    if (value.StartsWith("Buqi.", StringComparison.Ordinal) || value.Contains("Assets/"))
                        continue;
                    if (Regex.IsMatch(value, "^[a-z0-9_.{}-]+$", RegexOptions.IgnoreCase))
                        continue;
                    string playerText = PlayerText(value);
                    foreach (string term in BannedTerms)
                    {
                        if (playerText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        failures.Add(BattleText.Format(
                            "{0} 动态文案含禁用词“{1}”：{2}。", Relative(root, path), term, match.Value));
                        break;
                    }
                    if (Regex.IsMatch(playerText, "[A-Za-z]"))
                    {
                        failures.Add(BattleText.Format(
                            "{0} 动态文案含英文：{1}。", Relative(root, path), match.Value));
                    }
                }

                ValidateVisibleLiteralContexts(root, path, text, failures);
            }
        }

        private static void ValidateGeneratedUiBuilders(string root, ICollection<string> failures)
        {
            string editorRoot = Path.Combine(
                root, "Unity", "Assets", "Scripts", "Game", "Hot", "Code", "Editor", "Buqi");
            if (!Directory.Exists(editorRoot))
                return;

            const string literal = "(?<literal>\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"|@\"(?:\"\"|[^\"])*\")";
            string pattern = "CreateText\\s*\\(\\s*[^,]+,\\s*\"[^\"]*\"\\s*,\\s*" + literal;
            foreach (string path in Directory.EnumerateFiles(editorRoot, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                foreach (Match match in Regex.Matches(text, pattern))
                {
                    string value = LiteralContent(match.Groups["literal"].Value);
                    string playerText = PlayerText(value);
                    if (value.StartsWith("Buqi.", StringComparison.Ordinal)
                        || Regex.IsMatch(value, "^[A-Z][A-Z0-9]*-\\d+$"))
                    {
                        continue;
                    }
                    if (!Regex.IsMatch(playerText, "[A-Za-z]"))
                        continue;
                    failures.Add(BattleText.Format(
                        "{0} 生成界面的可见文案不得使用英文：{1}。",
                        Relative(root, path), match.Groups["literal"].Value));
                }
            }
        }

        private static void ValidateVisibleLiteralContexts(
            string root,
            string path,
            string text,
            ICollection<string> failures)
        {
            const string literal = "(?<literal>\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"|@\"(?:\"\"|[^\"])*\")";
            string[] patterns =
            {
                "(?:AddAction|ShowError|Rejected)\\s*\\(\\s*" + literal,
                "SetText\\s*\\([^,]+,\\s*" + literal,
                "(?:Title|Body|Description|Status|Message|Label|Error)\\s*=\\s*" + literal,
            };
            foreach (string pattern in patterns)
            {
                foreach (Match match in Regex.Matches(text, pattern))
                {
                    string value = LiteralContent(match.Groups["literal"].Value);
                    string playerText = PlayerText(value);
                    if (!Regex.IsMatch(playerText, "[A-Za-z]"))
                        continue;
                    failures.Add(BattleText.Format(
                        "{0} 可见文案不得使用英文：{1}。",
                        Relative(root, path), match.Groups["literal"].Value));
                }
            }
        }

        private static void ValidateDynamicText(ICollection<string> failures)
        {
            string[] values =
            {
                Game.Hot.Buqi.Battle.BuqiBattleText.Outcome(Game.Hot.Buqi.Battle.BattleOutcome.LeftWin),
                Game.Hot.Buqi.Battle.BuqiBattleText.Outcome(Game.Hot.Buqi.Battle.BattleOutcome.Draw),
                Game.Hot.Buqi.Battle.BuqiBattleText.Termination("HardCap"),
                Game.Hot.Buqi.Battle.BuqiBattleText.EventReason("Damage"),
                Game.Hot.Buqi.Battle.BuqiBattleText.EventReason("W8-003-attack"),
                Game.Hot.Buqi.Battle.BuqiBattleText.Quality(Game.Hot.Buqi.Battle.BuqiQuality.Fixed),
            };
            foreach (string value in values)
            {
                if (!Regex.IsMatch(value, "[\\u3400-\\u9fff]") || Regex.IsMatch(value, "[A-Za-z]"))
                    failures.Add(BattleText.Format("关键动态文本不是纯中文：{0}。", value));
            }
        }

        private static string PlayerText(string value)
        {
            string result = Regex.Replace(value ?? string.Empty, "\\{[^{}]*\\}", string.Empty);
            return Regex.Replace(result, "\\\\[nrt]", string.Empty);
        }

        private static string LiteralContent(string literal)
        {
            int start = literal.IndexOf('"');
            int end = literal.LastIndexOf('"');
            return start >= 0 && end > start ? literal.Substring(start + 1, end - start - 1) : literal;
        }

        private static string[][] ReadFirstWorksheet(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sharedStrings = new List<string>();
            ZipArchiveEntry sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry != null)
            {
                using Stream stream = sharedEntry.Open();
                sharedStrings.AddRange(XDocument.Load(stream).Descendants(spreadsheet + "si")
                    .Select(si => string.Concat(si.Descendants(spreadsheet + "t").Select(t => t.Value))));
            }

            ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? throw new InvalidDataException(BattleText.Format("{0} 缺少 sheet1.xml。", path));
            XDocument sheet;
            using (Stream stream = sheetEntry.Open())
                sheet = XDocument.Load(stream);
            var rows = new List<string[]>();
            foreach (XElement row in sheet.Descendants(spreadsheet + "row"))
            {
                var cells = new Dictionary<int, string>();
                foreach (XElement cell in row.Elements(spreadsheet + "c"))
                {
                    string reference = cell.Attribute("r")?.Value ?? string.Empty;
                    int column = ColumnIndex(reference);
                    string type = cell.Attribute("t")?.Value ?? string.Empty;
                    string raw = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
                    string value = type == "s" && int.TryParse(raw, out int sharedIndex)
                        ? sharedStrings[sharedIndex]
                        : type == "inlineStr"
                            ? string.Concat(cell.Descendants(spreadsheet + "t").Select(t => t.Value))
                            : raw;
                    cells[column] = value;
                }
                int width = cells.Count == 0 ? 0 : cells.Keys.Max() + 1;
                var values = new string[width];
                foreach (KeyValuePair<int, string> cell in cells)
                    values[cell.Key] = cell.Value;
                rows.Add(values);
            }
            return rows.ToArray();
        }

        private static int ColumnIndex(string reference)
        {
            int value = 0;
            foreach (char character in reference)
            {
                if (!char.IsLetter(character))
                    break;
                value = value * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            }
            return value - 1;
        }

        private static string Cell(string[] row, int index)
        {
            return index < row.Length ? row[index] ?? string.Empty : string.Empty;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "Unity")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("找不到 GameDevelopmentKit 仓库根目录。");
        }

        private static string Relative(string root, string path)
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }
    }
}
