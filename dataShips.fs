/// <summary>
/// The ships and equipment side of the data layer: loads every ship hull (with its
/// parsed equipment slots) and every piece of ship equipment from the asset files,
/// exposing them as the pure ShipInfo/EquipmentInfo records.
/// (Phase G of the refactor will also move the WRITING of placedobjects.xml and
/// loadouts.xml here.)
/// </summary>
[<AutoOpen>]
module X4.Data.ShipData

open System
open System.IO
open System.Xml.Linq
open X4.Types
open X4.Utilities
open X4.Data.Xml

// Get all the assets defined in the core game and the DLCs. This includes
// equipment for ships, as well as miscellaneous assets like wares, adsigns, etc
let (allAssets: Asset list) =
    // For ship equipment, it's not enough to look at the AllComponentMacros.
    // This is because the component file doesn't contain all items. Instead, the index macro
    // defines all items, and them points to a reference in the component file that might be shared.
    // eg: Boron small shields. Index contains all shield entries, but each points to the same shared
    // component entry of
    // <entry name="shield_bor_s_standard_01" value="extensions\ego_dlc_boron\assets\props\surfaceelements\shield_bor_s_standard_01" />

    // So, for each entry in the index file, filter down to just those that refer
    // to files in the X4EquipmentDirectory. These entries point to a specific component
    // file, which we need to load and parse.
    // In the macro file, we'll need to find the reference to the component
    // We use this component reference to look up the actual component file in the component index.
    AllIndexMacros
    |> Array.filter (fun index -> index.File.ToLower().Contains("assets/"))
    |> Array.map (fun index ->
        printfn "Loading equipment: %s" index.File
        // Load the referenced file.
        let fileName = X4UnpackedDataFolder + "/" + index.File.Replace("\\", "/")

        if File.Exists fileName then
            Some(index, X4ShipsMacro.Load fileName)
        else
            printfn "Warning: Component file %s not found." fileName
            None)
    |> Array.choose id
    |> Array.toList
    |> List.map (fun (index, macro) ->
        option {
            // We have the macro loaded, so now find the component the macro references,
            // and look it up in the components index, and THEN finally load that component.
            let! componentEntry =
                AllComponentMacros
                |> Array.tryFind (fun componentEntry -> componentEntry.Name =? macro.Macro.Component.Ref)

            let componentFilename =
                X4UnpackedDataFolder + "/" + componentEntry.File.Replace("\\", "/")

            printfn "Found component %s -> %s" componentEntry.Name componentFilename
            return index, macro.Macro.Name, componentFilename
        }

    )
    |> List.choose id
    |> List.map (fun (index, name, componentFilename) ->
        try
            // Now parse the file using the X4Equipment type.
            let parsed = X4Equipment.Load(componentFilename)
            // Ensure the name is set correctly in the XElement. Should be index, not component name
            // parsed.Component.XElement.SetAttributeValue("name", name.Trim())
            // printfn "Loaded equipment: %-35s %-20s from %s" parsed.Component.Name parsed.Component.Class x
            Some {
                Name = name.Trim() // Ensure the name is set correctly. Should be index, not component name
                File = componentFilename
                DLC = index.DLC
                Class = parsed.Component.Class.Trim()
                Asset = parsed.Component
            }
        with ex ->
            printfn $"\nError loading equipment: {componentFilename}: {ex.Message}"
            // try find root of parse error:
            let raw = System.Xml.Linq.XDocument.Load(componentFilename)
            printfn "Children of <components>:"
            raw.Root.Elements() |> Seq.iter (fun e -> printfn "- %s" e.Name.LocalName)
            printfn "End of children.\n"

            None)
    |> List.choose id


// Some tags are not relevant for selection/slot match
let tagsToIgnore =
    set [
        "component"
        "symmetry"
        "symmetry_1"
        "symmetry_2"
        "symmetry_3"
        "symmetry_right"
        "symmetry_left"
        "platformcollision"
        "mandatory"
        "notupgradeable"
        "envmap_cockpit"
    // the following are important after all
    // Seem to be used to identify things line internal shield generators vs external.
    // eg: medium shields can be external for components, or internal for M ships

    //"hittable"
    //"unhittable"
    ]

let componentSizeClasses = set [ "small"; "medium"; "large"; "extralarge" ] // I really wish there was some kind of consistency when it comes to referring to sizes.

