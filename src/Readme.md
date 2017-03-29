# Startup code for F# projects

## Initialize

Download the gist using:

```PowerShell
Get-Gist -Id e9af99313640e44145b90733e9358cba -Destination DELETEME -Verbose
````

Then:

- Run:
  ```PowerShell
  gci DELETEME -Recurse |% { Get-Content $_ | Out-File $_ -Encoding ascii }
  ```
  
- Copy all files from the `DELETEME` directory except this `README.md` to the target directory.

- Run `SetupPaket.ps1` if necessary

- Adjust the `paket.dependencies` file, i.e. remove those packages that are not necessary.

- Run `.\.paket\paket.exe restore` to download the packages.

- Delete the `DELETEME` directory

- Adapt the `build.fsx` script
