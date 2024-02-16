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