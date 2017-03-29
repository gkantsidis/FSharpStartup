[CmdletBinding()]
param(
    [ValidateScript({
        Test-Path -Path $_ -PathType Container
    })]
    [string]
    $Path = $PSScriptRoot,

    [switch]
    $SetupGit
)

Push-Location -Path $Path
if (-not (Test-Path -Path .paket -PathType Container)) {
    Write-Verbose -Verbose "Creating directory .paket"
    New-Item -Name ".paket" -ItemType Directory
}

$paketBootstrapper = Join-Path -Path ".paket" -ChildPath "paket.bootstrapper.exe"
if (-not (Test-Path -Path $paketBootstrapper -PathType Leaf)) {
    Write-Verbose -Message "Downloading Paket bootstrapper"
    Invoke-WebRequest -Uri https://github.com/fsprojects/Paket/releases/download/4.0.11/paket.bootstrapper.exe -OutFile $paketBootstrapper
    if ($SetupGit) {
        git add -f $paketBootstrapper
    }
}

$paket = Join-Path -Path ".paket" -ChildPath "paket.exe"
if (-not (Test-Path -Path $paket -PathType Leaf)) {
    Write-Verbose -Message "Retrieving paket executable"
    . $paketBootstrapper
}

if ($SetupGit -and (-not (Test-Path -Path .gitignore -PathType Leaf))) {
    $ignore = @"
.paket/paket.exe
packages/    
"@

    Write-Verbose -Message "Creating standard ignore file"
    $ignore | Out-File -FilePath ".gitconfig" -Encoding ascii
}

Pop-Location
