/// <summary>
/// The universe side of the data layer: clusters, sectors, zones and the galaxy map,
/// loaded from the map files and exposed through pure lookups (faction territory,
/// positions, safe/unsafe sectors) and the pure Gate records for every jump gate.
/// Also owns the single shared seeded Random used by god.fs and ships.fs.
/// </summary>
[<AutoOpen>]
module X4.Data.Universe

open System
open System.Xml.Linq
open X4.Types
open X4.Utilities
open X4.Territories
open X4.Data.Xml

let rand = new Random(12345) // Seed the random number generator so we get the same results each time as long as were not changing code.



// Load the clusters/sectors/zones from the core game and every DLC map file,
// combined into one list in load order.
// Convinience functions to search/manipulate these lists are defined below.
let AllClusters =
    X4ClusterFiles
    |> List.collect (fun file -> X4Cluster.Load(file).Macros |> Array.toList)

let allSectors =
    X4SectorFiles
    |> List.collect (fun file -> X4Sector.Load(file).Macros |> Array.toList)

let allZones =
    X4ZoneFiles
    |> List.collect (fun file -> X4Zone.Load(file).Macros |> Array.toList)

let allGalaxy =
    // we're assuming that the galaxy file just contains connections, and that the connection fields/structure
    // is pretty much the same between core and DLCs. Otherwise this casting from one to the other using the
    // XElement is dangerous. This only runs on mod creation though, and if it crashes it means something has
    // changed that we need to account for anyway.
    let loadFromDiff (file: string) = [
        // A DLC galaxy diff just adds a list of connections.
        for connection in X4GalaxyDiff.Load(file).Add.Connections -> new X4Galaxy.Connection(connection.XElement)
    ]

    (X4Galaxy.Load(X4GalaxyFileCore).Macro.Connections |> Array.toList)
    @ (X4GalaxyDlcFiles |> List.collect loadFromDiff)


// ===== FINISHED LOADING DATA FROM XML FILES =====



// Get all the factions defined in the speficied DLC
let dlcFactions dlc = X4.WriteModfiles.dlcFactions dlc
// Filter the territories list to only include those that are in the specified DLC
let dlcTerritories dlc =
    let factions = dlcFactions dlc

    territories
    |> List.filter (fun t ->
        // This is a little messy: A territory may explicitly set a DLC. Check for this.
        match t.dlc with
        | Some territoryDlc -> territoryDlc = dlc
        | None -> List.contains t.faction factions)

// Given a cluster name, return the X4Cluster object representing it.
let findCluster (clusterName: string) =
    AllClusters |> List.tryFind (fun cluster -> cluster.Name =? clusterName)

let getClusterMacroConnectionsByType connectionType (cluster: X4Cluster.Macro) =
    cluster.Connections
    |> Array.toList
    |> List.filter (fun connection -> connection.Ref = connectionType)

// Given a cluster name, find it, and then return all of it's connections of the specific type in a list.
// Note that here, unlike in other places, the type is pluralised. eg, don't search for 'sector', use 'sectors'
// Returns empty list if no sectors found.
let getClusterConnectionsByType connectionType clusterName =
    findCluster clusterName
    |> Option.map (fun cluster -> getClusterMacroConnectionsByType connectionType cluster)
    |> Option.defaultValue []

// Given a cluster name, return all the X4Sector objects in a list.
let findSectorsInCluster (cluster: string) =
    getClusterConnectionsByType "sectors" cluster
    |> List.map (fun connection -> Option.defaultValue "no_sector_name" connection.Macro.Ref)
    |> List.map (fun sector -> sector.ToLower()) // Lower case for consistency

// Given a faction and a territory record (encapsulating a cluster and the faction that owns it), return all the sectors in that territory that belong to the faction. Empty list if none.
let getFactionSectorsInTerritory (faction: string) (territory: Territory) =
    if territory.faction <> faction then
        // If the territory doesn't belong to the faction, then no sectors do.
        []
    else if territory.sectors.IsEmpty then
        // If sectors list is empty, then all sectors belong to the faction
        findSectorsInCluster territory.cluster
    else
        // Otherwise, return the sectors explicitly listed in the territory.
        territory.sectors

let getFactionSectors (faction: string) =
    territories
    |> List.collect (fun territory -> getFactionSectorsInTerritory faction territory)

let getFactionSectorsInCluster (faction: string) (cluster: string) =
    territories
    |> List.filter (fun territory -> territory.cluster =? cluster)
    |> List.collect (fun territory -> getFactionSectorsInTerritory faction territory)

// Given a sector name, which cluster does it belong to?
let findClusterFromSector (sector: string) =
    AllClusters
    |> List.tryFind
        (
        // For each cluster, we'll check if there's a connection to this sector.
        fun cluster ->
            getClusterMacroConnectionsByType "sectors" cluster
            |> List.exists (fun c -> Option.defaultValue "no_sector_name" c.Macro.Ref =? sector))
    // If we actually found a match, change the return value from Some Cluster to Some Cluster.Name
    |> Option.map (fun cluster -> cluster.Name)

