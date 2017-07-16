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
    [PluginInfo(Name = "BooleanMesh", Category = "3d BooleanMesh", Author = "digitalWannabe", Credits = "libigl ETH Zurich, lichterloh", Help = "Boolean operations on triangulated surface meshes", Tags = "cgal, csg, libigl, lichterloh, dope")]
    #endregion PluginInfo
    public unsafe class C3dBooleanMeshNode : IPluginEvaluate, IDisposable
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
                string fullPath = Assembly.GetAssembly(typeof(C3dBooleanMeshNode)).Location;
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
        static C3dBooleanMeshNode()
        {
            LoadDllFile(CoreAssemblyNativeDir, "libigl_BooleanMesh.dll");
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
        [System.Runtime.InteropServices.DllImport("libigl_MeshBoolean.dll")]
        private static extern IntPtr meshBoolean(
                                                string opString, 
                                                double[] vertXYZ_A, 
                                                int[] triIndices_A,
                                                double[] vertXYZ_B,
                                                int[] triIndices_B,
                                                int[] binSizes
                                                //[In, Out] int[] bFacesOut
                                                );

        [System.Runtime.InteropServices.DllImport("libigl_MeshBoolean.dll")]
        private static extern void getValues (
                                                [In, Out] double[] verticesOut,
                                                [In, Out] int[] indicesOut);

        [System.Runtime.InteropServices.DllImport("libigl_MeshBoolean.dll")]
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

        [Input("Operation String")]
        public ISpread<string> FOperation;

        [Input("Calculate", DefaultValue = 0.0, IsBang = true)]
        public ISpread<bool> FCal;




        [Output("Vertices ", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<Vector3D>> FVecOut;

        [Output("Triangle Indices", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<int>> FTriOut;

//        [Output("Birth Face Indices", BinVisibility = PinVisibility.Hidden)]
//        public ISpread<ISpread<int>> BFaceOut;




        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        struct mesh
        {
            public double[] V;
            public int[] I;
        }

        int[] binNew = new int[2];

        void workTheStack(string opElement, double[] vertices, int[] indices, int[] vertexCounts, int[] indexCounts, int[] vertexShifts, int[] indexShifts, Stack<mesh> CSGStack)
        {
            if (IsOperand(opElement))
            {
                int meshIndex;// vertexCount, indexCount, vertexshift, indexShift;
                int.TryParse(opElement, out meshIndex);// return error message here
                mesh Operand = new mesh();
                Operand.V = new double[vertexCounts[meshIndex] * 3];
                Operand.I = new int[indexCounts[meshIndex]];
                Array.Copy(vertices, vertexShifts[meshIndex]*3, Operand.V, 0, vertexCounts[meshIndex]*3);
                Array.Copy(indices, indexShifts[meshIndex], Operand.I, 0, indexCounts[meshIndex]);
                CSGStack.Push(Operand);
            }
            else //if operator
            {
                mesh B = CSGStack.Pop();
                mesh A = CSGStack.Pop();
                mesh C = new mesh();
                //                C.V = new double[];
                //                C.I = new int[6];
                double[] V;
//                int[] binNew = new int[2];

                int[] binSizes = new int[4];
                binSizes[0] = A.V.Length / 3;
                binSizes[1] = A.I.Length;
                binSizes[2] = B.V.Length / 3;
                binSizes[3] = B.I.Length;

                try
                {
//                    meshBoolean(opElement, A.V, A.I, B.V, B.I, binSizes);
 //                   binTest[0] = binNew[0];
                    C.V = new double[binNew[0]];
                    C.I = new int[binNew[1]];
                    getValues(C.V, C.I);
                    CSGStack.Push(C);
                }
                catch (Exception ex)
                {

                }
            }
}

        bool IsOperand(string s)
        {
            int n;
            return int.TryParse(s,out n);
        }

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            SpreadMax = SpreadUtils.SpreadMax(FVec, FVecNum, FTI, FTINum, FOperation/*,FPA,FPM*/);
            FVecOut.SliceCount = SpreadMax;
            FTriOut.SliceCount = SpreadMax;
          //  BFaceOut.SliceCount = SpreadMax;


            if (FCal[0])
            {
                var help = new Helpers();

                for (int binID = 0; binID < SpreadMax; binID++)
                {

                    int entries = FVec[binID].SliceCount;
                    int entriesXYZ = entries * 3;
                    int numIndices = FTI[binID].SliceCount;
                    int numTriangles = numIndices / 3;
//                    int numIndicesXYZ = numIndices * 3;

                    int[] perModelVertCount = FVecNum[binID].ToArray();
                    int[] perModelIndCount = FTINum[binID].ToArray();

                    int[] verticesShift = help.Integrate(perModelVertCount);
                    int[] indicesShift = help.Integrate(perModelIndCount);

/*                    Stack<mesh> CSGStack = new Stack<mesh>();

                    /*                  

                                      mesh Operand = new mesh();
                                      Operand.V = new double[perModelVertCount[0] * 3];
                   //                   Operand.I = new int[indexCounts[meshIndex]];
                                      Array.Copy(V, verticesShift[0] * 3, Operand.V, 0, perModelVertCount[0] * 3);
                                      //                   Array.Copy(indices, indexShifts[meshIndex], Operand.I, 0, indexCounts[meshIndex]);

                                      CSGStack.Push(Operand);
                                      */

                    double[] V = new double[entriesXYZ];
                    V = help.Vector3DToArray(V, FVec[binID]);

                    int[] tI = new int[numIndices];
                    tI = FTI[binID].ToArray();

                    //                   string[] opElements = FOperation[binID].Split(' ');

                    /*                   foreach (var element in opElements)
                                       {
                                           workTheStack(element, V, tI, perModelVertCount, perModelIndCount, verticesShift, indicesShift, CSGStack);
                                       }
                                       */
                    //                   mesh result = CSGStack.First();
                    //                   result.V = new double[CSGStack.First().V.Length];
                    //                   result.I = new int[CSGStack.First().I.Length];


                    //                  int newVertCount = CSGStack.First().V.Length / 3;
                    //                  int newIndCount = CSGStack.First().I.Length;
                    ////                  mesh B = new mesh();
                    //                  mesh A = new mesh();
                    //                mesh C = new mesh();
                    //                C.V = new double[];
                    //                C.I = new int[6];
                    //                int[] binNew = new int[2];
                    
                    double[] AV = new double[perModelVertCount[0] * 3];
                    int[] AI = new int[perModelIndCount[0]];
                    Array.Copy(V, verticesShift[0] * 3, AV, 0, perModelVertCount[0] * 3);
                    Array.Copy(tI, indicesShift[0], AI, 0, perModelIndCount[0]);

                    double[] BV = new double[perModelVertCount[1] * 3];
                    int[] BI = new int[perModelIndCount[1]];
                    Array.Copy(V, verticesShift[1] * 3, BV, 0, perModelVertCount[1] * 3);
                    Array.Copy(tI, indicesShift[1], BI, 0, perModelIndCount[1]);

                    int[] binSizes = new int[4];
                    binSizes[0] = perModelVertCount[0];
                    binSizes[1] = perModelIndCount[0];
                    binSizes[2] = perModelVertCount[1];
                    binSizes[3] = perModelIndCount[1];

                                        
                    try {
                        IntPtr meshInfo = meshBoolean("i", AV, AI, BV, BI, binSizes);
                        int size = 2;
                        int[] meshInfoArr = new int[size];
                        Marshal.Copy(meshInfo, meshInfoArr, 0, size);

                        int numPoints = meshInfoArr[0];
                        int numInd = meshInfoArr[1];

                        double[] vertOut = new double[numPoints*3];
                        int[] indOut = new int[numInd];

                        getValues(vertOut, indOut);

                        FVecOut[binID].SliceCount = numPoints;
                        FTriOut[binID].SliceCount = numInd;

                        for (int i = 0; i < numPoints; i++)
                        {
                            FVecOut[binID][i] = new Vector3D(vertOut[i * 3], vertOut[i * 3 + 1], vertOut[i * 3 + 2]);
                        }

                        for (int j = 0; j < numInd; j++)
                        {
                            FTriOut[binID][j] = indOut[j];
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
        [PluginInfo(Name = "BooleanMeshOperation", Category = "3d BooleanMesh", Author = "digitalWannabe", Credits = "lichterloh", Help = "Operator node for CSG (constructional solid geometry) tree", Tags = "TetGen, Mesh, Region, 3D Delaunay, dope")]
        #endregion PluginInfo
        public class C3DBooleanMeshOperationNode : IPluginEvaluate
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

            public void SpreadToArray<T>(T[] I, ISpread<T> Spread, int begin, int end)
            {
                int entries = Math.Abs(end - begin);
                for (int i = 0; i < entries; i++)
                {
                    I[i] = Spread[begin+i];
                }
            }



            public void EnumSpreadToIndexArray(int[] I, ISpread<operatorCSG> Spread, int range)
            {
                for (int i = 0; i < range; i++)
                {
                    I[i] = (int)Spread[i];
                }
            }

            public int[] Integrate(int[] Input)
            {
                int size = Input.Length;
                int[] Output = new int[size];
                Output[0] = 0;

                for (int i = 1; i < size; i++)
                {
                    Output[i] = Output[i - 1] + Input[i - 1];
                }

                return Output;
            }

        }


    }
}


