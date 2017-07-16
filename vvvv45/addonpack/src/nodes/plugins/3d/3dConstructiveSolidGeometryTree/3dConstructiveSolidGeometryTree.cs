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
    [PluginInfo(Name = "BooleanGeometry", Category = "3d", Author = "digitalWannabe", Credits = "libigl ETH Zurich, lichterloh", Help = "Boolean operations on triangulated surface meshes", Tags = "cgal, csg, libigl, lichterloh, dope")]
    #endregion PluginInfo
    public unsafe class C3dBooleanGeometryNode : IPluginEvaluate, IDisposable
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
                string fullPath = Assembly.GetAssembly(typeof(C3dBooleanGeometryNode)).Location;
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
        static C3dBooleanGeometryNode()
        {
            LoadDllFile(CoreAssemblyNativeDir, "CSGTree4V.dll");
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
        /// char * operationString, double *allVerticesIn, int *allIndicesIn, int *verticesCount, int *indicesCount, int * verticesShift, int * indicesShift, int *binSizes, double *verticesOut, int *indicesOut)
        [System.Runtime.InteropServices.DllImport("CSGTree4V.dll")]
        private static extern IntPtr ComputeCSG(string opString, 
                                                double[] vertXYZ, 
                                                int[] triIndices, 
                                                int[] verticesCount, 
                                                int[] indicesCount, 
                                                int[] verticesShift, 
                                                int[] indicesShift,
                                                int[] binSizes);

        [System.Runtime.InteropServices.DllImport("CSGTree4V.dll")]
        private static extern void getValues(
                                                        [In, Out] double[] verticesOut,
                                                        [In, Out] int[] indicesOut,
                                                        [In, Out] int[] refOut);

        [System.Runtime.InteropServices.DllImport("CSGTree4V.dll")]
        private static extern int ReleaseMemory(IntPtr ptr);

        #region fields & pins
        [Input("Operator Vertices ", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<Vector3D>> FVec;

        [Input("Vertices per Mesh", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FVecNum;

        [Input("Triangle Indices", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FTI;

        [Input("Indices per Mesh", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FTINum;

        [Input("Operation String", DefaultString = "0 1 union")]
        public ISpread<string> FOperation;

        [Input("Calculate", DefaultValue = 0.0, IsBang = true)]
        public ISpread<bool> FCal;




        [Output("Vertices ", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<Vector3D>> FVecOut;

        [Output("Triangle Indices", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<int>> FTriOut;

        [Output("Reference Indices", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<int>> FRefOut;




        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        //helper function

        int[] Integrate(int[] Input)
        {
            int size = Input.Length;
            int[] Output = new int[size];
            Output[0] = 0;

            for (int i=1;i< size; i++)
            {
                Output[i] = Output[i-1]+Input[i-1];
            }

            return Output;
        }



        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            SpreadMax = SpreadUtils.SpreadMax(FVec, FVecNum, FTI, FTINum, FOperation/*,FPA,FPM*/);
            FVecOut.SliceCount = SpreadMax;
            FTriOut.SliceCount = SpreadMax;
            FRefOut.SliceCount = SpreadMax;

            var help = new Helpers();

            if (FCal[0])
            {
                

                for (int binID = 0; binID < SpreadMax; binID++)
                {

                    int entries = FVec[binID].SliceCount;
                    int entriesXYZ = entries * 3;
                    int numIndices = FTI[binID].SliceCount;
                    int numTriangles = numIndices / 3;
                    int numIndicesXYZ = numIndices * 3;

                    int[] perModelVertCount = FVecNum[binID].ToArray();
                    int[] perModelIndCount = FTINum[binID].ToArray();

                    int[] verticesShift = Integrate(perModelVertCount);
                    int[] indicesShift = Integrate(perModelIndCount);

                    double[] verticesOut = new double[entriesXYZ];
                    int[] indicesOut = new int[numIndicesXYZ];
 

                    double[] V = new double[entriesXYZ];
                    V = help.Vector3DToArray(V, FVec[binID]);

                    int[] tI = new int[numIndices];
                    tI = FTI[binID].ToArray();

                    int[] binSizes = new int[2];
                    binSizes[0] = entries;
                    binSizes[1] = numTriangles;

                    try
                    {
                       

                        IntPtr meshInfo = ComputeCSG(FOperation[binID], V, tI, perModelVertCount, perModelIndCount, verticesShift, indicesShift, binSizes);
                        int size = 3;
                        int[] meshInfoArr = new int[size];
                        Marshal.Copy(meshInfo, meshInfoArr, 0, size);

                        int numPoints = meshInfoArr[0];
                        int numInd = meshInfoArr[1];
                        int numRef = meshInfoArr[2];

                        double[] vertOut = new double[numPoints * 3];
                        int[] indOut = new int[numInd];
                        int[] refOut = new int[numRef];

                        getValues(vertOut, indOut, refOut);

                        FVecOut[binID].SliceCount = numPoints;
                        FTriOut[binID].SliceCount = numInd;
                        FRefOut[binID].SliceCount = numRef;

                        for (int i = 0; i < numPoints; i++)
                        {
                            FVecOut[binID][i] = new Vector3D(vertOut[i * 3], vertOut[i * 3 + 1], vertOut[i * 3 + 2]);
                        }

                        for (int j = 0; j < numInd; j++)
                        {
                            FTriOut[binID][j] = indOut[j];
                        }

                        for (int j = 0; j < numRef; j++)
                        {
                            FRefOut[binID][j] = refOut[j];
                        }

                        ReleaseMemory(meshInfo);

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


        //// OPERATOR PLUGIN
        public enum operatorCSG
        {
            Union,
            Intersect,
            Minus,
            XOR,
            Resolve

        }


        #region PluginInfo
        [PluginInfo(Name = "BooleanGeometryNode", Category = "3D", Author = "digitalWannabe", Credits = "lichterloh", Help = "Operator node for CSG (constructional solid geometry) tree", Tags = "TetGen, Mesh, Region, 3D Delaunay, dope")]
        #endregion PluginInfo
        public class C3DBooleanGeometryNodeNode : IPluginEvaluate
        {

            #region fields & pins
            [Input("Operands", BinVisibility = PinVisibility.OnlyInspector)]
            public IDiffSpread<ISpread<string>> FOperand;

            //       [Input("B", BinVisibility = PinVisibility.OnlyInspector)]
            //       public IDiffSpread<ISpread<string>> FOperandB;

            [Input("Operator")]
            public IDiffSpread<operatorCSG> FOperator;



            [Output("Output", BinVisibility = PinVisibility.Hidden)]
            public ISpread<ISpread<string>> FOutput;

            [Import()]
            public ILogger FLogger;
            #endregion fields & pins

            public string[] operatorsSmallCaps = { "union", "intersect", "minus", "xor", "resolve" };

            public string generateOperationString(string A, string B, string op)
            {
                string output =  A + " " + B + " " + "" + op + "";

                return output;
            }

            //called when data for any output pin is requested
            public void Evaluate(int SpreadMax)
            {
                SpreadMax = SpreadUtils.SpreadMax(FOperand, FOperator/*,FPA,FPM*/);
                FOutput.SliceCount = SpreadMax;

                var help = new Helpers();

                for (int binID = 0; binID < SpreadMax; binID++)
                {
                    int numOperands = FOperand[binID].SliceCount;// + FOperandB[binID].SliceCount;



                    //{{{VA,FA},{VB,FB},"i"},{{{VC,FC},{VD,FD},"u"},{VE,FE},"u"},"m"};
                    string operationString = FOperand[binID][0];

                    for (int operandID = 0; operandID < numOperands - 1; operandID++)
                    {

                        operationString = generateOperationString(operationString, FOperand[binID][operandID + 1], operatorsSmallCaps[(int)FOperator[binID]]);
                    }

                    FOutput[binID].SliceCount = 1;
                    FOutput[binID][0] = operationString;

                }
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

            public void EnumSpreadToIndexArray(int[] I, ISpread<operatorCSG> Spread, int range)
            {
                for (int i = 0; i < range; i++)
                {
                    I[i] = (int)Spread[i];
                }
            }

        }


    }
}


