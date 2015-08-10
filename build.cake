///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// PREPARATION
///////////////////////////////////////////////////////////////////////////////

var projectName = "CakeTryout";

// Get whether or not this is a local build.
var local = BuildSystem.IsLocalBuild;
var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;

// Parse release notes.
var releaseNotes = ParseReleaseNotes("./ReleaseNotes.md");

// Get version.
var semanticVersion = releaseNotes.Version.ToString();

// Define directories.
var sourceDirectory = Directory("./Source");
var buildDirectory = sourceDirectory + Directory(projectName + "/bin") + Directory(configuration);
var outputDirectory = Directory("./Output");
var testResultsDirectory = outputDirectory + Directory("TestResults");
var artifactsDirectory = outputDirectory + Directory("Artifacts");
var solutions = GetFiles("./**/*.sln");
var solutionPaths = solutions.Select(solution => solution.GetDirectory());

// Define files.
var nugetExecutable = "./Tools/nuget.exe"; 

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
    // Executed BEFORE the first task.
    Information("Target: " + target);
    Information("Configuration: " + configuration);
    Information("Is local build: " + local.ToString());
    Information("Is running on AppVeyor: " + isRunningOnAppVeyor.ToString());
    Information("Semantic Version: " + semanticVersion);
});

Teardown(() =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
    }

	CleanDirectories(outputDirectory);
});

Task("Create-Directories")
	.IsDependentOn("Clean")
    .Does(() =>
{
	var directories = new List<DirectoryPath>{ outputDirectory, testResultsDirectory, artifactsDirectory };
	directories.ForEach(directory => 
	{
		if (!DirectoryExists(directory))
		{
			CreateDirectory(directory);
		}
	});
});

Task("Restore-NuGet-Packages")
	.IsDependentOn("Create-Directories")
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Information("Restoring {0}...", solution);
        NuGetRestore(solution, new NuGetRestoreSettings { ConfigFile = solution.GetDirectory() + "/nuget.config" });
    }
});

Task("Patch-Assembly-Info")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
	var assemblyInfoFiles = GetFiles("./**/AssemblyInfo.cs");
	foreach(var assemblyInfoFile in assemblyInfoFiles)
	{
	    CreateAssemblyInfo(assemblyInfoFile, new AssemblyInfoSettings {
			Product = projectName,
			Version = semanticVersion,
			FileVersion = semanticVersion,
			InformationalVersion = semanticVersion,
			Copyright = "Copyright (c) Gaurav Narkhede"
		});
	}
});

Task("Build")
    .IsDependentOn("Patch-Assembly-Info")
    .Does(() =>
{
    // Build all solutions.
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);
        MSBuild(solution, settings => 
            settings.SetPlatformTarget(PlatformTarget.MSIL)
                .WithProperty("TreatWarningsAsErrors","true")
                .WithTarget("Build")
                .SetConfiguration(configuration));
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
	var testDlls = GetFiles("./Source/**/bin/" + configuration + "/*.Tests.dll");
	if(testDlls.Count() > 0)
	{
	    NUnit("./Source/**/bin/" + configuration + "/*.Tests.dll", 
		new NUnitSettings 
			{ 
				OutputFile = testResultsDirectory.Path + "/TestResults.xml", 
				NoResults = true 
			}
		);
	}
});

Task("Create-NuGet-Packages")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
	var nuspecFiles = GetFiles(sourceDirectory.Path + "/**/*.nuspec");
	foreach(var nuspecFile in nuspecFiles)
	{
		var projectFileName = nuspecFile.GetFilenameWithoutExtension() + ".csproj";
		StartProcess(nugetExecutable, new ProcessSettings 
			{ 
				Arguments = "pack -Symbols " + projectFileName + " -Version " + semanticVersion + " -Properties Configuration=" + configuration, 
				WorkingDirectory = nuspecFile.GetDirectory() 
			}
		);
	}

	var nugetPackageFiles = GetFiles(sourceDirectory.Path + "/**/*.nupkg");
	MoveFiles(nugetPackageFiles, artifactsDirectory);
});

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

Task("Package")
    .IsDependentOn("Create-NuGet-Packages");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);