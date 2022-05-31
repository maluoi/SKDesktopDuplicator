using System;
using Vortice.DXGI;
using Vortice.Direct3D11;
using SharpGen.Runtime;
using System.Runtime.InteropServices;
using System.Collections;

namespace StereoKit.Framework
{
	public class DesktopDuplicator : IStepper
	{
		bool enabled = true;
		public bool Enabled => enabled;

		IDXGIOutputDuplication duplication;
		Tex                    duplicationTex;
		Vec2                   duplicationPointer;
		Tex                    duplicationPointerTex;

		Material desktopBlitMat;
		Mesh     desktopMesh;
		Tex      desktopTex;
		Material desktopMaterial;

		Pose desktopPose       = new Pose(0, 0, -0.5f, Quat.LookDir(0, 0, 1));
		Pose desktopPoseSmooth = new Pose(0, 0, -0.5f, Quat.LookDir(0, 0, 1));
		bool wasInteracting = false;

		public Pose Pose { get => desktopPose; set => desktopPoseSmooth = desktopPose = value; }

		public bool Initialize()
		{
			desktopMesh    = Mesh.GeneratePlane(Vec2.One, V.XYZ( 0,0,1 ), V.XYZ(0,1,0));
			desktopTex     = new Tex(TexType.Rendertarget);
			duplicationTex = new Tex();
			desktopTex    .AddressMode = TexAddress.Clamp;
			duplicationTex.AddressMode = TexAddress.Clamp;

			desktopMaterial = Material.Unlit.Copy();
			desktopBlitMat  = new Material(Shader.FromFile("DesktopBlit.hlsl"));
			desktopMaterial[MatParamName.DiffuseTex] = desktopTex;
			desktopBlitMat["source"] = duplicationTex;

			LoadSettings();

			if (enabled)
				Start();

			return true;
		}

		public void Step()
		{
			if (duplication == null) return;

			// Update our desktop texture
			if (DuplicationNextFrame(duplication, duplicationTex, ref duplicationPointer, ref duplicationPointerTex)) {
				if (desktopTex.Width  != duplicationTex.Width ||
					desktopTex.Height != duplicationTex.Height) {
					desktopTex.SetSize(duplicationTex.Width, duplicationTex.Height);
					CreateScreenMesh(desktopTex, 1, desktopMesh);
				}
				desktopBlitMat["pointer"] = duplicationPointer;
				//Log.Info(duplicationPointer.ToString());
				if (duplicationPointerTex != null)
				{
					desktopBlitMat["cursor_size"] = new Vec2(duplicationPointerTex.Width / (float)desktopTex.Width, duplicationPointerTex.Height / (float)desktopTex.Height);
					desktopBlitMat["cursor"] = duplicationPointerTex;
				}
				Renderer.Blit(desktopTex, desktopBlitMat);
			}

			// prepare a little grab handle underneath the desktop texture
			float  size   = desktopTex.Height * 0.0004f;
			Bounds bounds = desktopMesh.Bounds;
			bounds.center     = (bounds.center + V.XYZ( 0, -bounds.dimensions.y / 2, bounds.dimensions.z / 2 )) * size + V.XYZ(0,-0.02f,-0.02f);
			bounds.dimensions = V.XYZ(0.2f, 0.02f, 0.02f);

			// UI for the grab handle
			Hierarchy.Push(World.HasBounds ? World.BoundsPose.ToMatrix() : Matrix.T(0,-1.5f,0));
				//UI.EnableFarInteract = false;
				bool interacting = UI.Handle("Desktop", ref desktopPose, bounds, true);
				//UI.EnableFarInteract = true;

				// Smooth out the motion extra for nicer placement
				desktopPoseSmooth = Pose.Lerp(desktopPoseSmooth, desktopPose, 4 * Time.Elapsedf);
				desktopPose = desktopPoseSmooth;
				desktopMesh.Draw(desktopMaterial, desktopPose.ToMatrix(size));
			Hierarchy.Pop();

			// Save the pose the file if we just stopped interacting with it!
			if (interacting == false && wasInteracting == true)
			{
				SaveSettings();
			}
			wasInteracting = interacting;
		}

		public void Shutdown()
		{
			duplication?.ReleaseFrame();
			duplication = null;
		}

		public void Start()
		{
			if (enabled == false)
			{
				enabled = true;
				SaveSettings();
			}
			duplication = DuplicationInit();
		}
		public void Stop()
		{
			if (enabled == true)
			{
				enabled = false;
				SaveSettings();
			}
			Shutdown();
		}

		void SaveSettings()
		{
			Platform.WriteFile("DesktopDuplicator.ini", $"{enabled} {Pose.position.x} {Pose.position.y} {Pose.position.z} {Pose.orientation.x} {Pose.orientation.y} {Pose.orientation.z} {Pose.orientation.w}");
		}

		void LoadSettings()
		{
			string[] at = Platform.ReadFileText("DesktopDuplicator.ini")?.Split(' ');
			if (at != null && at.Length == 8)
			{
				enabled = bool.Parse(at[0]);
				Pose    = new Pose(
					new Vec3(float.Parse(at[1]), float.Parse(at[2]), float.Parse(at[3])),
					new Quat(float.Parse(at[4]), float.Parse(at[5]), float.Parse(at[6]), float.Parse(at[7])));
			}
		}

