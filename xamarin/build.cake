#addin "Cake.Plist"
#addin "Cake.AndroidAppManifest"

var configFile = "build.xml";

/// ARGUMENTS

var target = Argument("target", "Default");
var branch = EnvironmentVariable("MOBILECENTER_BRANCH") ?? Argument("branch", "master");

var args = new 
{
    Sources = EnvironmentVariable("MOBILECENTER_SOURCE_DIRECTORY") ?? Argument("sources", "."),
    Branch = branch,
    App = new 
    {
        Name = XmlPeek(configFile, $"/config/{branch}/@displayName") ?? Argument<string>("displayName"),
        BundleId = XmlPeek(configFile, $"/config/{branch}/@bundleId") ?? Argument<string>("bundleId"),
        Version = XmlPeek(configFile, $"/config/{branch}/@version") ?? Argument<string>("version"),
        BuildNumber = EnvironmentVariable ("MOBILECENTER_BUILD_ID") ?? Argument("buildId", "0"),
    }
};

/// TOOLS

Task("Tools.PrintArguments").Does(() => 
{
    Information($"* Sources: {args.Sources}");
    Information($"* Branch: {args.Branch}");
    Information($"* App:");
    Information($"  - Name: {args.App.Name}");
    Information($"  - BundleId: {args.App.BundleId}");
    Information($"  - Version: {args.App.Version}");
    Information($"  - BuildNumber: {args.App.BuildNumber}");
});

/// PREPARE

Task("iOS.Prepare").Does(() => 
{
    var manifestFiles = GetFiles(System.IO.Path.Combine(args.Sources, "**/Info.plist"));
    foreach(var appmanifest in manifestFiles)
    {
        Information($"Updating manifest '{appmanifest}' ...");
        dynamic data = DeserializePlist(appmanifest);
        data["CFBundleShortVersionString"] = args.App.Version;
        data["CFBundleVersion"] = args.App.BuildNumber;
        data["CFBundleName"] = args.App.Name;
        data["CFBundleIdentifier"] = args.App.BundleId;
        SerializePlist(appmanifest, data);
    }
});

Task("Android.Prepare").Does(() => 
{
    var manifestFiles = GetFiles(System.IO.Path.Combine(args.Sources, "**/Properties/AndroidManifest.xml"));
    foreach(var appmanifest in manifestFiles)
    {
        Information($"Updating manifest '{appmanifest}' ...");
        var path = new FilePath(appmanifest.FullPath);
        var manifest = DeserializeAppManifest(path);
        manifest.PackageName = args.App.BundleId;
        manifest.VersionName = args.App.Version;
        manifest.VersionCode = int.Parse(args.App.BuildNumber);
        manifest.ApplicationLabel = args.App.Name;
        manifest.Debuggable = false;
        SerializeAppManifest(path, manifest);
    }
});

Task("Default")
	.IsDependentOn("Tools.PrintArguments");

RunTarget(target);