// Using the data in sector.xml, which is represented by the X4Sector type, find the name of
// the sector given the name of the zone. the zone is stored as a connection in the sector definition.
let findSectorFromZone (zone: string) =
    // allSectors is a list of secto Macros. Each macro represents a sector. In that sector we'll find connections.
    // Each connection will have zero or more zones for use to check. So we try find a macro that contains a zone with the name we're looking for.
    // Then return the name of that macro.
    allSectors
    |> List.tryFind (fun sector ->
        sector.Connections
        |> Array.tryFind (fun connection ->
            connection.Ref = "zones"
            && connection.Macro.Connection = "sector"
            && connection.Macro.Ref =? zone)
        |> Option.isSome)
    |> Option.map (fun sector -> sector.Name.ToLower()) // return the sector name, but in lower case, as the case varies in the files. I prefer to make it consistent

let findClusterFromLocation (locationClass: string) (locationMacro: string) =
    match locationClass with
    | "zone" ->
        findSectorFromZone locationMacro
        |> Option.map findClusterFromSector
        |> Option.flatten
    | "sector" -> findClusterFromSector locationMacro
    | "cluster" -> Some locationMacro
    | _ -> None

// Explicit check for whether we've ALLOWED a faction in a cluster in our territory mapping.
// For most factions this is a lot less than what is in the base game.
// Cluster is not fine grained enough for most things (we can assign down to sector level), but
// jobs are often defined at the cluster level. This should not be used for most things.
let isFactionInCluster (faction: string) (cluster: string) =
    territories
    |> List.exists (fun record -> record.faction = faction && record.cluster =? cluster)

// This function returns whether a faction is ALLOWED to be in the sector as per our mod rules
let isFactionInSector (faction: string) (sector: string) =
    // Find the cluster the sector belongs to, THEN check if sector is in the list of faction sectors in that cluster.
    findClusterFromSector sector
    |> Option.map (fun cluster -> getFactionSectorsInCluster faction cluster |> List.exists (fun s -> s =? sector))
    |> Option.defaultValue false

// Have we ALLOWED the faction to be in this specific zone?
let isFactionInZone (faction: string) (zone: string) =
    match findSectorFromZone zone with
    | None -> false
    | Some sector -> isFactionInSector faction sector

// Given any location name and class, return whether the faction is ALLOWED to be in that location.
let isFactionInLocation (faction: string) (location: string) (locationClass: string) =
    match locationClass with
    | "galaxy" -> true // well, if the class is galaxy, then definitely
    | "sector" -> isFactionInSector faction location
    | "cluster" -> isFactionInCluster faction location
    | "zone" -> isFactionInZone faction location
    | _ -> failwith ("Unhandled location class in job: " + locationClass)


let findFactionFromCluster (cluster: string) =
    territories
    |> List.tryFind (fun record -> record.cluster =? cluster)
    |> Option.map (fun record -> record.faction)

// Sectors are a bit more complicated, as a faction might only have *some* of the sectors in a cluster.
let findFactionFromSector (sector: string) =
    match findClusterFromSector sector with
    | Some cluster ->
        // TODO: A cluster might be listed more than once, for factions like MIN. Probably not important in our use case.
        // Find the territory record, then check if the sector is in the list of sectors for that territory.
        territories
        |> List.filter (fun record -> record.cluster =? cluster)
        |> List.tryPick (fun territory ->
            if List.contains sector territory.sectors then
                // Is this sector explicitly listed? then it belongs to the territories faction.
                Some territory.faction
            else if territory.sectors.IsEmpty then
                // if the list is empty, it means the faction owns the entire cluster
                Some territory.faction
            else
                // otherwise, it's not in the list, for this specific territory record.
                None)
    | None -> None

let findFactionFromZone (zone: string) =
    match findSectorFromZone zone with
    | None -> None
    | Some sector -> findFactionFromSector sector


// this function wil take an XElement, and return the integer version of the value.
// It will handle both decimals and floating point strings in scientific notation.
// We need this because the Boron DLC, for some reason, has positions in scientific notation.
// eg, 1.234e+005
let getIntValue (element: string) = int (float element)

let parsePosition (element: XElement) =
    let x = getIntValue (element.Attribute("x").Value)
    let y = getIntValue (element.Attribute("y").Value)
    let z = getIntValue (element.Attribute("z").Value)
    x, y, z

