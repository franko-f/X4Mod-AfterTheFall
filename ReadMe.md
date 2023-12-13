# X4MLParser
This project creates a the 'end of days/the last gate' mod for X4 - or whatever
 I eventually decide to call it.

The mod will convert most of the galaxy to xenon owned and shrink the economies
of the other AI factions. Existing gamestarts won't work so well, as you may
start in the middle of a Xenon owned sector. I recommend using a custom start.

The code generator will parse the X4 data files, including expansions, and
generation an x4 XML diff to apply these changes to the universe start.

To use this mod-generator, you'll need to extract the x4 foundations catalog
using the egosoft provided tools, and place these files in the folder
> x4_unpacked_data
  > core
  > boron
  > split
  > pirate
  > terran

Each DLC has it's own subdirectory as shown above, and the core games data
will reside under 'core'.

