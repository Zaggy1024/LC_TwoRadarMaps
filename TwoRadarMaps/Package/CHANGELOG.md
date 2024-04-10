## Version 1.3.6
- Fixed NullReferenceExceptions when Steam players joined a lobby.

## Version 1.3.5
- Prevented a conflict with LobbyControl by detect if it has already patched updating of players' usernames on the map.

## Version 1.3.4
- Fixed a vanilla bug that caused players' usernames to be desynced on the map.
- Fixed an issue where the main map target would be desynced on initial join to a lobby.

## Version 1.3.3
- Fixed line spacing of the command descriptions.
- Fixed an error that could lock players into the terminal with SellFromTerminal installed.

## Version 1.3.2
- Fixed placeholder names like `Player #0` appearing in the Terminal when connected to Steam.
- Displayed transition animations for changes in targets when radar boosters are added and removed again.
- Made various improvements to vanilla bug fixes for the edge cases that necessitate changing map targets.

## Version 1.3.1
- Added descriptions for all commands under the `other` help category.
- Removed some debug spam that would occur when teleporting players from the terminal.

## Version 1.3.0
- Added an opt-in `activate teleporter` command to allow teleporting the player that is targeted on the terminal map.
- Improved the vanilla bug fixes to solve issues with invalid map targets being selected by both maps.

## Version 1.2.3
- Added a compatibility patch for EnhancedRadarBooster's to allow the `zoom` command to work with the mod's increased radar booster map range.

## Version ~~1.2.1~~ 1.2.2
- Fixed an unintentional hard dependency on [OpenBodyCams](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams/).

## Version 1.2.0
- Added a compatibility mode for [OpenBodyCams](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams/) to display a separate body cam when using its `view bodycam` command.
- Added an option to select the texture filtering used on the map. By default, nearest-neighbor (`Point`) filtering will be used.

## Version 1.1.2
- Fixed zoom level parsing for locales that use `,` as the decimal separator.

## Version 1.1.1
- Fixed an error that would occur when loading into a game with GeneralImprovements installed.

## Version 1.1.0
- Implemented an opt-in feature feature which allows the terminal to select from a list of customizable zoom levels.

## Version 1.0.2
- Render a separate instance of the fixed UI for the terminal map
  - This fixes an issue where the planet description and video would not display after initially loading into a save
  - If a mod is installed that keeps the terminal visible after exiting it, the "MONITORING: [player]" text will remain visible as well

## Version 1.0.1
- Track the visibility of the terminal contents so that the map continues updating if a mod forces it to remain visible

## Version 1.0.0
- Initial release