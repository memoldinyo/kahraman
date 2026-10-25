using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Kahraman.Core;

/// <summary>Farbliche Kennzeichnung (Priorisierung) einer Unter-/Tätigkeit, gespeichert im Attribut "status".</summary>
public enum ItemStatus
{
    Normal = 0,
    Allgemein = 1,
    Projekt = 2,
    Unwichtig = 3,
    Wichtig = 4,
    Standard = 5,
}

public class Item(string text, int status = 0)
{
    public string Text { get; set; } = text;
    public int Status { get; set; } = status;
}

public sealed class Activity(string text, int status = 0) : Item(text, status)
{
    public SortedDictionary<string, Item> Subs { get; } = new(StoreOrder.Instance);

    public Item? FindSub(string text) => Lookup(Subs, text);

    internal static T? Lookup<T>(SortedDictionary<string, T> dict, string text) where T : class
    {
        if (dict.TryGetValue(text, out var hit)) return hit;
        var trimmed = text.Trim();
        return dict.FirstOrDefault(kv => string.Equals(kv.Key.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)).Value;
    }
}

/// <summary>
/// Die Datei "taetigkeiten.kahraman" (XML): Tätigkeiten, deren Untertätigkeiten und die Vorlagen für Untertätigkeiten.
/// </summary>
public sealed class ActivityStore
{
    public const string FileName = DataFiles.StoreBaseName + DataFiles.Extension;
    public const string FirstActivity = "Erste Schritte mit Kahraman";

    DateTime loadedWriteTime;
    long loadedLength = -1;

    public ActivityStore(string path) => Path = path;

    public string Path { get; }
    public SortedDictionary<string, Activity> Activities { get; } = new(StoreOrder.Instance);
    public List<Item> Defaults { get; } = [];
    public const string FormatVersion = "1";

    public Activity? Find(string text) => Activity.Lookup(Activities, text);

    public static ActivityStore Load(string path)
    {
        var store = new ActivityStore(path);
        store.Reload();
        return store;
    }

    public static ActivityStore CreateNew(string path, string firstActivity = FirstActivity)
    {
        var store = new ActivityStore(path);
        store.Add(firstActivity);
        store.Save();
        return store;
    }

    public void Reload()
    {
        var info = new FileInfo(Path);
        var bytes = File.ReadAllBytes(Path);
        loadedWriteTime = info.LastWriteTimeUtc;
        loadedLength = info.Length;

        // Deklariert ist ISO-8859-1; gelesen wird als Windows-1252 (Obermenge für Sonderzeichen wie €).
        var text = TextCodec.Ansi.GetString(bytes);
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        XDocument doc;
        try { doc = XDocument.Parse(text); }
        catch (Exception ex) { throw new KahramanException(L.F("store.readError", Path, ex.Message)); }

        var root = doc.Root;
        if (root is null || root.Name != "kahraman")
            throw new KahramanException(L.F("store.readError", Path, "<kahraman>?"));

        Activities.Clear();
        Defaults.Clear();
        var list = root.Element("taetigkeiten");
        if (list is null) return;
        foreach (var el in list.Elements())
        {
            if (el.Name == "defaults")
            {
                foreach (var d in el.Elements())
                    if (d.Attribute("text") is { } t) Defaults.Add(new Item(t.Value, ParseStatus(d)));
            }
            else if (el.Name == "taetigkeit" && el.Attribute("text") is { } t)
            {
                var act = new Activity(t.Value, ParseStatus(el));
                foreach (var s in el.Element("taetigkeiten")?.Elements("taetigkeit") ?? [])
                    if (s.Attribute("text") is { } st) act.Subs[st.Value] = new Item(st.Value, ParseStatus(s));
                Activities[act.Text] = act;
            }
        }
    }

    static int ParseStatus(XElement el) =>
        int.TryParse((string?)el.Attribute("status"), out var s) ? s : 0;

    public bool ChangedOnDisk()
    {
        if (!File.Exists(Path)) return loadedLength != -1;
        var info = new FileInfo(Path);
        return info.Length != loadedLength || info.LastWriteTimeUtc != loadedWriteTime;
    }

