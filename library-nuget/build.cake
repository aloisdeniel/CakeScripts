var args = new 
{
    Target = Argument("target", "Default"),
    OutputDirectory = Argument("output", "build"),
    NugetApiKey = Argument("nugetApiKey", "none"),
    Configuration = EnvironmentVariable("CONFIGURATION") ?? Argument("configuration", "Release"),
    Version = EnvironmentVariable("APPVEYOR_BUILD_VERSION") ?? Argument<string>("packageVersion"),
};

var buildDirectory = MakeAbsolute(Directory(args.OutputDirectory));

/// TOOLS

Task("Tools.PrintArguments").Does(() => 
{
    Information($"Arguments:");
    Information($"  * Target: {target}");
    Information($"  * OutputDirectory: {args.OutputDirectory}");
    Information($"  * Configuration: {args.Configuration}");
    Information($"  * Version: {args.Version}");
    Information($"  * NugetApiKey: <*>");
});

Task("Tools.Clean").Does(() => 
{
	CleanDirectories(args.OutputDirectory);
	CleanDirectories("./**/bin");
	CleanDirectories("./**/obj");
});

/// BUILD

Task("Build.Solutions")
	.IsDependentOn("Tools.Clean")
    .Does(() =>
{
    var slnFiles = GetFiles("**/*.sln");
    foreach(var sln in slnFiles)
    {
        NuGetRestore(sln);
        MSBuild(sln, c => 
        {
            c.Configuration = args.Configuration;
            c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
        });
    }
});

// NUGET

Task("Nuget.Pack")
	.IsDependentOn("Build.Solutions")
	.Does(() =>
{
    if(!DirectoryExists(buildDirectory.FullPath))
    {
        CreateDirectory(buildDirectory.FullPath);
    }

    var nuspecFiles = GetFiles("**/*.nuspec");
    foreach(var nuspec in nuspecFiles)
    {
        var wd = MakeAbsolute(nuspec).GetDirectory();
        NuGetPack(nuspec, new NuGetPackSettings 
        { 
            Version = args.Version,
            OutputDirectory = buildDirectory.FullPath,
            BasePath = wd,
        });
    }
});

Task("Nuget.Push")
	.IsDependentOn("Nuget.Pack")
	.Does(() =>
{
    var path = buildDirectory + File($"*.{args.Version}.nupkg");
	var packages = GetFiles(path);
	NuGetPush(packages, new NuGetPushSettings 
    {
		Source = "https://www.nuget.org/api/v2/package",
		ApiKey = args.NugetApiKey,
	});
});

Task("Default")
    .IsDependentOn("Tools.PrintArguments")
    .IsDependentOn("Nuget.Pack");

RunTarget(args.Target);