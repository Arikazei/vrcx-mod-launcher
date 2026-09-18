using System.Diagnostics;

namespace VRCXMods;

public class MainForm : Form
{
    private readonly Label _status = new();
    private readonly TextBox _log = new();
    private readonly Button _apply = new();
    private readonly Button _remove = new();
    private readonly Button _start = new();
    private readonly Button _choose = new();
    private readonly Button _refresh = new();
    private readonly Button _autorun = new();
    private readonly CheckedListBox _mods = new();
    private Manifest? _manifest;
    private bool _suppressModEvents;
    private string? _vrcxDir;

    public MainForm(string? problem) : this()
    {
        if (problem is not null)
        {
            Log("PROBLEM: " + problem);
            MessageBox.Show(problem, "VRCX Mods", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public MainForm()
    {
        Text = "VRCX Mods";
        Width = 760;
        Height = 580;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        _status.Dock = DockStyle.Top;
        _status.Height = 96;
        _status.Padding = new Padding(14, 12, 14, 8);
        _status.TextAlign = ContentAlignment.TopLeft;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(10, 4, 10, 4)
        };

        SetupButton(_apply, "Mods installieren / aktualisieren", 230, OnApply);
        SetupButton(_remove, "Mods entfernen", 140, OnRemove);
        SetupButton(_start, "VRCX starten", 120, OnStart);
        SetupButton(_choose, "Ordner waehlen", 130, OnChoose);
        SetupButton(_refresh, "Aktualisieren", 110, (_, _) => Refresh0());
        SetupButton(_autorun, "Autostart", 150, OnAutoRun);
        buttons.Controls.AddRange([_apply, _remove, _start, _choose, _refresh, _autorun]);

        _mods.Dock = DockStyle.Top;
        _mods.Height = 110;
        _mods.CheckOnClick = true;
        _mods.BorderStyle = BorderStyle.None;
        _mods.BackColor = Color.FromArgb(38, 38, 40);
        _mods.ForeColor = Color.Gainsboro;
        _mods.IntegralHeight = false;
        _mods.ItemCheck += OnModCheck;

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BackColor = Color.FromArgb(30, 30, 30);
        _log.ForeColor = Color.Gainsboro;
        _log.BorderStyle = BorderStyle.None;

        Controls.Add(_log);
        Controls.Add(_mods);
        Controls.Add(buttons);
        Controls.Add(_status);

        _vrcxDir = Paths.FindVrcx();
        Refresh0();
    }

    private void SetupButton(Button b, string text, int width, EventHandler onClick)
    {
        b.Text = text;
        b.Width = width;
        b.Height = 32;
        b.Click += onClick;
    }

    private void Log(string line)
    {
        _log.AppendText(line + Environment.NewLine);
    }

    private void Refresh0()
    {
        if (_vrcxDir is null)
        {
            _status.Text = "VRCX nicht gefunden.\nBitte den Ordner mit VRCX.exe manuell waehlen.";
            _apply.Enabled = _remove.Enabled = _start.Enabled = false;
            return;
        }

        try
        {
            var s = Patcher.GetStatus(_vrcxDir);
            var payloadOk = File.Exists(Path.Combine(Paths.PayloadDir, "loader.js"));
            var updateHint = s.Patched && s.PatchedVersion is not null && s.PatchedVersion != s.VrcxVersion
                ? $"\n⚠ VRCX wurde aktualisiert ({s.PatchedVersion} → {s.VrcxVersion}) - Mods neu installieren."
                : "";

            var autoText = AutoRun.GetState() switch
            {
                AutoRunState.Mods => "Autostart: über VRCX Mods (patcht nach Updates selbst nach)",
                AutoRunState.Vrcx => "Autostart: direkt VRCX — nach einem Update fehlen die Mods",
                _ => "Autostart: nicht eingerichtet"
            };
            _autorun.Text = AutoRun.GetState() == AutoRunState.Mods ? "Autostart zurücksetzen" : "Autostart übernehmen";

            _status.Text =
                $"VRCX {s.VrcxVersion}   ·   {_vrcxDir}\n" +
                $"Status: {(s.Patched ? "Mods aktiv" : "nicht gepatcht")}   ·   " +
                $"{s.ModCount} Mod(s)   ·   Backup: {(s.BackupExists ? "ja" : "nein")}\n" +
                $"{autoText}\n" +
                $"Mod-Dateien: {(payloadOk ? Paths.PayloadDir : "fehlen in " + Paths.PayloadDir)}" +
                updateHint;

            FillModList();

            _apply.Enabled = payloadOk;
            _remove.Enabled = s.Patched || s.BackupExists;
            _start.Enabled = true;
        }
        catch (Exception ex)
        {
            _status.Text = "Status nicht lesbar: " + ex.Message;
        }
    }

    /// <summary>
    /// Zeigt die Mods einzeln mit Name, Version und Haken fuer aktiv/inaktiv.
    /// Quelle ist das Manifest im Payload; installiert wird der Stand erst mit
    /// "Mods installieren".
    /// </summary>
    private void FillModList()
    {
        _suppressModEvents = true;
        try
        {
            _mods.Items.Clear();
            _manifest = Manifest.Load(Manifest.PayloadManifest);

            if (_manifest is null || _manifest.Mods.Count == 0)
            {
                _mods.Items.Add("keine Mods im Payload gefunden");
                return;
            }

            var installed = _vrcxDir is null ? null : Manifest.Load(Manifest.InstalledManifest(_vrcxDir));

            foreach (var mod in _manifest.Mods)
            {
                var live = installed?.Mods.FirstOrDefault(m => m.Id == mod.Id);
                var note = live is null
                    ? "  (noch nicht installiert)"
                    : live.Version != mod.Version
                        ? $"  (installiert: v{live.Version ?? "?"})"
                        : "";
                _mods.Items.Add(mod.Display + note, mod.Enabled);
            }
        }
        finally
        {
            _suppressModEvents = false;
        }
    }

    private void OnModCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suppressModEvents || _manifest is null || e.Index >= _manifest.Mods.Count) return;

        var mod = _manifest.Mods[e.Index];
        mod.Enabled = e.NewValue == CheckState.Checked;
        try
        {
            _manifest.Save(Manifest.PayloadManifest);
            Log($"{mod.Name ?? mod.Id}: {(mod.Enabled ? "aktiviert" : "deaktiviert")} — wirksam nach \"Mods installieren\".");
        }
        catch (Exception ex)
        {
            Log("Konnte Manifest nicht speichern: " + ex.Message);
        }
    }

