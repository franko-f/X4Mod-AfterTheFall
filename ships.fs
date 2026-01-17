/// <summary>
/// Functions to handle processing and manipulation of ship data, and generation of ship related mod content.
/// Builds on data loaded in X4.Data to convert to our internal 'shipInfo' and 'shipequipment' types.
/// </summary>

module X4.Ships

open System
open System.IO
open System.Xml
open System.Xml.Linq

open X4.Data
open X4.Utilities

let rand = new Random(12345) // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

// Define a few types for ship location that we'll use when we place an abandoned ship
type Position = int * int * int
type Rotation = int * int * int
type ShipLocation = string * string * Position * Rotation // Ship name, sector name, position, rotation. Should probably use a record type here.

type ShipEquipmentSlot = {
    Name: String
    Class: String
    Size: String
    Group: Option<String>
    Tags: String Set
}

type ShipInfo = {
    Name: String
    MacroName: String
    Size: String
    DLC: String
    Type: String
    Thruster: String
    ComponentRef: String
    ComponentFile: String
    Macro: X4ShipsMacro.Macro
    Connections: X4Ships.Connection array
    EquipmentSlots: ShipEquipmentSlot list
}

// Extracts the groups from a list of ship equipment slots.
let shipEquipmentGroups (allSlots: ShipEquipmentSlot list) =
    allSlots
    |> List.choose (fun slot -> slot.Group |> Option.map (fun group -> ((group, slot.Class), slot))) // Filter out anything without a group
    |> List.groupBy fst // New list grouped by group and class of item.
    |> List.map (fun (groupBy, slots) -> (groupBy, List.map snd slots)) // At this point our 'slots' is actually (group,slots) list, due to our previous processing. Reduce down to slots again
    |> List.filter (fun ((_, className), _) -> className <> "engine") // While engines are listed with a group for L ships, they should always be specified in the macros section.
    |> List.sortBy fst

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
            Macro = macro.Macro
            ComponentRef = macro.Macro.Component.Ref
            ComponentFile = componentFilename
            Connections = connections
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


let findShipByName (shipName: string) =
    // Find a ship by its name, case insensitive.
    allShips |> List.tryFind (fun ship -> ship.Name =? shipName)

let findShipByMacroName (macroName: string) =
    // Find a ship by its macro name, case insensitive.
    allShips |> List.tryFind (fun ship -> ship.MacroName =? macroName)

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
    X4.Data.allAssets
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
                ComponentConnection = componentConnection
                Connections = asset.Asset.Connections
            }
        })
    |> List.choose id


// Searches though all ship equipment for the items that 'match' the given tags.
// In most cases, the all tags on the equpment MUST also match the slot's tags.
// The exception to this rule is the 'one of' tags, which can be used to permit
// a range of different 'use cases', like 'mining' or 'combat'.
let findMatchingEquipmentForTagsImpl (slotTags: Set<String>) =
    // This subset of tags basically give a 'class' to the components. And some slots can handle
    // more than one class of component. eg, some miners can mount both mining and combat turrets.
    // Some ships can mount both 'missile' or 'combat' weapons, and so on.
    // If the equipment item has one of these tags, then the *slot* must have them too.
    let OneOfTags =
        set [ "standard"; "highpower"; "missile"; "mining"; "combat"; "advanced" ] // Advanced is used by Boron and Terran


    // Now filter through all of the ship equipment, finding those that have the oneOfTags,
    // and also match on all the rest of the tags.
    // This logic really needs to be cleaned up further, though at least it now works.
    allShipEquipment
    |> List.filter (fun equipment ->
        // First check: does the equipment have any 'special' tags that match the valid ones for this slot?
        let equipmentOneOfTags = Set.intersect equipment.Tags OneOfTags
        // Find the 'oneOfTags' that are present in the slot's tags.
        let slotOneOfTags = Set.intersect slotTags OneOfTags
        // And the equipment tags that match any 'one of' tags in the slot.
        let eAny = Set.intersect equipment.Tags slotOneOfTags

        if Set.isEmpty equipmentOneOfTags && Set.isEmpty slotOneOfTags then
            // If neither have any of the 'one of' tags, then just check for exact matches
            equipment.Tags = slotTags
        elif not (equipmentOneOfTags.IsSubsetOf slotOneOfTags) then
            // If the equipment item has one of these tags, then the *slot* must have them too.
            // e.g. If slot allows ['standard', 'combat'], and equipment has ['standard', 'mining'], it should fail because 'mining' is not allowed.
            false
        elif not (Set.isEmpty eAny) then
            // If the equipment tags has a subset of the slot 'any of' tags
            // e.g, equipment is 'mining', slot is ['mining'; 'combat'],
            // then we check to see if the remaining tags match.
            let eRemainingTags = Set.difference equipment.Tags OneOfTags
            let sRemainingTags = Set.difference slotTags OneOfTags
            sRemainingTags = eRemainingTags
        else
            // If we reach here, it means one of them has 'one of' tags and the other doesn't.
            //This means that it's not a match.
            false)

