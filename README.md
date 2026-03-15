# Creative Mode

A BepInEx mod for MineMogul that adds a creative inventory, unlimited building, and faster item access for building and testing.

## Features

- Creative mode toggle in General Settings
- Unlimited building when creative mode is enabled
- Creative inventory UI shown above the normal inventory
- Category tabs based on shop categories
- Paged tabs when categories exceed the visible width
- Tooltip support for tabs and items
- Cursor-based creative item pickup and placement

## Controls

- `Tab`: Open inventory. When creative mode is enabled, the creative inventory is shown above the normal inventory.
- `Left Click`: Pick up 1 item to the cursor
- `Left Click` same item again: Add 1 more to the held stack
- `Mouse Wheel Up`: Increase held stack by 1 per notch
- `Mouse Wheel Down`: Decrease held stack by 1 per notch
- `Ctrl + Left Click`: Pick up a full stack
- `Shift + Left Click`: Send directly to inventory or use world fallback
- `Left Click` inventory slot while holding a creative item: Place into that slot
- `Left Click` elsewhere while holding a creative item: Drop/spawn it in front of the player
- `Right Click` while holding a creative item: Cancel the held item

## Configuration

Config entries are defined in `Config/CreativeModeConfig.cs`.

- `General.CreativeModeEnabled`
- `UI.TabsPerPage`

## Project Structure

- `Config/`: BepInEx config bindings
- `Core/`: Runtime and plugin control flow
- `Patches/`: Harmony patches
- `UI/`: Creative inventory UI and tooltip logic
- `Utilities/`: Small shared helpers

## Build

This project targets `.NET Framework 4.7.2` and uses the shared MineMogul paths defined in `CreativeMode.csproj`.

Default path variables:

- `MineMogulDir`
- `MineMogulManagedDir`
- `MineMogulBepInExCoreDir`
- `MineMogulPluginsDir`

Build command:

```powershell
dotnet build .\CreativeMode.csproj -c Release
```

## Notes

- This repository intentionally ignores the `Branding/` folder.
- Build output folders such as `bin/` and `obj/` are also ignored.
