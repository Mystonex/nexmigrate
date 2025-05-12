using Spectre.Console;
using Terminal.Gui;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        Console.Title = "Windows Migration Helper";

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold cyan]Welcome to the Windows Migration Helper![/]");
            AnsiConsole.MarkupLine("[gray]Choose a function to continue[/]\n");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Main Menu")
                    .AddChoices(
                        "Install Apps",
                        "Remove Installed Apps",
                        "Quit"));

            if (choice == "❌ Quit")
                return;

            if (choice == "🗑 Remove Installed Apps")
                ShowAppRemovalScreen();
            else if (choice == "⬇️ Install Apps")
                ShowAppInstallScreen();

            AnsiConsole.MarkupLine("\n[gray]Press any key to return to the main menu...[/]");
            Console.ReadKey(true);
        }
    }

    static void ShowAppRemovalScreen()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold red]Select apps to uninstall[/]\n");

        var apps = GetInstalledApps();
        if (!apps.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No apps found.[/]");
            return;
        }

        var selected = GridSelectApps(apps, 3);
        if (selected == null || selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No apps selected.[/]");
            return;
        }

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold red]Please confirm uninstall[/]\n");
        var uninstallTable = new Table().Expand();
        uninstallTable.AddColumn("Application");
        foreach (var app in selected)
            uninstallTable.AddRow(app.DisplayName);
        AnsiConsole.Write(uninstallTable);

        bool confirm = AnsiConsole.Confirm("\n[bold yellow]Uninstall these applications?[/]");
        if (!confirm)
        {
            AnsiConsole.MarkupLine("\n[grey]Operation cancelled.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\n[bold yellow]Starting uninstalls...[/]\n");
        foreach (var app in selected)
        {
            AnsiConsole.MarkupLine($"[underline]→ {app.DisplayName}[/]");

            if (string.IsNullOrWhiteSpace(app.UninstallString))
            {
                AnsiConsole.MarkupLine("  [darkorange]No uninstall command, skipped.[/]\n");
                continue;
            }

            var (exe, args) = SplitCommand(app.UninstallString);
            if (exe.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) ||
                exe.Equals("msiexec",    StringComparison.OrdinalIgnoreCase))
            {
                args = args.Replace("/X", "/x");
                if (!args.Contains("/qn", StringComparison.OrdinalIgnoreCase))
                    args += " /qn /norestart";
            }

            AnsiConsole.MarkupLine($"  [grey]Running:[/] {exe} {args}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName        = exe,
                    Arguments       = args,
                    UseShellExecute = true,
                    Verb            = "runas"
                };
                var proc = Process.Start(psi);
                proc.WaitForExit();
                if (proc.ExitCode == 0)
                    AnsiConsole.MarkupLine("  [green]✔ Uninstall succeeded.[/]\n");
                else
                    AnsiConsole.MarkupLine($"  [red]✖ Exit code {proc.ExitCode} — may have failed.[/]\n");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]Error: {ex.Message}[/]\n");
            }
        }
    }

    static void ShowAppInstallScreen()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold green]Select apps to install[/]\n");

        var options = GetInstallOptions();
        var selectedOpts = GridSelectInstallOptions(options, 3);
        if (selectedOpts == null || selectedOpts.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No apps selected.[/]");
            return;
        }

        // Expand "baseline" into its members
        var installList = new List<InstallOption>();
        foreach (var opt in selectedOpts)
        {
            if (opt.Id == "baseline")
            {
                installList.AddRange(options.Where(x => x.Id != "baseline" && x.Group == "baseline"));
            }
            else
            {
                installList.Add(opt);
            }
        }
        installList = installList
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Confirmation
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold green]Please confirm install[/]\n");
        var installTable = new Table().Expand();
        installTable.AddColumn("Application");
        foreach (var opt in installList)
            installTable.AddRow(opt.Name);
        AnsiConsole.Write(installTable);

        bool confirm = AnsiConsole.Confirm("\n[bold yellow]Install these applications?[/]");
        if (!confirm)
        {
            AnsiConsole.MarkupLine("\n[grey]Operation cancelled.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\n[bold yellow]Starting installations...[/]\n");
        foreach (var opt in installList)
        {
            AnsiConsole.MarkupLine($"[underline]→ {opt.Name}[/]");
            var args = $"install --id {opt.Id} --accept-package-agreements --accept-source-agreements";
            AnsiConsole.MarkupLine($"  [grey]Running:[/] winget {args}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName        = "winget",
                    Arguments       = args,
                    UseShellExecute = true,
                    Verb            = "runas"
                };
                var proc = Process.Start(psi);
                proc.WaitForExit();
                if (proc.ExitCode == 0)
                    AnsiConsole.MarkupLine("  [green]✔ Installed successfully.[/]\n");
                else
                    AnsiConsole.MarkupLine($"  [red]✖ Exit code {proc.ExitCode} — may have failed.[/]\n");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]Error: {ex.Message}[/]\n");
            }
        }
    }

    static List<InstallOption> GetInstallOptions()
    {
        // Group "baseline" entries by Group="baseline"
        var list = new List<InstallOption>
        {
            new InstallOption("baseline", "Baseline (7zip, VLC, Chrome)", "baseline"),
            new InstallOption("7zip.7zip", "7-Zip", "baseline"),
            new InstallOption("VideoLAN.VLC", "VLC media player", "baseline"),
            new InstallOption("Google.Chrome", "Google Chrome", "baseline"),

            new InstallOption("Valve.Steam", "Steam", null),
            new InstallOption("Discord.Discord", "Discord", null),
            new InstallOption("Blizzard.Battle.net", "Battle.net", null),
            new InstallOption("Signal.Signal", "Signal", null),
            new InstallOption("HWiNFO.HWiNFO", "HWiNFO64", null),
            new InstallOption("Ubisoft.UbisoftConnect", "Ubisoft Connect", null),
            new InstallOption("Parsec.Parsec", "Parsec", null),
            new InstallOption("NVIDIA.GeForceExperience", "NVIDIA GeForce Experience", null),
            new InstallOption("Spotify.Spotify", "Spotify", null),
            new InstallOption("CPUID.CPU-Z", "CPU-Z", null),
            new InstallOption("1Password.1Password", "1Password", null),
        };
        return list;
    }

    static List<AppInfo> GridSelectApps(List<AppInfo> apps, int columns)
    {
        return GridSelect(
            apps.Select(a => (Label: a.DisplayName, Data: (object)a)).ToList(),
            columns)
            .Cast<AppInfo>()
            .ToList();
    }

    static List<InstallOption> GridSelectInstallOptions(List<InstallOption> opts, int columns)
    {
        return GridSelect(
            opts.Select(o => (Label: o.Name, Data: (object)o)).ToList(),
            columns)
            .Cast<InstallOption>()
            .ToList();
    }

    static List<object> GridSelect(List<(string Label, object Data)> items, int columns)
    {
        List<object> result = null;
        Application.Init();
        var top = Application.Top;

        var win = new Window()
        {
            X      = 0,
            Y      = 1,
            Width  = Dim.Fill(),
            Height = Dim.Fill()
        };
        top.Add(win);

        var lbl = new Label("Use ↑ ↓ ← → to navigate, space to toggle, ENTER or OK to confirm")
        {
            X = Pos.Center(),
            Y = 0
        };
        win.Add(lbl);

        var scroll = new ScrollView()
        {
            X                             = 0,
            Y                             = 1,
            Width                         = Dim.Fill(),
            Height                        = Dim.Fill(3),
            ShowVerticalScrollIndicator   = true,
            ShowHorizontalScrollIndicator = true
        };
        win.Add(scroll);

        int maxLen = items.Max(i => i.Label.Length);
        int cellW  = maxLen + 4;
        int rows   = (int)Math.Ceiling(items.Count / (double)columns);

        var checkboxes = new List<(CheckBox box, object data)>();
        for (int i = 0; i < items.Count; i++)
        {
            var (text, data) = items[i];
            int col = i % columns;
            int row = i / columns;

            var chk = new CheckBox(text)
            {
                X     = col * cellW,
                Y     = row,
                Width = cellW
            };
            scroll.Add(chk);
            checkboxes.Add((chk, data));
        }

        scroll.ContentSize = new Terminal.Gui.Size(cellW * columns, rows);

        var btnOk = new Button(" OK ")
        {
            X         = Pos.Center(),
            Y         = Pos.Bottom(scroll) + 1,
            IsDefault = true
        };
        btnOk.Clicked += () => Application.RequestStop();
        win.Add(btnOk);

        Application.Run();
        Application.Shutdown();

        result = checkboxes
            .Where(t => t.box.Checked)
            .Select(t => t.data)
            .ToList();
        return result;
    }

    static (string exe, string args) SplitCommand(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.StartsWith("\""))
        {
            int end = cmd.IndexOf('"', 1);
            if (end > 0)
            {
                var exe  = cmd.Substring(1, end - 1);
                var args = cmd.Length > end + 1 ? cmd.Substring(end + 1).Trim() : "";
                return (exe, args);
            }
        }
        var parts = cmd.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    class AppInfo
    {
        public string DisplayName     { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
    }

    class InstallOption
    {
        public string Id    { get; }
        public string Name  { get; }
        public string? Group{ get; }

        public InstallOption(string id, string name, string? group)
        {
            Id    = id;
            Name  = name;
            Group = group;
        }
    }

    static List<AppInfo> GetInstalledApps()
    {
        var apps = new List<AppInfo>();
        var roots = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (root, path) in roots)
        {
            using var key = root.OpenSubKey(path);
            if (key == null) continue;

            foreach (var sub in key.GetSubKeyNames())
            {
                using var sk = key.OpenSubKey(sub);
                if (sk == null) continue;

                var nameObj = sk.GetValue("DisplayName");
                var name    = nameObj as string;
                var cmdObj  = sk.GetValue("UninstallString");
                var cmd     = cmdObj as string ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(name))
                    apps.Add(new AppInfo { DisplayName = name, UninstallString = cmd });
            }
        }

        return apps
            .GroupBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
