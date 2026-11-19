using CommunityToolkit.Mvvm.ComponentModel;
using IDDStruct.Core.Model;
using IDDStruct.Core.Styles;
using Microsoft.UI.Xaml;

namespace IDDStruct.Desktop.ViewModels;

/// <summary>
/// Editable wrapper around a <see cref="Block"/>. Setters that originate from text boxes don't
/// raise change notifications for their own property, so the caret isn't reset while typing.
/// </summary>
public sealed class BlockViewModel : ObservableObject
{
    private readonly Func<StyleSheet> _styles;

    public BlockViewModel(Block model, Func<StyleSheet> styles)
    {
        Model = model;
        _styles = styles;
    }

    public Block Model { get; }

    public BlockKind Kind => Model.Kind;

    public int KindIndex
    {
        get => (int)Model.Kind;
        set
        {
            if (value < 0 || value == (int)Model.Kind || !Enum.IsDefined(typeof(BlockKind), value)) return;
            Model.Kind = (BlockKind)value;
            if (Model.Kind == BlockKind.Table && Model.Table is null)
            {
                Model.Table = string.IsNullOrWhiteSpace(Model.Text)
                    ? new TableData { Rows = { new List<string> { "Column 1", "Column 2" }, new List<string> { "", "" } } }
                    : TableData.FromDelimitedText(Model.Text);
            }
            Model.Level = ClampLevel(Model.Level);
            RaiseAll();
        }
    }

    public double Level
    {
        get => Model.Level;
        set
        {
            if (double.IsNaN(value)) return;
            var level = ClampLevel((int)Math.Round(value));
            if (level == Model.Level) return;
            Model.Level = level;
            OnPropertyChanged();
            RaiseDisplay();
        }
    }

    public double MaxLevel => Model.Kind == BlockKind.Heading ? 6 : 3;

    public string Style
    {
        get => Model.Style ?? "";
        set
        {
            Model.Style = string.IsNullOrWhiteSpace(value) ? null : value;
            RaiseDisplay();
        }
    }

    public string Text
    {
        get => Model.Text;
        set
        {
            // WinUI TextBox uses '\r' for new lines; store '\n'.
            Model.Text = (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            RaiseDisplay();
        }
    }

    public string TableText
    {
        get => Model.Table?.ToDelimitedText() ?? "";
        set
        {
            var text = (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            Model.Table = TableData.FromDelimitedText(text, Model.Table?.HeaderRows ?? 1);
            RaiseDisplay();
        }
    }

    public double HeaderRows
    {
        get => Model.Table?.HeaderRows ?? 0;
        set
        {
            if (Model.Table is null || double.IsNaN(value)) return;
            Model.Table.HeaderRows = Math.Max(0, (int)Math.Round(value));
            OnPropertyChanged();
        }
    }

    public bool HasLevel => Model.Kind is BlockKind.Heading or BlockKind.BulletItem or BlockKind.NumberedItem;
    public bool IsText => Model.Kind is not (BlockKind.Table or BlockKind.PageBreak);
    public bool IsTable => Model.Kind == BlockKind.Table;
    public bool HasStyle => Model.Kind != BlockKind.PageBreak;

    public string DefaultStyleName => Model.Kind switch
    {
        BlockKind.Heading => StyleSheet.HeadingStyle(Model.Level),
        BlockKind.BulletItem => StyleSheet.BulletStyle(Model.Level),
        BlockKind.NumberedItem => StyleSheet.NumberedStyle(Model.Level),
        BlockKind.PageBreak => "",
        _ => StyleSheet.BodyStyle,
    };

    public string StyleName => Model.Kind == BlockKind.PageBreak ? "" : _styles().ResolveParagraphStyle(Model);

    public string Badge => Model.Kind switch
    {
        BlockKind.Heading => $"H{Model.Level}",
        BlockKind.Paragraph => "¶",
        BlockKind.BulletItem => "•",
        BlockKind.NumberedItem => "1.",
        BlockKind.Table => "Tbl",
        BlockKind.PageBreak => "Brk",
        _ => "?",
    };

    public string Preview
    {
        get
        {
            var text = Model.Kind switch
            {
                BlockKind.PageBreak => "— page break —",
                BlockKind.Table => $"Table, {Model.Table?.Rows.Count ?? 0} rows × {Model.Table?.ColumnCount ?? 0} columns",
                _ => Model.PlainText.Replace('\n', ' '),
            };
            if (text.Length == 0) text = "(empty)";
            return text.Length > 140 ? text[..137] + "…" : text;
        }
    }

    public Thickness Indent => new(Model.Kind switch
    {
        BlockKind.Heading => (Model.Level - 1) * 16,
        BlockKind.BulletItem or BlockKind.NumberedItem => Model.Level * 16,
        _ => 0,
    }, 4, 0, 4);

    private int ClampLevel(int level) => Math.Clamp(level, 1, Model.Kind == BlockKind.Heading ? 6 : 3);

    private void RaiseDisplay()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(StyleName));
        OnPropertyChanged(nameof(DefaultStyleName));
        OnPropertyChanged(nameof(Indent));
    }

    private void RaiseAll()
    {
        foreach (var name in new[] { nameof(KindIndex), nameof(Kind), nameof(Level), nameof(MaxLevel), nameof(Text), nameof(TableText),
                                     nameof(HeaderRows), nameof(HasLevel), nameof(IsText), nameof(IsTable), nameof(HasStyle), nameof(Style) })
            OnPropertyChanged(name);
        RaiseDisplay();
    }
}
