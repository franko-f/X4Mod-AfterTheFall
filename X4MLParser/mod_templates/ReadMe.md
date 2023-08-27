This folder contains a 'demo mod' that can be used as a type provider for fsharp.data.
By having examples of all the modding types we want, the type provider can 
generate our type information in our fsharp code.

This is not a real mod. It's a copy paste of various examples found online in X4 modding
tutorials and help files. It's purely demo data to allow the fsharp type provider to do
it's magic. Most of the data in here will NOT make it in to the final mod. Thjere are
some exceptions that are explained below.

Each file matches an existing x4 vanilla data file, showing sample modding information
for each of the files we wish to mod.

We keep each separate to avoid 'type confusion' - If we put all this in to a single file,
our type provider will be pretty big, trying to provide options for every single potential
option across all the different XML object types, rendering it less useful. This way we
get one type provider focused around adding each type of resource.


In some of the files, not only does the data act as a general type provider template, we
DO re-use some of the data to provide a default value for some of the changes.
For example, in god.xml template, we have definitions for Xenon stations. We use those
as the templates for when we place new stations of these types in the world.
For the most part, we just duplicate the entry, and update the location and ID to place
a new unique station in the map for the xenon.