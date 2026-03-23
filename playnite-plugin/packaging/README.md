# Packaging

This directory is reserved for packaging assets and release-time metadata for the Gumo Playnite plugin.

Near-term intent:

- keep Playnite packaging logic in-repo
- package from Windows
- use Toolbox or a custom PowerShell script to produce `.pext` artifacts

Current packaging entrypoint:

- `../scripts/package.ps1`
