/// <summary>
/// Gate defence policy and geometry: which gates border hostile territory, and where
/// the ring of bastion stations should be placed around each of them.
/// Gate DISCOVERY (parsing zones/galaxy XML) lives in the data layer
/// (X4.Data.Universe.allGates); this module is pure logic over the Gate records.
/// </summary>
module X4.Gates

open System
open MathNet.Numerics.LinearAlgebra.Double

open X4.Data
open X4.Types


// Given the name of a connection, find the gate that it refers to.
let findGateByConnectionName (connectionName: string) =
    allGates |> List.tryFind (fun gate -> gate.ConnectionName = connectionName)

let findConnectedGate (gate: Gate) =
    match gate.Connection with
    | None -> None
    | Some connection ->
        // Ok, we've got a connection, so first thing to do is find the gate at the other end.
        // A connection refers to two gates by connection name, so we need to ignore the one
        // that's the same as the gate we're looking at.
        let srcConnectionName = System.IO.Path.GetFileName(connection.Path.Value)
        let destConnectionName = System.IO.Path.GetFileName(connection.MacroPath.Value)
        // discard the connectio name that matches the gate we're looking at.
        let remoteConnectionName =
            match srcConnectionName = gate.ConnectionName with
            | true -> destConnectionName
            | false -> srcConnectionName

        // Now that we have the connection name for the remote gate, we can find it.
        findGateByConnectionName remoteConnectionName


// Given a gate, is it safe? That is, is the remote side it's connected to part of
// the same factions territory?
let isGateConnectionSafe (gate: Gate) =
    match findConnectedGate gate with
    | None -> true // no connection? Always safe!
    | Some remoteGate -> remoteGate.Faction = gate.Faction // Is the remote gate of the same faction? Then safe!


// This is the primary function of this module: IT uses all the other functions to process all gates defined
// in the ZONES file, figure out how they are connected to each other via the GALAXY file, and then determines
// which gates connected to potentially hostile territory for our main AI factions. We will use this list of
// unsafe gates when we determine where to place our defense stations.
let findUnsafeGates =
    allGates
    |> List.filter (fun gate ->
        gate.Faction <> "xenon"
        && gate.Faction <> "Unknown"
        && gate.Faction <> "ministry"
        && gate.Faction <> "alliance") // Ministry are embedded in Teladi space, so don't need their own defense. Besides, they have no defense stations in game. Similar for ALI
    |> List.filter (fun gate -> not (isGateConnectionSafe gate))


let calculateStationPosition (position: Position) (rotation: Quaternion) (distance: float) (angle: float) =
    // Convert quaternion to rotation matrix
    let q = rotation

    let rotationMatrix =
        DenseMatrix.OfArray(
            array2D [|
                [|
                    1.0 - 2.0 * q.Y * q.Y - 2.0 * q.Z * q.Z
                    2.0 * q.X * q.Y - 2.0 * q.Z * q.W
                    2.0 * q.X * q.Z + 2.0 * q.Y * q.W
                |]
                [|
                    2.0 * q.X * q.Y + 2.0 * q.Z * q.W
                    1.0 - 2.0 * q.X * q.X - 2.0 * q.Z * q.Z
                    2.0 * q.Y * q.Z - 2.0 * q.X * q.W
                |]
                [|
                    2.0 * q.X * q.Z - 2.0 * q.Y * q.W
                    2.0 * q.Y * q.Z + 2.0 * q.X * q.W
                    1.0 - 2.0 * q.X * q.X - 2.0 * q.Y * q.Y
                |]
            |]
        )

    // Convert angle from degrees to radians
    let angleRad = angle * Math.PI / 180.0

    // Create rotation matrix for the angle around the y-axis
    let angleRotationMatrix =
        DenseMatrix.OfArray(
            array2D [|
                [| Math.Cos(angleRad); 0.0; Math.Sin(angleRad) |]
                [| 0.0; 1.0; 0.0 |]
                [| -Math.Sin(angleRad); 0.0; Math.Cos(angleRad) |]
            |]
        )

    // Combine the initial rotation with the angle rotation
    let combinedRotationMatrix = rotationMatrix * angleRotationMatrix

    // Apply the rotation and distance to the position
    let positionVector = DenseVector.OfArray [| position.X; position.Y; position.Z |]
    let displacementVector = distance * combinedRotationMatrix.Column(0)
    let newPositionVector = positionVector + displacementVector

    // Convert the result back to a Position
    let newPosition = {
        X = newPositionVector.[0]
        Y = newPositionVector.[1]
        Z = newPositionVector.[2]
    }

    printfn
        "Position old/new: %A / %A    (angle: %A, distance: %A, rotation: %A)"
        position
        newPosition
        angle
        distance
        rotation

    newPosition

// This function, given a gate, will extract the position, and rotation defined in quaternion, and then
// return three new locations that are positioned around the gate, each at an offset of 120 degrees, and each
// 10000 meters away from the gate. This is used to determine where to place defense stations around the gate.
let getDefenseStationLocations (gate: Gate) (numberOfStations: int) (distanceFromGate: int) =
    let position = gate.Position
    let rotation = gate.Quarternion
    let angle = float (360 / numberOfStations)
    let angle_offset = float (360 / numberOfStations) / 2.0 // offset the angle by half the angle so that the first station is not right in front of the gate

    [
        for i in 0 .. numberOfStations - 1 ->
            calculateStationPosition position rotation (float (distanceFromGate)) (float (i) * angle + angle_offset), i
    ]


// using the gates from 'findUnsafeGates', this function will return the location of the defense stations
// for each gate, in a tuble that is 'race, gate, location'. It takes a single paramter that is "numberOfStations"
// which is the number of stations to place around each gate.
let getRequiredDefenseStationLocations numberOfStations distanceFromGate =
    findUnsafeGates
    |> List.collect (fun gate ->
        (getDefenseStationLocations gate numberOfStations distanceFromGate)
        |> List.map (fun (location, n) -> (gate, n, location)))


/// ======== Some debug dump functions to print out the gates we've found. =========

let printGates (gates: Gate list) =
    [|
        for gate in gates do
            printfn "%A (%A)" (gate.asString ()) (isGateConnectionSafe gate)
    |]
    |> ignore

    printfn "Total gates: %d" gates.Length


// dump some debut: print out all the gates found in the game.
let printGatesInZones () =
    // Start with all the gates
    allGates |> printGates

    // Now lets just print out the gates in our new faction zones
    let factionGates =
        allGates
        |> List.filter (fun gate -> gate.Faction <> "xenon" && gate.Faction <> "Unknown")

    factionGates |> printGates

    // And now the specific faction unsafe gates we want to defend:
    findUnsafeGates |> printGates
