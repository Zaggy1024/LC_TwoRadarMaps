# TwoRadarMaps
TwoRadarMaps separates the radar map within the terminal from the radar map displayed on the screen at the front of the ship so that they can monitor the perspectives of different players/radar boosters. The `switch` command in the terminal will only have an effect on its own radar map.

When combined with the [Touchscreen mod](https://thunderstore.io/c/lethal-company/p/TheDeadSnake/Touchscreen/), those two players will both be able to disable turrets/mines, open/close doors, etc. without interfering with each other.

Additionally, as a side effect of confining the visibility of night vision lighting to the map radar cameras, the night vision will now function outside the facility:

![Radar map view of outside the facility at night](https://raw.githubusercontent.com/Zaggy1024/LC_TwoRadarMaps/main/TwoRadarMaps/Package/outside_night.png)

## Features:

### Radar zoom
Disabled by default, this feature allows the terminal to select from a list of customizable map view sizes. To enable it, open the config and set the `Enabled` option under `[Zoom]` to `true`.

Once enabled, the following commands are available in the terminal:
- `zoom` will cycle through the configured zoom levels.
- `zoom in` and `zoom out` will stop at the minimum or maximum zoom levels.
- `reset zoom` will set the zoom level back to the configured default.

## Developer information
To the terminal mod developer reading this, if you have any commands that you would like to interact with the target being viewed by the map in this mod, you can check for the following:

```cs
UnityEngine.Object.FindObjectOfType<Terminal>().GetComponent<ManualCameraRenderer>();
```

If that component is found, then TwoRadarMaps is present, and the renderer can be used in place of the `StartOfRound.Instance.mapScreen` field for any commands interacting with it.