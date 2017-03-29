#r @"packages/__commandlinetools/FAKE/tools/FakeLib.dll"
open Fake

let buildDir = "../build/"
let testDir = "../test/"
let deployDir= "../deploy/"

let hasFxCopCmd =
        let useFxCop = getBuildParamOrDefault "UseFxCop" "TRUE"
        match useFxCop.ToUpperInvariant() with
        | "TRUE"  -> true
        | "FALSE" -> false
        | _       -> false


Target "Clean" (fun _ ->
        CleanDir buildDir
)

Target "MYTARGET" (fun _ ->
        !! "MYTARGET/MYTARGET.fsproj"
        |> MSBuildRelease buildDir "Build"
        |> Log "Parser build output:"
)

Target "Default" (fun _ ->
        trace "Build completed"
)

Target "FxCop" (fun () ->  
    !! (buildDir + @"\**\*.dll") 
    ++ (buildDir + @"\**\*.exe") 
    |> FxCop 
        (fun p -> 
            {p with 
              // override default parameters
              ReportFileName = testDir + "FXCopResults.xml"
              //FailOnError = FxCopErrorLevel.CriticalWarning
              //ProjectFile = "ErrorAnalysis.fxcop"
              CustomDictionary = "CustomDictionary.xml"
              ToolPath = "FxCopCmd.exe"})
)

Target "CopyCustomDictionary" (fun _ ->
        Copy buildDir [ "CustomDictionary.xml" ]
)

Target "Build" DoNothing
Target "Rebuild" DoNothing

// If clean runs, it should be *before* any other task
"Clean" ?=> "MYTARGET"
"Clean" ?=> "Build"

// Rebuild requires Clean to run first
"Clean" ==> "Rebuild"

// Build requires building each task
"MYTARGET" ==> "Build"

"CopyCustomDictionary" ==> "FxCop"
"CopyCustomDictionary" ==> "Build"           // This is for custom FxCop runs, e.g. from UI
"Build" ==> "FxCop"
"Build" ==> "Rebuild"
"Build" ==> "Default"
"FxCop" =?> ("Default", hasFxCopCmd)

RunTargetOrDefault "Default"
