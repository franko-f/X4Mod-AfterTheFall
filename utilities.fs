/// <summary>
/// A few random utility functions.
/// </summary>

module X4.Utilities

open System
open System.Xml.Linq


// Neat case insensitive string comparison function from https://stackoverflow.com/questions/1936767/f-case-insensitive-string-compare
// It's important as the X4 data files often mix the case of identifiers like zone and sector names.
let (=?) s1 s2 =
    System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase)
// Similar to =?, but it takes in an option string as s1; returning False if s1 is None.
let (=??) (s1: Option<string>) s2 = Option.exists (fun s1 -> s1 =? s2) s1

// Borrowed from. Promise to give it back
// https://www.fssnip.net/gO/title/Copy-a-directory-of-files
let rec directoryCopy srcPath dstPath copySubDirs =

    if not <| System.IO.Directory.Exists(srcPath) then
        let msg =
            System.String.Format("Source directory does not exist or could not be found: {0}", srcPath)

        raise (System.IO.DirectoryNotFoundException(msg))

    if not <| System.IO.Directory.Exists(dstPath) then
        System.IO.Directory.CreateDirectory(dstPath) |> ignore

    let srcDir = new System.IO.DirectoryInfo(srcPath)

    for file in srcDir.GetFiles() do
        let temppath = System.IO.Path.Combine(dstPath, file.Name)
        file.CopyTo(temppath, true) |> ignore

    if copySubDirs then
        for subdir in srcDir.GetDirectories() do
            let dstSubDir = System.IO.Path.Combine(dstPath, subdir.Name)
            directoryCopy subdir.FullName dstSubDir copySubDirs


// Given a filename with full path, create all the parent directories recursively if they don't exist.
let check_and_create_dir (filename: string) =
    let dir = System.IO.Path.GetDirectoryName(filename)

    if not (System.IO.Directory.Exists(dir)) then
        System.IO.Directory.CreateDirectory(dir) |> ignore // Should really catch the failure here. TODO

// Given a list of 2 element tuples, split them in to two lists.
// The first list contains all the first elements, the second list contains all the second elements.
// It will strip out any 'None' values.
let splitTuples (tuples: ('a option * 'b option * 'c option) list) =
    let x, y, z = List.unzip3 tuples
    (List.choose id x, List.choose id y, List.choose id z)

let parseStringList (input: string) : string list =
    let trimmedInput = input.Trim('[', ']')

    let elements =
        trimmedInput.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.toList

    List.map (fun (element: string) -> element.Trim('"', ' ')) elements

// return either A or B, depending on the value of check. basically a simple ternary operator.
// so I don't need to do full if then else. Unlike C-style '?', The check value is the last parameter,
// so I can chain via |>
let either a b check =
    match check with
    | true -> a
    | false -> b


// Convert a space separated string of tags into a list of tags.
let tagStringToList (tags: String) =
    System.Text.RegularExpressions.Regex.Split(tags.Trim(), @"\s+") |> List.ofArray

// Convert a space separated string of tags into a set of tags.
let tagStringToSet (tags: String) = tags |> tagStringToList |> Set.ofList

// Really useful computation expression build for options (credit somewhere online.)
// allows us to write cleaner and easier to understand code filled with option types
type OptionBuilder() =
    member _.Bind(value, binder) =
        match value with
        | Some x -> binder x
        | None -> None

    member _.Return(value) = Some value

    member _.ReturnFrom(value) = value

    member _.Zero() = None

let option = OptionBuilder()


// Quick function to return a generator that produces unique IDs.
// Use like this:
// let getId = makeIdGenerator()
// let id1 = getId()
// let id2 = getId()
// Useful for things like ship loadout creation where some ships need to
// have a unique loadout generate for it, and needs a unique reference.
let makeIdGenerator () =
    let counter = ref 0

    fun () ->
        counter.Value <- counter.Value + 1
        counter.Value
