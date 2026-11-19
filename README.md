# IDDStruct

A Windows app for importing structured data (CSV/TSV, Excel, JSON, XML), editing it as a
long-form document outline and converting it into an Adobe InDesign document.

The first output format is **IDML** (InDesign Markup Language). It is written directly, so
InDesign does not need to be installed on the machine running IDDStruct. Open the `.idml` in
InDesign with *File → Open*. The output layer is pluggable, so an exporter that drives an
installed copy of InDesign can be added later.

## Solution layout

| Project | Target | Purpose |
|---|---|---|
| `src/IDDStruct.Core` | .NET 8 | Document model, importers, mapping rules, style sheet, project file format |
| `src/IDDStruct.Idml` | .NET 8 | IDML exporter (`IDocumentExporter` implementation) |
| `src/IDDStruct.Cli` | .NET 8 | `iddstruct` command-line tool for batch conversion and inspection |
| `src/IDDStruct.Desktop` | .NET 8 + WinUI 3 (Windows App SDK 1.6) | The Windows desktop app |
| `tests/IDDStruct.Tests` | xUnit | Unit tests and sample-file smoke tests |
| `samples/` | | Example inputs for every format |

```
 CSV / XLSX ──► TabularData ──► TabularMapper ──┐
                                                ├──► StructuredDocument ──► IDocumentExporter ──► .idml
 JSON / XML ──► DataNode tree ─► HierarchyMapper┘       (editable in the app,     (later: InDesign
                                                          saved as .iddstruct)      scripting exporter)
```

### Document model

A `StructuredDocument` is a flat list of `Block`s: **Heading** (levels 1–6), **Paragraph**,
**Bullet item** / **Numbered item** (levels 1–3), **Table** and **Page break**. Text uses light
inline markup: `**bold**`, `*italic*`, `***bold italic***`, with `\*` for a literal asterisk.

Each block maps to a paragraph style (`Heading 2`, `Body`, `Bullet 1`, `Numbered 1`,
`Table Header`, `Table Body`, `Quote`). A block can name any other style instead. A style the
style sheet doesn't define is created automatically, based on `Body`, so you can restyle it in
InDesign. Bold and italic map to the character styles `Bold`, `Italic` and `Bold Italic`.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

**Desktop app** (Windows 10 1809 or later). Open `IDDStruct.sln` in Visual Studio 2022, which
needs the *Windows application development* workload. Select the **x64** platform and set
`IDDStruct.Desktop` as the start-up project. Or build from a terminal:

```bash
dotnet build src/IDDStruct.Desktop/IDDStruct.Desktop.csproj -p:Platform=x64
```

The app is unpackaged (a plain `.exe`, no MSIX install), and it bundles the Windows App SDK
runtime (self-contained).

**Core, CLI and tests** build on Windows, macOS or Linux:

```bash
dotnet test tests/IDDStruct.Tests
```

## Command line

```bash
dotnet run --project src/IDDStruct.Cli -- convert samples/manual.xml -o manual.idml
```

```bash
dotnet run --project src/IDDStruct.Cli -- outline samples/glossary-records.csv --heading-column Term --group-by Category
```

Run `iddstruct --help` for every option: page size, columns, fonts, sheet selection,
delimiter, and so on.

## How data maps to a document

### CSV / TSV / Excel

The delimiter is detected automatically (comma, semicolon, tab or pipe). Files without a BOM
are read as UTF-8, with a fallback to Latin-1 for legacy Windows exports. Two modes:

- **Block mode**: used when the columns include `text` plus `type` and/or `style` (optionally
  `level`). Each row becomes one block. See `samples/handbook-blocks.csv`. Recognised types:
  `h1`–`h6`, `title`, `heading`, `p`/`paragraph`, `bullet`/`li`, `numbered`/`ol`, `quote`,
  `table` (cells separated by `|`, one row per line), `pagebreak`. Any other type value is used
  as a custom paragraph style name.
- **Record mode**: used for any other table. Each row becomes a heading, from the first column
  or `--heading-column`, followed by one paragraph per other column, labelled `**Column:**`
  unless you turn labels off. `--group-by` adds a level-1 heading whenever that column's value
  changes. A blank line inside a cell starts a new paragraph. See `samples/glossary-records.csv`.

For Excel, the first worksheet is imported by default. Pick another with `--sheet`, or use
`--all-sheets` to import every worksheet, each under its own level-1 heading.

### JSON / XML

Both are read into the same tree and mapped by one set of rules:

- HTML/DocBook-like names are understood: `h1`–`h6`, `title`, `p`/`para`, `ul`/`ol`/`li`,
  `table`/`thead`/`tr`/`th`/`td`, `blockquote`, `pagebreak`.
- An object or element with a `title`/`name`/`heading` becomes a section. Its title becomes a
  heading, and its children are nested one level deeper.
- An array of values becomes a bulleted list. An array of objects with the key `table` becomes
  a table, with the keys as header row.
- An object with a `type` property is treated as a block, like a block-mode CSV row. A
  top-level `"blocks": [...]` array gives exact control.
- In XML, inline `<b>`/`<strong>`, `<i>`/`<em>` and `<br/>` become bold, italic and forced line
  breaks. A `style="…"` attribute sets a custom paragraph style.
- Any other value becomes a paragraph labelled with its humanised field name
  (`firstName` → `**First name:**`).

See `samples/guide.json` and `samples/manual.xml`.

## About the IDML output

- The output is a single story, flowed through threaded text frames (one per page) inside the
  margins, with styles defined in the document. The DOM version is 8.0 (CS6), so it opens in
  CS6 and every later release, including current Creative Cloud.
- IDML has no layout engine, so the exporter **estimates** the page count from the text length
  and the style sizes, with a safety margin. *Smart Text Reflow* is switched on in the file:
  InDesign adds pages if text oversets and removes empty pages as soon as the text is edited.
  You can force a page count with `--pages`.
- The default fonts are Minion Pro (body) and Myriad Pro (headings). Change them with
  `--body-font` / `--heading-font`, or edit the paragraph styles in InDesign.

## Roadmap

- [ ] Verify IDML output in InDesign across versions, then build a regression set from the results
- [ ] Style sheet editor in the app (fonts, sizes, spacing), plus import of styles from an existing `.indd`/`.idml` template
- [ ] "Place into template" mode: flow content into a user-supplied IDML template's master text frames
- [ ] Column-mapping UI with a live preview for tabular imports
- [ ] Undo/redo and unsaved-changes prompts in the editor
- [ ] Images (anchored graphics) and footnotes in the document model
- [ ] XML tagging of exported content, for InDesign structured workflows
- [ ] InDesign automation exporter (COM/UXP) for exact pagination and native `.indd` / PDF output

## Licence

GPL-3.0. See [LICENSE](LICENSE).
