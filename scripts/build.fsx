#r "paket:
    nuget Fake.Core.Target
    nuget Fake.Core.Trace
    nuget Fake.DotNet.Cli
    nuget Fake.Tools.Git //"

#load "versionNumber.fsx"
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO
open Fake.Core.TargetOperators
open Fake.DotNet


let project = "../src"

let artifacts = __SOURCE_DIRECTORY__ + "/artifacts"

// Lazily install DotNet SDK in the correct version if not available
let dotnetCliPath = lazy DotNet.install DotNet.Versions.FromGlobalJson

let inline dotnetOptions arg = DotNet.Options.lift dotnetCliPath.Value arg

Target.create "Clean" (fun _ ->
        DotNet.exec dotnetOptions "clean" |> ignore
        Shell.cleanDirs [artifacts;]
    )

Target.create "Restore-packages" (fun _ ->       
        [project;]
        |> Seq.iter (DotNet.restore dotnetOptions)    
    )
  
Target.create "Build" (fun _ ->
        [project;]
        |> Seq.iter (DotNet.build dotnetOptions)    
    )   

Target.create "Pack" (fun _ -> 
    let versionNumber = VersionNumber.getFromGit ()

    match versionNumber with
    | Some x -> 
        Trace.log ("version number was something, doing pack")
        Shell.replaceInFiles 
                [("$(BUILDNUMBER)", x)]  
                [sprintf "../Directory.Build.props" ]
        DotNet.pack 
            (fun o -> 
                { o with 
                    OutputPath = Some artifacts 
                }) 
            project
    | None -> Trace.log ("Latest commit has no tag, no nuget created")    
)

"Clean"
==> "Restore-packages"
==> "Build"
==> "Pack"

Target.runOrDefaultWithArguments "Pack"