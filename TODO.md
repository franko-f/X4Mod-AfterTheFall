## TODO:
 * Handle STATION
   ** MOVE/REPLACE some shipyards/wharves rather than just removing.
   *** Need to find a zone for these moved items. Or create. Some won't need it.
   
 * Handle PRODUCT
   ** I don't think we need REMOVE for products, so clean this code up.
   ** Reduce QUOTAS. Factor 4? 3?
   ** Alter sector max to be same as galaxy max
   ** INCREASE Xenon product quota
   ** write output REPLACE xml
   ** add 'matchextension="false"' to location tags.
   ** Give TER a few more solar stations: they're energy starved with the asteroid belt and mars

 * Systems:
  * Place GATE DEFENCE station
   * Each faction needs a strong station guarding it's gate.
   * check ZONES.xml file: seems to define zones for gates, and their connections
  * FLEETS: Reduce starting fleet sizes
   * Military
   * Civilian
  * Neutral/pirate factions:
   * SCALE: Figure out where/how their stations are placed.
     * Looks like SOME factories/product are set to 'ownerless' for faction for HAT/SCA (spaceweed)
        So we might need to either override this to place in another faction, or create a 'safe'
        ownerless sector somewhere for them.
        This actually is probably good: 
        WE SHOULD CREATE AT LEAST ONE 'SAFE' OWNERLESS FOR THE PLAYER!

 * DLC:
  * Check if DLC exists
  * Load/process each DLC
    * SPLIT
    * Terran
    * BOR: Maybe start unified? How?
      ** We're not changing their zones, as their economy is pretty wretched already.
      ** Apart from add a Bastion gate to main system
    * Pirate
      ** We're not changing zones for this faction. Large easy starting zone
      ** Add bastion gate to entry system.

* Configure GAME START options.
  * Strong/easy start
  * Hard start
  * Each faction.
  * Include DLC

## Optional/Maybe do/other ideas:
  * Add more derelict ships around
  * Handle better ways to get blueprints: (limit economy == slow gain)
    * Other mods?
    * Deconstructing ships?
    * Loot drops?
    * Lower prices?

# DONE
 * Handle STATION
   ** write output ADD XML for stations we're added
   ** write output REPLACE xml for stations we're removing.
   ** add 'matchextension="false"' to location tags.
   ** CHECK: Do I need to add 'matchextension' for 'REMOVE' tags?
      => Seems to have worked without it.

 * Systems:
    * Assign 2 systems per faction
     * place ALI near PAR
     * HAt near ARG
   * RESOURCES:
     * Ensure each faction has basic resources: Add fields to systems.
        => Currently, we've assigned existing systems with existing resources. Some are pretty sparse, and might not be enough.
     * Make sure solar energy is 1x for at least one of chosen system: increase if required.
        => TER is light on energy (heh). Maybe increase solar to compensate.
