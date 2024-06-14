# Map Workshop
Writing to the Cartography Table will render a custom map which can be saved to player save file or to the set of graphical files. The map can be shown instead of original map in no-map mode or when reading from Cartography Table. 

The code of the mode is based on the [NomapPrinter](https://github.com/shudnal/NomapPrinter) mod by **shudnal**, which in turn uses some map rendering algorithms of [MapPrinter](https://valheim.thunderstore.io/package/ASpy/MapPrinter/) mod by **ASpy**.

*NomapPrinter* mod was originally designed to enhance no-map playthroughs for players to look at their current exploration progress without the spoiler of live updates - it could be handy if you want some map reference for your travels but don't want to draw map yourself in paint or w/e.

But initially *NomapPrinter* didn't support high resolution map rendering (original resolution was 2048x2048 - same as for in-game minimap), wasn't too mach customizable and was working only in no-map mode. I've forked the project to make it more friendly, to improve quality of the rendered map and to shorten time of rendering.

Since v2.0.0 of *NomapPrinter* mod the differences in code became so great that it became too hard to maintain merging - so, I decided to start a separate project.

Up to the version 1.1.8 this mode is the exact mirror of [NomapPrinter](https://github.com/shudnal/NomapPrinter) mod - all commits are taken intact. Big thanks to **shudnal** for making his code public - I've learned a lot about Valheim modding and C# development (I'm a C/C++ adherent) from his efforts.

## Main features
* renders custom map footage using modified algorithms of [MapPrinter](https://valheim.thunderstore.io/package/ASpy/MapPrinter/) mod by **ASpy**
* shows custom map in in-game window (map is updated only via table interaction)
* map is saved between sessions to a player savefile
* map can be saved to a specified graphical file
* map is generated in up to 8192x8192 resolution
* different styles of map are supported including presence of contour lines
* configurable visibility of pins on map
* visible pins configuration is server synced
* custom server map can be used instead of the rendered one - it can be shared to all clients

## Default configuration for the pins
* pins are shown only in explored areas of the map
* Haldor and Hildir pins are always shown (especially handy for Hildir's quest pins)
* only your own pins are shown (no shared pins)
* red crossed pins are not shown
* bed and death pins are not shown

## Map window can be
* opened by Map bind key (default M)
* closed by the same key or Escape
* dragged by left mouse click and drag
* zoomed by mouse wheel
* set to default zoom by right mouse click
* centered at spawn point by middle mouse click

## Best mods to use with
* To place pins immersively in no-map mode you can use [AutoPinSigns](https://valheim.thunderstore.io/package/shudnal/AutoPinSigns/)
* To see pins without map you can use [Compass](https://www.nexusmods.com/valheim/mods/851)

## Compatibility
* This mod interacts with very little of the game code, conflicts with other mods are pretty unlikely
* Gamepad isn't supported for in-game map window

## Configuring
The best way to handle configs is to use one of the configuration managers:
* [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) by shudnal
* [Official BepInEx Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/) by Azumatt

## Mirrors
None
