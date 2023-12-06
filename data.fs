/// <summary>
/// This module contains data around factions, sectors and rules we'll use to
/// generate our new universe.
/// </summary>

module X4MLParser.Data

// Neat case insensitive string comparison function from https://stackoverflow.com/questions/1936767/f-case-insensitive-string-compare
// It's important as the X4 data files often mix the case of identifiers like zone and sector names.
let (=?) s1 s2 = System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase)


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
type Location = { x: float; y: float; z: float }
type MiningResource = { name: string; amount: int; location: Location }
type Territory = { faction: string; sector: string; zone: string; resources: MiningResource list; gates: Location list }
                   static member Default = { faction = ""; sector = ""; zone = ""; resources = []; gates = [] }

// create a list of all the factions and their territories as Territory records
// We'll pull resources and gates from the xml files, based on the sector name.
// We will likely need to create a zone (or find) in each of these sectors to place the station
let territories = [
    // core
    { Territory.Default with faction = "argon"; sector = "Cluster_14_Sector001_macro" }      // Argon Prime
    { Territory.Default with faction = "argon"; sector = "Cluster_29_Sector002_macro" }      // Hatikvah's choice III
    { Territory.Default with faction = "hatikvah"; sector = "Cluster_29_Sector001_macro" }   // Hatikvah's choice I
    { Territory.Default with faction = "antigone"; sector = "Cluster_27_Sector001_macro" }   // The Void
    { Territory.Default with faction = "antigone"; sector = "Cluster_28_Sector001_macro" }   // Antigone Menorial

    { Territory.Default with faction = "teladi"; sector = "Cluster_15_Sector001_macro" }     // Ianamus Zura IV
    { Territory.Default with faction = "teladi"; sector = "Cluster_15_Sector002_macro" }     // Ianamus Zura VII
    { Territory.Default with faction = "ministry"; sector = "Cluster_15_Sector001_macro" }   // Ianamus Zura IV
    { Territory.Default with faction = "ministry"; sector = "Cluster_15_Sector002_macro" }   // Ianamus Zura VII
//    { faction = "scaleplate"; sector = "" }

    { Territory.Default with faction = "paranid"; sector = "Cluster_47_Sector001_macro" }   // Trinity Sanctum VII
    { Territory.Default with faction = "paranid"; sector = "Cluster_18_Sector001_macro" }   // Trinity Sanctum III
    { Territory.Default with faction = "alliance"; sector = "Cluster_18_Sector001_macro" }  // Trinity Sanctum III
    { Territory.Default with faction = "holyorder"; sector = "Cluster_24_Sector001_macro" }  // Holy Vision
    { Territory.Default with faction = "holyorder"; sector = "Cluster_11_Sector001_macro" }  // Pontifex Claim

    // split: zyarth. freesplit: free families
    { Territory.Default with faction = "split"; sector = "Cluster_418_Sector001_macro" }      // Family Nhuut
    { Territory.Default with faction = "split"; sector = "Cluster_401_Sector001_macro" }      // Family Zhin
    { Territory.Default with faction = "freesplit"; sector = "Cluster_411_Sector001_macro" }  // Heart of Acrmony II
    { Territory.Default with faction = "freesplit"; sector = "Cluster_410_Sector001_macro" }  // Tharkas Ravine XVI
    { Territory.Default with faction = "freesplit"; sector = "Cluster_412_Sector001_macro" }  // Tharkas Ravine VIII

    // cradle of humanity
    { Territory.Default with faction = "terran"; sector = "Cluster_101_Sector001_macro" }     // Mars
    { Territory.Default with faction = "terran"; sector = "Cluster_100_Sector001_macro" }     // Asteroid belt
    { Territory.Default with faction = "pioneers"; sector = "Cluster_114_Sector001_macro" }   // Gaian Prohecy
    { Territory.Default with faction = "pioneers"; sector = "Cluster_113_Sector001_macro" }   // Segaris

    // tides of avarice
    { Territory.Default with faction = "loanshard"; sector = "" }   // VIG : leave these UNCHANGED
    { Territory.Default with faction = "scavenger"; sector = "" }   // RIP : UNCHANGED

    // boron
    { Territory.Default with faction = "boron"; sector = "" }       // Boron: Economy is kinda screwed without player help anyway. Leave them alone for now?

]

let findRecordsByFaction (faction: string) records =
    records |> List.filter (fun record -> record.faction = faction)

let isFactionInSector (faction: string) (sector: string) =
    territories |> List.exists (fun record -> record.faction = faction && record.sector =? sector)