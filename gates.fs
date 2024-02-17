module X4.Gates

open FSharp.Data
open X4.Utilities
open X4.Data

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
let isConnectionAGate (connection:X4Zone.Connection) =
    connection.Ref = "gates"

// Given the array of connections in a zone, return Some connection if theres a gate, or None if it's not.
// While we might have an array as input, it seems there's only ever a single gate in a zone.
let findGateFromConnections (connections:X4Zone.Connections) =
    connections.Connections |> Array.tryFind isConnectionAGate

// Is this zone a zone that contains a gate?
let IsGateZone (zone:X4Zone.Macro) =
    match zone.Connections with
    | None -> false
    | Some connections -> 
        match findGateFromConnections connections with
        | None -> false
        | Some gate -> true

// A record that represents a gate and the zone it's in, along with code to
// extract the data from a the type provider zone type. We do this to simplify
// handling, but also store the full zone, in case we need it to write some XML out.
type Gate = 
    {
        Sector: string
        Zone: string
        GateType: string
        ConnectionType: string
        ConnectionName: string
        Position: Position
        Quarternion: Quaternion
        X4Zone: X4Zone.Macro    // Store the full record for later use.
    }
    static member FromZone(zone: X4Zone.Macro) =
        let connection = (findGateFromConnections zone.Connections.Value).Value
        let connectionMacro = connection.Macro.Value   // the caller of FromZone must make sure this is always present: ie, this is a valid gate zone
        let sector = findSectorFromZone zone.Name allSectors |> Option.defaultValue "Unknown"
        let position = Position.FromOffset connection.Offset.Value.Position
        let quaternion = // Quarternians are almost, but not always, set
            match connection.Offset.Value.Quaternion with 
            | None -> Quaternion.Default
            | Some q -> Quaternion.FromQuaternion q

        let connectionName = connection.Name.Value // safe, as connection name always exists for gate connections.
        {
            Sector = sector
            Zone = zone.Name
            GateType = zone.Class
            ConnectionType = connectionMacro.Ref
            ConnectionName = connectionName
            Position = position
            Quarternion = quaternion
            X4Zone = zone
        }
    static member Print (gate:Gate) = 
        printfn "%A/%A: (%A) %A %A - %A rotation: %A " gate.Sector gate.Zone gate.ConnectionType gate.ConnectionName gate.GateType gate.Position gate.Quarternion


// If a zone contains a gate, extract the data and return Some Gate, otherwise None
let getGateFromZone (zone:X4Zone.Macro) =
    match IsGateZone zone with
    | false -> None
    | true -> Some (Gate.FromZone zone)

// Given a list of zones, extract and return all zones that contain gates as a list of Gate objects.
let findGatesInZones (zones:X4Zone.Macro list) =
    zones |> List.choose getGateFromZone

// dump some debut: print out all the gates found in the game.
let printGatesInZones = // (zones) =
    let gates = findGatesInZones X4.Data.allZones
    [| for gate in gates do Gate.Print gate |] |> ignore
    printfn "Total gates: %d" gates.Length
