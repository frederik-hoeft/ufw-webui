# IBM Plex webfonts

These assets are the self-hosted browser fonts used by `Ufw.Client`.

Only the normal 400, 500, and 600 weights required by the UI are included for IBM Plex Sans and IBM Plex Mono. Each WOFF2 file is produced from the corresponding approved static TTF with FontTools WOFF2 compression; no glyph subsetting is applied, so the complete source character repertoire remains available for localized and user-provided text.

To regenerate the assets from the approved Sans/Mono source ZIP, install FontTools with Brotli support and run `python scripts/generate-ibm-plex-webfonts.py <archive.zip>` from the repository root. Font generation is a maintainer workflow and is not part of the application build.

The fonts are Copyright (c) 2017 IBM Corp. with Reserved Font Name "Plex" and are redistributed under the SIL Open Font License 1.1. See `OFL.txt` in this directory.
