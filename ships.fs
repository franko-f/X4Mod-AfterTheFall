module X4.Ships

open System
open System.Xml
open System.Xml.Linq


let rand = new Random(12345)    // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

// List of abandoned ships and sector we're going to randomly place them in.
// We define the sector, but we'll just dump it randomly within the sector.
let abandonedShipsList = 
    [
        // "macro.ship_test_s_scout_01_macro"   //test ship, ignore.
        "macro.ship_arg_l_destroyer_01_b_macro"
        "macro.ship_arg_l_miner_liquid_01_b_macro"
        "macro.ship_arg_l_miner_solid_01_b_macro"
        "macro.ship_arg_l_trans_container_02_b_macro"
        "macro.ship_arg_l_trans_container_01_b_macro"
        "macro.ship_arg_m_bomber_01_b_macro"
        "macro.ship_arg_m_frigate_01_b_macro"
        "macro.ship_arg_m_miner_liquid_01_b_macro"
        "macro.ship_arg_m_miner_solid_01_b_macro"
        "macro.ship_arg_m_trans_container_02_b_macro"
        "macro.ship_arg_m_trans_container_01_b_macro"
        "macro.ship_arg_s_fighter_01_b_macro"
        "macro.ship_arg_s_fighter_02_b_macro"
        "macro.ship_arg_s_heavyfighter_01_b_macro"
        "macro.ship_arg_s_scout_01_b_macro"
        "macro.ship_arg_xl_builder_01_b_macro"
        "macro.ship_arg_xl_carrier_01_b_macro"
        "macro.ship_arg_xl_resupplier_01_b_macro"
        "macro.ship_par_l_destroyer_01_b_macro"
        "macro.ship_par_l_miner_liquid_01_b_macro"
        "macro.ship_par_l_miner_solid_01_b_macro"
        "macro.ship_par_l_trans_container_02_b_macro"
        "macro.ship_par_l_trans_container_01_b_macro"
        "macro.ship_par_m_corvette_01_b_macro"
        "macro.ship_par_m_frigate_01_b_macro"
        "macro.ship_par_m_miner_liquid_01_b_macro"
        "macro.ship_par_m_miner_solid_01_b_macro"
        "macro.ship_par_m_trans_container_02_b_macro"
        "macro.ship_par_m_trans_container_01_b_macro"
        "macro.ship_par_s_fighter_01_b_macro"
        "macro.ship_par_s_scout_01_b_macro"
        "macro.ship_par_xl_carrier_01_b_macro"
        "macro.ship_par_xl_resupplier_01_b_macro"
        "macro.ship_tel_l_destroyer_01_b_macro"
        "macro.ship_tel_l_miner_liquid_01_b_macro"
        "macro.ship_tel_l_miner_solid_01_b_macro"
        "macro.ship_tel_l_trans_container_02_b_macro"
        "macro.ship_tel_l_trans_container_01_b_macro"
        "macro.ship_tel_m_bomber_01_b_macro"
        "macro.ship_tel_m_frigate_01_b_macro"
        "macro.ship_tel_m_miner_liquid_01_b_macro"
        "macro.ship_tel_m_miner_solid_01_b_macro"
        "macro.ship_tel_m_trans_container_02_b_macro"
        "macro.ship_tel_m_trans_container_01_b_macro"
        "macro.ship_tel_s_fighter_01_b_macro"
        "macro.ship_tel_s_scout_01_b_macro"
        "macro.ship_tel_xl_carrier_01_b_macro"
        "macro.ship_tel_xl_resupplier_01_b_macro"
        "macro.ship_par_s_scout_01_a_macro"
        "macro.ship_arg_m_bomber_02_a_macro"
        "macro.ship_arg_m_frigate_01_a_macro"
        "macro.ship_par_m_frigate_01_a_macro"
        "macro.ship_tel_m_frigate_01_a_macro"
        "macro.ship_arg_l_destroyer_01_a_macro"
        "macro.ship_arg_l_miner_liquid_01_a_macro"
        "macro.ship_arg_l_miner_solid_01_a_macro"
        "macro.ship_arg_l_trans_liquid_02_a_macro"
        "macro.ship_arg_l_trans_container_01_a_macro"
        "macro.ship_par_l_destroyer_01_a_macro"
        "macro.ship_par_l_miner_liquid_01_a_macro"
        "macro.ship_par_l_miner_solid_01_a_macro"
        "macro.ship_par_l_trans_liquid_02_a_macro"
        "macro.ship_par_l_trans_container_01_a_macro"
        "macro.ship_tel_l_destroyer_01_a_macro"
        "macro.ship_tel_l_miner_liquid_01_a_macro"
        "macro.ship_tel_l_miner_solid_01_a_macro"
        "macro.ship_tel_l_trans_liquid_02_a_macro"
        "macro.ship_tel_l_trans_container_01_a_macro"
        "macro.ship_arg_xl_builder_01_a_macro"
        "macro.ship_arg_xl_carrier_01_a_macro"
        "macro.ship_arg_xl_resupplier_01_a_macro"
        "macro.ship_par_xl_carrier_01_a_macro"
        "macro.ship_par_xl_resupplier_01_a_macro"
        "macro.ship_tel_xl_carrier_01_a_macro"
        "macro.ship_tel_xl_resupplier_01_a_macro"
        "macro.ship_arg_s_fighter_02_a_macro"
        "macro.ship_arg_m_trans_container_01_a_macro"
        "macro.ship_arg_m_trans_liquid_02_a_macro"
        "macro.ship_arg_m_miner_solid_01_a_macro"
        "macro.ship_arg_m_bomber_01_a_macro"
        "macro.ship_arg_m_miner_liquid_01_a_macro"
        "macro.ship_arg_s_scout_01_a_macro"
        "macro.ship_arg_s_heavyfighter_01_a_macro"
        "macro.ship_arg_s_fighter_01_a_macro"
        "macro.ship_par_m_corvette_01_a_macro"
        "macro.ship_par_m_trans_container_01_a_macro"
        "macro.ship_par_m_trans_liquid_02_a_macro"
        "macro.ship_par_m_miner_solid_01_a_macro"
        "macro.ship_par_m_miner_liquid_01_a_macro"
        "macro.ship_par_s_fighter_01_a_macro"
        "macro.ship_tel_m_bomber_01_a_macro"
        "macro.ship_tel_s_scout_01_a_macro"
        "macro.ship_tel_s_fighter_01_a_macro"
        "macro.ship_tel_m_trans_container_01_a_macro"
        "macro.ship_tel_m_trans_liquid_02_a_macro"
        "macro.ship_tel_m_miner_solid_01_a_macro"
        "macro.ship_tel_m_miner_liquid_01_a_macro"
        "macro.ship_arg_m_trans_container_02_a_macro"
        "macro.ship_arg_l_trans_container_02_a_macro"
        "macro.ship_tel_m_trans_container_02_a_macro"
        "macro.ship_par_m_trans_container_02_a_macro"
        "macro.ship_par_l_trans_container_02_a_macro"
        "macro.ship_tel_l_trans_container_02_a_macro"
//        "macro.ship_tfm_xl_carrier_01_a_macro"        // probably a plot ship, remove.
        "macro.ship_arg_s_trans_container_01_a_macro"
        "macro.ship_arg_l_trans_container_03_a_macro"
        "macro.ship_arg_l_trans_container_04_a_macro"
        "macro.ship_arg_l_trans_container_05_a_macro"
        "macro.ship_arg_l_trans_container_03_b_macro"
        "macro.ship_arg_l_trans_container_04_b_macro"
        "macro.ship_arg_l_trans_container_05_b_macro"
        "macro.ship_arg_s_heavyfighter_02_a_macro"
        "macro.ship_par_xl_builder_01_a_macro"
        "macro.ship_par_xl_builder_01_b_macro"
        "macro.ship_tel_xl_builder_01_a_macro"
        "macro.ship_tel_xl_builder_01_b_macro"
        "macro.ship_arg_s_fighter_03_a_macro"
        "macro.ship_arg_s_fighter_04_a_macro"
        "macro.ship_arg_s_trans_container_02_a_macro"
        "macro.ship_tel_s_fighter_02_a_macro"
        "macro.ship_tel_s_fighter_02_b_macro"
        "macro.ship_par_s_fighter_02_a_macro"
        "macro.ship_par_s_fighter_02_b_macro"
        "macro.ship_tel_s_scout_02_a_macro"
        "macro.ship_tel_s_scout_02_b_macro"
        "macro.ship_tel_s_trans_container_01_a_macro"
        "macro.ship_par_s_trans_container_01_a_macro"
        "macro.ship_arg_s_trans_container_01_b_macro"
        "macro.ship_arg_s_trans_container_02_b_macro"
        "macro.ship_par_s_trans_container_01_b_macro"
        "macro.ship_tel_s_trans_container_01_b_macro"
        "macro.ship_arg_s_miner_solid_01_a_macro"
        "macro.ship_par_s_miner_solid_01_a_macro"
        "macro.ship_tel_s_miner_solid_01_a_macro"
        "macro.ship_tel_m_trans_container_03_a_macro"
        "macro.ship_par_s_heavyfighter_01_a_macro"
        "macro.ship_xen_s_scout_01_a_macro"
        "macro.ship_par_m_trans_container_03_a_macro"
        "macro.ship_par_l_destroyer_02_a_macro"
        "macro.ship_tel_s_trans_container_02_a_macro"
        "macro.ship_par_l_trans_container_03_a_macro"
        "macro.ship_par_xl_resupplier_02_a_macro"
        "macro.ship_par_l_miner_solid_02_a_macro"
        "macro.ship_par_l_miner_liquid_02_a_macro"
        "macro.ship_par_xl_carrier_02_a_macro"
        "macro.ship_spl_s_fighter_01_a_macro"
        "macro.ship_spl_s_heavyfighter_01_a_macro"
        "macro.ship_spl_s_scout_01_a_macro"
        "macro.ship_spl_s_trans_container_01_a_macro"
        "macro.ship_spl_m_bomber_01_a_macro"
        "macro.ship_spl_m_trans_container_01_a_macro"
        "macro.ship_spl_m_miner_liquid_01_a_macro"
        "macro.ship_spl_m_miner_solid_01_a_macro"
        "macro.ship_spl_m_frigate_01_a_macro"
        "macro.ship_spl_m_corvette_01_a_macro"
        "macro.ship_spl_l_destroyer_01_a_macro"
        "macro.ship_spl_l_trans_container_01_a_macro"
        "macro.ship_spl_l_miner_solid_01_a_macro"
        "macro.ship_spl_l_miner_liquid_01_a_macro"
//        "macro.ship_spl_xl_battleship_01_a_macro" // probably a special ship, as the raptor would be the carrier below.
        "macro.ship_spl_s_fighter_02_a_macro"
        "macro.ship_spl_s_heavyfighter_02_a_macro"
        "macro.ship_spl_xl_carrier_01_a_macro"
        "macro.ship_spl_xl_resupplier_01_a_macro"
        "macro.ship_spl_s_miner_solid_01_a_macro"
        "macro.ship_spl_s_fighter_02_b_macro"
        "macro.ship_spl_m_corvette_01_b_macro"
//        "macro.ship_spl_s_trans_container_01_plot_01_macro"   // another plot ship, don't include
        "macro.ship_spl_xl_builder_01_a_macro"
        "macro.ship_ter_l_miner_solid_01_a_macro"
        "macro.ship_ter_l_trans_container_01_a_macro"
        "macro.ship_ter_l_destroyer_01_a_macro"
        "macro.ship_ter_xl_resupplier_01_a_macro"
        "macro.ship_ter_xl_carrier_01_a_macro"
        "macro.ship_ter_s_trans_container_01_a_macro"
        "macro.ship_ter_s_heavyfighter_01_a_macro"
        "macro.ship_ter_s_scout_01_a_macro"
        "macro.ship_ter_s_fighter_01_a_macro"
        "macro.ship_ter_s_miner_solid_01_a_macro"
        "macro.ship_ter_m_corvette_01_a_macro"
        "macro.ship_ter_m_trans_container_01_a_macro"
        "macro.ship_ter_m_miner_liquid_01_a_macro"
        "macro.ship_ter_m_miner_solid_01_a_macro"
        "macro.ship_ter_m_frigate_01_a_macro"
        "macro.ship_atf_xl_battleship_01_a_macro"
        "macro.ship_atf_l_destroyer_01_a_macro"
        "macro.ship_ter_l_research_01_a_macro"
        "macro.ship_ter_xl_builder_01_a_macro"
        "macro.ship_ter_l_miner_liquid_01_a_macro"
        "macro.ship_yak_s_fighter_01_a_macro"
        "macro.ship_yak_m_corvette_01_a_macro"
//        "macro.ship_ter_l_trans_container_01_landmark_macro"  // Likely special, remove
        "macro.ship_ter_s_fighter_02_a_macro"
        "macro.ship_ter_s_fighter_03_a_macro"
        "macro.ship_ter_m_gunboat_01_a_macro"
        "macro.ship_ter_s_scout_02_a_macro"
        "macro.ship_pir_l_scavenger_01_a_macro"
        "macro.ship_gen_m_yacht_01_a_macro" // The special yacht, we're going to include it anyway.
        "macro.ship_pir_l_miner_solid_01_a_macro"
        "macro.ship_pir_s_fighter_01_a_macro"
        "macro.ship_pir_s_fighter_02_a_macro"
//        "macro.ship_pir_xl_battleship_01_a_macro"     // erlking. We only want one of these.
        "macro.ship_pir_l_scrapper_01_macro"
        "macro.ship_pir_s_heavyfighter_01_a_macro"
        "macro.ship_pir_s_trans_container_01_a_macro"
        "macro.ship_pir_xl_builder_01_macro"
        "macro.ship_pir_s_trans_condensate_01_a_macro"
//        "macro.ship_pir_l_scavenger_01_a_storyhighcapacity_macro" // Special ship, don't include
        "macro.ship_bor_s_fighter_01_a_macro"
        "macro.ship_bor_l_trans_container_01_a_macro"
        "macro.ship_bor_l_destroyer_01_a_macro"
        "macro.ship_bor_l_miner_solid_01_a_macro"
        "macro.ship_bor_s_miner_solid_01_story_macro"
        "macro.ship_bor_xl_carrier_01_a_macro"
        "macro.ship_bor_m_corvette_01_a_macro"
        "macro.ship_bor_m_miner_liquid_01_a_macro"
        "macro.ship_bor_m_miner_solid_01_a_macro"
        "macro.ship_bor_m_trans_container_01_a_macro"
        "macro.ship_bor_s_trans_container_01_a_macro"
        "macro.ship_bor_s_miner_solid_01_a_macro"
        "macro.ship_bor_s_heavyfighter_01_a_macro"
        "macro.ship_bor_s_scout_01_a_macro"
//        "macro.ship_bor_xl_carrier_01_landmark_macro" // Special ship, don't include
        "macro.ship_bor_xl_resupplier_01_a_macro"
        "macro.ship_bor_l_miner_liquid_01_a_macro"
        "macro.ship_bor_m_corvette_02_a_macro"
        "macro.ship_bor_m_gunboat_01_a_macro"
        "macro.ship_bor_xl_builder_01_a_macro"
        "macro.ship_bor_s_scout_02_a_macro"
        "macro.ship_bor_l_carrier_01_a_macro"
    ]

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
    ]

