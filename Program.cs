using StereoKit;
using StereoKit.Framework;

SK.AddStepper<DesktopDuplicator>();
SK.AddStepper<PassthroughFBExt> ();

if (!SK.Initialize(new SKSettings{ appName = "StereoKit Dream Dev", assetsFolder = "Assets" }))
    return;

SK.Run(()=>{
 
});