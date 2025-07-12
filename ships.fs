module X4.Ships

open System
open System.Xml
open System.Xml.Linq

// Define a few types for ship location that we'll use when we place an abandoned ship
type Position = int * int * int
type Rotation = int * int * int
type ShipLocation = string * string * Position * Rotation   // Ship name, sector name, position, rotation. Should probably use a record type here.

let rand = new Random(12345)    // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

// List of all ships that are valid candidates for being generated as abandoned ships.
let abandonedShipsList =
    // Our filters. Factions we'll look for, and tags we'll omit. Both must be true.
    let factions = [ "_arg_"; "_par_"; "_tel_"; "_spl_"; "_ter_"; "_atf_"; "_yak_"; "_pir_"; ]  // Removed boron '_bor_', as they're spawning without engins
    let omit = [
        "_xs_"; "_plot_"; "_landmark_"; "_story"; "_highcapacity_";
        "ship_spl_xl_battleship_01_a_macro"; "ship_pir_xl_battleship_01_a_macro"; // specific ships that seems unsupported or don't exist in game
        // Some terran destroyers that are spawning without main guns.
        "ship_atf_l_destroyer_01_a_macro"; "ship_atf_xl_battleship_01_a_macro"; "ship_ter_l_destroyer_01_a_macro"
    ]

    X4.Data.allShipMacros
    |> List.filter (
        fun shipName ->
            ( factions |> List.exists (fun tag -> shipName.Contains tag) )        // Must contain one of the factions we're interested in.
            && (not ( omit |> List.exists (fun tag -> shipName.Contains tag) )))  // Must not contain any of the tags we're not interested in from the omit list.


// Quickly filter by a search string substring. eg, 'bor' will return all the Boron ships by filtering for '_bor_'.
let rec filterListBy (searchTags:string list) (ships:string list) =
    match searchTags with
    | [] -> ships
    | H::T ->
        ships
        |> List.filter (fun x -> x.Contains("_" + H.ToLower() + "_"))
        |> filterListBy T

// Filter the abandoned ships list by a list of search tags. eg, ['bor', 's'] will return all Boron small ships.
// ['tel', 'xl', 'carrier'] will return all Teladi extra large carriers.
let filterBy (search:string list) =
    filterListBy search abandonedShipsList

let militaryShips =
    List.concat [
        filterBy ["corvette"]
        filterBy ["gunboat"]
        filterBy ["frigate"]
        filterBy ["destroyer"]
        filterBy ["battleship"]
        filterBy ["carrier"]
        filterBy ["resupplier"]
        filterBy ["fighter"]
        filterBy ["heavyfighter"]
        filterBy ["bomber"]
        filterBy ["scout"]
    ]

let economyShips =
    List.concat [
        filterBy ["miner"]
        filterBy ["builder"]
        filterBy ["trans"]
    ]

// given a sector and a list of possible ships, select one of the ships,
// and assign it coordinates in the given sector.
let generateRandomAbandonedShipFromListInSector (sector:string) (shipList:string list) :ShipLocation =
    let ship = shipList.[rand.Next(shipList.Length)]
    // generate random coordinates within the sector, in KM offset from sector center (different from other coordinates)
    let x, y, z = rand.Next(-160, 160), rand.Next(-10, 10), rand.Next(-180, 180)
    // generate random yaw and pitch
    let yaw, pitch, roll = rand.Next(-180, 180), rand.Next(-180, 180), rand.Next(-180, 180)
    (ship, sector, (x, y, z), (yaw, pitch, roll))

// given a list of possible ships, select one, and place it randomly in any of the
// unsafe sectors in the game.
let generateRandomAbandonedShipFromList (shipList:string list) =
    let sector = X4.Data.selectRandomUnsafeSector() // We don't want these wrecks to be in the faction sectors.
    generateRandomAbandonedShipFromListInSector sector.Name shipList

// Generate COUNT random abandoned military ships of the given size in a random unsafe sector.
// 'size' is one of 'XL', 'L', 'm', 's'. - the standard X4 ship size classes.
let generateRandomMilitaryAbandonedShips (count:int) (size:string) =
    let ships = filterListBy [size] militaryShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// As above, but for economy ships.
let generateRandomEconomyAbandonedShips (count:int) (size:string) =
    let ships = filterListBy [size] economyShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// This function generates a bunch of abandoned ships near each other, as if a major battle occurred.
