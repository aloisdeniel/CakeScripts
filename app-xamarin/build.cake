#addin "Cake.Plist"
#addin "Cake.AndroidAppManifest"
#addin "Cake.MobileCenter"

var configFile = "build.xml";

/// ARGUMENTS

var target = Argument("target", "Default");
var branch = EnvironmentVariable("MOBILECENTER_BRANCH") ?? Argument("branch", "master");
var buildId = EnvironmentVariable ("MOBILECENTER_BUILD_ID") ?? Argument("buildId", "0");
var output = EnvironmentVariable("MOBILECENTER_OUTPUT_DIRECTORY") ?? Argument("output", "temp");

var args = new 
{
    Sources = EnvironmentVariable("MOBILECENTER_SOURCE_DIRECTORY") ?? Argument("sources", "."),
    Output = output,
    Branch = branch,
    Android = new 
    {
        Name = XmlPeek(configFile, $"/config/android/{branch}/@displayName") ?? Argument<string>("android_displayName"),
        BundleId = XmlPeek(configFile, $"/config/android/{branch}/@bundleId") ?? Argument<string>("android_bundleId"),
        Version = XmlPeek(configFile, $"/config/android/{branch}/@version") ?? Argument<string>("android_version"),
        BuildNumber = buildId,
        UITestOutput = "",
        MobileCenter = new 
        {
            Token = Argument("android_mc_token", "none"),
            AppId = XmlPeek(configFile, $"/config/android/mobile-center/@appid") ?? Argument("android_mc_appid", "none"),
            Locale = XmlPeek(configFile, $"/config/android/mobile-center/@locale") ?? Argument("android_mc_locale", "en_US"),
            DeviceSet = XmlPeek(configFile, $"/config/android/mobile-center/@deviceSet") ?? Argument("android_mc_deviceset", "none"),
        },
    },
    iOS = new 
    {
        Name = XmlPeek(configFile, $"/config/ios/{branch}/@displayName") ?? Argument<string>("ios_displayName"),
        BundleId = XmlPeek(configFile, $"/config/ios/{branch}/@bundleId") ?? Argument<string>("ios_bundleId"),
        Version = XmlPeek(configFile, $"/config/ios/{branch}/@version") ?? Argument<string>("ios_version"),
        BuildNumber = buildId,
        MobileCenter = new 
        {
            Token = Argument("ios_mc_token", "none"),
            AppId = XmlPeek(configFile, $"/config/ios/mobile-center/@appid") ?? Argument("ios_mc_appid", "none"),
            Locale = XmlPeek(configFile, $"/config/ios/mobile-center/@locale") ?? Argument("ios_mc_locale", "en_US"),
            DeviceSet = XmlPeek(configFile, $"/config/ios/mobile-center/@deviceSet") ?? Argument("ios_mc_deviceset", "none"),
        },
    }
};

/// TOOLS

Task("Tools.PrintArguments").Does(() => 
{
    Information($"Arguments:");
    Information($"  * Target: {target}");
    Information($"  * Sources: {args.Sources}");
    Information($"  * Branch: {args.Branch}");
    Information($"  * Android:");
    Information($"      - Name: {args.Android.Name}");
    Information($"      - BundleId: {args.Android.BundleId}");
    Information($"      - Version: {args.Android.Version}");
    Information($"      - BuildNumber: {args.Android.BuildNumber}");
    Information($"      - MobileCenter:");
    Information($"          - Token: <*>");
    Information($"          - AppId: {args.Android.MobileCenter.AppId}");
    Information($"          - Locale: {args.Android.MobileCenter.Locale}");
    Information($"          - DeviceSet: {args.Android.MobileCenter.DeviceSet}");
    Information($"  * iOS:");
    Information($"      - Name: {args.iOS.Name}");
    Information($"      - BundleId: {args.iOS.BundleId}");
    Information($"      - Version: {args.iOS.Version}");
    Information($"      - BuildNumber: {args.iOS.BuildNumber}");
    Information($"      - MobileCenter:");
    Information($"          - Token: <*>");
    Information($"          - AppId: {args.iOS.MobileCenter.AppId}");
    Information($"          - Locale: {args.iOS.MobileCenter.Locale}");
    Information($"          - DeviceSet: {args.iOS.MobileCenter.DeviceSet}");
});

