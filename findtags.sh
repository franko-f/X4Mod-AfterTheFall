#!/bin/sh

find X4_unpacked_data -type f -name "*.xml" -exec sh -c '
  for file; do
    xmlstarlet sel -t -m "//create_ship" -c "." -n "$file" | pygmentize -l xml
  done
' sh {} +