    /// <summary>Änderung durchführen und speichern. Vorher neu laden, falls jemand anders (z. B. im Netz) die Datei geändert hat.</summary>
    public void Modify(Action<ActivityStore> change)
    {
        if (File.Exists(Path) && ChangedOnDisk()) Reload();
        change(this);
        Save();
    }

    public Activity Add(string text, int status = 0)
    {
        if (Find(text) is { } existing) return existing;
        var act = new Activity(text, status);
        Activities[text] = act;
        return act;
    }

    public bool Remove(string text)
    {
        var act = Find(text);
        return act is not null && Activities.Remove(act.Text);
    }

    public Item AddSub(string activity, string sub, int status = 0)
    {
        var act = Find(activity) ?? Add(activity);
        if (act.FindSub(sub) is { } existing) return existing;
        var item = new Item(sub, status);
        act.Subs[sub] = item;
        return item;
    }

    /// <summary>Fügt die Standarduntertätigkeiten als Untertätigkeiten ein. Liefert die Anzahl neuer Einträge.</summary>
    public int InsertDefaults(string activity)
    {
        var act = Find(activity) ?? throw new KahramanException(L.F("store.activityNotFound", activity));
        int added = 0;
        foreach (var d in Defaults)
        {
            if (act.FindSub(d.Text) is not null) continue;
            act.Subs[d.Text] = new Item(d.Text, d.Status);
            added++;
        }
        return added;
    }

    public void Save()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n");
        sb.Append($"<kahraman version=\"{FormatVersion}\" lastchangetimestamp=\"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)}\" lastchangeuser=\"{Escape(Environment.UserName)}\" lastchangehost=\"{Escape(Environment.MachineName)}\">\n");
        sb.Append("  <taetigkeiten>\n");
        if (Defaults.Count == 0) sb.Append("    <defaults/>\n");
        else
        {
            sb.Append("    <defaults>\n");
            foreach (var d in Defaults.OrderBy(d => d.Text, StoreOrder.Instance))
                sb.Append($"      <taetigkeit text=\"{Escape(d.Text)}\" status=\"{d.Status}\"/>\n");
            sb.Append("    </defaults>\n");
        }
        foreach (var act in Activities.Values)
        {
            if (act.Subs.Count == 0)
            {
                sb.Append($"    <taetigkeit text=\"{Escape(act.Text)}\" status=\"{act.Status}\"/>\n");
                continue;
            }
            sb.Append($"    <taetigkeit text=\"{Escape(act.Text)}\" status=\"{act.Status}\">\n");
            sb.Append("      <taetigkeiten>\n");
            foreach (var sub in act.Subs.Values)
                sb.Append($"        <taetigkeit text=\"{Escape(sub.Text)}\" status=\"{sub.Status}\"/>\n");
            sb.Append("      </taetigkeiten>\n");
            sb.Append("    </taetigkeit>\n");
        }
        sb.Append("  </taetigkeiten>\n");
        sb.Append("</kahraman>\n");

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllBytes(tmp, TextCodec.Ansi.GetBytes(sb.ToString()));
        if (File.Exists(Path)) File.Replace(tmp, Path, Path + ".bak");
        else File.Move(tmp, Path);
        var info = new FileInfo(Path);
        loadedWriteTime = info.LastWriteTimeUtc;
        loadedLength = info.Length;
    }

    /// <summary>XML-Escaping; Zeichen außerhalb von Latin-1 werden als Zeichenreferenz geschrieben.</summary>
    static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var rune in s.EnumerateRunes())
        {
            int c = rune.Value;
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\t': sb.Append("&#9;"); break;
                case '\n': sb.Append("&#10;"); break;
                case '\r': sb.Append("&#13;"); break;
                default:
                    if (c > 0xFF || (c >= 0x80 && c < 0xA0)) sb.Append($"&#{c};");
                    else sb.Append((char)c);
                    break;
            }
        }
        return sb.ToString();
    }
}
