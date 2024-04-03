in progress:
up next:
    * increase job quota on
        * xenon_destroyer_patrol_xl_cluster
        * xenon_destroyer_patrol_xl_cluster
    * too many traders running between factions - they will get destroyed. Need to cluster things closer?

BUGS:
* Bastions are:
    * sometimes not alwayd generated in right place. Does the game move them?
      * testing shows its close enough to work, so won't worry about it.
* Pirate density too high now: Cripples economies. Drop to 1/3
* Increase faction territory and resources in attempt to reduce factions trading through Xenon space,
loosing all ships, and crippling economy. Pick to make trade routes a bit safer.
  * increase: ZYA, CURB, ANT, TEL, PAR
  * Can we increase HOP without completely blocking Xenon routes to 2nd contact?

* increase resources
* Check product factory spawns: Do some factions spawn with less, increase requirement for external trade?


## TODO:


* Move the default PlayerHQ location to somewhere safe
    * Or should I leave it? It has plot immunity, and it might be a good challenge
    to claim it, and try hold it from the Xenon, remote from other factions.
    Consider moving it closer to faction territory, but not in it; like 2nd Contact.

## Post Beta/pre v1.0
This is stuff I want to clean up during the beta period, prior to final release.
* Remove TER cutscene for approaching mars gate: they don't pocess it any more.

 
## Post V1: Optional/Maybe do/other ideas:
Here's some optional stuff I may add after the initial release

* Improve compatibility when we don't have DLC installed.
    Move DLC specific stuff in to /extensions/dlc_name (I think this does the trick?)

* Handle better ways to get blueprints: (since I've limited economy => slow gain of resources to buy blueprints)
  I think this is better left as optional to the user, let them install other mods, BUT:
    * Other mods?
    * Deconstructing ships?
    * Loot drops? (I'm partial to this solution: some kind of drop system that sometimes drops blueprints when destroying Xenon.)
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

* Some more optional product stuff.
    * I'm not sure the following really matters, so I'm putting it in the 'maybe' list
    * DLC slightly increases galaxy product module count with new product entries for existing factions: Remove these.
        * We also have products allocated in 'friendly' sectors, such as PIO in Oort cloud.
        eg:     <location class="sector" macro="cluster_116_sector001_macro" relation="self" comparison="ge" />
        Also we have things like Xenon factories assigned to specific sectors: do NOT increase these limits.


# DONE
### How to add derelict ships
* Add more derelict ships around
`/md/placedobjects.xml`
Append xml to list:
/mdscript[@name='PlacedObjects']/cues/cue[@name='Place_Claimable_Ships']/actions

```
        <!--deep in xenon space - L ship-->
        <!--teleport sector="cluster_25_sector002_macro">
          <offset>
            <position x="-165396.516" y="-489.434" z="145876.938"/>
            <rotation yaw="-112.03669" pitch="0.620682"/>
          </offset>
        </teleport-->
        <find_sector name="$sector" macro="macro.cluster_25_sector002_macro"/>
        <do_if value="$sector.exists">
          <create_ship name="$ship" macro="macro.ship_par_l_destroyer_01_a_macro" sector="$sector">
            <owner exact="faction.ownerless"/>
            <position x="-165.396km" y="-0.489km" z="145.876km"/>
            <rotation yaw="-112deg" pitch="0.6deg" roll="30deg"/>
          </create_ship>
        </do_if>
```

* add a gamestart to enable easy testing: show all sectors.
* Handle STATION
    * write output ADD XML for stations we're added
    * write output REPLACE xml for stations we're removing.
    * add 'matchextension="false"' to location tags.
    * CHECK: Do I need to add 'matchextension' for 'REMOVE' tags?
      => Seems to have worked without it.
    * add DLC support => this gives MVP (WORKING ON RIGHT NOW)
    * MOVE/REPLACE one shipyards/wharves/trade station per faction rather than just removing.
        * Replace at old location with xenon equivalent.
        * Need to find a zone for these moved items. Or create. Some won't need it.
      Looks like I might be able to just specify a sector as location, and have it auto placed in sector.

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
    * Give TER a few more solar stations: they're energy starved with the asteroid belt and mars // AND REMOVE THIS AGAIN
    * add 'matchextension="false"' to location tags. - Is this needed? (yes, turned out it was)

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

THIS MEANS WE'RE GOING TO HAVE TO:
* ADD RESOURCE REGIONS
    * Looking at definitions in files, it looks like regions can be reused. The vanilla clusters.xml, for
    example, refers to the region `p1_40km_ice_field` multiple times. So we can just select some default
    regions for each of our resources, and scatter them around by adding a cluster connection. eg:
```
      <connection name="C01S02_Region001_connection" ref="regions">
        <offset>
          <position x="-21136452" y="0" z="7237120" />
        </offset>
        <macro name="C01S02_Region001_macro">
          <component connection="cluster" ref="standardregion" />
          <properties>
            <region ref="p1_40km_ice_field" />
          </properties>
        </macro>
      </connection>
```
Position: Get basic position from the cluster position element. These positions are *much* larger than
positions specified in the sector file for zones. I suspect that sector file zone positions are an offset
from the sector center; while the cluster connection positions are offsets from the galactic center.
Either way, from the cluster file, find the sector connection and gets it's position.
Annoyingly, not all sector connection have a position. eg: Cluster_01_Sector001_connection.
Does this mean it defaults to the cluster position in galaxy.xml? Looking at the data, this *might* be
just defaulting to 0,0,0 when position isn't given. Cluster 01 is galactic center, after all.

* generate resource fields in new sectors sectors.
    * select some default regions for each type of resource
    * place them randomly in the factions valid sectors if the faction needs it.

## Balance changes:
* Do't spawn boron abandoned ships - no engines
* Don't spawn Terran L & XL destroyers: no main gun.
* rebalance abandoned ships:
  * reduce count of small ships, and put them in 'safe' sectors. Make exploration more valuable by having unsafe sectors mostly M, L and XL ships
