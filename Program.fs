// For more information see https://aka.ms/fsharp-console-apps
open System.Xml.Linq
open FSharp.Data

[<Literal>]
let X4UnpackedDataFolder = __SOURCE_DIRECTORY__ + "/X4_unpacked_data"
[<Literal>]
let X4Core_WorldStartDataFile = X4UnpackedDataFolder + "/core/libraries/god.xml" // Core game data.

// The default GOD.xml file, etc, don't have an 'add/replace' XML section, so they can't
// be used as type providers for our output XML. So we've created a template that has
// versions of our types that we need (like station, product) as well as the XML selectors
// for replace/add
[<Literal>]
let X4GodModFile = __SOURCE_DIRECTORY__ + "/mod_templates/god.xml"


// The 'god.xml' file defines the starting state of the universe, in particular the starting
// unique stations and random factories for each faction that are scattered around the
// map. I'll call it 'world start' to make it a little clearer.
// There are two primary sets of data in this file we're interested in:
// 1. STATIONS : These are manually placed unique stations, shipyards, tradeposts and defense stations.
//  My understanding that it's the placement of these stations that will determine faction ownership
// of the sectors. Unlike 'products', they're given an explicit sector they spawn in, and sometimes
// a specific location in the sector. This is different from Product factories which are goverened by:
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

type X4WorldStart = XmlProvider<X4Core_WorldStartDataFile>
type X4GodMod = XmlProvider<X4GodModFile>

// DU that will allow us to build up a list of mod items that we'll convert to
// an XML diff file for output.  
type X4Mod =
    | Add     of selector: string * item:X4GodMod.Add
    | Replace of X4GodMod.Replace
    | Remove  of X4GodMod.Remove


let x4WorldStartData = X4WorldStart.Load(X4Core_WorldStartDataFile)
let X4GodModData = X4GodMod.Load(X4GodModFile)   // It's a type provider AND has some template data we want

// Extract the Xenon stations from the GodModTemplate. We'll use these as templates when we add new xenon stations
let XenonShipyard = Array.find (fun (elem:X4GodMod.Station) -> elem.Id = "shipyard_xenon_cluster") X4GodModData.Add.Stations
let XenonWharf = Array.find (fun (elem:X4GodMod.Station) -> elem.Id = "wharf_xenon_cluster") X4GodModData.Add.Stations
let XenonDefence = Array.find (fun (elem:X4GodMod.Station) -> elem.Id = "xen_defence_cluster") X4GodModData.Add.Stations

// the 'log' functions just extract a bit of data about a station, and log it
// to the terminal for debugging and tracking purposes.
let logStation (station:X4WorldStart.Station) =
    let locationClass= station.Location.Class |> Option.defaultValue "none"
    let locationMacro= station.Location.Macro |> Option.defaultValue "none"
    let stationType  = station.Type           |> Option.defaultValue "none"
    let stationMacro = station.Station.Macro  |> Option.defaultValue "none"
    let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
    printfn "PROCESSING STATION %s race: %s, owner: %s, type: %s, location: %s:%s, id: %s, station: %s   " tags station.Race station.Owner stationType locationClass locationMacro station.Id stationMacro

let logAddStation (station:X4GodMod.Station) =
    let locationClass= station.Location.Class
    let locationMacro= station.Location.Macro
    let stationType  = station.Type 
    let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
    let stationMacro = "none"
    printfn "   REPLACE STATION %s race: %s, owner: %s, type: %s, location: %s:%s, id: %s, station: %s   " tags station.Race station.Owner stationType locationClass locationMacro station.Id stationMacro

let logProduct (product:X4WorldStart.Product) =
    let prodModule = product.Module.Select
    let faction    = prodModule.Faction |> Option.defaultValue "none"
    let quotaSector      = product.Quota
    printfn "PROCESSING PRODUCT [%s:%s] %s/%s with quotas %i/%i" product.Owner product.Location.Faction product.Type product.Ware product.Quota.Galaxy (product.Quota.Sector |> Option.defaultValue -1)

// Given a station, process it according to our rules. We may replace it
// with a Xenon one, remove it, etc. This function is call once per station
let processStation (station:X4WorldStart.Station) =
    logStation station

    // So, turns out XmlProvider is more focused around reads. Writes are not... great.
    // To edit underlying fields, you really need to get to the underlying linq XElement
    // and then manipulate that. When trying to create a new XML Provider element based
    // off a copy of an old one, it will use a reference to the underlying XElement.
    // that means our edits will overwrite, sooo we need to clone the XElement each time.

    match station.Owner with
    | "khaak" | "xenon" | "yaki" | "scaleplate" | "buccaneers" | "player" ->
        // We're ignoring certain pirate bases for now like scaleplate and bucconeers. HAT bases we'll still replace below
        printfn "  IGNORING [%s]" station.Owner // These will still exist, and probably get wiped pretty quick, unless they're well hidden.
        (None, None)
    | _ ->
        // 'Select' contains the tags that describe whether this is a defence station, wharf or shipyard.
        let stationClone =
            match station.Station.Select, station.Type with
            | (None, Some "tradingstation") ->
                // These seem to be teladi tranding stations. Replace with something more... interesting
                Some (new XElement(XenonWharf.XElement))
            | (None, _) ->
                // Any other example in the logs without a tag all seem to be scenarios we weren't going to replace anyway. Khaak, xenon, etc.
                None
            | (Some tag, _) ->
                // create the new xelement clone so we can edit it later as part of the replacement station.
                // We're going to replace different types of NPC buildings with different Xenon stations.
                match tag.Tags with
                | "[shipyard]" ->
                    Some (new XElement(XenonShipyard.XElement))
                | "[wharf]" | "[equipmentdock]" -> 
                    Some (new XElement(XenonWharf.XElement))
                | "[defence]" | "[tradestation]" | "[piratebase]" ->
                    Some (new XElement(XenonDefence.XElement)) // For now, replace HAT piratebase with xenon defense.
                | x ->
                    printfn "UNHANDLED STATION TYPE: %s - DEFAULTING TO XENON DEFENCE" x
                    Some (new XElement(XenonDefence.XElement))

        match stationClone with
        | None -> (None, None)
        | Some stationClone ->
            let replacement = new X4GodMod.Station(stationClone)
            replacement.XElement.SetAttributeValue(XName.Get("id"), replacement.Id + "_" + station.Id)   // Give it a new unique ID
            // update location. As they're different types (as far as the type provider is concerned), we have to manually set
            // the important zone and macro fields.
            let locationClass= match station.Location.Class with | Some x -> x | None -> "none"
            let locationMacro= match station.Location.Macro with | Some x -> x | None -> "none"
            replacement.Location.XElement.SetAttributeValue(XName.Get("class"), locationClass)
            replacement.Location.XElement.SetAttributeValue(XName.Get("macro"), locationMacro)

            logAddStation replacement

            // Todo: Handle station REMOVE
            let remove = None
            (Some replacement, remove)  // return an add/remove options,

let processProduct (product:X4WorldStart.Product) =
    logProduct product
    (Some product, None)

let godModStations = 
    [| for station in x4WorldStartData.Stations.Stations do
            let (add, remove) = processStation station
            match add    with Some station -> yield station | _ -> ()
            match remove with Some station -> yield station | _ -> ()
    |]

let godModProducts = 
    [| for product in x4WorldStartData.Products do
            let (add, remove) = processProduct product
            match add    with Some product -> yield product | _ -> ()
            match remove with Some product -> yield product | _ -> ()
    |]