let findMatchingEquipmentForTags (tags: Set<String>) =
    // Terran L turrets are flagged with 'hittable', but there are no L turrets with 'hittable'.
    // So if there are no matching tags, remove 'hittable' from tags and try again.
    // In other cases, 'hittable' is important, as there might be two versions of M turrets, for example.
    // One for M ships, where they are not hittable, and one for L ships.
    match findMatchingEquipmentForTagsImpl tags with
    | [] when tags.Contains "hittable" -> findMatchingEquipmentForTagsImpl (Set.remove "hittable" tags)
    | matches -> matches


let findMatchingEquipmentForSlot (slot: ShipEquipmentSlot) = findMatchingEquipmentForTags slot.Tags

// Find any asset that includes the specified search tags.
// Useful for debugging rather than assigning equipment, as it ignores
// the subtleties relating to matching equipmenmt to slots.
let findMatchingEquipment (searchTags: Set<String>) (equipment: list<EquipmentInfo>) =
    // Find an asset by its name, case insensitive.
    equipment
    // Filter down to only those that match the tags.
    |> List.filter (fun equipment ->
        // Check if any of the connections have the tags we're looking for.
        searchTags.IsSubsetOf equipment.Tags)


// Helper to find matching equipment for a set of tags and pick a random one.
let pickEquipment tags : EquipmentInfo =
    // Helper to pick a random item from a list. Assumes list is not empty.
    let pickRandom (items: 'a list) =
        // printfn "pickRandom: items.Length = %d" items.Length
        items.[rand.Next(items.Length)]

    // printfn "pickEquipment: tags = %A" tags
    findMatchingEquipmentForTags tags |> pickRandom

let dumpEquipmentInfo (prefix: string) (info: EquipmentInfo) =
    let tags =
        info.Tags |> Set.toList |> List.map (fun tag -> tag.Trim()) |> String.concat " "

    printfn "%s%45s %-15s %-10s %-22s | %s" prefix info.Name info.Class info.Size info.ComponentName tags

let dumpAllShipEquipment () =
    printfn "\nAll Equipment:"
    allShipEquipment |> List.iter (dumpEquipmentInfo "")


let printShipInfo (ship: ShipInfo) =
    let formatShipSlot (slot: ShipEquipmentSlot) =
        sprintf
            "  %-30s %-10s %-10s %-25s | %s"
            slot.Name
            slot.Class
            slot.Size
            (Option.defaultValue "---" slot.Group)
            (slot.Tags |> Seq.map (fun tag -> tag.Trim()) |> String.concat " ")

    // Print the ship info in a nice format.
    printfn "\n Ship: %s" ship.Name
    // ship.Connections
    // |> Seq.iter (fun connection -> printfn "  Connection %-50s/%-20s / %s" connection.Name (Option.defaultValue "" connection.Group ) connection.Tags)
    // printfn " Discovered Equipment Slots:"
    ship.EquipmentSlots
    |> Seq.iter (fun slot ->
        printfn "%s" (formatShipSlot slot)
        // Now find, and print out valid equipment for this slot.
        slot |> findMatchingEquipmentForSlot |> List.iter (dumpEquipmentInfo "    - "))

    printfn "  Equipmment Groups"

    ship.EquipmentSlots
    |> shipEquipmentGroups
    |> Seq.iter (fun ((group, slotClass), slots) ->
        printfn "  Group: %-25s x%d  %s " group slots.Length (formatShipSlot slots[0]))


let dumpShips () =
    printfn "All Ship Macros:"

    for ship in allShips do
        printfn "macro: %s," (ship.MacroName)




// List of all ships that are valid candidates for being generated as abandoned ships.
let abandonedShipsList =
    // Our filters. Factions we'll look for, and tags we'll omit. Both must be true.
    let factions = [
        "_arg_"
        "_par_"
        "_tel_"
        "_spl_"
        "_ter_"
        "_atf_"
        "_yak_"
        "_pir_"
        "_bor_"
    ]

    let omit = [
        "_xs_"
        "_plot_"
        "_landmark_"
        "_story"
        "_highcapacity_"
        "ship_spl_xl_battleship_01_a_macro"
        "ship_pir_xl_battleship_01_a_macro" // specific ships that seems unsupported or don't exist in game
    // Some terran destroyers that are spawning without main guns. <<should be fixed now with custom loadouts>>
    //"ship_atf_l_destroyer_01_a_macro"
    //"ship_atf_xl_battleship_01_a_macro"
    //"ship_ter_l_destroyer_01_a_macro"
    ]

    allShips
    |> List.map (fun ship -> ship.MacroName)
    |> List.filter (fun shipName ->
        (factions |> List.exists (fun tag -> shipName.Contains tag)) // Must contain one of the factions we're interested in.
        && (not (omit |> List.exists (fun tag -> shipName.Contains tag)))) // Must not contain any of the tags we're not interested in from the omit list.


// Quickly filter by a search string substring. eg, 'bor' will return all the Boron ships by filtering for '_bor_'.
let rec filterListBy (searchTags: string list) (ships: string list) =
    match searchTags with
    | [] -> ships
    | h :: t ->
        ships
        |> List.filter (fun x -> x.Contains("_" + h.ToLower() + "_"))
        |> filterListBy t

// Filter the abandoned ships list by a list of search tags. eg, ['bor', 's'] will return all Boron small ships.
// ['tel', 'xl', 'carrier'] will return all Teladi extra large carriers.
let filterBy (search: string list) = filterListBy search abandonedShipsList

let militaryShips =
    List.concat [
        filterBy [ "corvette" ]
        filterBy [ "gunboat" ]
        filterBy [ "frigate" ]
        filterBy [ "destroyer" ]
        filterBy [ "battleship" ]
        filterBy [ "carrier" ]
        filterBy [ "resupplier" ]
        filterBy [ "fighter" ]
        filterBy [ "heavyfighter" ]
        filterBy [ "bomber" ]
        filterBy [ "scout" ]
    ]

let economyShips =
    List.concat [ filterBy [ "miner" ]; filterBy [ "builder" ]; filterBy [ "trans" ] ]


// given a sector and a list of possible ships, select one of the ships,
// and assign it coordinates in the given sector.
let generateRandomAbandonedShipFromListInSector (sector: string) (shipList: string list) : ShipLocation =
    let ship = shipList.[rand.Next(shipList.Length)]
    // generate random coordinates within the sector, in KM offset from sector center (different from other coordinates)
    let x, y, z = rand.Next(-160, 160), rand.Next(-10, 10), rand.Next(-180, 180)
    // generate random yaw and pitch
    let yaw, pitch, roll =
        rand.Next(-180, 180), rand.Next(-180, 180), rand.Next(-180, 180)

    (ship, sector, (x, y, z), (yaw, pitch, roll))

// given a list of possible ships, select one, and place it randomly in any of the
// unsafe sectors in the game.
let generateRandomAbandonedShipFromList (shipList: string list) =
    let sector = X4.Data.selectRandomUnsafeSector () // We don't want these wrecks to be in the faction sectors.
    generateRandomAbandonedShipFromListInSector sector.Name shipList

// Generate COUNT random abandoned military ships of the given size in a random unsafe sector.
// 'size' is one of 'XL', 'L', 'm', 's'. - the standard X4 ship size classes.
let generateRandomMilitaryAbandonedShips (count: int) (size: string) =
    let ships = filterListBy [ size ] militaryShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// As above, but for economy ships.
let generateRandomEconomyAbandonedShips (count: int) (size: string) =
    let ships = filterListBy [ size ] economyShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// This function generates a bunch of abandoned ships near each other, as if a major battle occurred.
// The parameters determine how many of each class are in the field. Ships are clustered within 5km of a point
// in a random unsafe sector.
let generateBattlefield (countXL: int) (countL: int) (countM: int) (countS: int) =
    printfn "GENERATING BATTLEFIELD: XL: %i, L: %i, M: %i, S: %i" countXL countL countM countS
    // First generate the ships for each class.
    let xl, l, m, s =
        generateRandomMilitaryAbandonedShips countXL "xl",
        generateRandomMilitaryAbandonedShips countL "l",
        generateRandomMilitaryAbandonedShips countM "m",
        generateRandomMilitaryAbandonedShips countS "s"

    // Then we update the location of each ship to be within 5km of the location of the first ship.
    // First find the location of the first ship. We concat all the size classes, as we don't if any
    // size classes were empty for this battlefield.
    let _ship, sector, (x, y, z), _rotation = (List.concat [ xl; l; m; s ]).[0]
    // Now update every ships sector and location to be near the first ship, keeping other data the same.
    List.concat [
        [
            for (ship, _sector, _, rotation) in xl ->
                (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)), rotation)
        ]
        [
            for (ship, _sector, _, rotation) in l ->
                (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)), rotation)
        ]
        [
            for (ship, _sector, _, rotation) in m ->
                (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)), rotation)
        ]
        [
            for (ship, _sector, _, rotation) in s ->
                (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)), rotation)
        ]
    ]


