using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IDDStruct.Core.Model;
using IDDStruct.Core.Styles;

namespace IDDStruct.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private StructuredDocument _document = new();

    public MainViewModel() => Load(new StructuredDocument(), null);

    public ObservableCollection<BlockViewModel> Blocks { get; } = new();

    /// <summary>Display names in <see cref="BlockKind"/> order (bound by index).</summary>
    public IReadOnlyList<string> BlockKindNames { get; } = new[] { "Heading", "Paragraph", "Bullet item", "Numbered item", "Table", "Page break" };

    public IReadOnlyList<string> PageSizes { get; } = PageSetup.Presets.Keys.Append("Custom").ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(RemoveBlockCommand), nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(DuplicateBlockCommand))]
    private BlockViewModel? _selectedBlock;

    [ObservableProperty]
    private string _title = "Untitled";

    [ObservableProperty]
    private int _pageSizeIndex;

    [ObservableProperty]
    private double _columns = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _projectPath;

    [ObservableProperty]
    private string _statusText = "Ready";

    public bool HasSelection => SelectedBlock is not null;

    public string WindowTitle => ProjectPath is null ? "IDDStruct" : $"{Path.GetFileName(ProjectPath)} – IDDStruct";

    public StyleSheet Styles => _document.Styles;

    public void Load(StructuredDocument document, string? projectPath)
    {
        _document = document;
        Blocks.Clear();
        foreach (var block in document.Blocks) Blocks.Add(Wrap(block));
        Title = document.Title;
        var preset = document.Page.PresetName;
        PageSizeIndex = preset is null ? PageSizes.Count - 1 : PageSizes.ToList().IndexOf(preset);
        Columns = document.Page.Columns;
        ProjectPath = projectPath;
        SelectedBlock = Blocks.FirstOrDefault();
        UpdateStatus();
    }

    /// <summary>Returns the document with the current editor state applied.</summary>
    public StructuredDocument BuildDocument()
    {
        _document.Title = string.IsNullOrWhiteSpace(Title) ? "Untitled" : Title.Trim();
        _document.Blocks = Blocks.Select(b => b.Model).ToList();
        _document.Page.Columns = Math.Clamp((int)(double.IsNaN(Columns) ? 1 : Columns), 1, 12);
        return _document;
    }

    public void UpdateStatus()
    {
        int headings = Blocks.Count(b => b.Kind == BlockKind.Heading);
        StatusText = $"{Blocks.Count} blocks · {headings} headings";
    }

    partial void OnPageSizeIndexChanged(int value)
    {
        if (value < 0 || value >= PageSizes.Count || PageSizes[value] == "Custom") return;
        var preset = PageSetup.FromPreset(PageSizes[value]);
        _document.Page.Width = preset.Width;
        _document.Page.Height = preset.Height;
    }

    private BlockViewModel Wrap(Block block) => new(block, () => _document.Styles);

    private void Insert(Block block)
    {
        int index = SelectedBlock is null ? Blocks.Count : Blocks.IndexOf(SelectedBlock) + 1;
        var vm = Wrap(block);
        Blocks.Insert(index, vm);
        SelectedBlock = vm;
        UpdateStatus();
    }

    [RelayCommand]
    private void AddHeading() => Insert(Block.Heading(SelectedBlock?.Kind == BlockKind.Heading ? (int)SelectedBlock.Level : 2, "New heading"));

    [RelayCommand]
    private void AddParagraph() => Insert(Block.Paragraph(""));

    [RelayCommand]
    private void AddBullet() => Insert(Block.Bullet(""));

    [RelayCommand]
    private void AddNumbered() => Insert(Block.Numbered(""));

    [RelayCommand]
    private void AddTable() => Insert(Block.FromTable(TableData.FromDelimitedText("Column 1|Column 2\n|")));

    [RelayCommand]
    private void AddPageBreak() => Insert(Block.PageBreak());

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveBlock()
    {
        if (SelectedBlock is null) return;
        int index = Blocks.IndexOf(SelectedBlock);
        Blocks.RemoveAt(index);
        SelectedBlock = Blocks.Count == 0 ? null : Blocks[Math.Min(index, Blocks.Count - 1)];
        UpdateStatus();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateBlock()
    {
        if (SelectedBlock is not null) Insert(SelectedBlock.Model.Clone());
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (SelectedBlock is null) return;
        var item = SelectedBlock;
        int from = Blocks.IndexOf(item), to = from + delta;
        if (to < 0 || to >= Blocks.Count) return;
        Blocks.Move(from, to);
        SelectedBlock = null; // force the list to re-select the moved item
        SelectedBlock = item;
    }
}