let shipEquipmentClasses = [
    // Allow us to filter down all assets to the ship equipment we're interested in.
    // Ship mounted equipment
    "engine"
    "shieldgenerator"
    "weapon"
    "missileturret"
    "missilelauncher"
    "turret"
    // Ship deployables.
    "missile"
    "resource_probe"
    "satellite"
]

let shipEquipmentConnectionTags =
    set [ "weapon"; "turret"; "shield"; "engine"; "thruster" ]


// Checks to see if the ship connection is an equipment slot connection,
// and if so, parses it to return the relevant information about the equipment slot.
let parseConnectionForEquipmentSlot (connection: X4Ships.Connection) =
    // We determine whether the connection is an equipment slot by checking the tags.
    // If the tags contains one of the special equipment slot tags defined in
    // ShipEquipmentConnectionTag, then it's an equipment slot.

    // split the tag string in to a set of tags, and trim excess whitespace
    let tags = Set.difference (tagStringToSet connection.Tags) tagsToIgnore

    // find the first tag that matches one of the equipment slot tags. ie, the element, if any,
    // that appears both in the tags set and in the ShipEquipmentConnectionTag set.
    let equipmentTag = Set.intersect tags shipEquipmentConnectionTags |> Seq.tryHead

    match equipmentTag with
    | None -> None // Not an equipment slot, so return None
    | Some tag ->
        Some {
            Name = connection.Name.Trim()
            Class = tag
            Size =
                Set.intersect tags componentSizeClasses
                |> Seq.tryHead
                |> Option.defaultValue "unknown"
            Group =
                connection.Group
                |> Option.map (fun group -> group.Trim())
                |> Option.filter (fun group -> group.Length > 0)
            Tags = tags
        }


// Given a ship identified by 'index', and it's data in 'macro', this function will look up which
// file contains it's component information, then load and process the data contained therein.
// Returns a 'ShipInfo' record containing the detailed information on tghe ship, along with what
// constitutes a valid loadout.
let LoadShipComponents (entry: Index) (macro: X4ShipsMacro.Macros) =
    let componentEntry =
        Array.Find(AllComponentMacros, (fun componentEntry -> componentEntry.Name =? macro.Macro.Component.Ref))

    let componentFilename =
        X4UnpackedDataFolder + "/" + componentEntry.File.Replace("\\", "/")

    // Lets load the compoenent file and parse it.
    try
        let parsed = X4Ships.Load(componentFilename)
        let name = parsed.Component.Name
        let size = parsed.Component.Class
        let connections = parsed.Component.Connections

        // Not all macro files include the 'type' property, so we need to check if it exists.
        let shiptype =
            try
                macro.Macro.Properties.Ship.Type
            with _ex ->
                "unknown"

        let thruster =
            try
                macro.Macro.Properties.Thruster.Tags
            with _ex ->
                "unknown"

        // printfn "Loaded ship: %-35s %-35s from %s" name macro.Macro.Component.Ref componentFilename

        Some {
            Name = name
            Size = size
            Type = shiptype
            Thruster = thruster
            DLC = entry.DLC
            MacroName = entry.Name
            ComponentRef = macro.Macro.Component.Ref
            ComponentFile = componentFilename
            EquipmentSlots = connections |> Array.choose parseConnectionForEquipmentSlot |> Array.toList
        }
    with ex ->
        printfn $"Error loading ship:  {componentFilename}: {ex.Message}"
        None