// Some ships have custom loadouts, so we need a unique ID for them. Used by ProcessShip.
let loadoutUniqueId = makeIdGenerator ()

// Generate a section of XML with a wrapper tag. used for groups and macros.
// Could use XML classes, but loadouts are so simple that string manipulation is just easier.
let generateLoadoutSection sectionTag (lines: string list) =
    $"""
            <{sectionTag}>
                {lines |> String.concat "\n                "}
            </{sectionTag}>"""

let generateSoftwareXml () =
    $"""
            <software>
                <software ware="software_dockmk2"/>
                <software ware="software_flightassistmk1"/>
                <software ware="software_scannerlongrangemk2"/>
                <software ware="software_scannerobjectmk1"/>
                <software ware="software_targetmk1"/>
            </software>"""

// Selects a random thruster compatible with the ship's size and thruster tags.
let generateThrusterXml (ship: ShipInfo) =
    let thruster = pickEquipment (Set.ofList [ "thruster"; ship.Thruster ])

    $"""
            <virtualmacros>
                <thruster macro="{thruster.MacroName}"/>
            </virtualmacros>"""

let generateGroupLine groupName className count (equipment: EquipmentInfo) =
    let tagName =
        if className = "shieldgenerator" then
            "shields"
        else
            className + "s"

    let exactAttr = if count > 1 then $" exact=\"{count}\"" else ""
    $"""<{tagName} macro="{equipment.MacroName}" path=".." group="{groupName}"{exactAttr}/>"""

