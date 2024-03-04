module X4.Gates

open System
open MathNet.Numerics.LinearAlgebra.Double

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
let findGatesInConnections (connections:X4Zone.Connection list) =
    connections |> List.filter isZoneConnectionAGate


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
[<StructuredFormatDisplay("{Sector}/{Zone}:{Faction} ({ConnectionType})/{ConnectionName} {GateType} - {Position} rotation{Quarternion}")>]
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
    // Create a 'gate' record from a zone and a connection. While the zone already contains the connection, a zone may have 
    // multiple gate connections, so we need to be specific about the connection we want.
    static member FromZone(zone: X4Zone.Macro) (connection: X4Zone.Connection ) =
        let connectionMacro = connection.Macro.Value   // the caller of FromZone must make sure this is always present: ie, this is a valid gate zone
        let sector = findSectorFromZone zone.Name |> Option.defaultValue "Unknown"
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
    X4.Data.allZones
    |> List.collect (
        fun zone ->
            zone.Connections 
            |> Option.map (fun x -> Array.toList x.Connections)            // IF there is a connections array in the zone Option<connections>, convert to a list
            |> Option.defaultValue []                                      // convert the None result to an empty list
            |> findGatesInConnections                                      // Find any connections that represent a gate in the connections list for the zone.
            |> List.map (fun connection -> Gate.FromZone zone connection)  // And convert these connections to a Gate record.
       )


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
        |> List.filter (fun gate -> gate.Faction <> "xenon" && gate.Faction <> "Unknown" && gate.Faction <> "ministry" && gate.Faction <> "alliance") // Ministry are embedded in Teladi space, so don't need their own defense. Besides, they have no defense stations in game. Similar for ALI
        |> List.filter (fun gate -> not (isGateConnectionSafe gate))


let calculateStationPosition (position:Position) (rotation:Quaternion) (distance:float) (angle:float) =
    // Convert quaternion to rotation matrix
    let q = rotation
    let rotationMatrix = DenseMatrix.OfArray (array2D [|
        [| 1.0 - 2.0*q.Y*q.Y - 2.0*q.Z*q.Z; 2.0*q.X*q.Y - 2.0*q.Z*q.W; 2.0*q.X*q.Z + 2.0*q.Y*q.W |]
        [| 2.0*q.X*q.Y + 2.0*q.Z*q.W; 1.0 - 2.0*q.X*q.X - 2.0*q.Z*q.Z; 2.0*q.Y*q.Z - 2.0*q.X*q.W |]
        [| 2.0*q.X*q.Z - 2.0*q.Y*q.W; 2.0*q.Y*q.Z + 2.0*q.X*q.W; 1.0 - 2.0*q.X*q.X - 2.0*q.Y*q.Y |]
    |])

    // Convert angle from degrees to radians
    let angleRad = angle * Math.PI / 180.0

    // Create rotation matrix for the angle around the y-axis
    let angleRotationMatrix = DenseMatrix.OfArray (array2D [|
        [| Math.Cos(angleRad); 0.0; Math.Sin(angleRad) |]
        [| 0.0; 1.0; 0.0 |]
        [| -Math.Sin(angleRad); 0.0; Math.Cos(angleRad) |]
    |])

    // Combine the initial rotation with the angle rotation
    let combinedRotationMatrix = rotationMatrix * angleRotationMatrix

    // Apply the rotation and distance to the position
    let positionVector = DenseVector.OfArray [| position.X; position.Y; position.Z |]
    let displacementVector = distance * combinedRotationMatrix.Column(0)
    let newPositionVector = positionVector + displacementVector

    // Convert the result back to a Position
    let newPosition = { X = newPositionVector.[0]; Y = newPositionVector.[1]; Z = newPositionVector.[2] }

    printfn "Position old/new: %A / %A    (angle: %A, distance: %A, rotation: %A)" position newPosition angle distance rotation
    newPosition

// This function, given a gate, will extract the position, and rotation defined in quaternion, and then
// return three new locations that are positioned around the gate, each at an offset of 120 degrees, and each
// 10000 meters away from the gate. This is used to determine where to place defense stations around the gate.
let getDefenseStationLocations (gate:Gate) (numberOfStations:int) (distanceFromGate:int)=
    let position = gate.Position
    let rotation = gate.Quarternion
    let angle = float(360/numberOfStations)
    let angle_offset = float(360/numberOfStations)/2.0  // offset the angle by half the angle so that the first station is not right in front of the gate
    [ for i in 0..numberOfStations-1 -> calculateStationPosition position rotation (float(distanceFromGate)) (float(i) * angle + angle_offset), i ]


// using the gates from 'findUnsafeGates', this function will return the location of the defense stations
// for each gate, in a tuble that is 'race, gate, location'. It takes a single paramter that is "numberOfStations"
// which is the number of stations to place around each gate.
let getRequiredDefenseStationLocations numberOfStations distanceFromGate =
    findUnsafeGates
        |> List.collect (fun gate -> (getDefenseStationLocations gate numberOfStations distanceFromGate) |> List.map (fun (location, n) -> (gate, n, location)) )


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
