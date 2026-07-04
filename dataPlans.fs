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

