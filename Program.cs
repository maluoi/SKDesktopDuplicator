using StereoKit;
using StereoKit.Framework;
using StereoKit.HolographicRemoting;

const string defaultIp = "192.168.1.231";
int          remoteArg = Array.IndexOf(args, "-remote");
if (remoteArg != -1)
{
	bool ipArg = (remoteArg + 1 < args.Length && args[remoteArg + 1].StartsWith("-") == false);
	SK.AddStepper(new HolographicRemoting(ipArg ? args[remoteArg + 1] : defaultIp));
}

SK.AddStepper<DesktopDuplicator>();
SK.AddStepper<PassthroughFBExt> ();

if (!SK.Initialize(new SKSettings{ appName = "StereoKit Dream Dev", assetsFolder = "Assets" }))
    return;

SK.Run(()=>{
 
});