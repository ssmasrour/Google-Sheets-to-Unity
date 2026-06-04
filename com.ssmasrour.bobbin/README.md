# Bobbin: Google Sheets to Unity

Bobbin is a lightweight Unity editor package that downloads public Google
Sheets, Google Docs, and other URL files into your Unity project's `Assets`
folder.

This package is a fork of
[radiatoryang/bobbin](https://github.com/radiatoryang/bobbin), with fixes and
improvements for newer Unity projects.

## Install With Unity Package Manager

In Unity:

1. Open `Window > Package Manager`.
2. Click `+`.
3. Choose `Add package from git URL...`.
4. Enter:

```text
https://github.com/ssmasrour/Google-Sheets-to-Unity.git?path=/com.ssmasrour.bobbin
```

To pin a branch or tag, add it after the package path:

```text
https://github.com/ssmasrour/Google-Sheets-to-Unity.git?path=/com.ssmasrour.bobbin#master
```

You can also add it directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.ssmasrour.bobbin": "https://github.com/ssmasrour/Google-Sheets-to-Unity.git?path=/com.ssmasrour.bobbin"
  }
}
```

If your project already has an old copied `Assets/Bobbin` folder, remove it
before installing this package to avoid duplicate editor classes.

## Use

1. Open `Bobbin > Add URLs and Settings...`.
2. Click `Add New File`.
3. Paste a public URL.
4. For Google Sheets, optionally enter the sheet `gid`.
5. Choose an output type.
6. Click `Save As...` and choose a path inside `Assets`.
7. Click `Refresh`.

Bobbin creates project-owned settings at:

```text
Assets/Bobbin/Editor/BobbinSettings.asset
```

Commit that settings asset if your team should share the same download list.

## Notes

- Bobbin is one-way and read-only.
- Google files must be public/viewable by link.
- Bobbin downloads files at editor time; it does not perform runtime network
  requests.
- Your game code still needs to parse and use the downloaded assets.

## License

MIT.