let economyShips =
    List.concat [
        filterBy ["miner"]
        filterBy ["builder"]
        filterBy ["trans"]
    ]

let generateRandomAbandonedShipFromListInSector (sector:string) (shipList:string list) =
    let ship = shipList.[rand.Next(shipList.Length)]
    // generate random coordinates within the sector, in KM offset from sector center (different from other coordinates)
    let x, y, z = rand.Next(-160, 160), rand.Next(-10, 10), rand.Next(-180, 180)
    // generate random yaw and pitch
    let yaw, pitch, roll = rand.Next(-180, 180), rand.Next(-180, 180), rand.Next(-180, 180)
    printfn "GENERATING ABANDONED SHIP: %s, Sector: %s, Position: %A, Rotation: %A" ship sector (x, y, z) (yaw, pitch, roll)
    (ship, sector, (x, y, z), (yaw, pitch, roll))

let generateRandomAbandonedShipFromList (shipList:string list) =
    let sector = X4.Data.selectRandomUnsafeSector() // We don't want these wrecks to be in the faction sectors.
    generateRandomAbandonedShipFromListInSector sector.Name shipList

let generateRandomMilitaryAbandonedShips (count:int) (size:string) =
    let ships = filterListBy [size] militaryShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

let generateRandomEconomyAbandonedShips (count:int) (size:string) =
    let ships = filterListBy [size] economyShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// Generate the XML cue for creating an abandoned ship of the given class in the sector.
