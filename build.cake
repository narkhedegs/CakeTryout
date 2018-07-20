#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#tool nuget:?package=GitVersion.CommandLine

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var version = GitVersion();

///////////////////////////////////////////////////////////////////////////////
// PREPARATION
///////////////////////////////////////////////////////////////////////////////

// Get whether or not this is a local build.
var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;

// Define directories.
var toolsDirectory = Directory("./Tools");
var outputDirectory = Directory("./Output");
var temporaryDirectory = Directory("./Temporary");
var testResultsDirectory = outputDirectory + Directory("TestResults");
var artifactsDirectory = outputDirectory + Directory("Artifacts");
var solutionFile = GetFiles("./**/*.sln").First();

// Define files.
var nugetExecutable = "./Tools/nuget.exe"; 

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    // Executed BEFORE the first task.
    Information("Target: " + target);
    Information("Configuration: " + configuration);
    Information("Is local build: " + isLocalBuild.ToString());
    Information("Is running on AppVeyor: " + isRunningOnAppVeyor.ToString());    
});

Teardown(context =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");    
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    CleanDirectories(solutionFile.FullPath + "/**/bin/" + configuration);
    CleanDirectories(solutionFile.FullPath + "/**/obj/" + configuration);
	CleanDirectories(outputDirectory);
	CleanDirectories(temporaryDirectory);
});

Task("Create-Directories")
	.IsDependentOn("Clean")
    .Does(() =>
{
	var directories = new List<DirectoryPath>{ outputDirectory, testResultsDirectory, artifactsDirectory, temporaryDirectory };

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
    DotNetCoreRestore();
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(solutionFile.FullPath, settings =>
        settings.SetConfiguration(configuration));
    }
    else
    {
      // Use XBuild
      XBuild(solutionFile.FullPath, settings =>
        settings.SetConfiguration(configuration));
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    var unitTestProjectFiles = GetFiles("./**/*.UnitTests.csproj");

    foreach(var unitTestProjectFile in unitTestProjectFiles)
    {
        DotNetCoreTest(unitTestProjectFile.FullPath);
    }
});

Task("Create-NuGet-Packages")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    var projectFiles = GetFiles("./**/*.csproj").Where(projectFile => !projectFile.GetFilename().FullPath.ToLower().Contains("unittests"));

    foreach(var projectFile in projectFiles)
    {
        DotNetCorePack(projectFile.FullPath, new DotNetCorePackSettings{
            Configuration = configuration,
            OutputDirectory = artifactsDirectory
        });
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);