// Generates XML for grouped equipment (turrets, shields).
// Assumes all slots in a group have identical tags, so picks equipment based on the first slot.
let generateGroupsXml (ship: ShipInfo) =
    ship.EquipmentSlots
    |> shipEquipmentGroups
    |> List.sortBy (fun (groupName, _className) -> groupName)
    |> List.map (fun ((groupName, className), slots) ->
        // printfn "GROUP for %s: %s, %s, %A" ship.Name groupName className slots[0].Tags

        slots[0].Tags
        |> pickEquipment
        |> generateGroupLine groupName className slots.Length)
    |> generateLoadoutSection "groups"

let generateMacroLine className name (equipment: EquipmentInfo) =
    $"""<{className} macro="{equipment.MacroName}" path="../{name}"/>"""

let generateSynchronizedMacroLines (slots: ShipEquipmentSlot list) =
    match slots with
    | [] -> []
    | first :: _ ->
        let equipment = pickEquipment first.Tags

        slots
        |> List.sortBy (fun x -> x.Name)
        |> List.map (fun slot -> generateMacroLine slot.Class slot.Name equipment)

// Generates XML for ungrouped equipment (main guns, etc.). Picks equipment for each slot independently.
// EXCEPT for engines and shields - if there's more than one slot of them, ensure they're the same.
let generateMacrosXml (ship: ShipInfo) =
    let ungroupedSlots =
        ship.EquipmentSlots
        |> List.filter (fun slot -> slot.Group.IsNone || slot.Class = "engine")

    let engineSlots = ungroupedSlots |> List.filter (fun s -> s.Class = "engine")

    let shieldSlots = ungroupedSlots |> List.filter (fun s -> s.Class = "shield")

    let otherSlots =
        ungroupedSlots
        |> List.filter (fun s -> s.Class <> "engine" && s.Class <> "shield")

    let engineXmlLines = generateSynchronizedMacroLines engineSlots
    let shieldXmlLines = generateSynchronizedMacroLines shieldSlots

    let otherXmlLines =
        otherSlots
        |> List.sortBy (fun x -> x.Name)
        |> List.map (fun slot -> pickEquipment slot.Tags |> generateMacroLine slot.Class slot.Name)

    List.concat [ engineXmlLines; shieldXmlLines; otherXmlLines ]
    |> generateLoadoutSection "macros"

