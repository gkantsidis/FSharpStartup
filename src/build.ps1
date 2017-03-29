[CmdletBinding()]
param(
    [ValidateScript( { Test-Path -Path $_ -PathType Leaf })]
    [string]
    $BuildScript = "build.fsx",

    [switch]
    $IgnoreFxCop,

    [int]
    $Parallel = 1
)

DynamicParam {                                                                                                                         
    # Set the dynamic parameters' name                                                                                                 
    $ParameterName = 'Target'                                                                                                         
                                                                                                                                       
    # Create the dictionary                                                                                                            
    $RuntimeParameterDictionary = New-Object -TypeName System.Management.Automation.RuntimeDefinedParameterDictionary                  
                                                                                                                                       
    # Create the collection of attributes                                                                                              
    $AttributeCollection = New-Object -TypeName System.Collections.ObjectModel.Collection[System.Attribute]                            
                                                                                                                                       
    # Create and set the parameters' attributes                                                                                        
    $ParameterAttribute = New-Object -TypeName System.Management.Automation.ParameterAttribute                                         
    $ParameterAttribute.Mandatory = $false                                                                                              
    $ParameterAttribute.Position = 1                                                                                                   
                                                                                                                                       
    # Add the attributes to the attributes collection
    $AttributeCollection.Add($ParameterAttribute)
                                                                                                                                       
    # Generate and set the ValidateSet
    if (Test-Path -Path $PSScriptRoot\packages\FAKE\tools\FAKE.exe) {
        $targetsraw = . "$PSScriptRoot\packages\FAKE\tools\FAKE.exe" --listTargets
        $targets = $targetsraw | ForEach-Object -Process { if ($_ -match "^\s+-\s(\w+)\s+") { $Matches[1] }}

        $arrSet = $targets
        $ValidateSetAttribute = New-Object System.Management.Automation.ValidateSetAttribute($arrSet)
        $AttributeCollection.Add($ValidateSetAttribute)
    } else {

    }
                                                                                      
    # Create and return the dynamic parameter                                                                                          
    $RuntimeParameter = New-Object System.Management.Automation.RuntimeDefinedParameter($ParameterName, [string], $AttributeCollection)
    $RuntimeParameterDictionary.Add($ParameterName, $RuntimeParameter)                                                                 
    return $RuntimeParameterDictionary                                                                                                 
}                                                                                                                                       

Begin {
    $paket = Join-Path -Path $PSScriptRoot -ChildPath ".paket" | `
             Join-Path -ChildPath "paket.exe"
    $bootstrap = Join-Path -Path $PSScriptRoot -ChildPath ".paket" | `
                 Join-Path -ChildPath "paket.bootstraper.exe"

    $Target = $PsBoundParameters[$ParameterName]
}

Process{
    if ( (-not (Test-Path -Path $paket -PathType Leaf )) -and
        (-not (Test-Path -Path $bootstrap -PathType Leaf )) )
    {
        Write-Error -Message "Cannot find paket."
        throw "Paket is not installed in this project"
    }

    if (-not (Test-Path -Path $paket -PathType Leaf )) {
        Write-Verbose -Message "Paket execututable does not exist; will try to download"
        Invoke-Expression -Command $bootstrap
    }

    if (-not (Test-Path -Path $paket -PathType Leaf )) {
        Write-Verbose -Message "Cannot download paket.exe; aborting"
        throw "Cannot find paket.exe"
    }

    Invoke-Expression -Command ("{0} restore" -f $paket)
    if (-not $?) {
        Write-Verbose "Failed to restore packages; aborting"
        throw "Paket failed to restore packages"
    }

    $fake = Join-Path -Path $PSScriptRoot -ChildPath 'packages' | `
            Join-Path -ChildPath '__CommandLineTools' | `
            Join-Path -ChildPath 'FAKE' | `            
            Join-Path -ChildPath 'tools' | `
            Join-Path -ChildPath 'Fake.exe'

    if (-not (Test-Path -Path $fake -PathType Leaf)) {
        Write-Error -Message "Cannot find fake.exe in $fake"
        throw [System.IO.FileNotFoundException]"File $fake does not exist"
    }

    if ($null -eq (Get-Command -Name FxCopCmd -ErrorAction SilentlyContinue)) {
        Write-Verbose -Message "Cannot find FxCopCmd in path; will try to find and add"

        if (Get-Command -Name es -ErrorAction SilentlyContinue) {
            [string[]]$candidates = es -r "\FxCopCmd.exe$"
            if ( ($candidates -ne $null) -and
                 ($candidates.Length -gt 0) -and
                 ($candidates[0] -ne "Everything IPC window not found, IPC unavailable.")
               )
            {
                [System.IO.FileSystemInfo[]]$candidates = $candidates | Get-Item | Sort-Object -Property LastWriteTime -Descending
            }
            else {
                [System.IO.FileSystemInfo[]]$candidates = @()
            }

            if ($candidates.Length -gt 0) {
                $candidate = $candidates[0]
                Write-Verbose -Message "Candidate is $candidate"                
                $path = $candidate.DirectoryName
                Write-Verbose -Message "Will use FxCopCmd from $path"
                $Env:Path += ";$path"
            } else {
                Write-Error -Message "Cannot find FxCopCmd.exe; please add to path"
                $IgnoreFxCop = $true            
            }
        } else {
            Write-Error -Message "Cannot find FxCopCmd.exe; please add to path"
            $IgnoreFxCop = $true
        }    
    } else {
        Write-Verbose -Message "FxCopCmd.exe exists in path"
    }

    if (-not (Test-Path -Path "test" -PathType Container)) {
        New-Item -Name test -ItemType Directory
    }

    [string[]]$extraArgs = @()

    if ($Parallel -gt 1) {
        $extraArgs += "parallel-jobs={0}" -f $Parallel        
    }

    if ($IgnoreFxCop) {
        $extraArgs += "UseFxCop=FALSE"
    }

    [string]$buildargs = [System.String]::Join(' ', $extraArgs)
    Write-Verbose -Message "Extra arguments: $buildargs"

    if ([System.String]::IsNullOrWhiteSpace($Target)) {
        $command = "{0} {1} {2}" -f $fake,$BuildScript,$buildargs
    } else {
        $command = "{0} {1} {2} {3}" -f $fake,$BuildScript,$Target,$buildargs
    }

    Write-Verbose -Message "Command to run: $command"
    Invoke-Expression -Command $command
}

End {

}