// Get the X, Y, Z position of a sector, offset from the galactic center.
// sector position *may* be defined in the cluster.xml connection. If it's not, I'm assuming it defaults
// to the clusters position.
let getSectorPosition (sectorName: string) =
    // Search all clusters for the connection to the sector. We can use this to get the position,
    // either in the connection, or by looking up the cluster position.
    // Start by finding the cluster object that contains this sector.
    let cluster =
        findClusterFromSector sectorName
        |> Option.defaultValue "no_cluster"
        |> findCluster
        |> Option.get // Crap out if we can't find the cluster. This should never happen, and if it does, it means data has changed.

    // Now look for the connection to the sector in the cluster, and get the position for that connection.
    cluster.Connections
    |> Array.tryFind (fun connection -> connection.Ref = "sectors" && connection.Macro.Ref =?? sectorName)
    |> Option.map (fun connection ->
        connection.Offset
        |> Option.map (fun offset -> parsePosition offset.Position.XElement))
    |> Option.flatten
    // The connection for the sector may not have had a position, in which case it defaults to cluster center: ie, 0,0,0
    |> Option.defaultValue (0, 0, 0)


// Get all the safe sectors in the game. that is, sectors where factions exist, rather than Xenon or neutral
let private getSafeSectors =
    allSectors
    |> List.filter
        (
        // Filter to every sector that is in a cluster mentioned in the territories list. Those are our safe clusters.
        fun sector ->
            let cluster = findClusterFromSector sector.Name
            territories |> List.exists (fun record -> cluster =?? record.cluster))

// Get all the UNSAFE sectors in the game. That's the sectors that are Xenon in our mod, or neutral.
let private getUnsafeSectors = allSectors |> List.except getSafeSectors

// The selectors return the sector's macro NAME with its original file casing: it is
// interpolated into generated md script content, so the casing is byte-relevant.
let selectRandomSafeSector () =
    getSafeSectors.[rand.Next(getSafeSectors.Length)].Name

let selectRandomUnsafeSector () =
    getUnsafeSectors.[rand.Next(getUnsafeSectors.Length)].Name



// ==== GATES ====
// Data on gates is scattered in two primary places:
// 1. The ZONES file that creates a zone, then places a gate within it.
// 2. The GALAXY file that holds the information on the connections between two gates.
// Here we discover every gate and expose it as the pure Gate record.

// Adapters from the zone-file provider types to the pure geometry records in X4.Types.
let private positionFromOffset (position: X4Zone.Position) : Position = {
    X = float (position.X)
    Y = float (position.Y)
    Z = float (position.Z)
}

let private quaternionFromOffset (quaternion: X4Zone.Quaternion) : Quaternion = {
    X = quaternion.Qx
    Y = quaternion.Qy
    Z = float (quaternion.Qz) // Qz has been determined to be a decimal by the type provider for some reason.
    W = quaternion.Qw
}

// Given a zone connection, does it represent a gate?
let private isZoneConnectionAGate (connection: X4Zone.Connection) = connection.Ref = "gates"

// Given the array of connections in a zone, return the connections that are gates.
let private findGatesInConnections (connections: X4Zone.Connection list) =
    connections |> List.filter isZoneConnectionAGate

let private findConnectionByDestination (destination: string) (connections: X4Galaxy.Connection list) =
    let containsDestination (connection: X4Galaxy.Connection) =
        let matchPath =
            match connection.Path with
            | None -> false
            | Some path -> path.EndsWith(destination)

        let macroPath =
            match connection.Macro.Path with
            | None -> false
            | Some macro -> macro.EndsWith(destination)

        macroPath || matchPath

    connections |> List.tryFind containsDestination

// Create a pure 'Gate' record from a zone and a connection. While the zone already contains
// the connection, a zone may have multiple gate connections, so we need to be specific about
// the connection we want.
let private gateFromZone (zone: X4Zone.Macro) (connection: X4Zone.Connection) : Gate =
    let connectionMacro = connection.Macro.Value // the caller must make sure this is always present: ie, this is a valid gate zone
    let sector = findSectorFromZone zone.Name |> Option.defaultValue "Unknown"
    let faction = findFactionFromZone zone.Name |> Option.defaultValue "Unknown"
    let position = positionFromOffset connection.Offset.Value.Position

    let quaternion = // Quarternians are almost, but not always, set
        match connection.Offset.Value.Quaternion with
        | None -> Quaternion.Default
        | Some q -> quaternionFromOffset q

    let connectionName = connection.Name.Value // safe, as connection name always exists for gate connections.

    let galaxyConnection =
        findConnectionByDestination connectionName allGalaxy
        |> Option.map (fun c -> {
            Name = c.Name
            Path = c.Path
            MacroPath = c.Macro.Path
        })

    {
        Sector = sector
        Zone = zone.Name
        Faction = faction
        GateType = zone.Class
        ConnectionType = connectionMacro.Ref
        ConnectionName = connectionName
        Position = position
        Quarternion = quaternion
        Connection = galaxyConnection
    }

// Generate a list of all the gates in the game across base and DLC
let allGates =
    allZones
    |> List.collect (fun zone ->
        zone.Connections
        |> Option.map (fun x -> Array.toList x.Connections) // IF there is a connections array in the zone Option<connections>, convert to a list
        |> Option.defaultValue [] // convert the None result to an empty list
        |> findGatesInConnections // Find any connections that represent a gate in the connections list for the zone.
        |> List.map (fun connection -> gateFromZone zone connection) // And convert these connections to a Gate record.
    )
