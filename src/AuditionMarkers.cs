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
        public string OutputFolder { get; set; }
        public ExportOptions() { IncludeDescription = true; OutputFolder = ""; }
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

        public static string Render(IList<Paragraph> cues, ExportOptions options, int[] selected)
        {
            var selection = new HashSet<int>(selected ?? new int[0]);
            if (options.SelectedOnly && selection.Count == 0)
                throw new InvalidDataException("Select subtitle rows first.");
            var output = new StringBuilder(Header + "\r\n");
            int count = 0;
            for (int i = 0; i < cues.Count; i++)
            {
                if (options.SelectedOnly && !selection.Contains(i)) continue;
                var cue = cues[i];
                if (cue.EndTime.TotalMilliseconds < cue.StartTime.TotalMilliseconds)
                    throw new InvalidDataException("Subtitle " + (i + 1) + " ends before it starts.");
                string start = Time(cue.StartTime.TotalMilliseconds);
                string duration = options.RangeMarkers ? Time(cue.EndTime.TotalMilliseconds - cue.StartTime.TotalMilliseconds) : "0:00.000";
                string text = PlainText(cue.Text);
                string name = options.TextAsName && text.Length > 0 ? text : "Маркер " + (i + 1);
                output.Append(Field(name)).Append('\t').Append(start).Append('\t').Append(duration)
                    .Append("\tdecimal\tCue\t").Append(Field(options.IncludeDescription ? text : "")).Append("\r\n");
                count++;
            }
            if (count == 0) throw new InvalidDataException("No subtitle rows to export.");
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

    public class AuditionMarkers : IPlugin
    {
        public string Name { get { return "AuditionMarkers"; } }
        public string Text { get { return Ru ? "Маркеры Audition" : "Audition markers"; } }
        public decimal Version { get { return 1.0m; } }
        public string Description { get { return "One-click Adobe Audition CSV marker export (DeCoCo)."; } }
        public string ActionType { get { return "Tool"; } }
        public string Shortcut { get { return ""; } }
        // Optional Classic host extension; older hosts still support the menu command.
        public string ToolbarText { get { return Text; } }
        public string StatusMessage { get; private set; }
        private static bool Ru { get { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"; } }
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

        private bool EditOptions(Form parent, ExportOptions options)
        {
            using (var dialog = new Form { Text = Text + " — " + Tr("Settings", "Настройки"), Width = 580, Height = 340,
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false, MaximizeBox = false, AutoScaleMode = AutoScaleMode.Dpi })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var ranges = new CheckBox { AutoSize = true, Text = Tr("Range markers (use subtitle duration)", "Маркеры-диапазоны по длительности реплик"), Checked = options.RangeMarkers };
                var names = new CheckBox { AutoSize = true, Text = Tr("Use subtitle text as marker name", "Текст реплики в названии маркера"), Checked = options.TextAsName };
                var descriptions = new CheckBox { AutoSize = true, Text = Tr("Include clean subtitle text in Description", "Текст без тегов в описании маркера"), Checked = options.IncludeDescription };
                var selection = new CheckBox { AutoSize = true, Text = Tr("Export selected rows only (extended Classic host)", "Только выбранные реплики (обновлённый Classic)"), Checked = options.SelectedOnly };
                foreach (var control in new Control[] { ranges, names, descriptions, selection }) { layout.Controls.Add(control); layout.SetColumnSpan(control, 2); }
                var label = new Label { AutoSize = true, Text = Tr("Output folder (blank = next to subtitles):", "Папка экспорта (пусто = рядом с субтитрами):") };
                layout.Controls.Add(label); layout.SetColumnSpan(label, 2);
                var folder = new TextBox { Dock = DockStyle.Top, Text = options.OutputFolder ?? "" };
                var browse = new Button { Text = "…", AutoSize = true };
                browse.Click += delegate { using (var picker = new FolderBrowserDialog()) { if (Directory.Exists(folder.Text)) picker.SelectedPath = folder.Text; if (picker.ShowDialog(dialog) == DialogResult.OK) folder.Text = picker.SelectedPath; } };
                layout.Controls.Add(folder); layout.Controls.Add(browse);
                var footer = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
                var cancel = new Button { AutoSize = true, Text = Tr("Cancel", "Отмена"), DialogResult = DialogResult.Cancel };
                var ok = new Button { AutoSize = true, Text = Tr("Save", "Сохранить") };
                ok.Click += delegate {
                    if (!string.IsNullOrWhiteSpace(folder.Text) && (!Path.IsPathRooted(folder.Text.Trim()) || !Directory.Exists(folder.Text.Trim()))) {
                        MessageBox.Show(dialog, Tr("Choose an existing absolute folder path.", "Выберите существующую папку с полным путём.")); return;
                    }
                    var draft = new ExportOptions { Configured = true, RangeMarkers = ranges.Checked, TextAsName = names.Checked,
                        IncludeDescription = descriptions.Checked, SelectedOnly = selection.Checked, OutputFolder = folder.Text.Trim() };
                    try { Persist(draft); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); return; }
                    dialog.DialogResult = DialogResult.OK;
                };
                footer.Controls.Add(cancel); footer.Controls.Add(ok); layout.Controls.Add(footer); layout.SetColumnSpan(footer, 2);
                dialog.Controls.Add(layout); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                return dialog.ShowDialog(parent) == DialogResult.OK;
            }
        }

        public string DoAction(Form parent, string srt, double frameRate, string lineSeparator, string fileName, string videoFileName, string rawText)
        { return Export(parent, srt, fileName, null); }

        public string DoActionWithSelection(Form parent, string srt, double frameRate, string lineSeparator,
            string fileName, string videoFileName, string rawText, int[] selected)
        { return Export(parent, srt, fileName, selected); }

        private string Export(Form parent, string srt, string fileName, int[] selected)
        {
            StatusMessage = null;
            try
            {
                var cues = MarkerExport.Parse(srt);
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