Task("Tools.Help")
    .IsDependentOn("Tools.PrintArguments")
    .Does(() => 
{
    Information($"Available targets:");
    Information($"  * 'iOS.Prepare': prepare iOS package");
    Information($"  * 'iOS.Build': build the iOS package");
    Information($"  * 'Android.Prepare': prepare Android package");
    Information($"  * 'Android.Build': build the Android package");
    Information($"  * 'MobileCenter.iOS.Test': executes iOS UI Tests from Mobile Center");
    Information($"  * 'MobileCenter.Android.Test': executes Android UI Tests from Mobile Center");
});

Task("Tools.Clean").Does(() => 
{
    CleanDirectory($"temp");
});

/// PREPARE

Task("iOS.Prepare").Does(() => 
{
    var manifestFiles = GetFiles(System.IO.Path.Combine(args.Sources, "**/Info.plist"));
    foreach(var appmanifest in manifestFiles)
    {
        Information($"Updating manifest '{appmanifest}' ...");
        dynamic data = DeserializePlist(appmanifest);
        data["CFBundleShortVersionString"] = args.iOS.Version;
        data["CFBundleVersion"] = args.iOS.BuildNumber;
        data["CFBundleName"] = args.iOS.Name;
        data["CFBundleIdentifier"] = args.iOS.BundleId;
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
        manifest.PackageName = args.Android.BundleId;
        manifest.VersionName = args.Android.Version;
        manifest.VersionCode = int.Parse(args.Android.BuildNumber);
        manifest.ApplicationLabel = args.Android.Name;
        manifest.Debuggable = false;
        SerializeAppManifest(path, manifest);
    }
});

/// MOBILECENTER : TEST

Task("MobileCenter.Test.Build")
.Does(() => 
{
    var dir = MakeAbsolute(Directory(args.Output) + Directory("UITest"));
    var testCsproj = GetFiles(System.IO.Path.Combine(dir.FullPath, "**/*.UITests.csproj")).FirstOrDefault();

	MSBuild(testCsproj, c => 
	{
		c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
		c.SetConfiguration("Release").WithProperty("OutputPath", dir.FullPath);
	});
});

Task("MobileCenter.Test.iOS")
  .IsDependentOn("MobileCenter.Test.Build")
  .Does(() =>
{
    var dir = MakeAbsolute(Directory(args.Output) + Directory("UITest"));
    var ipaFiles = GetFiles(System.IO.Path.Combine(args.Output, "**/*.ipa"));

    foreach(var ipa in ipaFiles)
    {
        MobileCenterTestRunUitest(new MobileCenterTestRunUitestSettings 
        { 
            App = args.iOS.MobileCenter.AppId,
            AppPath = ipa.FullPath,
            BuildDir = dir.FullPath,
            TestSeries = args.Branch,
            Locale = args.iOS.MobileCenter.Locale,
            Devices = args.iOS.MobileCenter.DeviceSet,
            Token = args.iOS.MobileCenter.Token,
        });
    }
});

Task("MobileCenter.Test.Android")
  .IsDependentOn("MobileCenter.Test.Build")
  .Does(() =>
{
    var dir = MakeAbsolute(Directory(args.Output) + Directory("UITest"));
    var apkFiles = GetFiles(System.IO.Path.Combine(args.Output, "**/*.apk"));

    foreach(var apk in apkFiles)
    {
        MobileCenterTestRunUitest(new MobileCenterTestRunUitestSettings 
        { 
            App = args.Android.MobileCenter.AppId,
            AppPath = apk.FullPath,
            BuildDir = dir.FullPath,
            TestSeries = args.Branch,
            Locale = args.Android.MobileCenter.Locale,
            Devices = args.Android.MobileCenter.DeviceSet,
            Token = args.Android.MobileCenter.Token,
        });
    }
});

/// MOBILECENTER : MAIN

Task("MobileCenter.Pre.iOS")
  .IsDependentOn("iOS.Prepare");

Task("MobileCenter.Pre.Android")
  .IsDependentOn("Android.Prepare");

Task("MobileCenter.Post.iOS")
  .IsDependentOn("MobileCenter.Test.iOS");

Task("MobileCenter.Post.Android")
  .IsDependentOn("MobileCenter.Test.Android");

/// RUN

Task("Default")
    .IsDependentOn("Tools.Help");

RunTarget(target);