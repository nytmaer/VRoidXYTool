# VRoidXYTool

[中文说明](README.md)

Extension Plugin for VRoid Studio

## Experimental IL2CPP live texture migration

The `dev/il2cpp-migration` branch includes a working live PNG texture prototype for **VRoid Studio 2.14.0** with BepInEx 6 IL2CPP. The legacy camera, pose, guide and other tools have not been ported. This is a draft migration, not a replacement release for all original features.

See [installation and usage](docs/IL2CPP_INSTALLATION.md), [verified compatibility results](docs/COMPATIBILITY_RESULTS.md), and [architecture audit](docs/ARCHITECTURE_AUDIT.md). Original MIT attribution and legacy source are retained.

The optional [second-monitor Companion](docs/COMPANION.md) shows linked layers and sync status and opens their PNGs in an external editor. This first Windows WPF slice uses a local bridge snapshot and requires .NET 10 Windows Desktop; installer work is deferred.

## Why is the plug-in unavailable after VRoid Studio 1.26.1?

Starting from 1.26.1, vroid studio changed mono to il2cpp, which resulted in all plugins becoming invalid and difficult to fix. I will try to remake the plugin under il2cpp, but it is also possible to permanently stop updating. If you want to continue using plugins, you can download version 1.26.0 of vroid studio from the official website.

## Why is the plug-in unavailable after VRoid Studio 1.18?
You can edit VRoid Studio\BepInEx\config\BepInEx.cfg, change `HideManagerGameObject = false` to `HideManagerGameObject = true`, then VRoidXYTool can work on the new version VRoid Studio.

## Introduce

- Base on [BeplnEx][1]
- Link Texture Tool. You can edit image in drawing tools(eg PS/SAI), when you save file, the file will auto sync to VRoidStudio.
- Camera Tool. Quickly set the position of the camera around the body or around the head. Set camera orthographic or perspective mode.
- Guide Tool. Add grid and guide image to VRoid Studio.
- Pose Perset Tool. In the PhotoBooth and pose mode, you can save and load custom pose perset.
- Anti-Aliasing.
- MMD Player(WIP). You can import VMD  files in the VRoid Studio for play
- Video Record. Do not rely on external software to record HD video in vroidstudio
- Wireframe Mode.

![Preview](LinkTexturePreview.gif)

![Preview](MMDPreview.gif)

![Preview](WireframePreview.png)

## Tutorial

- Tutorial Video[bilibili][2] (now only chinese video, If you have recorded tutorials in other languages, you can submit links to me.)

## Q&A

`Q:` I don't have the bepinex folder in the video. What should I do?

`A:` Install [BeplnEx][1]

`Q:` I installed the plugin. How can I open it in the VRoid Studio?

`A:` Tab or edit hotkey in BepInEx/config/me.xiaoye97.plugin.VRoidStudio.VRoidXYTool.cfg, `Hotkey = Tab`

`Q:` How can I contact you?

`A:` VRoid QQGroup(684544577), My private QQGroup (528385469), discord xiaoye#3171(Slow reply), Twitter @xiaoye1997 (Slow reply)

[1]: https://github.com/BepInEx/BepInEx/releases
[2]: https://www.bilibili.com/video/BV1TP4y1V7Qn/
[3]: https://www.bilibili.com/video/BV1BL41137Tc/
