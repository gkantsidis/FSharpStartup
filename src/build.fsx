(*
 * Generic build template using Fake
 *
 * It follows the general template from https://github.com/fsprojects/ProjectScaffold/blob/master/build.template
 *)

#r @"packages/__commandlinetools/FAKE/tools/FakeLib.dll"

// #r @"packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#I @"packages/Newtonsoft.Json/lib/net45"
#r "Newtonsoft.Json.dll"

open System
open System.IO
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open Newtonsoft.Json


// Default target configuration
let configuration = "Release"

(*
 * Read configuration variables
 *)

let projectTopDir = __SOURCE_DIRECTORY__
let projectDescriptionFile = "Project.Description.json"

type ProjectDescription =
    {
        Summary : string
        Description : string
        Authors : string list
        Tags : string list
        RepoHome : string
    }

let project =
    let fullname = Path.Combine (projectTopDir, projectDescriptionFile)
    if File.Exists fullname then
        let project = JsonConvert.DeserializeObject<ProjectDescription>(File.ReadAllText(fullname))
        Some project
    else
        None

let release =
    let file = Path.Combine (projectTopDir, "RELEASE_NOTES.md")
    LoadReleaseNotes file

(*
 * Configure output directories
 *)

let outputDir =
        let desiredOutputDir = Environment.GetEnvironmentVariable("OUTPUT_DIRECTORY")
        if System.String.IsNullOrWhiteSpace(desiredOutputDir) then
                __SOURCE_DIRECTORY__
        else
                desiredOutputDir

let buildDir = Path.Combine(outputDir, "build")
let testDir = Path.Combine(outputDir, "test")
let deployDir= Path.Combine(outputDir, "deploy")

(*
 * Helper code
 *)

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

let hasFxCopCmd =
    let useFxCop = getBuildParamOrDefault "UseFxCop" "TRUE"
    match useFxCop.ToUpperInvariant() with
    | "TRUE"  -> true
    | "FALSE" -> false
    | _       -> false


(*
 * Build rules
 *)

Target "Clean" (fun _ ->
    !! solutionFile |> MSBuildReleaseExt "" vsProjProps "Clean" |> ignore
    CleanDirs ["bin"; "temp"; "docs/output"]
)

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product project
          Attribute.Description summary
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion
          Attribute.Configuration configuration ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> CreateFSharpAssemblyInfo (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

// Copies binaries from default VS location to expected bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs
Target "CopyBinaries" (fun _ ->
    !! "src/**/*.??proj"
    -- "src/**/*.shproj"
    |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) </> "bin" </> configuration, "bin" </> (System.IO.Path.GetFileNameWithoutExtension f)))
    |>  Seq.iter (fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
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
