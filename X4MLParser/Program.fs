// For more information see https://aka.ms/fsharp-console-apps

open FSharp.Data

[<Literal>]
let X4UnpackedFolder = "g:/development/X4 modding/X4 Foundations/unpacked/"
[<Literal>]
let X4WorldStartDataFile = X4UnpackedFolder + "libraries/god.xml"

// Technically, this is all derived from the 'god.xml' file, but it reflects the starting
// unique stations and random factories for each faction that are scattered around the
// map. I'll call it 'world start' to make it a little clearer.
// There are two primary sets of data in this file we're interested in:
// 1. STATIONS : These are manually placed unique stations, shipyards, tradeposts and defense stations.
//  My understanding that it's the placement of these stations that will determine faction ownership
// of the sectors. Unlike 'products', they're given an explicit sector they spawn in, and sometimes
// a specific location in the sector. Product factories do not.
// 2. PRODUCTS: This defines the production factories in the universe. Unlike STATIONS, they are
// given a descriptor that describes the product they will produce, the faction they belong to,
// and the maximum that can exist for the faction in the galaxy. Then the game randomly creates
// and scatters these stations around the galaxy.
//
// For the purpose of this mod, we're going to REMOVE almost all the faction defence stations, 
// and REPLACE them with Xenon ones instead. Kh'aak will remain unaffected.
//
// We'll manually assign a sector or two to each faction by adding a defence stations, shipyard, wharf
// and add a few resources to those sectors so the factions can survive.
// PRODUCT factories will be updated to have a lot less stations to weaken the faction and also ensure
// their remaing sectors aren't overloaded with factories.
// Lastly, we'll manually position a very powerful defense station in front of the gate to protect the
// sector and help avoid the Xenon just running all over the weaker faction. They should be stopped by
// the station, giving a safe zone for the player, and a place to collect loot/get some action.
//
// Later, I may add a few more abandoned destroyers/carriers and smaller ships around the galaxy
// for the player to find.

type X4WorldStart = XmlProvider<X4WorldStartDataFile>
let x4WorldStartData = X4WorldStart.Load(X4WorldStartDataFile)

x4WorldStartData.Stations.Stations 
    |> Array.map( 
        fun station -> 
            let location     = match station.Location.Macro with | Some x -> x | None -> "none"
            let stationType  = match station.Type           with | Some x -> x | None -> "none"
            let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
            let stationMacro = match station.Station.Macro  with | Some x -> x | None -> "none"

            printfn "%s race: %s, owner: %s, type: %s, location: %s, id: %s, station: %s   " tags station.Race station.Owner stationType location station.Id stationMacro
        ) 
    |> ignore

// TODO: Create an example XML mod file so we can use it as an XML type provider.
