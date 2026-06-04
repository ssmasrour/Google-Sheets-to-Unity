# Bobbin

Bobbin downloads public Google Sheets, Google Docs, and other URL files into a
Unity project's `Assets` folder.

Open the tool from:

```text
Bobbin > Add URLs and Settings...
```

Use `Add New File` to create a row, paste a public URL, choose a destination with
`Save As...`, then click `Refresh`.

For Google Sheets, leave `GID` blank to use the first sheet tab or enter the
`gid` value from the sheet tab URL.

Bobbin stores project settings in:

```text
Assets/Bobbin/Editor/BobbinSettings.asset
```

That asset belongs to the user project, not the package cache, so it can be
committed to source control.
