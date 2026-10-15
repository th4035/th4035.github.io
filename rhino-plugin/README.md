# GSWebExport — Rhino 7 plug-in (.rhp)

Exports the current Rhino model as **one self-contained `.html` file** that opens in the
GSWeb Viewer (any browser, no server, no internet).

## Command

| Command | What it does |
|---|---|
| `GSWebExport` | Asks where to save, then writes the `.html`. If objects are **selected**, only those are exported; otherwise everything visible. |

## What is exported (v0.1)

- Surfaces, polysurfaces, extrusions, SubD, meshes, and blocks (nested blocks included)
- Layers (names, colours, visibility), object names and user text
- Object / material colour and transparency
- Text dots, named views, current camera, model units

Not exported yet: curves and points, textures, HDR environment, dimensions/text objects.

## Build (Windows, Rhino 7)

1. Install the **.NET SDK** (6 or newer) or Visual Studio 2019/2022.
2. In a terminal:
   ```
   cd rhino-plugin\GSWebExport
   dotnet build -c Release
   ```
3. The plug-in file is created here:
   `rhino-plugin\GSWebExport\bin\Release\net48\GSWebExport.rhp`

## Install in Rhino 7

1. Start Rhino 7, run the command `PlugInManager`.
2. Click **Install…** and choose `GSWebExport.rhp`. (Dragging the `.rhp` into the Rhino window also works.)
3. Run the command `GSWebExport`.

## When the viewer changes

The viewer page is embedded inside the plug-in at build time (`viewer/viewer-shell.html`).
After changing anything in `viewer/`, rebuild in this order:

```
cd viewer-build && npm install && npm run build:shell     (regenerates viewer/viewer-shell.html)
cd ../rhino-plugin/GSWebExport && dotnet build -c Release (embeds the new page)
```

## Format reference

The package follows the `.rhv` container described in the original project's
`docs/rhv-format.md` (MIT): gzip( `RV3D` | version | json length | metadata JSON | GLB ).
