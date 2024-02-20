in progress:

up next:
* move faction shipyards to their new sector; and replace old one with xenon

BUGS:
* Bastions are:
    * sometimes not alwayd generated in right place. Does the game move them?

## TODO:
* Handle STATION
    * MOVE/REPLACE one shipyards/wharves/trade station per faction rather than just removing.
        * Replace at old location with xenon equivalent.
        * Need to find a zone for these moved items. Or create. Some won't need it. 
      Looks like I might be able to just specify a sector as location, and have it auto placed in sector.

* Change the faction terratories again.
To stop the new defense stations from blockading paths for the xenon, I think I should move the factions
each to one or two sectors that are dead ends, and add resource to these zones.

Note: 2024/02/18: Turns out that the instructions I was following for gates applies only to, well, gates.
The superhighways and accellerators that take you between sectors in a cluster do NOT work like this,
and do not have configuration in the 'connections' file. They seem to be defined as a connection in the
 *clusters* file, with connection ref "sechighways"
 Which kind of leads to a lore issue: Clusters seem to be a system, so easy to traverse. Maybe I should be
 limiting faction to whole clusters, rather than only some sectors in a cluster. Two reasons. Lore, otherwise
 it's easy for Xenon to ignore gates. and b) I don't need to process even more data to figure out superhighways.

Here are some ideas, but maybe it's preferrable to find places I can give them TWO clusters, for more starting variety.
Really need two sectors per faction, as often station placement is limited per sector. Even with two sectors, 
some products won't be placed, reducing economies. This is ok, I think.

ZYA: Eleventh hour    OR Guiding Star V & VII
FREE FAMILIIES:  Heart of Acrimony II, Tharka Ravine XVI
HAT: Hatikvahs choice III
ARGON: Morning Star IV
ANT: Antigone Memorial (add a tiny bit of methane)
HOP: Cardinals Redress, Lasting Vengence
PAR: Trinity Sactum VII
TEL/MIN: Hewa's Twin V, III

VIG: Lets actually take Windfall I away from them, and limit them to Wind 1&2. Gives Xenon even more open pathways.
BORON: Retreat to core sectors? Need to set gamestarts to already open gate.


 
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
        WE SHOULD LEAVE AT LEAST ONE 'SAFE' OWNERLESS FOR THE PLAYER!

* Handle PRODUCT
    * I'm not sure the following really matter. Will move it to options.
    * DLC slightly increases galaxy product module count with new product entries for existing factions: Remove these.
        * We also have products allocated in 'friendly' sectors, such as PIO in Oort cloud.
        eg:     <location class="sector" macro="cluster_116_sector001_macro" relation="self" comparison="ge" />
        Also we have things like Xenon factories assigned to specific sectors: do NOT increase these limits.
    * add 'matchextension="false"' to location tags. - Is this needed? (yes, turned out it was)



# DONE
* add a gamestart to enable easy testing: show all sectors.
* Handle STATION
    * write output ADD XML for stations we're added
    * write output REPLACE xml for stations we're removing.
    * add 'matchextension="false"' to location tags.
    * CHECK: Do I need to add 'matchextension' for 'REMOVE' tags?
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
    * Reduce QUOTAS. Factor: try 2/3 of current allocation rounded up to start. Don't overdo, especially with increase X economy.
    * INCREASE Xenon product quota - so that new sectors are filled with stations and generation resources. 3x? 
    * write output REPLACE xml
    * Give TER a few more solar stations: they're energy starved with the asteroid belt and mars

* DLC:
    * Check if DLC exists
    * Load/process each DLC
        * SPLIT
        * Terran
        * BOR: Maybe start unified? How?
    * We're not changing their zones, as their economy is pretty wretched already.
    * Apart from add a Bastion gate to main system
    * Pirate
        * We're not changing zones for this faction. Large easy starting zone
        * Add bastion gate to entry system.

* FLEETS: 
 Fleets are handled in the JOBS file. It sets both initial game start fleets, and the
 max number of fleets the game will gradually order if required by faction logic.
 These quotas are 'galaxy' and 'maxgalaxy' respectively.
 So we want to INCREASE both for xenon, and DECREASE only 'galaxy' for other factions.
 We want them to start weaker than normal, but still have same max.
 see  https://forum.egosoft.com/viewtopic.php?t=444909

Many jobs have only galaxy quota set, not maxGalaxy. Does this mean max=quota? Or no limit?
We may need to add a maxgalaxy in these cases to set to the original quota.

'wings' jobs only have a single 'quota' line. We should ignore these.

* Reduce faction starting fleet sizes
    * Military
    * Civilian
* Increase XENON starting fleet size

* Systems:
    * Place GATE DEFENCE station
        * Each faction needs a strong station guarding it's gate.
        * check ZONES.xml file: seems to define zones for gates, and their connections
            * confirmed. More info at the bottom of page here:
            https://github.com/enenra/x4modding/wiki/Universe-Creation#advanced---gates
            * connection field 'ref="gates"' represents a connection to a gate.
        * Write algorithm that uses valid sectors for race to determine where to place gates.
            * advanced: Should I track paths of gates to determine whether the sector on the other side is a safe sector or not? Then places gates only on unsafe.

* Find unsafe gates to place defence stations next to.
    * extract all gates and their data.
    * find gates in territory
    * determine which gates are safe and which are not

* Stations:
    * Write code to find a type of station for a race
    * write code to place a station in a specific place/zone
    * place 3 race-specific defence stations around gate.
        * Figure out how to rotate the defense station around the gate so it's not blocking things

