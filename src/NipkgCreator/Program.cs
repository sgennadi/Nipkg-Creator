using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace NipkgCreator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal static class RepositoryGenerator
{
    public static string GetOutputPath(string feedsPath)
    {
        if (string.IsNullOrWhiteSpace(feedsPath)) throw new ArgumentException("Choose a feeds folder.");
        var folder = new DirectoryInfo(Path.GetFullPath(feedsPath.Trim().Trim('"').TrimEnd('\\', '/')));
        if (!folder.Name.Equals("feeds", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select a folder named feeds.");
        var parent = folder.Parent ?? throw new IOException("Parent folder not found.");
        var root = parent.Name.Equals("_Src", StringComparison.OrdinalIgnoreCase)
            ? parent.Parent ?? throw new IOException("Installation root not found.") : parent;
        return Path.Combine(root.FullName, "nipkg.ini");
    }

    public static (string Content, int Count) Generate(string feedsPath)
    {
        _ = GetOutputPath(feedsPath);
        if (!Directory.Exists(feedsPath)) throw new DirectoryNotFoundException("Feeds folder not found or inaccessible: " + feedsPath);
        var folders = Directory.GetDirectories(feedsPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        if (folders.Length == 0) throw new InvalidOperationException("No repository folders found.");
        var sections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in folders)
        {
            var name = Path.GetFileName(path).Replace('.', '-');
            if (name.IndexOfAny(new[] {'\r', '\n', '[', ']', '='}) >= 0)
                throw new InvalidOperationException("Invalid repository name: " + name);
            if (!seen.Add(name)) throw new InvalidOperationException("Duplicate normalized repository name: " + name);
            sections.Add($"[nipkg.repo.{name}]\r\nenabled=true\r\nname={name}\r\nuri={path}");
        }
        return (string.Join("\r\n\r\n", sections) + "\r\n", folders.Length);
    }

    public static void Save(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Output directory not found.");
        var temp = Path.Combine(directory, ".nipkg-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox feeds = new() { Dock = DockStyle.Fill };
    private readonly TextBox output = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox preview = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10) };
    private readonly Label status = new() { Dock = DockStyle.Fill, ForeColor = Color.DimGray, Text = "Ready" };

    public MainForm()
    {
        Text = "Nipkg Creator | NI Repository Configuration";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(750, 520);
        Size = new Size(940, 670);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(246, 248, 251);
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 8 };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        Controls.Add(main);
        main.Controls.Add(new Label { Text = "NI Package Repository Creator", Font = new Font("Segoe UI Semibold", 19), Dock = DockStyle.Fill }, 0, 0);
        main.Controls.Add(new Label { Text = "Feeds directory", Dock = DockStyle.Fill }, 0, 1);
        var feedRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        feedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        feedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        feedRow.Controls.Add(feeds, 0, 0);
        var browse = MakeButton("Browse...", false);
        browse.Click += (_, _) => Browse();
        feedRow.Controls.Add(browse, 1, 0);
        main.Controls.Add(feedRow, 0, 2);
        main.Controls.Add(new Label { Text = "Output file (automatically detected)", Dock = DockStyle.Fill }, 0, 3);
        main.Controls.Add(output, 0, 4);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var inspect = MakeButton("Preview", false);
        inspect.Click += (_, _) => Preview();
        var save = MakeButton("Create nipkg.ini", true);
        save.Click += (_, _) => Save();
        var open = MakeButton("Open output folder", false);
        open.Click += (_, _) => OpenOutputFolder();
        actions.Controls.AddRange(new Control[] { inspect, save, open });
        main.Controls.Add(actions, 0, 5);
        main.Controls.Add(preview, 0, 6);
        main.Controls.Add(status, 0, 7);
        feeds.TextChanged += (_, _) => UpdateOutput();
        UpdateOutput();
    }

    private static Button MakeButton(string text, bool accent) =>
        new() { Text = text, AutoSize = true, MinimumSize = new Size(112, 35), Height = 35,
            BackColor = accent ? Color.FromArgb(29, 100, 190) : Color.White,
            ForeColor = accent ? Color.White : Color.Black, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };

    private void UpdateOutput()
    {
        preview.Clear();
        try { output.Text = RepositoryGenerator.GetOutputPath(feeds.Text); status.Text = "Ready"; }
        catch (Exception ex) { output.Clear(); status.Text = ex.Message; }
    }

    private void Browse()
    {
        using var picker = new FolderBrowserDialog { Description = "Select NI feeds folder", UseDescriptionForTitle = true };
        if (Directory.Exists(feeds.Text)) picker.SelectedPath = feeds.Text;
        if (picker.ShowDialog(this) == DialogResult.OK) feeds.Text = picker.SelectedPath;
    }

    private (string Content, int Count)? Preview()
    {
        try
        {
            var result = RepositoryGenerator.Generate(feeds.Text.Trim());
            preview.Text = result.Content;
            status.ForeColor = Color.DarkBlue;
            status.Text = $"Found {result.Count} repositories | {output.Text}";
            return result;
        }
        catch (Exception ex)
        {
            status.ForeColor = Color.Firebrick;
            status.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }

    private void Save()
    {
        var result = Preview();
        if (result is null) return;
        var path = output.Text;
        if (File.Exists(path) && MessageBox.Show(this, $"Replace existing file?\n\n{path}", "Confirm overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            RepositoryGenerator.Save(path, result.Value.Content);
            status.ForeColor = Color.ForestGreen;
            status.Text = $"Saved {result.Value.Count} repositories to {path}";
            MessageBox.Show(this, status.Text, "Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            status.ForeColor = Color.Firebrick;
            status.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenOutputFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(output.Text)) throw new InvalidOperationException("Select a valid feeds folder first.");
            var directory = Path.GetDirectoryName(output.Text)!;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open folder failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
