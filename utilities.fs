/// <summary>
/// A few random utility functions.
/// </summary>

module X4.Utilities

open System.Xml.Linq

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
let splitTuples (tuples:('a option * 'b option)[]) =
    let firsts = [| for (a,_) in tuples do match a with Some x -> yield x | _ -> () |]
    let seconds = [| for (_,b) in tuples do match b with Some x -> yield x | _ -> () |]
    (firsts, seconds)


