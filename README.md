# Bobbin

Bobbin is a lightweight Unity editor tool for downloading public web files into a Unity project's `Assets` folder.

It is useful for teams that keep editable content in public Google Docs, Google Sheets, or simple hosted files and want a one-way sync into Unity.

![Bobbin screenshot](Media/bobbin_screenshot01.png)

## What It Does

- Downloads text, CSV, JSON, XML, images, bytes, or any other file Unity can import from a URL.
- Converts public Google Docs edit/share links to plain text export URLs.
- Converts public Google Sheets edit/share links to CSV export URLs.
- Saves all configured URLs and asset paths in `Assets/Bobbin/Editor/BobbinSettings.asset` for source control.
- Avoids reimporting unchanged files by comparing response hashes.
- Supports manual refresh or automatic refresh at a chosen interval.

## Quick Start

1. Import Bobbin into a Unity project.
2. Open `Bobbin > Add URLs and Settings...`.
3. Click `Add New File`.
4. Paste a source URL.
5. For Google Sheets, optionally enter the sheet `gid`.
6. Click `Save As...` and choose a path inside `Assets`.
7. Click `Refresh`.

Bobbin only downloads files. Your game code still needs to parse and use the downloaded assets.

## Supported Google Links

Bobbin accepts normal public share/edit links, for example:

```text
https://docs.google.com/document/d/DOCUMENT_ID/edit
https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=0
```

Existing Google export URLs also work. Private Google files will fail with a clear error until public view access is enabled.

## Limitations

- Bobbin is one-way and read-only. It cannot upload local changes back to Google Docs or Google Sheets.
- It does not generate C# types, schemas, or autocomplete.
- It does not authenticate. Only public URLs can be downloaded.

For larger spreadsheet workflows, consider dedicated data tools such as CastleDB or other schema-driven importers.

## License

MIT

## Acknowledgments

- Uses Editor Coroutines by marijnz.
- Uses Unity's TreeView editor API.
