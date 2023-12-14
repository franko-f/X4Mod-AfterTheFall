up next:
 * move faction shipyards to their new sector; and replace old one with xenon
 * add bastion
 * change fleets

## TODO:
 * Handle STATION
   ** MOVE/REPLACE one shipyards/wharves/trade station per faction rather than just removing.
   *** Replace at old location with xenon equivalent.
   *** Need to find a zone for these moved items. Or create. Some won't need it. 
      Looks like I might be able to just specify a sector as location, and have it auto placed in sector.

 * Handle PRODUCT
  ** DLC slightly increases galaxy product module count with new product entries for existing factions: Remove these.
   *** We also have products allocated in 'friendly' sectors, such as PIO in Oort cloud.
       eg:     <location class="sector" macro="cluster_116_sector001_macro" relation="self" comparison="ge" />
      Also we have things like Xenon factories assigned to specific sectors: do NOT increase these limits.
   ** add 'matchextension="false"' to location tags. - Is this needed?

 * FLEETS: 
  * Reduce faction starting fleet sizes
   * Military
   * Civilian
  * Increase XENON starting fleet size

 * Systems:
  * Place GATE DEFENCE station
   * Each faction needs a strong station guarding it's gate.
   * check ZONES.xml file: seems to define zones for gates, and their connections

 
## Optional/Maybe do/other ideas:
  * Add more derelict ships around
  * Handle better ways to get blueprints: (limit economy == slow gain)
    * Other mods?
    * Deconstructing ships?
    * Loot drops?
    * Lower prices?

  * Configure GAME START options.
    * Strong/easy start
    * Hard start
    * Each faction.
    * Include DLC

  * Neutral/pirate factions: for now, lets just ignore.
   * SCALE: Figure out where/how their stations are placed.
     * Looks like SOME factories/product are set to 'ownerless' for faction for HAT/SCA (spaceweed)
        So we might need to either override this to place in another faction, or create a 'safe'
        ownerless sector somewhere for them.
        This actually is probably good: 
        WE SHOULD CREATE AT LEAST ONE 'SAFE' OWNERLESS FOR THE PLAYER!



# DONE
 * add a gamestart to enable easy testing: show all sectors.
 * Handle STATION
   ** write output ADD XML for stations we're added
   ** write output REPLACE xml for stations we're removing.
   ** add 'matchextension="false"' to location tags.
   ** CHECK: Do I need to add 'matchextension' for 'REMOVE' tags?
      => Seems to have worked without it.
   * add DLC support => this gives MVP (WORKING ON RIGHT NOW)

 * Systems:
    * Assign 2 systems per faction
     * place ALI near PAR
     * HAt near ARG
   * RESOURCES:
     * Ensure each faction has basic resources: Add fields to systems.
        => Currently, we've assigned existing systems with existing resources. Some are pretty sparse, and might not be enough.
     * Make sure solar energy is 1x for at least one of chosen system: increase if required.
        => TER is light on energy (heh). Maybe increase solar to compensate.

 * Handle PRODUCT
   ** Reduce QUOTAS. Factor: try 2/3 of current allocation rounded up to start. Don't overdo, especially with increase X economy.
   ** INCREASE Xenon product quota - so that new sectors are filled with stations and generation resources. 3x? 
   ** write output REPLACE xml
   ** Give TER a few more solar stations: they're energy starved with the asteroid belt and mars

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



   BUG: ALI wharf in Trinity has been replaced by Xenon wharf. Check faction sector mapping/ALI name
   BUG: Looks like some terran and segaris defence stations are not being replaced: Why? Wrong name? Wharfs and shipyards are.
   
