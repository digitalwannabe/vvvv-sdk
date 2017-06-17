#region usings
using System;
using System.ComponentModel.Composition;
using System.Text;
using System.Collections.Generic;
using System.Linq;


using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;

using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "ComputeNormals", Category = "3d", Author="digitalWannabe", Credits = "libigl ETH Zurich, lichterloh",Help = "Compute  face/vertex/corner normals for triangulated surface mesh", Tags = "cgal, normals, lichterloh, dope")]
	#endregion PluginInfo
	public unsafe class C3dComputeNormalsNode : IPluginEvaluate, IDisposable
    {
        // dll loading code copied from tonfilm.s VVVV.OpenVR
        private class UnsafeNativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetDllDirectory(string lpPathName);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern int GetDllDirectory(int bufsize, StringBuilder buf);

            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string librayName);
        }

        public static string CoreAssemblyNativeDir
        {
            get
            {
                //get the full location of the assembly with DaoTests in it
                string fullPath = Assembly.GetAssembly(typeof(C3dComputeNormalsNode)).Location;
                var subfolder = Environment.Is64BitProcess ? "x64" : "x86";

                //get the folder that's in
                return Path.Combine(Path.GetDirectoryName(fullPath), subfolder);
            }
        }

        public static void LoadDllFile(string dllfolder, string libname)
        {
            var currentpath = new StringBuilder(255);
            var length = UnsafeNativeMethods.GetDllDirectory(currentpath.Length, currentpath);

            // use new path
            var success = UnsafeNativeMethods.SetDllDirectory(dllfolder);

            if (success)
            {
                var handle = UnsafeNativeMethods.LoadLibrary(libname);
                success = handle.ToInt64() > 0;
            }

            // restore old path
            UnsafeNativeMethods.SetDllDirectory(currentpath.ToString());
        }
        static C3dComputeNormalsNode()
         {
            LoadDllFile(CoreAssemblyNativeDir, "libigl_Normals.dll");
            //add dependencies folder to path when used as a standalone plugin
            var platform = IntPtr.Size == 4 ? "x86" : "x64";
            var pathToThisAssembly = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
//            var pathToBinFolder = Path.Combine(pathToThisAssembly, "dependencies");
            var pathToBinFolder = Path.Combine(pathToThisAssembly, "dependencies", platform);
            var envPath = Environment.GetEnvironmentVariable("PATH");
            envPath = string.Format("{0};{1}", envPath, pathToBinFolder);
            Environment.SetEnvironmentVariable("PATH", envPath);

            
        }


        /// dll import
        [System.Runtime.InteropServices.DllImport("libigl_Normals.dll")]
        private static extern void ComputeNormals(double[] vertXYZ, int[] triIndices, [In, Out] double[] _faceNxyz, [In, Out] double[] _vertexNxyz, [In, Out] double[] _cornerNxyz, double angle, uint vMode, int[] binSizes, bool computeFace, bool computeVertex, bool computeCorner);

        [System.Runtime.InteropServices.DllImport("libigl_Normals.dll")]
        private static extern int ReleaseMemory(IntPtr ptr);

        #region fields & pins
        [Input("Input ", BinVisibility = PinVisibility.OnlyInspector)]
		public ISpread<ISpread<Vector3D>> FVec;
    	
		[Input("Triangle Indices", BinVisibility = PinVisibility.OnlyInspector)]
		public ISpread<ISpread<int>> FTI;

        [Input("Compute Face Normals")]
        public ISpread<bool> FDoFace;

        [Input("Compute Vertex Normals")]
        public ISpread<bool> FDoVertex;

        [Input("Vertex Normals Wheighting", DefaultValue = 3, MaxValue = 4, MinValue = 0)]
        public ISpread<uint> VM;

        [Input("Compute Corner Normals")]
        public ISpread<bool> FDoCorner;

        [Input("Dihedral Angle", DefaultValue = 20.0)]
        public ISpread<double> FDA;

        [Input("Calculate", DefaultValue = 0.0, IsBang=true)]
		public ISpread<bool> FCal;
		

		
		
		[Output("Face Normals ", BinVisibility = PinVisibility.Hidden)]
		public ISpread<ISpread<Vector3D>> FFaceOut;

        [Output("Vertex Normals ", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<Vector3D>> FVertexOut;

        [Output("Corner Normals ", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<Vector3D>> FCornerOut;

    	

		[Import()]
		public ILogger FLogger;
        #endregion fields & pins

        //        string importDLL = pathToPlatformDLL();


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            SpreadMax = SpreadUtils.SpreadMax(FVec, FVec/*,FPA,FPM*/);
            FFaceOut.SliceCount = SpreadMax;
            FVertexOut.SliceCount = SpreadMax;
            FCornerOut.SliceCount = SpreadMax;



            if (FCal[0])
            {
                var help = new Helpers();

                for (int binID = 0; binID < SpreadMax; binID++)
                {

                    int entries = FVec[binID].SliceCount;
                    int entriesXYZ = entries * 3;
                    int numIndices = FTI[binID].SliceCount;
                    int numTriangles = numIndices / 3;
                    int numIndicesXYZ = numIndices * 3;

                    double[] faceN = new double[numIndices];
                    double[] vertexN = new double[entriesXYZ];
                    double[] cornerN = new double[numIndicesXYZ];


                    double[] V = new double[entriesXYZ];
                    V = help.Vector3DToArray(V, FVec[binID]);

                    int[] tI = new int[numIndices];
                    tI = FTI[binID].ToArray();

                    int[] binSizes = new int[2];
                    binSizes[0] = entries;
                    binSizes[1] = numTriangles;

                    try
                    {
                        ComputeNormals(V, tI, faceN, vertexN, cornerN, FDA[binID], VM[binID], binSizes, FDoFace[binID], FDoVertex[binID], FDoCorner[binID]);
                    }
                    catch (Exception ex)
                    {

                    }

                    FFaceOut[binID].SliceCount = numTriangles;
                    FVertexOut[binID].SliceCount = entries;
                    FCornerOut[binID].SliceCount = numIndices;

                    if (FDoFace[binID])
                    {
                        for (int i = 0; i < numTriangles; i++)
                        {
                            FFaceOut[binID][i] = new Vector3D(faceN[i * 3], faceN[i * 3 + 1], faceN[i * 3 + 2]);
                        }
                    }

                    if (FDoVertex[binID])
                    {
                        for (int i = 0; i < entries; i++)
                        {
                            FVertexOut[binID][i] = new Vector3D(vertexN[i * 3], vertexN[i * 3 + 1], vertexN[i * 3 + 2]);
                        }
                    }

                    if (FDoCorner[binID])
                    {
                        for (int i = 0; i < numIndices; i++)
                        {
                            FCornerOut[binID][i] = new Vector3D(cornerN[i * 3], cornerN[i * 3 + 1], cornerN[i * 3 + 2]);
                        }
                    }

                }
            }
        }

								

	public void Dispose()
         {
			//	Marshal.FreeHGlobal(Vptr);
         }
	}

 
    /// HELPERS
    /// 

    public class Helpers
    {
        public double[] Vector3DToArray(double[] V, ISpread<Vector3D> VertexSpread)
        {
            int entries = VertexSpread.SliceCount;
            for (int i = 0; i < entries; i++)
            {
                V[i * 3] = VertexSpread[i].x;
                V[i * 3 + 1] = VertexSpread[i].y;
                V[i * 3 + 2] = VertexSpread[i].z;
            }
            return V;
        }

        public double[] Vector3DToArray2D(double[] V, ISpread<Vector3D> VertexSpread)
        {
            int entries = VertexSpread.SliceCount;
            for (int i = 0; i < entries; i++)
            {
                V[i * 2] = VertexSpread[i].x;
                V[i * 2 + 1] = VertexSpread[i].y;
            }
            return V;
        }

        public double[] Matrix4x4ToArray(double[] V, ISpread<Matrix4x4> Transform)
        {
            int entries = Transform.SliceCount;
            for (int i = 0; i < entries; i++)
            {
                double[] trans = Transform[i].Values;
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        V[i * 16 + j * 4 + k] = trans[j * 4 + k];
                    }
                }
            }
            return V;
        }

        public double[] Matrix4x4ToArray3x4(double[] V, ISpread<Matrix4x4> Transform)
        {
            int entries = Transform.SliceCount;
            for (int i = 0; i < entries; i++)
            {
                double[] trans = Transform[i].Values;
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        V[i * 12 + j * 3 + k] = trans[j * 4 + k];
                    }
                }
            }
            return V;
        }

        public int[] IndicesToArray(int[] I, ISpread<int> IndexSpread)
        {
            int entries = IndexSpread.SliceCount;
            for (int i = 0; i < entries; i++)
            {
                I[i] = IndexSpread[i];
            }
            return I;
        }


        public void SpreadToArray<T>(T[] I, ISpread<T> Spread)
        {
            int entries = Spread.SliceCount;
            for (int i = 0; i < entries; i++)
            {
                I[i] = Spread[i];
            }
        }

    }


}