    /// <summary>
    /// index.html wird nur beim Laden der App gelesen - ein laufendes VRCX merkt
    /// vom Patch nichts. Darum beenden wir es hier selbst und starten es danach
    /// wieder. Erst hoeflich ueber das Fenster; VRCX minimiert sich aber in den
    /// Tray, deshalb nach Ablauf der Wartezeit nachfragen statt blind killen -
    /// ein harter Abschuss waehrend eines DB-Schreibvorgangs waere riskant.
    /// </summary>
    private bool StopVrcx()
    {
        if (!Paths.VrcxRunning()) return true;

        Log("beende VRCX ...");
        foreach (var p in Process.GetProcessesByName("VRCX"))
        {
            try { p.CloseMainWindow(); } catch { /* egal, wird unten geprueft */ }
        }

        var waited = 0;
        while (Paths.VrcxRunning() && waited < 8000)
        {
            Application.DoEvents();
            Thread.Sleep(200);
            waited += 200;
        }

        if (!Paths.VrcxRunning())
        {
            Log("VRCX beendet.");
            return true;
        }

        var r = MessageBox.Show(
            "VRCX laeuft noch (es minimiert sich beim Schliessen in den Tray).\n\n" +
            "Jetzt erzwungen beenden? VRCX schreibt laufend in seine Datenbank - " +
            "sauberer ist es, VRCX ueber das Tray-Icon selbst zu beenden und dann erneut zu klicken.",
            "VRCX beenden", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (r != DialogResult.Yes)
        {
            Log("abgebrochen - VRCX laeuft weiter.");
            return false;
        }

        foreach (var p in Process.GetProcessesByName("VRCX"))
        {
            try { p.Kill(true); } catch { /* schon weg */ }
        }
        Thread.Sleep(800);
        Log("VRCX erzwungen beendet.");
        return true;
    }

    private void StartVrcx()
    {
        if (_vrcxDir is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(_vrcxDir, "VRCX.exe")) { UseShellExecute = true });
            Log("VRCX neu gestartet.");
        }
        catch (Exception ex)
        {
            Log("Neustart fehlgeschlagen: " + ex.Message);
        }
    }

    private void OnApply(object? sender, EventArgs e)
    {
        if (_vrcxDir is null) return;
        var lief = Paths.VrcxRunning();
        if (!StopVrcx()) return;

        try
        {
            Log("--- Installation ---");
            Patcher.Apply(_vrcxDir, Log);
            Log("fertig.");
            if (lief) StartVrcx();
            else Log("VRCX war nicht gestartet - beim naechsten Start sind die Mods aktiv.");
        }
        catch (Exception ex)
        {
            Log("FEHLER: " + ex.Message);
            MessageBox.Show(ex.Message, "Installation fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        Refresh0();
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        if (_vrcxDir is null) return;
        var lief = Paths.VrcxRunning();
        if (!StopVrcx()) return;

        try
        {
            Log("--- Rueckbau ---");
            Patcher.Remove(_vrcxDir, Log);
            Log("VRCX ist wieder im Originalzustand.");
            if (lief) StartVrcx();
        }
        catch (Exception ex)
        {
            Log("FEHLER: " + ex.Message);
        }
        Refresh0();
    }

    private void OnStart(object? sender, EventArgs e)
    {
        if (_vrcxDir is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(_vrcxDir, "VRCX.exe")) { UseShellExecute = true });
            Log("VRCX gestartet.");
        }
        catch (Exception ex)
        {
            Log("Start fehlgeschlagen: " + ex.Message);
        }
    }

    private void OnAutoRun(object? sender, EventArgs e)
    {
        try
        {
            if (AutoRun.GetState() == AutoRunState.Mods)
            {
                AutoRun.Disable();
                Log("Autostart zurückgesetzt.");
            }
            else
            {
                var exe = Environment.ProcessPath ?? Application.ExecutablePath;
                AutoRun.Enable(exe);
                Log("Autostart läuft jetzt über: " + exe);
                Log("Nach einem VRCX-Update wird beim nächsten Start automatisch nachgepatcht.");
            }
        }
        catch (Exception ex)
        {
            Log("Autostart-Änderung fehlgeschlagen: " + ex.Message);
        }
        Refresh0();
    }

    private void OnChoose(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Ordner mit VRCX.exe waehlen" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        if (!Paths.IsVrcxDir(dlg.SelectedPath))
        {
            MessageBox.Show("In diesem Ordner liegen keine VRCX.exe und html\\index.html.", "Falscher Ordner");
            return;
        }
        _vrcxDir = dlg.SelectedPath;
        // Pfad merken, damit die Erkennung ihn beim naechsten Start sofort findet -
        // auch wenn VRCX gerade nicht laeuft und nirgends registriert ist.
        var st = State.Load() ?? new State();
        st.VrcxDir = _vrcxDir;
        st.Save();
        Log("VRCX-Ordner gemerkt: " + _vrcxDir);
        Refresh0();
    }
}
