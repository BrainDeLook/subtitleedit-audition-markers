using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.PluginLogic;
class EmbedMedia {static int Main(string[] a){try{Console.WriteLine(Mp4Markers.ExportCopy(a[0],new List<AuditionMarker>{new AuditionMarker{Name="Маркер 1 (25)",Start=1234,Duration=750,Description="Проверка",Actor="ДАНЯ"}},null));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}}