// The parameters determine how many of each class are in the field. Ships are clustered within 5km of a point
// in a random unsafe sector.
let generateBattlefield (countXL:int) countL countM countS =
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
    let _ship, sector, (x, y, z), _rotation  = (List.concat [xl;l;m;s]).[0]
    // Now update every ships sector and location to be near the first ship, keeping other data the same.
    List.concat [
        [ for (ship, _sector, _, rotation) in xl -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
        [ for (ship, _sector, _, rotation) in l  -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
        [ for (ship, _sector, _, rotation) in m  -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
        [ for (ship, _sector, _, rotation) in s  -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
   ]

// Generate the XML diff for placing an abandoned ship in the game based
// on the ship, sector, position and rotation given as parameters.
let ProcessShip ((ship, sector, (x, y, z), (yaw, pitch, roll)):ShipLocation) =
    // Interestingly, the units of KM and deg are specified in the XML attribute fields for abandoned ships.
    // I've not seen this elsewhere, and don't know if it's necessary, but for safety I'll duplicate it.
    printfn "GENERATING ABANDONED SHIP: %s, Sector: %s, Position: %A, Rotation: %A" ship sector (x, y, z) (yaw, pitch, roll)
    let xml = $"""
    <add sel="/mdscript[@name='PlacedObjects']/cues/cue[@name='Place_Claimable_Ships']/actions">
        <find_sector name="$sector" macro="macro.{sector}"/>
        <do_if value="$sector.exists">
          <create_ship name="$ship" macro="macro.{ship}" sector="$sector">
            <owner exact="faction.ownerless"/>
            <position x="{x}km" y="{y}km" z="{z}km"/>
            <rotation yaw="{yaw}deg" pitch="{pitch}deg" roll="{roll}deg"/>
          </create_ship>
        </do_if>
    </add>
    """
    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    let xtr = new XmlTextReader(new System.IO.StringReader(xml));
    XElement.Load(xtr);

// Create a list of random ships, assign them to random sectors, then generate XML that will place
// them as abandoned ships in the game.
// We don't want it completely random, as we want to make sure there's a good mix of ships in the game.
// We lean slighly towards generated economy ships vs military, though there's plenty of both.
// there should be, on average, one or two ships per sector.
let generate_abandoned_ships_file (filename:string) =
    let shipDiff =  List.concat [
        // A bunch of ships in unsafe space to being
        generateRandomMilitaryAbandonedShips 4 "xl" |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 6 "l"  |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 6 "m"  |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 6 "s"  |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 3  "xl" |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 12 "l"  |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 8 "m"   |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 6 "s"   |> List.map ProcessShip
        [filterBy ["spl"; "xl"; "carrier"]    |> generateRandomAbandonedShipFromList |> ProcessShip]      // Make sure there's at least one Raptor!
        // Until we figure out how to generate these with faction specific equipment, we'll leave them out. Currently, they're spawning without main batteries.
        //[filterBy ["atf"; "xl"; "battleship"] |> generateRandomAbandonedShipFromList |> ProcessShip]   // And Asgard!
        //[filterBy ["atf"; "l"; "destroyer"]   |> generateRandomAbandonedShipFromList |> ProcessShip]     // And Syn.

        // Lets generate a few battlefields of varying sizes
        generateBattlefield 1 3 2 2 |> List.map ProcessShip
        generateBattlefield 0 3 4 0 |> List.map ProcessShip
        generateBattlefield 0 1 4 5 |> List.map ProcessShip
        generateBattlefield 0 0 4 8 |> List.map ProcessShip
        generateBattlefield 0 0 8 2 |> List.map ProcessShip


        // followed by a bunch of M & S in safe space.
        [
            for i in 1..6 ->
                militaryShips |> filterListBy ["m"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..8 ->
                economyShips  |> filterListBy ["m"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..8 ->
                militaryShips |> filterListBy ["s"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..10 ->
                economyShips  |> filterListBy ["s"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip

            // ok, a couple large l economy ship.
            for i in 1..2 ->
                economyShips  |> filterListBy ["l"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip

        ]

    ]

    // Create the new XML Diff document to contain our region additions
    let diff = XElement.Parse(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
        </diff>
        ")

    // Now add the region changes, one by one, to the the xml diff.
    [| for element in shipDiff do
        diff.Add(element)
        diff.Add( new XText("\n")) // Add a newline after each element so the output is readible
    |] |> ignore

    WriteModfiles.write_xml_file "core" filename diff