		IDXGIOutputDuplication DuplicationInit()
		{
			ID3D11Device d3dDevice = new ID3D11Device(Backend.D3D11.D3DDevice);
			IDXGIDevice  device    = d3dDevice.QueryInterface<IDXGIDevice>();
			IDXGIAdapter adapter   = device.GetParent<IDXGIAdapter>();

			Result hr = adapter.EnumOutputs(0, out IDXGIOutput output);
			IDXGIOutput1 output1 = output.QueryInterface<IDXGIOutput1>();
			return output1.DuplicateOutput(d3dDevice);
		}

		IntPtr pointerMem;
		int    pointerMemSize = 0;
		byte[] pointerBytes;
		bool DuplicationNextFrame(IDXGIOutputDuplication duplication, Tex frameTex, ref Vec2 pointerAt, ref Tex pointerTex)
		{
			if (duplication == null) return false;

			if (frameTex.AssetState == AssetState.Loaded)
			{
				duplication.ReleaseFrame();
				frameTex.SetNativeSurface(IntPtr.Zero);
			}

			Result hr = duplication.AcquireNextFrame(0, out var info, out IDXGIResource resource);
			if (hr == Result.WaitTimeout) return false;
			if (hr.Failure) return false;

			ID3D11Texture2D desktopImage = resource.QueryInterface<ID3D11Texture2D>();
			frameTex.SetNativeSurface(desktopImage.NativePointer, TexType.ImageNomips);

			if (info.PointerPosition.Visible)
			{
				if (pointerMemSize < info.PointerShapeBufferSize)
				{
					if (pointerMemSize > 0) Marshal.FreeHGlobal(pointerMem);
					pointerMemSize = info.PointerShapeBufferSize;
					pointerMem     = Marshal.AllocHGlobal(pointerMemSize);
				}
				hr = duplication.GetFramePointerShape(info.PointerShapeBufferSize, pointerMem, out int req, out var shapeInfo);
				if (hr.Success && info.PointerShapeBufferSize > 0)
				{
					int width  = shapeInfo.Width;
					int height = shapeInfo.Type == 0x1 ? shapeInfo.Height/2 : shapeInfo.Height;
					
					if (pointerBytes == null || pointerBytes.Length < width * height * 4)
						pointerBytes = new byte[width * height * 4];
					if (shapeInfo.Type == 0x1) // monochrome 1bpp
					{
						byte[] srcData = new byte[shapeInfo.Pitch * shapeInfo.Height];
						Marshal.Copy(pointerMem, srcData, 0, srcData.Length);
						
						int xorOff = height * shapeInfo.Pitch;
						BitArray bits   = new BitArray(srcData);
						for (int y = 0; y < height; y++)
						{
							int yOff = y * shapeInfo.Pitch;
							for (int x = 0; x < width; x++)
							{
								byte mask = (byte)(1 << (7 - (x % 8)));
								bool and_bit = (srcData[x / 8 + yOff         ] & mask) > 0;
								bool xor_bit = (srcData[x / 8 + yOff + xorOff] & mask) > 0;
								int  i       = (x + y * width) * 4;
								byte col, alpha;
								if (and_bit) {
									if (xor_bit) { col = 255; alpha = 255; }
									else         { col = 0;   alpha = 0;   }
								} else {
									if (xor_bit) { col = 255; alpha = 255; }
									else         { col = 0;   alpha = 255; }
								}
								pointerBytes[i    ] = col;
								pointerBytes[i + 1] = col;
								pointerBytes[i + 2] = col;
								pointerBytes[i + 3] = alpha;
							}
						}
					}
					else if (shapeInfo.Type == 0x2)
					{
						Marshal.Copy(pointerMem, pointerBytes, 0, shapeInfo.Width * shapeInfo.Height * 4);
					}
					else
					{
						Log.Warn($"Unknown shape info: {shapeInfo.Type}");
					}

					if (pointerTex == null)
						pointerTex = new Tex(TexType.ImageNomips|TexType.Dynamic, TexFormat.Rgba32);
					pointerTex.SetColors(width, height, pointerBytes);
				}

				pointerAt = new Vec2(
					(info.PointerPosition.Position.X) / (float)frameTex.Width,
					(info.PointerPosition.Position.Y) / (float)frameTex.Height);
			}

			return true;
		}


		void CreateScreenMesh(Tex forTex, float curveRadius, Mesh mesh) {
			int cols = 32;
			int rows = 2;

			float aspect =  forTex.Width / (float)forTex.Height;
			float angle  = aspect / curveRadius;

			Vertex[] verts = new Vertex[cols * rows];
			uint  [] inds  = new uint[ (cols-1) * (rows-1) * 6];
			for (int y = 0; y < rows; y++)
			{
				float yp = y / (float)(rows - 1);
				for (int x = 0; x < cols; x++)
				{
					float xp   = x / (float)(cols - 1);
					float curr = (xp - 0.5f) * angle;

					verts[x + y * cols] = new Vertex( V.XYZ(SKMath.Sin(curr) * curveRadius, yp - 0.5f, SKMath.Cos(curr)*curveRadius -curveRadius), -Vec3.Forward, V.XY(1-xp,1-yp));

					if (x < cols-1 && y < rows-1) {
						int ind = (x+y*cols)*6;
						inds[ind  ] = (uint)((x  ) + (y  ) * cols);
						inds[ind+1] = (uint)((x+1) + (y+1) * cols);
						inds[ind+2] = (uint)((x+1) + (y  ) * cols);

						inds[ind+3] = (uint)((x  ) + (y  ) * cols);
						inds[ind+4] = (uint)((x  ) + (y+1) * cols);
						inds[ind+5] = (uint)((x+1) + (y+1) * cols);
					}
				}
			}

			mesh.SetVerts(verts);
			mesh.SetInds(inds);
		}
	}
}
