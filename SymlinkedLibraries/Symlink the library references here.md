# The following libraries should be symlinked in this folder:

0Harmony.dll
BaseX.dll
CodeX.dll
CSCore.dll
FrooxEngine.dll
NeosModLoader.dll

# To symlink a DLL in Windows, use the following PowerShell cmdlet:

New-Item -ItemType SymbolicLink -Path "<Path to Symlink in Repo>" -Target "<Path to DLL in Neos' folder>"