/// <summary>
/// Functions to handle processing and manipulation of ship data, and generation of ship related mod content.
/// Builds on data loaded in X4.Data to convert to our internal 'shipInfo' and 'shipequipment' types.
/// </summary>

module X4.Territories

// lookup map to the resource definitions from the XML that we will use to place extra resources
// for factions now in sectors without resources.
let resourceMap = Map [
    "minerals", "atf_60km_asteroid_field_high";     // ore, silicon and a little bit of nvidium
    "ice",      "atf_60km_ice_field_high";          // ice
    "scrap",    "atf_wreckfield_xenon_battle_30km"; // scrap
    "hydrogen", "p1_40km_hydrogen_field";
    "helium",   "p1_40km_helium_highyield_field";
    "methane",  "p1_40km_methane_highyield_field"
]
// The standard resources that we'll use to populate the sectors. Two halves, one for each system.
let standardResources1stHalf = ["hydrogen"; "helium"; "methane" ]
let standardResources2ndHalf = [ "minerals"; "minerals"; "ice"; "scrap"; ] // 2x minerals to make it more common

// Factions will be limited to only a sector or two of valid territory.
// We'll put their defence stations and shipyards in these sectors. The game
// will then generate product factories here.
// We also need to place their starting ships in these sectors.
// We need to scan the xml files for what resources are available in these
// sectors; and if it's not enough, add new resources to the sector so that
// the faction has at least a minimal economy.
// Lastly, we'll need to place the Bastion stations near the jumpgates. We
// can also find the location of the jumpgates in the xml files, and then
// place three bastion stations around each one, encircling it.

// In addition to the games default collection of sectors/clusters that have no owners, we're going
// to add a few more, so that players have options on almost-safe sectors in which to start their own
// empire should they wish it
let neutralClusters = [
    "Cluster_01_macro"      // Grand Exchange, 3 sectors
    "Cluster_27_macro"      // Eighteen Billion
    // "Cluster_46_macro"      // Morningstar IV  - Changed to Xenon now that we've moved ARG to Morningstar III
    "Cluster_401_macro"     // Family Zhin
    "Cluster_422_macro"     // Wretched Skies X
    "Cluster_116_macro"     // Oort Cloud
    "Cluster_13_macro"      // second contact - Give ARG/ANt some breathing room, just a little, before Xenon storm through.
    "Cluster_09_macro"      // Bright promise - again, sector between TEL and PAR, to give a little bit of time before xenon play havoc
    "Cluster_24_macro"      // Holy Vision - Since HOP is split in two, make the sector between neutral to start to give them some breathing room.
]

// Type that specifies a station type and the class, locationj it will appear in.
// ego: XenonShipyard "sector" "cluster_110_sector001_macro"
type XenonStation =
    | XenonShipyard of string * string
    | XenonWharf of string * string

// add new Xenon shipyards/wharfs to the following clusters:
let newXenonStations = [
    XenonShipyard("sector", "Cluster_46_sector001_macro") // Morningstar IV
    XenonWharf("sector", "Cluster_46_sector001_macro")

    XenonShipyard("sector", "Cluster_100_sector001_macro") // Asteroid belt
    XenonShipyard("sector", "Cluster_109_sector001_macro") // Uranus

    XenonWharf("sector", "Cluster_413_sector001_macro") // Tharka Ravine IV: Tharkas Fall

    XenonWharf("sector", "Cluster_705_sector001_macro") // Nopoleas Memorial
    XenonShipyard("sector", "Cluster_722_sector001_macro") // Sanctum Verge

    // Since we made Holy Vision (cluster_24_macro) a neutral sector, we'll add Xenon stations that
    // would have spawned there nearby.
    XenonShipyard("sector", "Cluster_12_sector001_macro") // True Sight.
    XenonWharf("sector", "Cluster_12_sector001_macro") // True Sight.
    XenonWharf("sector", "Cluster_725_sector001_macro") // Void of Opportunity

    // Same for TEL/Hewa
    XenonWharf("sector", "Cluster_43_sector001_macro") // Hewas Twin 5
]


type Territory = {
    faction: string;
    cluster: string;
    resources: string list
    } with static member Default = { faction = ""; cluster = ""; resources = []}