// Determines if a ship needs a custom loadout (Boron or Terran L/XL Military) and generates it.
// Loadouts have ammunition and crew sections too (and wares?). We're not setting those currently.
// see loadouts.xml in various DLC for examples
let MaybeCustomLoadout (shipName: string) =
    option {
        let! ship = findShipByMacroName shipName

        let isBoron = ship.MacroName.Contains "_bor_"
        let isTerran = ship.MacroName.Contains "_ter_" || ship.MacroName.Contains "_atf_"
        let isLargeOrXL = ship.Size = "ship_l" || ship.Size = "ship_xl"
        let isMilitary = militaryShips |> List.contains ship.MacroName

        // printfn "Ship: %s %s %b %b %b %b" shipName ship.Size isBoron isTerran isLargeOrXL isMilitary

        if isBoron || (isTerran && isLargeOrXL && isMilitary) then
            printfn "    Generating CustomLoadout for %s" shipName
            let id = $"eod_abandoned_ship_loadout_{loadoutUniqueId ()}"

            return
                (id,
                 $"""
        <add sel="/loadouts">
            <loadout id="{id}" macro="{ship.MacroName}">
                {generateMacrosXml ship}
                {generateGroupsXml ship}
                {generateThrusterXml ship}
                {generateSoftwareXml ()}
            </loadout>
        </add>
        """)
    }

// Generate the XML diff for placing an abandoned ship in the game based
// on the ship, sector, position and rotation given as parameters.
let ProcessShip ((ship, sector, (x, y, z), (yaw, pitch, roll)): ShipLocation) =
    // Interestingly, the units of KM and deg are specified in the XML attribute fields for abandoned ships.
    // I've not seen this elsewhere, and don't know if it's necessary, but for safety I'll duplicate it.
    printfn
        "GENERATING ABANDONED SHIP: %s, Sector: %s, Position: %A, Rotation: %A"
        ship
        sector
        (x, y, z)
        (yaw, pitch, roll)

    // If a custom loadout is needed for this ship, 'MaybeCustomLoadout' returns it's ID, and
    // the loadout XML itself. We refer to the loadout in the placedObject XML, and return the
    // loadout XML as a separate element to write out to the loadouts file.
    let loadoutReference, loadout =
        match MaybeCustomLoadout ship with
        | Some(id, xml) -> $"""<loadout ref="{id}" />""", Some xml
        | None -> "", None

    let xml =
        $"""
    <add sel="/mdscript[@name='PlacedObjects']/cues/cue[@name='Place_Claimable_Ships']/actions">
        <find_sector name="$sector" macro="macro.{sector}"/>
        <do_if value="$sector.exists">
          <create_ship name="$ship" macro="macro.{ship}" sector="$sector">
            <owner exact="faction.ownerless"/>
            <position x="{x}km" y="{y}km" z="{z}km"/>
            <rotation yaw="{yaw}deg" pitch="{pitch}deg" roll="{roll}deg"/>
            {loadoutReference}
          </create_ship>
        </do_if>
    </add>
    """

    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    let shipXML = XElement.Load(new XmlTextReader(new System.IO.StringReader(xml)))

    let loadoutXML =
        loadout
        |> Option.map (fun x -> XElement.Load(new XmlTextReader(new System.IO.StringReader(x))))

    shipXML, loadoutXML

