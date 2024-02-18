module X4.Gates

open FSharp.Data
open X4.Utilities
open X4.Data

// Data on gates is scattered in a two primary places.
// 1. The ZONES file that creates a zone, then places a gate within it.
// 2. The GALAXY file that holds the information on the connections between two gates.


[<StructuredFormatDisplay("({X}, {Y}, {Z})")>]
type Position = 
    {
        X: float
        Y: float
        Z: float
    } 
    static member Default = { X = 0; Y = 0; Z = 0 }
    static member FromOffset (position:X4Zone.Position) =
        { X = float(position.X); Y = float(position.Y); Z = float(position.Z) }

[<StructuredFormatDisplay("({X}, {Y}, {Z}, {W})")>]
type Quaternion = 
    {
        X: float
        Y: float
        Z: float
        W: float
    }
    static member Default = { X = 0; Y = 0; Z = 0; W = 1 }
    static member FromQuaternion (quaternion:X4Zone.Quaternion) =
        { X = quaternion.Qx; Y = quaternion.Qy; Z = float(quaternion.Qz); W = quaternion.Qw }   // Qz as been determined to be a decimal by the type provider for some reason.


// Given a zone connection, does it represent a gate?
let isZoneConnectionAGate (connection:X4Zone.Connection) =
    connection.Ref = "gates"

// Given the array of connections in a zone, return Some connection if theres a gate, or None if it's not.
// While we might have an array as input, it seems there's only ever a single gate in a zone.
let findGateFromZoneConnections (connections:X4Zone.Connections) =
    connections.Connections |> Array.tryFind isZoneConnectionAGate


let findConnectionByDestination (destination:string) (connections:X4Galaxy.Connection list) =
    let containsDestination (connection:X4Galaxy.Connection) = 
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

// A record that represents a gate and the zone it's in, along with code to
// extract the data from a the type provider zone type. We do this to simplify
// handling, but also store the full zone, in case we need it to write some XML out.
[<StructuredFormatDisplay("{Sector}/{Zone}:{Faction} ({ConnectionType})/{ConnectionName} {GateType} - {Position} rotation{Quarternion} {connection}")>]
type Gate =
    {
        Sector: string
        Zone: string
        Faction: string
        GateType: string
        ConnectionType: string
        ConnectionName: string
        Position: Position
        Quarternion: Quaternion

        connection: Option<X4Galaxy.Connection>
        X4Zone: X4Zone.Macro    // Store the full record for later use.
    }
    static member FromZone(zone: X4Zone.Macro) =
        let connection = (findGateFromZoneConnections zone.Connections.Value).Value
        let connectionMacro = connection.Macro.Value   // the caller of FromZone must make sure this is always present: ie, this is a valid gate zone
        let sector = findSectorFromZone zone.Name allSectors |> Option.defaultValue "Unknown"
        let faction = findFactionFromZone zone.Name |> Option.defaultValue "Unknown"
        let position = Position.FromOffset connection.Offset.Value.Position
        let quaternion = // Quarternians are almost, but not always, set
            match connection.Offset.Value.Quaternion with 
            | None -> Quaternion.Default
            | Some q -> Quaternion.FromQuaternion q

        let connectionName = connection.Name.Value // safe, as connection name always exists for gate connections.
        let connection = findConnectionByDestination connectionName allGalaxy

        {
            Sector = sector
            Zone = zone.Name
            Faction = faction
            GateType = zone.Class
            ConnectionType = connectionMacro.Ref
            ConnectionName = connectionName
            Position = position
            Quarternion = quaternion

            connection = connection
            X4Zone = zone
        }
    member gate.asString () =
        let connection = if gate.connection.IsNone then "Unknown" else gate.connection.Value.Name
        sprintf "%A/%A:%A (%A) %A %A - %A rotation: %A Connection: %A" gate.Sector gate.Zone gate.Faction gate.ConnectionType gate.ConnectionName gate.GateType gate.Position gate.Quarternion connection



// Generate a list of all the gates in the game across base and DLC
let allGates =
    // Helper function: Is this zone a zone that contains a gate?
    let IsGateZone (zone:X4Zone.Macro) =
        match zone.Connections with
        | None -> false
        | Some connections -> 
            match findGateFromZoneConnections connections with
            | None -> false
            | Some gate -> true

    // If a zone contains a gate, extract the data and return Some Gate, otherwise None
    let getGateFromZone (zone:X4Zone.Macro) =
        match IsGateZone zone with
        | false -> None
        | true -> Some (Gate.FromZone zone)
    X4.Data.allZones |> List.choose getGateFromZone


// Given the name of a connection, find the gate that it refers to.
let findGateByConnectionName (connectionName:string) =
    allGates |> List.tryFind (fun gate -> gate.ConnectionName = connectionName)

let findConnectedGate(gate:Gate) =
    match gate.connection with
    | None -> None
    | Some connection ->
        // Ok, we've got a connection, so first thing to do is find the gate at the other end.
        // A connection refers to two gates by connection name, so we need to ignore the one
        // that's the same as the gate we're looking at.
        let srcConnectionName  = System.IO.Path.GetFileName(connection.Path.Value)
        let destConnectionName = System.IO.Path.GetFileName(connection.Macro.Path.Value)
        // discard the connectio name that matches the gate we're looking at.
        let remoteConnectionName =
            match srcConnectionName = gate.ConnectionName with
            | true -> destConnectionName
            | false -> srcConnectionName

        // Now that we have the connection name for the remote gate, we can find it.
        findGateByConnectionName remoteConnectionName


// Given a gate, is it safe? That is, is the remote side it's connected to part of
// the same factions territory?
let isGateConnectionSafe (gate:Gate) =
    match findConnectedGate gate with
    | None -> true // no connection? Always safe!
    | Some remoteGate -> remoteGate.Faction = gate.Faction  // Is the remote gate of the same faction? Then safe!


// This is the primary function of this module: IT uses all the other functions to process all gates defined
// in the ZONES file, figure out how they are connected to each other via the GALAXY file, and then determines
// which gates connected to potentially hostile territory for our main AI factions. We will use this list of
// unsafe gates when we determine where to place our defense stations.
let findUnsafeGates =
    allGates
        |> List.filter (fun gate -> gate.Faction <> "xenon" && gate.Faction <> "Unknown")
        |> List.filter (fun gate -> not (isGateConnectionSafe gate))




/// ======== Some debug dump functions to print out the gates we've found. =========

let printGates (gates:Gate list) =
    [| for gate in gates do printfn "%A (%A)" (gate.asString()) (isGateConnectionSafe gate) |] |> ignore
    printfn "Total gates: %d" gates.Length


// dump some debut: print out all the gates found in the game.
let printGatesInZones() =
    // Start with all the gates
    allGates |> printGates

    // Now lets just print out the gates in our new faction zones
    let factionGates = allGates |> List.filter (fun gate -> gate.Faction <> "xenon" && gate.Faction <> "Unknown")
    factionGates |> printGates

    // And now the specific faction unsafe gates we want to defend:
    findUnsafeGates |> printGates