// create a list of all the factions and their territories as Territory records
// We'll pull resources and gates from the xml files, based on the sector name.
// We will likely need to create a zone (or find) in each of these sectors to place the station
let territories = [
    // core
    { Territory.Default with faction = "argon";    cluster = "Cluster_07_macro"; resources=standardResources1stHalf }   // The Reach
    { Territory.Default with faction = "argon";    cluster = "Cluster_14_macro"; resources=standardResources2ndHalf }   // Argon Prime
    { Territory.Default with faction = "argon";    cluster = "Cluster_30_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf]) } // Morningstar III - Lets add more resources here where it's more exposed and dangerous.

    { Territory.Default with faction = "hatikvah"; cluster = "Cluster_29_macro"; resources=standardResources1stHalf }     // Hatikvahs Choice . ARG also have a station in one of the sectors in Hat choice via manual station assignment.

    { Territory.Default with faction = "antigone"; cluster = "Cluster_27_macro"; resources=["ice"; "scrap"] }  // The Void
    { Territory.Default with faction = "antigone"; cluster = "Cluster_28_macro"; resources=["minerals"] }      // Antigone Memorial
    { Territory.Default with faction = "antigone"; cluster = "Cluster_49_macro"; resources=[] }                // Frontiers Edge.
    { Territory.Default with faction = "antigone"; cluster = "Cluster_40_macro"; resources=[] }                // Second Contact VII - split from the other sectors to add some spice; also to give enough territory to spawn all stations.

    { Territory.Default with faction = "teladi";   cluster = "Cluster_15_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf; ["minerals"; "scrap"; "methane"; "hydrogen"]]) }   // Ianumas Zura
    { Territory.Default with faction = "teladi";   cluster = "Cluster_19_macro"; resources=[] }   // Hewas Twin. Next to PAR, but slightly distant from rest of TEL to add some spice
    //{ Territory.Default with faction = "teladi";   cluster = "Cluster_408_macro"; resources=[] }   // Thuruks Demise: A sector from Split DLC. Leaves Freelsplit just slightly less isolated. Won't put resources here due to a flaw in our code. Needs a refactor to permit added resources to another factions DLC sector.. Also does not generate stations in this territory either!
    { Territory.Default with faction = "ministry"; cluster = "Cluster_15_macro" }   // No need for resources, they're in teladi sectors already.
//    { faction = "scaleplate"; sector = "" }

    { Territory.Default with faction = "paranid";   cluster = "Cluster_18_macro"; resources=["minerals"] }    // Trinity III - already has some resources, but adding more.
    { Territory.Default with faction = "paranid";   cluster = "Cluster_47_macro"; resources=["scrap"] }      // Trinity VII
    { Territory.Default with faction = "paranid";   cluster = "Cluster_10_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf])  }      // Unholy Retribution
    { Territory.Default with faction = "alliance";  cluster = "Cluster_47_macro" }  // If we have to move an ALI station, move it to PAR space.

    // 7.0 added new sectors which made the HOP placement a lot easier than before.
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_35_macro"; resources=["helium"; "methane"; "ice"; "minerals"; "minerals"] }  // Lasting Vengence
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_36_macro"; resources=["minerals"; "scrap"; "helium"; "methane"] }  // Cardinals Redress =
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_11_macro"; resources=["minerals"; "methane"; "helium"] }          // Pontifax claim. Separated by one sector, but very close gates. Still can get messy though, and kill HOP traders if Zenon XL camps gate. Which tends to happen...
 //   { Territory.Default with faction = "holyorder"; cluster = "Cluster_714_macro"; resources=["minerals"; "methane"; "helium"] }          // 7.0 introduces the perfectly placed 'Freedoms Reach' just under cardinals redress. We'll add a few resources though. For some reason not spawning station though. Removing.

    // split: zyarth. freesplit: free families
    { Territory.Default with faction = "split";     cluster = "Cluster_405_macro"; resources=List.concat([["minerals"; "minerals"; "methane"; "hydrogen"]; standardResources1stHalf]) }  // Zyarth Dominion IV. These sectors are completely without resources, so through on some extra for ZYA
    { Territory.Default with faction = "split";     cluster = "Cluster_406_macro"; resources=List.concat([["minerals"; "helium"; "hydrogen"]; standardResources2ndHalf]) }  // Zyarth Dominion X
    { Territory.Default with faction = "split";     cluster = "Cluster_417_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf])}  // 11th Hour: Former Argon, but added in split DLC.

    { Territory.Default with faction = "freesplit"; cluster = "Cluster_410_macro"; resources=["minerals"; "scrap"; "methane"; "helium"] }      // Tharkas Ravine XVI
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_411_macro"; resources=["minerals"; "helium"; "methane"; "hydrogen"] }                // Heart of Acrmony II
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_412_macro"; resources=["minerals"] }  // Tharkas Ravine VIII

    // cradle of humanity
    { Territory.Default with faction = "terran";   cluster = "Cluster_104_macro"; resources= List.concat([standardResources1stHalf; standardResources2ndHalf]) }   // Earth and the Moon
    { Territory.Default with faction = "terran";   cluster = "Cluster_102_macro"; resources= List.concat([standardResources1stHalf; standardResources2ndHalf; ["helium"; "hydrogen"; "methane"; "minerals"]]) }   // venus
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_113_macro" }   // Segaris   - Plenty resources already, and next door to ANT.
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_114_macro" }   // Gaian Prophecy
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_115_macro" }   // Brennans Triumph. Since pioneers never seem to take territory, we'll leave them with their full original range.

    // tides of avarice
    // :eave VIG/Scavengers mostly unchanged. Leave Windfall I for sure to avoid issues with Erlking. (Or figure out how to move it in the future.)
    { Territory.Default with faction = "scavenger"; cluster = "Cluster_500_macro" }   // RIP: Unchanged. All sectors in cluster_500
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_501_macro" }   // Leave VIG unchanged.
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_502_macro"; resources=["scrap"] }   //
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_503_macro" }  // Windfall IV.

    // boron
    // Boron: Economy is kinda screwed without player help anyway. Leave them a few more sectors than most.
    // Removing These territories may screw up the default storyline, so players will need to set story complete in gamestart.
    // Cluster_602_macro: Barren Shores, Cluster_603_macro: Great Reef, Cluster_604_macro: Ocean of Fantasy
    { Territory.Default with faction = "boron"; cluster = "Cluster_606_macro"; resources=standardResources1stHalf }       // Kingdom End (cluster with 3 sectors) : Kingdoms end I, Reflected Stars, Towering Waves
    { Territory.Default with faction = "boron"; cluster = "Cluster_607_macro" }       // Rolk's Demise
    { Territory.Default with faction = "boron"; cluster = "Cluster_608_macro"; resources=standardResources2ndHalf }       // Atreus' Clouds
    { Territory.Default with faction = "boron"; cluster = "Cluster_609_macro" }       // Menelaus' Oasis
]
