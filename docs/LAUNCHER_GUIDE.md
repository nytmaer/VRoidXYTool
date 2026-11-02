# VRoidXYTool: start here

Windows x64 · local launcher candidate 0.4.0 · VRoid Studio 2.14.0 or 2.16.0.

## First use

1. Save your work and close VRoid Studio and the Companion.
2. Open **VRoidXYTool.exe**. It finds a compatible installed VRoid automatically. If it cannot find yours, expand **Advanced settings and diagnostics → Choose VRoid…**.
3. Click **Set up workspace**. Keep an internet connection available for the verified loader download and first startup. Your original application and models remain in place.
4. Click **Launch workspace**. First use can take several minutes while VRoid prepares its integration files. The launcher waits for a live connection, then opens the Companion already connected.
5. Open a test copy of your model. In **Edit Texture**, select a raster layer and use **Textures → Link / export selected**. Open its PNG in your image editor, save your changes, then save the model in VRoid.

No SDK or separate .NET installation is needed. An image editor is required for external texture editing. Recording optionally needs your own FFmpeg.

## Every day

Open **VRoidXYTool** from the Windows Start menu and click **Launch workspace**. Leave **Open the Companion with VRoid** checked if you want both windows. The launcher remembers its managed installation and data locations.

The Start menu entry is created after successful setup. You can also reopen the downloaded launcher. This local candidate is unsigned; if Windows blocks it, retain the exact message for troubleshooting without disabling Windows security settings.

## Updates and recovery

- A supported VRoid source update changes the main button to **Update and launch**. A separate managed version is prepared; your previous workspace remains available under **Advanced → Use previous workspace**.
- An unsupported Steam update does not replace your compatible managed copy. Keep launching that copy until support is available.
- New launcher builds carry the approved tool package. Open a newer launcher to offer the tool update. Internet-based launcher self-updates and code signing are not configured.
- **Repair** restores tool files while preserving configuration and artwork. Close VRoid and its Companion before repair or switching workspace versions.
- **Open startup log** helps diagnose a slow or failed launch. **Cancel** stops setup/waiting; it does not kill an already running VRoid.

Artwork and bridge state live in `<storage>\Data`. Applications live in `<storage>\Workspaces`. Camera/reference settings and saved poses are carried forward between managed VRoid versions. Companion project preferences remain in the Companion's existing per-user preferences.

Default storage is `%LOCALAPPDATA%\VRoidXYTool`. To use another drive, choose **Advanced → Choose storage location…** before setup. This selects storage; it does not move existing files. The created Start menu entry remembers the selected location.

VRoid's normal per-user preferences and custom-item storage are shared by application copies. Saving the PNG does not save the `.vroid`; reopening a model clears live links, so explicitly relink layers.

## Local validation

See the accompanying launcher acceptance report for verified workflows and remaining limits. The native 2.16 texture diagnostic passes, but full VMD/MP4 coverage, clean-machine testing, signing and resource benchmarking remain separate checks. Nothing is published automatically by this launcher.
