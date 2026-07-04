/// <summary>
/// Construction plan data: locating the vanilla defence plans that bastions are
/// stacked from, and measuring station module geometry from the asset files.
/// (Phase G of the refactor will also move the WRITING of the mod's
/// constructionplans.xml here.)
/// </summary>
[<AutoOpen>]
module X4.Data.Plans

open System.Xml.Linq
open X4.Types
open X4.Utilities
open X4.Tuning
open X4.Data.Xml

// Locate and load a vanilla defence construction plan. Returns the parsed <plan>
// element as an opaque handle - the bastion plan generator clones it.
let loadDefencePlan (source: DefencePlanSource) : XmlSource =
    let file =
        match source.Dlc with
        | None -> X4UnpackedDataFolder + "/libraries/constructionplans.xml"
        | Some(extension, _) ->
            X4UnpackedDataFolder + "/extensions/" + extension + "/libraries/constructionplans.xml"

    XDocument.Load(file).Descendants(XName.Get "plan") // DLC files may be diffs, so search all descendants
    |> Seq.tryFind (fun plan -> plan.Attribute(XName.Get "id").Value = source.PlanId)
    |> Option.map XmlSource.ofElement
    |> Option.defaultWith (fun () -> failwithf "Defence construction plan '%s' not found in %s" source.PlanId file)

// The physical vertical extent of a station module, in metres (below anchor, above
// anchor). A plan entry's offset position is only the module's ANCHOR point - the
// module body extends beyond it (e.g. the argon claim module reaches 586m below and
// 708m above its anchor). We bound the body using every connection offset on the
// module's component: turrets, shields and docks sit on the hull surface, so they give
// a good lower bound on the real mesh extent (which lives in binary files we can't read).
let moduleVerticalExtent =
    let cache = System.Collections.Generic.Dictionary<string, float * float>()

    fun (macroName: string) ->
        match cache.TryGetValue macroName with
        | true, extent -> extent
        | _ ->
            let entry =
                match AllIndexMacros |> Array.tryFind (fun e -> e.Name =? macroName) with
                | Some entry -> entry
                | None -> failwithf "Bastion plan module macro '%s' not found in the macro index" macroName

            let macroDoc =
                XDocument.Load(X4UnpackedDataFolder + "/" + entry.File.Replace("\\", "/"))

            let componentRef =
                (macroDoc.Descendants(XName.Get "component") |> Seq.head)
                    .Attribute(XName.Get "ref")
                    .Value

            let componentEntry =
                match AllComponentMacros |> Array.tryFind (fun e -> e.Name =? componentRef) with
                | Some entry -> entry
                | None -> failwithf "Component '%s' of module '%s' not found in the component index" componentRef macroName

            let componentDoc =
                XDocument.Load(X4UnpackedDataFolder + "/" + componentEntry.File.Replace("\\", "/"))

            // NOTE: because this list uses an explicit 'yield' in the loop, the first
            // element must be explicitly yielded too - a bare '0.0' would be discarded.
            let connectionYs = [
                yield 0.0 // the anchor itself, so a module with no offset connections gets extent (0,0)
                for connection in componentDoc.Descendants(XName.Get "connection") do
                    match connection.Element(XName.Get "offset") with
                    | null -> ()
                    | offset ->
                        match offset.Element(XName.Get "position") with
                        | null -> ()
                        | position ->
                            match position.Attribute(XName.Get "y") with
                            | null -> ()
                            | y -> yield float y.Value
            ]

            let extent = (-(List.min connectionYs), List.max connectionYs)
            cache.[macroName] <- extent
            extent



// ==== WRITER ====
// The mod's constructionplans.xml is produced here from BastionPlanDirective values.

