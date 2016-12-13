# ManeuverQueue

ManeuverQueue is an add on for Kerbal Space Program that allows you to sort and filter objects displayed in the Tracking Station list. It was developed and tested against KSP v1.1.3 and v1.2.2

## Installation

ManeuverQueue supports KSP v1.1.3 and v1.2.x but it is important that you install the correct file. 

[https://github.com/FatHand/ManeuverQueue/releases] (https://github.com/FatHand/ManeuverQueue/releases)
- For v1.1.3 use **ManeuverQueue_KSP1.1.3.zip**
- For v1.2.x use **ManeuverQueue_KSP1.2.zip**

Add the contents of the archive to your KSP GameData folder.

You can also build from source, but you'll need to ensure you add the correct version's assemblies to the associated projects.  

## Usage

When the add on is installed you will see a small toolbar at the top left of the Tracking Station. Switching modes will sort and filter the list in the tracking station.

**MET**

- Shows the default Tracking Station list

**MNV**

- Shows only those ships with maneuvers nodes
- Sorts ships by maneuver node time, earliest first
- Ships with maneuver node times in the past will appear at the top of the list
- A ship is only listed once even if it has multiple maneuver nodes
- Color coding
  - Yellow
    - Two ships' next maneuver nodes are less than 15m apart
    - A ship's next maneuver node is less than 15m away
  - Red
    - The ship's next maneuver node is in the past

**A-Z**

- Shows the default list, sorted alphabetically

**Time Warp**

- Time warp will slow to 1x when a maneuver node is 15m away. Very high speed warps can lag up to a few minutes when slowing

**Vessel Type Filters**

- Switching to MNV mode will unset all vessel type filters. Returning to MET or A-Z mode will restore filters set in those modes.

## Known Issues

- Linux is not supported
- Certain tracking station operations such as untracking an object, terminating a vessel, or switching vessel filters causes the list to flicker momentarily

## Bugs and Feature Requests

Feel free to submit issues or feature requests here or on the KSP forums [http://forum.kerbalspaceprogram.com/index.php?/topic/146568-122-maneuverqueue-v042/] (http://forum.kerbalspaceprogram.com/index.php?/topic/146568-122-maneuverqueue-v042/)
