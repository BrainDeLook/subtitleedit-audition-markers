using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.PluginLogic
{
    public partial class AuditionMarkers
    {
        private IList<Paragraph> _context;
        private int[] _selectedContext;
        private ExportOptions _burnOptions;
        private CheckBox _burnEnabled;
        private sealed class ActorChoice
        {
            public string Name;
            public override string ToString() { return Name.Length == 0 ? Tr("<No actor>", "<Без актёра>") : Name; }
        }

        public void ConfigureWithSubtitle(Form parent, Subtitle subtitle, int[] selected)
        {
            _context = new Subtitle(subtitle).Paragraphs; _selectedContext = selected;
            Configure(parent);
        }

        private bool EditOptions(Form parent, ExportOptions options)
        {
            using (var dialog = new Form { Text = Text + " — " + Tr("Settings", "Настройки"), Width = 650, Height = 640,
                MinimumSize = new Size(600, 580), StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, AutoScaleMode = AutoScaleMode.Dpi })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(12) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                Action<Control> row = delegate(Control c) { layout.Controls.Add(c); layout.SetColumnSpan(c, 2); };
                var ranges = new CheckBox { AutoSize = true, Text = Tr("Range markers (subtitle start/end)", "Маркеры-диапазоны по началу и концу реплик"), Checked = options.RangeMarkers };
                var names = new CheckBox { Name = "TextAsName", AutoSize = true, Text = Tr("Use subtitle text as marker name", "Текст реплики в названии маркера"), Checked = options.TextAsName };
                var descriptions = new CheckBox { AutoSize = true, Text = Tr("Clean subtitle text in Description", "Текст без тегов в описании маркера"), Checked = options.IncludeDescription };
                row(ranges); row(names); row(descriptions);
                row(new Label { AutoSize = true, Text = Tr("Subtitles to turn into markers:", "Какие реплики превращать в метки:") });
                var scope = new ComboBox { Name = "ExportScope", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
                scope.Items.AddRange(new object[] { Tr("All subtitles", "Все реплики"), Tr("Selected rows", "Выделенные строки"), Tr("Selected actors", "Выбранные актёры") });
                scope.SelectedIndex = options.SelectedOnly ? 1 : options.ActorsOnly ? 2 : 0; row(scope);
                var actors = new CheckedListBox { Name = "ActorNames", CheckOnClick = true, Height = 118, Dock = DockStyle.Top, IntegralHeight = false };
                var available = (_context ?? new List<Paragraph>()).Select(p => p.Actor ?? "").Concat(options.ActorNames ?? new string[0])
                    .Distinct(StringComparer.Ordinal).OrderBy(a => a, StringComparer.CurrentCulture).ToArray();
                foreach (var actor in available) actors.Items.Add(new ActorChoice { Name = actor }, (options.ActorNames ?? new string[0]).Contains(actor));
                row(actors);
                var actorNote = new Label { AutoSize = true, MaximumSize = new Size(580, 0), ForeColor = SystemColors.GrayText,
                    Text = Tr("Actors come from the ASS/SSA Actor field. Untagged rows are <No actor>. Text labels are not guessed.",
                        "Актёры берутся из поля ASS/SSA «Актёр». Неуказанные — «Без актёра». Имена из текста не угадываются.") }; row(actorNote);
                var renumber = new CheckBox { Name = "RenumberSelected", AutoSize = true, Text = Tr("Number from 1, keep original row: Маркер 1 (25)",
                    "Нумерация с 1, исходный номер в скобках: Маркер 1 (25)"), Checked = options.RenumberSelected };
                row(renumber);
                var embed = new CheckBox { Name = "EmbedOnHardSub", AutoSize = true,
                    Text = Tr("Embed markers after MP4 hard-subtitle export", "Встраивать метки после создания хардсаба MP4"), Checked = options.EmbedOnHardSub }; row(embed);
                row(new Label { AutoSize = true, MaximumSize = new Size(580, 0),
                    Text = Tr("In Audition: File → Import → Markers from File… (Alt+/), then choose the marked MP4. Import markers into the open audio or session.",
                        "В Audition: File → Import → Markers from File… (Alt+/), выберите MP4 с метками. Они импортируются в открытый звук или сессию.") });
                EventHandler refresh = delegate { actors.Enabled = scope.SelectedIndex == 2; actorNote.Enabled = actors.Enabled; renumber.Enabled = scope.SelectedIndex != 0 && !names.Checked; };
                scope.SelectedIndexChanged += refresh; names.CheckedChanged += refresh; refresh(null, EventArgs.Empty);
                row(new Label { AutoSize = true, Text = Tr("CSV folder (blank = next to subtitles):", "Папка CSV (пусто = рядом с субтитрами):") });
                var folder = new TextBox { Dock = DockStyle.Top, Text = options.OutputFolder ?? "" };
                var browse = new Button { Text = "…", AutoSize = true };
                browse.Click += delegate { using (var picker = new FolderBrowserDialog()) { if (Directory.Exists(folder.Text)) picker.SelectedPath = folder.Text; if (picker.ShowDialog(dialog) == DialogResult.OK) folder.Text = picker.SelectedPath; } };
                layout.Controls.Add(folder); layout.Controls.Add(browse);
                var footer = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
                var cancel = new Button { AutoSize = true, Text = Tr("Cancel", "Отмена"), DialogResult = DialogResult.Cancel };
                var save = new Button { Name = "SaveSettings", AutoSize = true, Text = Tr("Save", "Сохранить") };
                var manual = new Button { AutoSize = true, Text = Tr("Embed in existing MP4…", "Метки в готовый MP4…"), Enabled = _context != null && _context.Count > 0 };
                Func<ExportOptions> draft = delegate {
                    if (!string.IsNullOrWhiteSpace(folder.Text) && (!Path.IsPathRooted(folder.Text.Trim()) || !Directory.Exists(folder.Text.Trim())))
                        throw new InvalidDataException(Tr("Choose an existing absolute folder.", "Выберите существующую папку с полным путём."));
                    string[] chosen = actors.CheckedItems.Cast<ActorChoice>().Select(a => a.Name).ToArray();
                    if (scope.SelectedIndex == 2 && chosen.Length == 0) throw new InvalidDataException(Tr("Choose at least one actor.", "Выберите хотя бы одного актёра."));
                    return new ExportOptions { Configured = true, RangeMarkers = ranges.Checked, TextAsName = names.Checked, IncludeDescription = descriptions.Checked,
                        SelectedOnly = scope.SelectedIndex == 1, ActorsOnly = scope.SelectedIndex == 2, ActorNames = chosen,
                        RenumberSelected = renumber.Checked, EmbedOnHardSub = embed.Checked, OutputFolder = folder.Text.Trim() };
                };
                save.Click += delegate { try { Persist(draft()); dialog.DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, Text); } };
                manual.Click += delegate {
                    try
                    {
                        var settings = draft();
                        var markers = MarkerExport.Build(_context, settings, _selectedContext, 0, double.PositiveInfinity, 1);
                        using (var picker = new OpenFileDialog { Filter = "MP4 (*.mp4)|*.mp4", Title = Tr("Choose video; a separate marked copy will be created", "Выберите видео — будет создана отдельная копия с метками") })
                        {
                            if (picker.ShowDialog(dialog) != DialogResult.OK) return;
                            string result = RunCopy(dialog, picker.FileName, markers);
                            if (result != null) MessageBox.Show(dialog, Tr("Created: ", "Создан файл: ") + result + "\n\nAudition: File → Import → Markers from File… (Alt+/)", Text);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { MessageBox.Show(dialog, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                footer.Controls.Add(cancel); footer.Controls.Add(save); footer.Controls.Add(manual);
                dialog.Controls.Add(layout); dialog.Controls.Add(footer); dialog.AcceptButton = save; dialog.CancelButton = cancel;
                return dialog.ShowDialog(parent) == DialogResult.OK;
            }
        }

        private string RunCopy(Form parent, string source, IList<AuditionMarker> markers)
        {
            using (var progress = new Form { Text = Text, Width = 420, Height = 150, StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                bool cancelled = false; string output = null; Exception error = null;
                var label = new Label { Dock = DockStyle.Top, Height = 35, Text = Tr("Writing XMP markers. Audio/video are not re-encoded.", "Запись меток XMP. Видео и звук не перекодируются.") };
                var bar = new ProgressBar { Dock = DockStyle.Top, Style = ProgressBarStyle.Marquee };
                var cancel = new Button { Dock = DockStyle.Bottom, Text = Tr("Cancel", "Отмена") };
                cancel.Click += delegate { cancelled = true; cancel.Enabled = false; };
                progress.Controls.Add(bar); progress.Controls.Add(label); progress.Controls.Add(cancel);
                progress.FormClosing += delegate(object sender, FormClosingEventArgs e) { if (progress.Tag == null) { cancelled = true; e.Cancel = true; } };
                progress.Shown += async delegate {
                    try { output = await Task.Run(() => Mp4Markers.ExportCopy(source, markers, () => System.Threading.Volatile.Read(ref cancelled))); }
                    catch (Exception ex) { error = ex; }
                    progress.Tag = true; progress.Close();
                };
                progress.ShowDialog(parent); if (error != null) throw error; return output;
            }
        }

        public Control CreateHardSubControl(Form parent, Subtitle subtitle, int[] selected)
        {
            _context = new Subtitle(subtitle).Paragraphs; _selectedContext = selected; _burnOptions = Load();
            var panel = new Panel { Name = "AuditionMarkersHardSub", Height = 30, Dock = DockStyle.Bottom };
            _burnEnabled = new CheckBox { Name = "EmbedAuditionMarkers", AutoSize = true, Left = 8, Top = 6,
                Text = Tr("Audition markers (MP4)", "Метки Audition (MP4)"), Checked = _burnOptions.EmbedOnHardSub };
            var button = new Button { Text = "…", Width = 38, Dock = DockStyle.Right };
            _burnEnabled.CheckedChanged += delegate {
                _burnOptions.EmbedOnHardSub = _burnEnabled.Checked;
                try { Persist(_burnOptions); }
                catch (Exception ex) { MessageBox.Show(parent, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            button.Click += delegate { ConfigureWithSubtitle(parent, subtitle, selected); _burnOptions = Load(); _burnEnabled.Checked = _burnOptions.EmbedOnHardSub; };
            panel.Controls.Add(_burnEnabled); panel.Controls.Add(button); return panel;
        }

        public void ValidateHardSub(string output, bool batch, double cutStart, double cutEnd, double scale)
        {
            if (_burnOptions == null || !_burnOptions.EmbedOnHardSub) return;
            if (batch) throw new NotSupportedException(Tr("Audition marker embedding currently supports individual exports. Turn it off for batch mode.", "Встраивание меток пока работает для одиночного хардсаба. Выключите его для пакетного режима."));
            if (!string.Equals(Path.GetExtension(output), ".mp4", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(Tr("Audition markers require MP4. Choose MP4 or turn marker embedding off.", "Для меток Audition выберите MP4 либо выключите встраивание меток."));
            MarkerExport.Build(_context, _burnOptions, _selectedContext, cutStart, cutEnd, scale);
        }

        public void ProcessHardSub(string output, double cutStart, double cutEnd, double scale, Func<bool> cancelled)
        {
            if (_burnOptions == null || !_burnOptions.EmbedOnHardSub) return;
            Mp4Markers.Embed(output, MarkerExport.Build(_context, _burnOptions, _selectedContext, cutStart, cutEnd, scale), cancelled);
        }
    }
}