// Create a list of random ships, assign them to random sectors, then generate XML that will place
// them as abandoned ships in the game.
// We don't want it completely random, as we want to make sure there's a good mix of ships in the game.
// We lean slighly towards generated economy ships vs military, though there's plenty of both.
// there should be, on average, one or two ships per sector.
let generate_abandoned_ships_file (placedObjectsFilename: string) (loadoutFilename: string) =
    let ships, loadouts =
        [

            // A bunch of ships in unsafe space to begin
            generateRandomMilitaryAbandonedShips 4 "xl" |> List.map ProcessShip
            generateRandomMilitaryAbandonedShips 6 "l" |> List.map ProcessShip
            generateRandomMilitaryAbandonedShips 6 "m" |> List.map ProcessShip
            generateRandomMilitaryAbandonedShips 6 "s" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips 3 "xl" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips 12 "l" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips 8 "m" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips 6 "s" |> List.map ProcessShip


            // Lets generate a few battlefields of varying sizes
            generateBattlefield 1 3 2 2 |> List.map ProcessShip
            generateBattlefield 0 3 3 0 |> List.map ProcessShip
            generateBattlefield 0 1 3 4 |> List.map ProcessShip
            generateBattlefield 0 0 3 6 |> List.map ProcessShip
            generateBattlefield 0 0 6 3 |> List.map ProcessShip

            // followed by a bunch of M & S in safe space.
            [
                for i in 1..5 ->
                    militaryShips
                    |> filterListBy [ "m" ]
                    |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name))
                    |> ProcessShip
                for i in 1..6 ->
                    economyShips
                    |> filterListBy [ "m" ]
                    |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name))
                    |> ProcessShip
                for i in 1..6 ->
                    militaryShips
                    |> filterListBy [ "s" ]
                    |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name))
                    |> ProcessShip
                for i in 1..8 ->
                    economyShips
                    |> filterListBy [ "s" ]
                    |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name))
                    |> ProcessShip

                // ok, a couple large l economy ship.
                for i in 1..2 ->
                    economyShips
                    |> filterListBy [ "l" ]
                    |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name))
                    |> ProcessShip

            ]

            // And finally a few individual ships
            [
                // Make sure there's at least one Raptor!
                filterBy [ "spl"; "xl"; "carrier" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]
            [
                // And Asgard!
                filterBy [ "atf"; "xl"; "battleship" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]
            [
                // And Syn.
                filterBy [ "atf"; "l"; "destroyer" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]
            [
                // Guppy, because they're fun
                filterBy [ "bor"; "l"; "carrier" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]

        // // Generate ships in specific sector to test loadouts for boron/terran
        // [
        //     filterBy [ "atf"; "xl"; "battleship" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "atf"; "l"; "destroyer" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "ter"; "l"; "destroyer" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "spl"; "l"; "destroyer" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "bor"; "xl"; "carrier" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "bor"; "l"; "miner" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]


        ]
        |> List.concat
        |> List.unzip

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
    addElementsToDiff placedXML ships
    addElementsToDiff loadoutXML (loadouts |> List.choose id)

    WriteModfiles.write_xml_file "core" placedObjectsFilename placedXML
    WriteModfiles.write_xml_file "core" loadoutFilename loadoutXML
