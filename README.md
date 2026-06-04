# Bobbin: Google Sheets and Docs to Unity

Bobbin is a lightweight Unity editor tool that downloads public web files into
your project's `Assets` folder. It is especially useful when designers, writers,
localizers, or producers keep game content in Google Sheets or Google Docs and
you want a simple one-way sync into Unity.

![Bobbin screenshot](Media/bobbin_screenshot01.png)

## About This Fork

This repository is a fork of
[radiatoryang/bobbin](https://github.com/radiatoryang/bobbin). The original
Bobbin is a small 2019 Unity editor utility for downloading public URLs, Google
Docs, and Google Sheets into Unity without login or OAuth.

This fork keeps the same simple idea, but includes practical fixes and
improvements made because this kind of tool became necessary in real Unity
projects. It is not an official upstream release; it is a project-driven version
focused on making the workflow safer, clearer, and more reliable in newer Unity
projects.

Notable changes from the original Bobbin:

- More reliable Google URL handling using URI parsing instead of fixed-length
  string slicing.
- Support for both Google Docs and Google Sheets edit/share links, plus existing
  export URLs.
- Better Google Sheets `gid` handling from the URL, query string, fragment, or
  Bobbin's `GID` field.
- Source URLs stay readable in the settings table while Bobbin converts them to
  export URLs only when refreshing.
- Safer destination path validation so downloads must resolve inside `Assets`.
- Automatic creation of the settings folder when needed.
- UnityWebRequest compatibility for newer Unity versions, while keeping a
  fallback for older versions.
- Configurable per-request timeout in the editor UI.
- Refresh-state protection to avoid overlapping refreshes.
- Clearer refresh progress, status messages, HTTP errors, and Google sign-in
  page detection.
- More robust change detection and asset reference recovery when files already
  exist.
- Better editor undo/dirty-state handling and small table UI fixes.
- Expanded documentation for setup, usage, troubleshooting, and runtime
  integration.

## Why Use Bobbin?

- Pull public Google Sheets into Unity as `.csv` files.
- Pull public Google Docs into Unity as `.txt` files.
- Download other public URL content as `txt`, `csv`, `json`, `xml`, `jpg`,
  `png`, or raw `bytes`.
- Save downloaded files anywhere inside `Assets`.
- Keep source URLs and destination paths in a Unity asset that can be committed
  to source control.
- Refresh manually, or let Bobbin auto-refresh on an interval while the editor is
  open.
- Avoid unnecessary reimports by hashing downloaded responses and only importing
  changed files.

Bobbin is editor-only. It downloads files for your project, then your game code
can read those imported assets like normal Unity assets.

## Requirements

- Unity project with editor scripting enabled.
- Internet access from the Unity Editor.
- Public, viewable URLs. Bobbin does not authenticate with Google accounts.

This sample project was saved with Unity `6000.3.9f1`. The Bobbin code lives
under `Assets/Bobbin/Editor`, so it is not included in player builds.

## Installation

### Use This Repository as a Unity Project

1. Clone or download this repository.
2. Open the folder in Unity.
3. Let Unity import the project.
4. Open `Bobbin > Add URLs and Settings...`.

The project includes two example downloads in `Assets/Example Files`:

- `Google Docs Example.txt`
- `Google Sheets Example.csv`

### Add Bobbin to an Existing Unity Project

1. Copy `Assets/Bobbin` into your Unity project's `Assets` folder.
2. Let Unity compile the editor scripts.
3. Open `Bobbin > Add URLs and Settings...`.

Bobbin stores its configuration in:

```text
Assets/Bobbin/Editor/BobbinSettings.asset
```

Commit this asset if you want everyone on the team to share the same URL list
and destination file paths.

## Quick Start

1. In Unity, open `Bobbin > Add URLs and Settings...`.
2. Click `Add New File`.
3. Give the row a clear name, such as `ItemStats`, `Dialogue_EN`, or
   `QuestText`.
4. Paste a public URL into the `URL` field.
5. For Google Sheets, optionally enter the sheet `gid` in the `GID` field.
   Leave it blank to use the first sheet.
6. Choose the output `Type`.
7. Click `Save As...` and choose a destination inside `Assets`.
8. Click `Refresh`.

After a successful refresh, the downloaded file appears in your project as a
normal Unity asset.

## Google Sheets Setup

1. Open the spreadsheet in Google Sheets.
2. Click `Share`.
3. Change general access to `Anyone with the link`.
4. Set the permission to `Viewer`.
5. Copy the link.
6. Paste it into Bobbin's `URL` field.

Bobbin accepts regular edit/share links like this:

```text
https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=0
```

It converts the link to a CSV export URL when refreshing.

### Choosing a Sheet Tab

For multi-tab spreadsheets, each tab has a `gid`. You can find it in the browser
URL after selecting the sheet tab:

```text
https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=123456789
```

Put `123456789` in Bobbin's `GID` field. Bobbin also accepts `gid=123456789`, a
`#gid=123456789` fragment, or a full Google Sheets URL in that field.

## Google Docs Setup

1. Open the document in Google Docs.
2. Click `Share`.
3. Change general access to `Anyone with the link`.
4. Set the permission to `Viewer`.
5. Copy the link.
6. Paste it into Bobbin's `URL` field.
7. Set the output type to `txt`.

Bobbin accepts regular edit/share links like this:

```text
https://docs.google.com/document/d/DOCUMENT_ID/edit
```

It converts the link to a plain text export URL when refreshing.

Google Docs exports text content only. Images, comments, rich formatting, and
document layout are not preserved in the downloaded `.txt` file.

## Bobbin Settings Reference

Open the settings with `Bobbin > Add URLs and Settings...`.

| Control | What it does |
| --- | --- |
| `Refresh` | Downloads all enabled rows immediately. |
| `Auto refresh?` | Automatically refreshes while the Unity Editor is open. |
| `every ... sec.` | Refresh interval. The minimum is 5 seconds. |
| `timeout ... sec.` | Per-request timeout. Values are clamped from 1 to 600 seconds. |
| `Add New File` | Adds a new download row. |
| `Remove Highlighted File(s)` | Removes selected rows from the settings asset. |

Each row has these fields:

| Field | What it means |
| --- | --- |
| `Enabled` | Disable this row without deleting it. |
| `Name` | A human-readable label for the row. |
| `URL` | The public source URL to download. |
| `>` | Opens the source URL in your browser. |
| `GID` | Optional Google Sheets tab id. Leave blank for the first sheet. |
| `Type` | File extension used when choosing a destination. |
| `Asset` | The Unity asset created by the download. Use `x` to clear the saved path. |

You can also run `Bobbin > Force Refresh All Files` from the Unity menu.

## Supported File Types

The built-in file type choices are:

```text
txt, csv, json, xml, jpg, png, bytes
```

The type mainly controls the extension used by the `Save As...` dialog. Unity's
normal importer decides how the saved file becomes an asset. For example:

- `txt`, `csv`, `json`, and `xml` usually import as `TextAsset`.
- `jpg` and `png` usually import as textures.
- `bytes` imports as a binary `TextAsset`.

To add more file extensions, edit the `FileType` enum in
`Assets/Bobbin/Editor/Scripts/BobbinSettings.cs`.

## Using Downloaded Files in Game Code

Bobbin does not parse your content or generate C# classes. It only keeps files in
sync. Your runtime code should consume the imported assets in the format you
choose.

Example with a downloaded CSV, TXT, JSON, or XML file:

```csharp
using UnityEngine;

public class TextAssetReader : MonoBehaviour
{
    [SerializeField] TextAsset sourceFile;

    void Start()
    {
        Debug.Log(sourceFile.text);
    }
}
```

Example with a downloaded image:

```csharp
using UnityEngine;
using UnityEngine.UI;

public class DownloadedImagePreview : MonoBehaviour
{
    [SerializeField] RawImage preview;
    [SerializeField] Texture2D downloadedTexture;

    void Start()
    {
        preview.texture = downloadedTexture;
    }
}
```

For production spreadsheet data, use a CSV parser that handles quoting,
commas inside fields, line breaks, and encoding correctly.

## Recommended Workflow

1. Keep spreadsheet and document structure stable.
2. Name each Bobbin row after the data's purpose, not the temporary file name.
3. Save downloaded files into a predictable folder such as `Assets/Data`,
   `Assets/Localization`, or `Assets/Narrative`.
4. Commit `BobbinSettings.asset` and the downloaded files when you want changes
   reviewed with the rest of the project.
5. Turn on auto-refresh only when actively editing remote content. For normal
   development, manual refresh is usually quieter.
6. Treat Google Docs and Sheets as source content, then validate or parse that
   content in your game's own import/runtime pipeline.

## Troubleshooting

### Google Returns a Sign-In Page

The file is not publicly viewable. In Google Docs or Google Sheets, open
`Share`, set general access to `Anyone with the link`, and use `Viewer`
permission.

### Nothing Downloads

Check that:

- The row is enabled.
- The URL is complete and public.
- The destination path is inside `Assets`.
- The Unity Editor has internet access.
- The request timeout is high enough for the file.

### The Wrong Sheet Tab Downloads

Select the desired sheet tab in Google Sheets, copy the number after `#gid=`,
and put it in Bobbin's `GID` field.

### A File Does Not Reimport

Bobbin skips imports when the downloaded bytes have not changed. To force a new
destination, clear the row's `Asset` field with `x`, choose `Save As...` again,
and refresh.

### CSV Data Looks Different Than the Sheet

Google Sheets exports CSV values, not formatting. Styling, formulas themselves,
merged cells, notes, comments, charts, and multiple tabs are not included in one
CSV export. Use one Bobbin row per sheet tab if you need multiple tabs.

## Project Layout

```text
Assets/
  Bobbin/
    Editor/
      Scripts/              Core editor logic and settings inspector
      TreeModel/            TreeView model used by the settings table
      Thirdparty/           Editor Coroutines dependency
      BobbinSettings.asset  Example/shared Bobbin configuration
  Example Files/            Example files downloaded by Bobbin
Media/
  bobbin_screenshot01.png   README screenshot
Packages/
  manifest.json             Unity package manifest
ProjectSettings/
  ProjectVersion.txt        Unity editor version used by this sample project
```

## Limitations

- Bobbin is one-way only. It cannot upload local changes back to Google Docs,
  Google Sheets, or any other remote source.
- Bobbin does not authenticate, so private Google files are not supported.
- Bobbin does not validate schemas, generate strongly typed classes, or provide
  spreadsheet editing tools.
- Auto-refresh only runs while the Unity Editor is open.
- Downloads are editor-time project files, not runtime network requests.

## Credits

This fork is based on Robert Yang's original
[radiatoryang/bobbin](https://github.com/radiatoryang/bobbin), which is released
under the MIT License. The improvements in this repository were made for
practical Unity project needs while preserving Bobbin's lightweight, one-way
download workflow.

This project also includes Editor Coroutines by Marijn Zwemmer.

## License

MIT. See `LICENSE` for the full license text.