let ProcessShip ((ship:string), (sector:string), position, rotation) =
    // Interestingly, the units of KM and deg are specified in the XML attribute fields for abandoned ships.
    // I've not seen this elsewhere, and don't know if it's necessary, but for safety I'll duplicate it.
    let ((x:int), (y:int), (z:int)), ((yaw:int), (pitch:int), (roll:int)) = position, rotation
    let xml = $"""
        <add sel="/mdscript[@name='PlacedObjects']/cues/cue[@name='Place_Claimable_Ships']/actions">
        <find_sector name="$sector" macro="macro.{sector}"/>
        <do_if value="$sector.exists">
          <create_ship name="$ship" macro="{ship}" sector="$sector">
            <owner exact="faction.ownerless"/>
            <position x="{x}KM" y="{y}KM" z="{z}KM"/>
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
let generate_abandoned_ships_file (filename:string) =

    let shipDiff =  List.concat [
        // A bunch of ships in unsafe space to being
        generateRandomMilitaryAbandonedShips 5 "XL" |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 10 "L" |>  List.map ProcessShip
        generateRandomMilitaryAbandonedShips 15 "m" |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 20 "s" |> List.map ProcessShip
        generateRandomEconomyAbandonedShips  4  "XL" |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 10 "L" |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 20 "m" |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 30 "s" |> List.map ProcessShip
        [filterBy ["spl"; "xl"; "carrier"] |> generateRandomAbandonedShipFromList |> ProcessShip]      // Make sure there's at least one Raptor!
        [filterBy ["atf"; "xl"; "battleship"] |> generateRandomAbandonedShipFromList |> ProcessShip]   // And Asgard!
        [filterBy ["atf"; "l"; "destroyer"] |> generateRandomAbandonedShipFromList |> ProcessShip]     // And Syn.

        // followed by a handful of M in safe space.
        [
            for i in 1..5 ->
                militaryShips |> filterListBy ["m"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..5 ->
                economyShips |> filterListBy ["m"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
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

    Utilities.write_xml_file filename diff