// Pull all the information about all the ships in the game by filtering down to the ship macros in the index macros,
// then loading those files; finding the name of relevant ship component reference, then using that reference to find
// the component file from the component index to get the file that contains the actual ship definition we're interested in.
// This will replace the 'allShipMacros' function.
let allShips =
    // 1. Get all the ship macros from the index macros, and filter them to only those that start with "ship_"
    AllIndexMacros
    |> Array.filter (fun entry -> entry.Name.StartsWith "ship_")
    |> Array.filter (fun entry -> not (entry.Name.Contains "_xs_")) // we don't wan't xs ships - plus they have no thruster entry, breaking our template file parser.
    // 2. For each ship macro, find the file it points to, and load it.
    // Ships are actually defined in two files. the macro file, and the 'component' file.
    // The macro file contains the ship definition, and the component file contains the ship component definition
    |> Array.choose (fun entry ->
        try
            // Get the file name from the entry, and load it.
            let fileName = X4UnpackedDataFolder + "/" + entry.File

            if File.Exists fileName then
                Some(entry, X4ShipsMacro.Load fileName)
            else
                printfn "Warning: Ship macro file %s not found." fileName
                None
        with ex ->
            printfn $"Error loading ship macro: {entry.Name}: {ex.Message}"
            None)
    // 3. From the loaded macro file, pull out the reference to the ship asset/component file,
    // and look up the ship asset file in the component index.
    |> Array.choose (fun (entry, macro) -> LoadShipComponents entry macro)
    |> Array.toList
    |> List.map (fun ship ->
        // Boron ships are only allowed to equip boron components in their equipment slots.
        // So, for any ship with the boron DLC, add a unique 'boron' tag to it's equipment slot tags.
        // we do the same for the boron ship slots when loading those.
        match ship.DLC with
        | "ego_dlc_boron" ->
            let slots =
                ship.EquipmentSlots
                |> List.map (fun slot -> {
                    slot with
                        Tags = slot.Tags |> Set.add "boron"
                })

            { ship with EquipmentSlots = slots }

        // Same for terran in order to try resolve issues with ATF L/XL loadouts not being able to be repaired/modified.
        | "ego_dlc_terran" ->
            let slots =
                ship.EquipmentSlots
                |> List.map (fun slot -> {
                    slot with
                        Tags = slot.Tags |> Set.add "terran"
                })

            { ship with EquipmentSlots = slots }

        | _ -> ship)

// Get all the assets, and filter them down to only the classes that are ship
// equipment we need to generation ship loadouts.
// Convert the xmln asset to a simplified ShipEquipment type, which gives us easy access
//  to the name, macro, class, tags, size and component connection.
let allShipEquipment =
    // There are some assets that are not valid for loadouts, even if their tags match.
    let assetsToIgnore = [
        "weapon_gen_lasertower_01_mk2"
        "weapon_gen_lasertower_01_mk1"
        "shield_arg_s_combattutorial_01_mk1"
        "_virtual_"
        "_story_" // some equipment showing up as 'story'. I assume it's special, so avoid using it.
        "_xen_"
        "_kha_"
        "generic_"
    ]

    // Find out all the different unique classes of assests
    allAssets
    |> List.filter (fun asset -> shipEquipmentClasses |> List.contains asset.Class)
    |> List.filter (fun asset -> not (assetsToIgnore |> List.exists (fun ignore -> asset.Name.Contains ignore)))
    |> List.map (fun asset ->
        option {
            // Find the connection in the assets list of connections that has 'compononent' in its tags.
            // let! will early return if the result is None here. ie, the asset has no component connections.
            let! componentConnection =
                asset.Asset.Connections
                |> Array.tryFind (fun connection -> tagStringToList connection.Tags |> List.contains "component")

            // Parse the tags string, stripping out  the tags we want to ignore.
            let tags = Set.difference (tagStringToSet componentConnection.Tags) tagsToIgnore

            let size =
                // one of the tags is the size class, so we try find any of the valid size tags in the tag list.
                tags
                |> Set.intersect componentSizeClasses
                |> Seq.tryHead
                |> Option.defaultValue "none"

            // If it's a boron or terran ship, add a tag to help with loadout generation.
            let tags =
                match asset.DLC with
                | "ego_dlc_boron" -> tags.Add "boron"
                | "ego_dlc_terran" -> tags.Add "terran"
                | _ -> tags

            return {
                Name = asset.Name
                MacroName = asset.Name // Asset name is the macro. Need to clean up name field later.
                Class = asset.Class
                Tags = tags
                Size = size
                ComponentName = componentConnection.Name
            }
        })
    |> List.choose id


// ==== WRITER ====
// placedobjects.xml and loadouts.xml are produced here from the pure AbandonedShip
// directives decided by the ships logic. The string templates are inherited verbatim
// from the original code, parsed with XmlTextReader to preserve whitespace exactly.

// Generate a section of XML with a wrapper tag. used for groups and macros.
// Could use XML classes, but loadouts are so simple that string manipulation is just easier.
let private generateLoadoutSection sectionTag (lines: string list) =
    $"""
            <{sectionTag}>
                {lines |> String.concat "\n                "}
            </{sectionTag}>"""

