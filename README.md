# Network Tools for Cities: Skylines II

![Network Tools](NetworkTools.Mod/Assets/Cover/1.jpg)

**Network Tools** gives you precise control over every network in your city: roads, paths, rail, and waterways. Add or remove nodes, reshape slopes and curves, generate grids, and more, with interactive 3D handles and real-time previews.

It is the spiritual successor to [Network Multitool](https://steamcommunity.com/sharedfiles/filedetails/?id=2560782729) for Cities: Skylines 1, rebuilt from the ground up for CS2.

[Paradox Mods](https://mods.paradoxplaza.com/mods/133736/Windows) · [Forum thread](https://forum.paradoxplaza.com/forum/threads/network-tools-early-access.1927685/) · [Discord](https://discord.gg/C2XQUYHwuV) · [Crowdin](https://crowdin.com/project/networktools-cs2) · [Ko-fi](https://ko-fi.com/lucadevdesign)

> [!WARNING]
> **Early access.** Not every feature is finished, updates can be frequent, and you may run into issues. The mod is generally stable, but **back up your save** before you use it.

## Tools

### Node tools
| Tool | Description |
| --- | --- |
| **Add Node** | Click any edge to insert a new node, splitting the segment in two. |
| **Remove Node** | Click a node to remove it and merge its two adjacent segments. |
| **Slide Node** *(coming soon)* | Drag a node along its connected edges without changing the curve's shape. |
| **Super Node** | Select several nearby nodes and merge them into a single large intersection. |

### Shape tools
| Tool | Description |
| --- | --- |
| **Slope** | Reshape the elevation profile of a path: constant slope, ease-in/out, or arch. |
| **Curve** | Straighten or smooth the horizontal curve of a path. |

### Creation tools
| Tool | Description |
| --- | --- |
| **Connect** | Connect two nodes with a simple curve, complex curve, or loop. |
| **Parallel** | Create a parallel copy of an existing path at an adjustable offset. |
| **Generate** | Generate new networks from scratch, such as grids or circles and ovals. |

## Features
- **Real-time preview:** see the result of every change before you apply it.
- **Interactive handles and parameters:** drag handles in the world or adjust values in the sidebar.
- **Network filtering:** choose which network types to target: roads, paths, rail, waterways, or invisible paths.
- **View options:** toggle underground view, the zone grid, and invisible network visibility.
- **Keyboard shortcuts:** select tools with `Shift+1` to `Shift+9`, toggle the panel with `Shift+T`, and apply with `Enter`. You can rebind all of them in the settings.

## How to use
1. Subscribe to the mod on [Paradox Mods](https://mods.paradoxplaza.com/mods/133736/Windows). It requires [Unified Icon Library](https://mods.paradoxplaza.com/mods/74417/Windows).
2. In game, open the **Network Tools** panel from the toolbar, or press `Shift+T`.
3. Pick a tool from the sidebar.
4. Click nodes or edges in the world to select them, adjust the handles and parameters, then apply.

## Support
For help and feedback, join the [CS2 Modding Discord](https://discord.gg/C2XQUYHwuV).

When you report a bug, please share your logs and playset with **Skyve**. A copy of your save helps a lot too. Without logs, most bugs are very hard to fix.

## Translations
Translations are managed on [Crowdin](https://crowdin.com/project/networktools-cs2). To help translate the mod into your language, join the project there or ask on the [Discord](https://discord.gg/C2XQUYHwuV).

## Development

### Requirements
- Cities: Skylines II with the official modding toolchain installed (it sets the `CSII_TOOLPATH` environment variable)
- .NET SDK and .NET Framework 4.8 targeting pack
- Node.js 18 or later, for the UI

### Getting started
Clone the repository with its submodules:

```bash
git clone --recurse-submodules https://github.com/lucarager/CS2-NetworkTools.git
```

Build through the solution, not the project file, so that `$(SolutionDir)` resolves for the codegen step:

```bash
dotnet build CS2-NetworkTools.sln
```

The build generates the UI parameter bindings, builds the TypeScript/React UI in `NetworkTools.Mod/UI`, and deploys the mod to your local mods folder.

### Repository layout
| Path | Contents |
| --- | --- |
| `NetworkTools.Mod/` | The mod itself: ECS systems, components, settings, and localization |
| `NetworkTools.Mod/Systems/Tools/` | One folder per tool, each extending `NT_BaseToolSystem` |
| `NetworkTools.Mod/UI/` | The in-game UI (TypeScript/React, Colossal UI) |
| `NetworkTools.Mod/Common/` | Shared library ([CS2-LucaModsCommon](https://github.com/lucarager/CS2-LucaModsCommon)), included as a submodule |
| `NetworkTools.Codegen/` | Generates `parameters.generated.ts` from the C# tool parameters |
| `NetworkTools.Tests/` | NUnit tests |

To regenerate the UI parameter bindings without a full build:

```bash
dotnet run --project NetworkTools.Codegen -- "NetworkTools.Mod\Systems" "NetworkTools.Mod\UI\src\generated\parameters.generated.ts" --configuration Release
```

## Support the project
If you'd like to support development, you can do so on [Ko-fi](https://ko-fi.com/lucadevdesign). Thank you!

## License
Released under the MIT License. See [LICENSE.txt](LICENSE.txt).