// Build a stacked bastion plan from a vanilla defence plan: duplicate the full entry
// list for each extra copy, remapping the entry indices and predecessor references of
// the duplicates, and lifting each copy vertically so the copies don't overlap. The
// first entry of each copy has no predecessor, so the game places it at its absolute
// offset - the same mechanism vanilla uses for entry 1 of every plan.
// The lift is the plan's PHYSICAL vertical span (anchor positions extended by each
// module's body) plus BastionCopyClearance. Defence plans range from ~1300m tall (arg,
// whose anchors all sit on one plane) to ~3000m (tel), so a fixed lift would leave
// some designs clipping and others needlessly far apart.
let makeBastionPlan (sourcePlan: XElement) (newId: string) (dlcPatch: (string * string) option) =
    let plan = new XElement(sourcePlan)
    plan.SetAttributeValue(XName.Get "id", newId)
    plan.SetAttributeValue(XName.Get "name", "Bastion")

    let entries = plan.Elements(XName.Get "entry") |> Seq.toList
    let entryCount = entries.Length

    // The anchor position of an entry within the plan.
    let entryY (entry: XElement) =
        match entry.Element(XName.Get "offset") with
        | null -> 0.0
        | offset ->
            match offset.Element(XName.Get "position") with
            | null -> 0.0
            | position ->
                match position.Attribute(XName.Get "y") with
                | null -> 0.0
                | y -> float y.Value

    // Physical top and bottom of the whole station: every module's anchor extended by
    // that module's body extent.
    let bounds =
        entries
        |> List.map (fun entry ->
            let y = entryY entry
            let below, above = moduleVerticalExtent (entry.Attribute(XName.Get "macro").Value)
            y - below, y + above)

    let physicalBottom = bounds |> List.map fst |> List.min
    let physicalTop = bounds |> List.map snd |> List.max
    let physicalSpan = physicalTop - physicalBottom

    printfn "    physical span %.0fm (%.0f .. %.0f), lifting copies by %.0fm" physicalSpan physicalBottom physicalTop (physicalSpan + GateDefence.BastionCopyClearance)

    for copy in 1 .. GateDefence.BastionStrengthMultiplier - 1 do
        let lift = (physicalSpan + GateDefence.BastionCopyClearance) * float copy

        for entry in entries do
            let clone = new XElement(entry)
            let index = int (clone.Attribute(XName.Get "index").Value)
            clone.SetAttributeValue(XName.Get "index", index + entryCount * copy)

            match clone.Element(XName.Get "predecessor") with
            | null -> ()
            | predecessor ->
                let predIndex = int (predecessor.Attribute(XName.Get "index").Value)
                predecessor.SetAttributeValue(XName.Get "index", predIndex + entryCount * copy)

            // Lift the whole copy vertically. Every offset position in the plan is in
            // absolute station coordinates, so shift them all uniformly.
            let offset =
                match clone.Element(XName.Get "offset") with
                | null ->
                    let o = new XElement(XName.Get "offset")
                    clone.AddFirst o
                    o
                | o -> o

            let position =
                match offset.Element(XName.Get "position") with
                | null ->
                    let p = new XElement(XName.Get "position")
                    offset.AddFirst p
                    p
                | p -> p

            let currentY =
                match position.Attribute(XName.Get "y") with
                | null -> 0.0
                | y -> float y.Value

            position.SetAttributeValue(XName.Get "y", currentY + lift)

            plan.Add(clone)
            plan.Add(new XText("\n"))

    // Declare the DLC the plan's modules come from, mirroring the pattern of the hand
    // written plans in the mod_xml constructionplans template.
    match dlcPatch with
    | Some(extension, name) ->
        let patch = new XElement(XName.Get "patch")
        patch.SetAttributeValue(XName.Get "extension", extension)
        patch.SetAttributeValue(XName.Get "version", "700")
        patch.SetAttributeValue(XName.Get "name", name)
        let patches = new XElement(XName.Get "patches")
        patches.Add patch
        plan.AddFirst patches
    | None -> ()

    plan

// Write the mod's constructionplans.xml from the bastion plan directives. Seeded from
// the hand written template (which holds e.g. the scrap cross plan) because our write
// replaces the copy Program.fs made of it.
let writeConstructionPlans (directives: BastionPlanDirective list) =
    printfn "======= Generating bastion construction plans"

    let plansOut =
        XElement.Load(new System.Xml.XmlTextReader(__SOURCE_DIRECTORY__ + "/mod_xml/libraries/constructionplans.xml"))

    for directive in directives do
        printfn "  BASTION PLAN atf_bastion_%s (%ix %s)" directive.SourcePlanId GateDefence.BastionStrengthMultiplier directive.SourcePlanId
        plansOut.Add(makeBastionPlan (XmlSource.value directive.SourcePlan) directive.NewId directive.DlcPatch)
        plansOut.Add(new XText("\n"))

    X4.WriteModfiles.write_xml_file "core" "libraries/constructionplans.xml" plansOut
