## TODO:
 * Handle STATION
   ** write output ADD XML
   ** write output REPLACE xml
 * Handle PRODUCT
   * Reduce QUOTAS. Factor 4? 3?
   * Alter sector max to be same as galaxy max
   * INCREASE Xenon product quota
   * write output REPLACE xml
 * Systems:
   * Assign 2 systems per faction
     * place ALI near HOP
     * HAt near ARG
   * RESOURCES:
     * Ensure each faction has basic resources: Add fields to 
       systems.
     * Make sure solar energy is 1x for at least one of chosen system: increase if required.

 * Place GATE DEFENCE station
   * Each faction needs a strong station guarding it's gate.
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
    * BOR: Maybe start unified? How?
    * SPLIT
    * Terran
    * Pirate
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