let private generateSoftwareXml () =
    $"""
            <software>
                <software ware="software_dockmk2"/>
                <software ware="software_flightassistmk1"/>
                <software ware="software_scannerlongrangemk2"/>
                <software ware="software_scannerobjectmk1"/>
                <software ware="software_targetmk1"/>
            </software>"""

let private generateThrusterXml (thrusterMacro: string) =
    $"""
            <virtualmacros>
                <thruster macro="{thrusterMacro}"/>
            </virtualmacros>"""

let private generateGroupLine (line: LoadoutGroupLine) =
    let exactAttr =
        match line.Exact with
        | Some count -> $" exact=\"{count}\""
        | None -> ""

    $"""<{line.TagName} macro="{line.Macro}" path=".." group="{line.Group}"{exactAttr}/>"""

let private generateMacroLine (line: LoadoutMacroLine) =
    $"""<{line.Class} macro="{line.Macro}" path="../{line.SlotName}"/>"""

// The complete <add> operation defining one custom loadout in loadouts.xml.
let private loadoutElementXml (loadout: ShipLoadout) =
    let macrosSection =
        loadout.Macros |> List.map generateMacroLine |> generateLoadoutSection "macros"

    let groupsSection =
        loadout.Groups |> List.map generateGroupLine |> generateLoadoutSection "groups"

    let xml =
        $"""
        <add sel="/loadouts">
            <loadout id="{loadout.Id}" macro="{loadout.ShipMacro}">
                {macrosSection}
                {groupsSection}
                {generateThrusterXml loadout.ThrusterMacro}
                {generateSoftwareXml ()}
            </loadout>
        </add>
        """

    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    XElement.Load(new System.Xml.XmlTextReader(new System.IO.StringReader(xml)))

// The placedobjects.xml <add> operation that spawns one abandoned ship.
let private abandonedShipXml (ship: AbandonedShip) =
    let x, y, z = ship.PositionKm
    let yaw, pitch, roll = ship.RotationDeg

    // If the ship has a custom loadout, the placed object references it by id; the
    // loadout itself is written separately to the loadouts file.
    let loadoutReference =
        match ship.Loadout with
        | Some loadout -> $"""<loadout ref="{loadout.Id}" />"""
        | None -> ""

    let xml =
        $"""
    <add sel="/mdscript[@name='PlacedObjects']/cues/cue[@name='Place_Claimable_Ships']/actions">
        <find_sector name="$sector" macro="macro.{ship.Sector}"/>
        <do_if value="$sector.exists">
          <create_ship name="$ship" macro="macro.{ship.Macro}" sector="$sector">
            <owner exact="faction.ownerless"/>
            <position x="{x}km" y="{y}km" z="{z}km"/>
            <rotation yaw="{yaw}deg" pitch="{pitch}deg" roll="{roll}deg"/>
            {loadoutReference}
          </create_ship>
        </do_if>
    </add>
    """

    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    XElement.Load(new System.Xml.XmlTextReader(new System.IO.StringReader(xml)))

// Write placedobjects.xml and loadouts.xml from the abandoned ship directives.
let writeAbandonedShips (placedObjectsFilename: string) (loadoutFilename: string) (ships: AbandonedShip list) =
    // Create the new XML Diff documents to contain our loadouts and placed ships
    let xmlTemplate =
        XElement.Parse(
            """<?xml version="1.0" encoding="utf-8"?>
        <diff xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" >
        </diff>"""
        )

    let placedXML = XElement(xmlTemplate)
    let loadoutXML = XElement(xmlTemplate)

    let addElementsToDiff (diff: XElement) (elements: XElement list) =
        elements
        |> List.iter (fun element ->
            diff.Add(element)
            diff.Add(new XText("\n")) // Add a newline after each element so the output is readible
        )

    // Now add the abandoned ships, one by one, to the the xml diff.
    addElementsToDiff placedXML (ships |> List.map abandonedShipXml)
    addElementsToDiff loadoutXML (ships |> List.choose (fun ship -> ship.Loadout) |> List.map loadoutElementXml)

    X4.WriteModfiles.write_xml_file "core" placedObjectsFilename placedXML
    X4.WriteModfiles.write_xml_file "core" loadoutFilename loadoutXML
