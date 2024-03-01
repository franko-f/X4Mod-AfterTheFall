/// <summary>
/// A few random utility functions.
/// </summary>

module X4.Utilities

open System
open System.Xml.Linq


// Neat case insensitive string comparison function from https://stackoverflow.com/questions/1936767/f-case-insensitive-string-compare
// It's important as the X4 data files often mix the case of identifiers like zone and sector names.
let (=?) s1 s2 = System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase)
// Similar to =?, but it takes in an option string as s1; returning False if s1 is None.
let (=??) (s1:Option<string>) s2 = Option.exists (fun s1 -> s1 =? s2) s1

// Borrowed from. Promise to give it back
// https://www.fssnip.net/gO/title/Copy-a-directory-of-files
let rec directoryCopy srcPath dstPath copySubDirs =

    if not <| System.IO.Directory.Exists(srcPath) then
        let msg = System.String.Format("Source directory does not exist or could not be found: {0}", srcPath)
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
let check_and_create_dir (filename:string) =
    let dir = System.IO.Path.GetDirectoryName(filename)
    if not (System.IO.Directory.Exists(dir)) then
        System.IO.Directory.CreateDirectory(dir) |> ignore   // Should really catch the failure here. TODO

// Write our XML output to a directory called 'mod'. If the directrory doesn't exist, create it.
let write_xml_file (filename:string) (xml:XElement) =
    let modDir = __SOURCE_DIRECTORY__ + "/mod/after_the_fall"
    let fullname = modDir + "/" + filename
    check_and_create_dir fullname   // filename may contain parent folder, so we use fullname when checking/creating dirs.
    xml.Save(fullname)

// Given a list of 2 element tuples, split them in to two lists.
// The first list contains all the first elements, the second list contains all the second elements.
// It will strip out any 'None' values.
let splitTuples (tuples:('a option * 'b option * 'c option) list) =
    let x, y, z = List.unzip3 tuples
    (List.choose id x, List.choose id y, List.choose id z)

let parseStringList (input: string) : string list =
    let trimmedInput = input.Trim('[', ']')
    let elements = trimmedInput.Split([|','|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
    List.map (fun (element:string) -> element.Trim('"', ' ')) elements

// return either A or B, depending on the value of check. basically a simple ternary operator.
// so I don't need to do full if then else. Unlike C-style '?', The check value is the last parameter,
// so I can chain via |>
let either a b check = match check with true -> a | false -> b
