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
    [PluginInfo(Name = "SurfaceParametrization", Category = "3d", Author = "digitalWannabe", Credits = "libigl ETH Zurich, lichterloh", Help = "From surface to 2d space", Tags = "cgal, csg, libigl, lichterloh, dope")]
    #endregion PluginInfo
    public unsafe class C3dSurfaceParametrizationNode : IPluginEvaluate, IDisposable
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
                string fullPath = Assembly.GetAssembly(typeof(C3dSurfaceParametrizationNode)).Location;
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
        static C3dSurfaceParametrizationNode()
        {
            LoadDllFile(CoreAssemblyNativeDir, "libigl_SurfaceParam.dll");
            //add dependencies folder to path when used as a standalone plugin
            var platform = IntPtr.Size == 4 ? "x86" : "x64";
            var pathToThisAssembly = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //            var pathToBinFolder = Path.Combine(pathToThisAssembly, "dependencies");
            var pathToBinFolder = Path.Combine(pathToThisAssembly, "dependencies", platform);
            var envPath = Environment.GetEnvironmentVariable("PATH");
            envPath = string.Format("{0};{1}", envPath, pathToBinFolder);
            Environment.SetEnvironmentVariable("PATH", envPath);
        }


        [System.Runtime.InteropServices.DllImport("libigl_SurfaceParam.dll")]
        private static extern void doARAPParam(double[] vertXYZ,
                                                int[] triIndices,
                                                int[] binSizes,
                                                [In,Out] double[] Vuv);

        [System.Runtime.InteropServices.DllImport("libigl_SurfaceParam.dll")]
        private static extern void doLSCMParam(double[] vertXYZ,
                                        int[] triIndices,
                                        int[] binSizes,
                                        [In, Out] double[] Vuv);

        [System.Runtime.InteropServices.DllImport("libigl_SurfaceParam.dll")]
        private static extern int ReleaseMemory(IntPtr ptr);


        #region fields & pins
        [Input("Vertices ", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<Vector3D>> FVec;


        [Input("Triangle Indices", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FTI;

        [Input("Mode", BinVisibility = PinVisibility.OnlyInspector)]
        public IDiffSpread<Mode> FMode;


        [Input("Calculate", DefaultValue = 0.0, IsBang = true)]
        public ISpread<bool> FCal;




        [Output("Vertices ", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<Vector2D>> FVecOut;



        [Import()]
        public ILogger FLogger;
        #endregion fields & pins


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            SpreadMax = SpreadUtils.SpreadMax(FVec, FTI /*,FPA,FPM*/);
            FVecOut.SliceCount = SpreadMax;

            var help = new Helpers();

            if (FCal[0])
            {


                for (int binID = 0; binID < SpreadMax; binID++)
                {

                    int entries = FVec[binID].SliceCount;
                    int entriesXYZ = entries * 3;
                    int entriesXY = entries * 2;
                    int numIndices = FTI[binID].SliceCount;
                    int numTriangles = numIndices / 3;

                    double[] V = new double[entriesXYZ];
                    V = help.Vector3DToArray(V, FVec[binID]);

                    int[] tI = new int[numIndices];
                    tI = FTI[binID].ToArray();

                    double[] Vuv = new double[entriesXY];

                    int[] binSizes = new int[2];
                    binSizes[0] = entries;
                    binSizes[1] = numTriangles;

                    try
                    {
                        if ((int)FMode[binID]== 0)
                        {
                            doARAPParam(V, tI, binSizes, Vuv);
                        }
                        else
                        {
                            doLSCMParam(V, tI, binSizes, Vuv);
                        }

                        
 //                       double[] tTexCoordsArr = new double[entriesXY];
   //                     Marshal.Copy(tTexCoords, tTexCoordsArr, 0, entriesXY);

                        FVecOut[binID].SliceCount = entries;

                        for (int i = 0; i < entries; i++)
                        {
                            FVecOut[binID][i] = new Vector2D(Vuv[i * 2], Vuv[i * 2 + 1]);
                        }

 //                       ReleaseMemory(tTexCoords);

                    }
                    catch (Exception ex)
                    {

                    }

                }
            }
        }



        public void Dispose()
        {
            //	Marshal.FreeHGlobal(Vptr);
        }

        public enum Mode
        {
            AsRigidAsPossible,
            LeastSquareConformalMap

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
}


