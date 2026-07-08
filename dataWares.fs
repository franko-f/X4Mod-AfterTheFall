/// <summary>
/// The wares.xml diff: equipment ware ownership grants.
///
/// The game fills a station/ship <loadout> level only with equipment wares the
/// owning faction is listed against in libraries/wares.xml. Granting a faction an
/// extra <owner> line therefore widens what its loadouts can mount - the mechanism
/// behind giving hatikvah's bastions plasma turrets (their vanilla list holds only
/// the L pulse laser). Which grants exist is decided in tuning
/// (Tuning.GateDefence.FactionWareGrants); this module just renders them.
/// </summary>
module X4.Data.Wares

open System.Xml.Linq
open X4.WriteModfiles

/// Write the wares.xml diff granting each (faction, ware id) pair an <owner> entry.
let writeWareOwnerGrants (filename: string) (grants: (string * string) list) =
    let diff = newDiff ()

    for faction, ware in grants do
        diff.Add(
            addOp
                $"/wares/ware[@id='{ware}']"
                [ new XElement(XName.Get "owner", new XAttribute(XName.Get "faction", faction)) ]
        )

    write_xml_file "core" filename diff
    printfn "Wrote %d ware ownership grant(s) to %s" (List.length grants) filename
