using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.PluginLogic
{
    // Classic discovers this contract by interface name, not assembly identity.
    public interface IPlugin
    {
        string Name { get; }
        string Text { get; }
        decimal Version { get; }
        string Description { get; }
        string ActionType { get; }
        string Shortcut { get; }
        string DoAction(Form parent, string srt, double frameRate, string lineSeparator,
            string fileName, string videoFileName, string rawText);
    }

    public class ExportOptions
    {
        public bool Configured { get; set; }
        public bool RangeMarkers { get; set; }
        public bool TextAsName { get; set; }
        public bool IncludeDescription { get; set; }
        public bool SelectedOnly { get; set; }
        public bool RenumberSelected { get; set; }
        public bool ActorsOnly { get; set; }
        public string[] ActorNames { get; set; }
        public bool EmbedOnHardSub { get; set; }
        public string OutputFolder { get; set; }
        public ExportOptions() { IncludeDescription = true; OutputFolder = ""; ActorNames = new string[0]; }
    }

    public static class MarkerExport
    {
        public const string Header = "Name\tStart\tDuration\tTime Format\tType\tDescription";

        public static List<Paragraph> Parse(string srt)
        {
            var subtitle = new Subtitle();
            var parser = new SubRip();
            parser.LoadSubtitle(subtitle, Regex.Split(srt ?? "", "\r\n|\n|\r").ToList(), null);
            if (subtitle.Paragraphs.Count == 0 || parser.ErrorCount != 0)
                throw new InvalidDataException("No valid subtitles, or subtitle data could not be read completely.");
            return subtitle.Paragraphs;
        }

        public static string Time(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0 || milliseconds > 1e12)
                throw new InvalidDataException("Invalid marker time.");
            long ms = (long)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
            return (ms / 60000).ToString("00", CultureInfo.InvariantCulture) + ":" +
                ((ms / 1000) % 60).ToString("00", CultureInfo.InvariantCulture) + "." +
                (ms % 1000).ToString("000", CultureInfo.InvariantCulture);
        }

        public static string PlainText(string text)
        {
            string clean = HtmlUtil.RemoveHtmlTags(text ?? "", true);
            clean = clean.Replace("\\N", " ").Replace("\\n", " ").Replace("\\h", " ");
            // One physical row per marker; quotes and tabs are escaped separately below.
            return Regex.Replace(clean, "[\\r\\n\\t]+", " ").Replace("\0", "").Trim();
        }

        public static string Field(string value)
        {
            value = value ?? "";
            return value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) < 0 ? value :
                "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static List<Paragraph> ParseCurrent(string srt, string raw)
        {
            if (!string.IsNullOrEmpty(raw) && raw.TrimStart().StartsWith("[Script Info]", StringComparison.OrdinalIgnoreCase))
            {
                var sub = new Subtitle();
                var lines = Regex.Split(raw, "\\r\\n|\\n|\\r").ToList();
                SubtitleFormat parser = (raw.IndexOf("[V4+ Styles]", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("v4.00+", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? (SubtitleFormat)new AdvancedSubStationAlpha() : new SubStationAlpha();
                parser.LoadSubtitle(sub, lines, null);
                if (parser.ErrorCount != 0 || sub.Paragraphs.Count == 0) throw new InvalidDataException("ASS actor data could not be read completely.");
                if (sub.Paragraphs.Count != Parse(srt).Count) throw new InvalidDataException("ASS and editor subtitle counts differ; actor filtering was not applied.");
                return sub.Paragraphs;
            }
            return Parse(srt);
        }

        public static List<AuditionMarker> Build(IList<Paragraph> cues, ExportOptions options, int[] selected,
            double cutStart, double cutEnd, double clockScale)
        {
            var selection = new HashSet<int>(selected ?? new int[0]);
            var actors = new HashSet<string>(options.ActorNames ?? new string[0], StringComparer.Ordinal);
            if (options.SelectedOnly && selection.Count == 0) throw new InvalidDataException("Select subtitle rows first.");
            if (options.ActorsOnly && actors.Count == 0) throw new InvalidDataException("Choose at least one actor in Audition Markers settings.");
            if (double.IsNaN(cutEnd) || cutStart < 0 || double.IsNaN(cutStart) || double.IsInfinity(cutStart) || cutEnd <= cutStart ||
                clockScale <= 0 || double.IsNaN(clockScale) || double.IsInfinity(clockScale)) throw new InvalidDataException("Invalid export timing window.");
            var result = new List<AuditionMarker>();
            for (int i = 0; i < cues.Count; i++)
            {
                if (options.SelectedOnly && !selection.Contains(i)) continue;
                var cue = cues[i];
                if (options.ActorsOnly && !actors.Contains(cue.Actor ?? "")) continue;
                double start = cue.StartTime.TotalMilliseconds, end = cue.EndTime.TotalMilliseconds;
                Time(start); Time(end);
                if (end < start) throw new InvalidDataException("Subtitle " + (i + 1) + " ends before it starts.");
                if (start >= cutEnd || end < cutStart || (end == cutStart && start < cutStart)) continue;
                start = Math.Max(0, (start - cutStart) * clockScale);
                end = Math.Min((end - cutStart) * clockScale, cutEnd - cutStart);
                if (end < start) continue;
                string text = PlainText(cue.Text), number = (i + 1).ToString(CultureInfo.InvariantCulture);
                string name = (options.SelectedOnly || options.ActorsOnly) && options.RenumberSelected
                    ? "Маркер " + (result.Count + 1).ToString(CultureInfo.InvariantCulture) + " (" + number + ")"
                    : "Маркер " + number;
                result.Add(new AuditionMarker { Start = start, Duration = options.RangeMarkers ? end - start : 0,
                    Name = options.TextAsName && text.Length > 0 ? text : name,
                    Description = options.IncludeDescription ? text : "", Actor = cue.Actor ?? "" });
            }
            if (result.Count == 0) throw new InvalidDataException("No matching subtitle markers in the output time range.");
            return result;
        }

        public static string Render(IList<Paragraph> cues, ExportOptions options, int[] selected)
        {
            var output = new StringBuilder(Header + "\r\n");
            foreach (var marker in Build(cues, options, selected, 0, double.PositiveInfinity, 1))
                output.Append(Field(marker.Name)).Append('\t').Append(Time(marker.Start)).Append('\t')
                    .Append(options.RangeMarkers ? Time(marker.Duration) : "0:00.000").Append("\tdecimal\tCue\t")
                    .Append(Field(marker.Description)).Append("\r\n");
            return output.ToString();
        }

        public static string Save(string folder, string baseName, string content)
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Output folder does not exist: " + folder);
            foreach (char c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "subtitles";
            // Fully render before touching disk; publish by a same-directory rename.
            string temp = Path.Combine(folder, ".audition-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                for (int n = 0; n < 10000; n++)
                {
                    string destination = Path.Combine(folder, baseName + "_audition" + (n == 0 ? "" : "_" + n) + ".csv");
                    try { File.Move(temp, destination); return destination; }
                    catch (IOException) { if (!File.Exists(destination)) throw; }
                }
                throw new IOException("Too many existing marker files.");
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public partial class AuditionMarkers : IPlugin
    {
        public string Name { get { return "AuditionMarkers"; } }
        public string Text { get { return Ru ? "Маркеры Audition" : "Audition markers"; } }
        public decimal Version { get { return 1.21m; } }
        public string Description { get { return "One-click Adobe Audition CSV marker export (DeCoCo)."; } }
        public string ActionType { get { return "Tool"; } }
        public string Shortcut { get { return ""; } }
        // Optional Classic host extension; older hosts still support the menu command.
        public string ToolbarText { get { return Text; } }
        public string StatusMessage { get; private set; }
        private static bool Ru { get {
            string language = Configuration.Settings.General.Language;
            return (string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture.Name : language).StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        } }
        private static string Tr(string en, string ru) { return Ru ? ru : en; }
        private static string SettingsFile { get { return Path.Combine(Configuration.DataDirectory, "Plugins", "AuditionMarkers.settings.xml"); } }

        private static ExportOptions Load()
        {
            if (!File.Exists(SettingsFile)) return new ExportOptions();
            try
            {
                using (var reader = System.Xml.XmlReader.Create(SettingsFile, new System.Xml.XmlReaderSettings {
                    DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
                    return (ExportOptions)new XmlSerializer(typeof(ExportOptions)).Deserialize(reader);
            }
            catch (Exception ex)
            {
                if (!(ex is InvalidOperationException || ex is IOException || ex is System.Xml.XmlException)) throw;
                throw new InvalidDataException(Tr("Plugin settings cannot be read. Use the button's Settings command to replace them.",
                    "Не удалось прочитать настройки плагина. Откройте настройки через стрелку кнопки и сохраните их заново."), ex);
            }
        }

        private static void Persist(ExportOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            string temp = SettingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) new XmlSerializer(typeof(ExportOptions)).Serialize(stream, options);
                if (File.Exists(SettingsFile)) File.Replace(temp, SettingsFile, null); else File.Move(temp, SettingsFile);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public void Configure(Form parent)
        {
            try
            {
                ExportOptions options;
                try { options = Load(); } catch (InvalidDataException) { options = new ExportOptions(); }
                EditOptions(parent, options);
            }
            catch (Exception ex) { MessageBox.Show(parent, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        public string DoAction(Form parent, string srt, double frameRate, string lineSeparator, string fileName, string videoFileName, string rawText)
        { return Export(parent, srt, fileName, rawText, null); }

        public string DoActionWithSelection(Form parent, string srt, double frameRate, string lineSeparator,
            string fileName, string videoFileName, string rawText, int[] selected)
        { return Export(parent, srt, fileName, rawText, selected); }

        private string Export(Form parent, string srt, string fileName, string rawText, int[] selected)
        {
            StatusMessage = null;
            try
            {
                var cues = MarkerExport.ParseCurrent(srt, rawText);
                _context = cues; _selectedContext = selected;
                var options = Load();
                if (!options.Configured) { if (!EditOptions(parent, options)) return null; options = Load(); }
                if (options.SelectedOnly && selected == null)
                    throw new InvalidOperationException(Tr("This Classic version cannot pass selected rows. Disable selected-only export in plugin settings or update Classic.",
                        "Эта версия Classic не передаёт выбор строк. Отключите экспорт только выбранных реплик в настройках плагина или обновите Classic."));
                string content = MarkerExport.Render(cues, options, selected);
                string folder = options.OutputFolder;
                string baseName = string.IsNullOrEmpty(fileName) ? "subtitles" : Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrWhiteSpace(folder) && !string.IsNullOrEmpty(fileName) && Path.IsPathRooted(fileName)) folder = Path.GetDirectoryName(fileName);
                if (string.IsNullOrWhiteSpace(folder))
                {
                    using (var picker = new FolderBrowserDialog { Description = Tr("Choose a folder for marker exports", "Выберите папку для экспорта маркеров") })
                    {
                        if (picker.ShowDialog(parent) != DialogResult.OK) return null;
                        folder = picker.SelectedPath; options.OutputFolder = folder; Persist(options);
                    }
                }
                string destination = MarkerExport.Save(folder, baseName, content);
                StatusMessage = Tr("Audition markers saved: ", "Маркеры Audition сохранены: ") + destination;
                // Older hosts have no StatusMessage extension; report without changing the subtitle.
                if (parent != null && parent.GetType().GetMethod("ShowStatus") != null)
                    parent.GetType().GetMethod("ShowStatus").Invoke(parent, new object[] { StatusMessage, true, 0, false });
            }
            catch (Exception ex)
            {
                StatusMessage = Tr("Audition export failed: ", "Ошибка экспорта Audition: ") + ex.Message;
                MessageBox.Show(parent, StatusMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Returning null prevents Classic's plugin replacement/Undo path.
            return null;
        }
    }
}
