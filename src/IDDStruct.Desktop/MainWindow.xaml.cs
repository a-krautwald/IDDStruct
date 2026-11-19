using IDDStruct.Core.Export;
using IDDStruct.Core.Import;
using IDDStruct.Core.Import.Mapping;
using IDDStruct.Core.Model;
using IDDStruct.Core.Serialization;
using IDDStruct.Desktop.ViewModels;
using IDDStruct.Idml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace IDDStruct.Desktop;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle)) Title = ViewModel.WindowTitle;
        };
        ViewModel.Blocks.CollectionChanged += (_, _) => ViewModel.UpdateStatus();
    }

    public static Visibility NotVisible(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private void BlockList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectedBlock = BlockList.SelectedItem as BlockViewModel;

    // ---------- File commands ----------

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Load(new StructuredDocument(), null);
        Notice.IsOpen = false;
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var file = await PickOpenAsync(DocumentImporter.SupportedExtensions);
            if (file is null) return;
            var path = file.Path;

            var options = new ImportOptions();
            if (DocumentImporter.IsTabular(path))
            {
                var chosen = await AskTabularOptionsAsync(path);
                if (chosen is null) return;
                options = chosen;
            }

            var result = await Task.Run(() => DocumentImporter.Import(path, options));
            bool isProject = path.EndsWith(ProjectSerializer.FileExtension, StringComparison.OrdinalIgnoreCase);
            ViewModel.Load(result.Document, isProject ? path : null);

            if (result.Warnings.Count > 0)
                ShowNotice(InfoBarSeverity.Warning, "Imported with warnings", string.Join("\n", result.Warnings));
            else
                ShowNotice(InfoBarSeverity.Success, "Imported", $"{result.Document.Blocks.Count} blocks from {Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            ShowNotice(InfoBarSeverity.Error, "Import failed", ex.Message);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ProjectPath is null) await SaveAsAsync();
        else await SaveToAsync(ViewModel.ProjectPath);
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e) => await SaveAsAsync();

    private async Task SaveAsAsync()
    {
        var file = await PickSaveAsync(ViewModel.Title, "IDDStruct project", ProjectSerializer.FileExtension);
        if (file is not null) await SaveToAsync(file.Path);
    }

    private async Task SaveToAsync(string path)
    {
        try
        {
            var doc = ViewModel.BuildDocument();
            await Task.Run(() => ProjectSerializer.Save(doc, path));
            ViewModel.ProjectPath = path;
            ShowNotice(InfoBarSeverity.Success, "Saved", path);
        }
        catch (Exception ex)
        {
            ShowNotice(InfoBarSeverity.Error, "Save failed", ex.Message);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exporter = new IdmlExporter();
            var file = await PickSaveAsync(ViewModel.Title, exporter.DisplayName, exporter.FileExtension);
            if (file is null) return;
            var doc = ViewModel.BuildDocument();
            var report = await Task.Run(() => exporter.ExportToFile(doc, file.Path));
            var message = $"{file.Name}: {report.PageCount} pages. Open it in InDesign (File → Open).";
            if (report.Warnings.Count > 0)
                ShowNotice(InfoBarSeverity.Warning, "Exported with warnings", message + "\n" + string.Join("\n", report.Warnings));
            else
                ShowNotice(InfoBarSeverity.Success, "Exported", message);
        }
        catch (Exception ex)
        {
            ShowNotice(InfoBarSeverity.Error, "Export failed", ex.Message);
        }
    }

    // ---------- Helpers ----------

    private void ShowNotice(InfoBarSeverity severity, string title, string message)
    {
        Notice.Severity = severity;
        Notice.Title = title;
        Notice.Message = message;
        Notice.IsOpen = true;
    }

    private async Task<StorageFile?> PickOpenAsync(IEnumerable<string> extensions)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        return await picker.PickSingleFileAsync();
    }

    private async Task<StorageFile?> PickSaveAsync(string suggestedName, string description, string extension)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = string.Join("_", (string.IsNullOrWhiteSpace(suggestedName) ? "Untitled" : suggestedName).Split(Path.GetInvalidFileNameChars())),
        };
        picker.FileTypeChoices.Add(description, new List<string> { extension });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        return await picker.PickSaveFileAsync();
    }

    /// <summary>Asks how a CSV/Excel file should map onto headings and paragraphs.</summary>
    private async Task<ImportOptions?> AskTabularOptionsAsync(string path)
    {
        var headers = await Task.Run(() => DocumentImporter.ReadHeaders(path));
        bool blockList = TabularMapper.LooksLikeBlockList(headers);
        bool isExcel = DocumentImporter.FindImporter(path) is XlsxImporter;

        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
        var recordMode = new CheckBox { Content = "Treat rows as records instead of blocks", IsChecked = !blockList };
        var heading = new ComboBox { Header = "Heading column", ItemsSource = headers.ToList(), SelectedIndex = headers.Count > 0 ? 0 : -1, HorizontalAlignment = HorizontalAlignment.Stretch };
        var group = new ComboBox { Header = "Group records by", ItemsSource = new[] { "(none)" }.Concat(headers).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var labels = new CheckBox { Content = "Prefix each value with its column name", IsChecked = true };
        var allSheets = new CheckBox { Content = "Import every worksheet", IsChecked = false, Visibility = isExcel ? Visibility.Visible : Visibility.Collapsed };

        var recordPanel = new StackPanel { Spacing = 12, Children = { heading, group, labels } };
        void Sync() => recordPanel.Visibility = recordMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        recordMode.Checked += (_, _) => Sync();
        recordMode.Unchecked += (_, _) => Sync();
        Sync();

        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = blockList
                ? "This file has type/text columns, so each row will become one block (heading, paragraph, bullet …)."
                : "Each row will become a section: a heading followed by one paragraph per column.",
        });
        if (blockList) panel.Children.Add(recordMode);
        panel.Children.Add(recordPanel);
        panel.Children.Add(allSheets);

        var dialog = new ContentDialog
        {
            Title = $"Import {Path.GetFileName(path)}",
            Content = panel,
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        var options = new ImportOptions
        {
            ForceRecordMode = blockList && recordMode.IsChecked == true,
            IncludeFieldLabels = labels.IsChecked == true,
            AllSheets = allSheets.IsChecked == true,
        };
        if (heading.SelectedItem is string h) options.HeadingColumn = h;
        if (group.SelectedIndex > 0 && group.SelectedItem is string g) options.GroupByColumn = g;
        return options;
    }
}
