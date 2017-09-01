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

using SlimDX.DXGI;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using System.ComponentModel.Composition;

using VVVV.Core.Logging;


#endregion usings

namespace VVVV.Nodes
{


    public class superMaterial
    {
        public string name;
        public string texture;
        public double metallic;
        public string metallicMap;
        public double roughness;
        public string roughnessMap;
        public double bumpiness;
        public double ambientOcclusion;
        public string ambientOcclusionMap;
        public bool isGlass;
        public double alpha;
    }

    public class superColor
    {
        public string name;
        public RGBAColor color;
    }
    /*
        public enum superMaterialEnum
        {
            none
        }
        */



    #region PluginInfo
    [PluginInfo(Name = "MaterialEncoder", Category = "Nsynk", Author = "digitalWannabe", Credits = "lichterloh", Help = "Define Materials for Superphysical shader", Tags = "superphysical, maetrials")]
    #endregion PluginInfo
    public class CNsynkMaterialEncoder : IPluginEvaluate
    {


        #region fields & pins

        [Input("Material Name")]
        public ISpread<string> FName;

        [Input("Texture", StringType = StringType.Filename)]
        public ISpread<string> FTex;

        [Input("Metallic")]
        public ISpread<double> FMet;

        [Input("Metallic Map", StringType = StringType.Filename)]
        public ISpread<string> FMetMap;

        [Input("Roughness")]
        public ISpread<double> FRough;

        [Input("Roughness Map", StringType = StringType.Filename)]
        public ISpread<string> FRoughMap;

        [Input("Bumpiness")]
        public ISpread<double> FBump;

        [Input("Ambient Occlusion")]
        public ISpread<double> FAO;

        [Input("Ambient Occlusion Map", StringType = StringType.Filename)]
        public ISpread<string> FAOMap;

        [Input("Is Glass")]
        public ISpread<bool> FIsG;

        [Input("Alpha")]
        public ISpread<double> FAlpha;



        [Output("Nsynk SuperMaterial")]
        public ISpread<superMaterial> FMaterial;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        //        string importDLL = pathToPlatformDLL();


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            FMaterial.SliceCount = SpreadMax;

            for (int i = 0; i < SpreadMax; i++)
            {
                superMaterial mat = new superMaterial();
                mat.name = FName[i];
                mat.texture = FTex[i];
                mat.metallic = FMet[i];
                mat.metallicMap = FMetMap[i];
                mat.roughness = FRough[i];
                mat.roughnessMap = FRoughMap[i];
                mat.bumpiness = FBump[i];
                mat.ambientOcclusion = FAO[i];
                mat.ambientOcclusionMap = FAOMap[i];
                mat.isGlass = FIsG[i];
                mat.alpha = FAlpha[i];

                FMaterial[i] = mat;

            }


        }

    }


    #region PluginInfo
    [PluginInfo(Name = "MaterialDecoder",
                Category = "Nsynk",
                Author = "digitalWannabe",
                Help = "Select Materials for Superphysical shader",
                Credits = "lichterloh",
                Tags = "superphysical, maetrials")]
    #endregion PluginInfo
    public class CNsynkMaterialDecoder : IPluginEvaluate
    {
        #region fields & pins
        

        [Input("UpdateEnum", IsBang = true, IsSingle =true)]
        public ISpread<bool> FChangeEnum;

        [Input("Draw", IsBang = true)]
        public IDiffSpread<bool> FDraw;

        [Input("Nsynk SuperMaterial")]
        public ISpread<superMaterial> FMaterial;

        [Input("Type", EnumName = "superMaterial")]
        public IDiffSpread<EnumEntry> FInput;

        [Input("Evaluate", IsSingle = true)]
        public IDiffSpread<bool> FEvaluate;


        [Output("Reorder")]
        public ISpread<int> FReSort;

        [Output("Texture")]
        public ISpread<string> FTex;

        [Output("Metallic")]
        public ISpread<double> FMet;

        [Output("Metallic Map")]
        public ISpread<string> FMetMap;

        [Output("Roughness")]
        public ISpread<double> FRough;

        [Output("Roughness Map")]
        public ISpread<string> FRoughMap;

        [Output("Bumpiness")]
        public ISpread<double> FBump;

        [Output("Ambient Occlusion")]
        public ISpread<double> FAO;

        [Output("Ambient Occlusion Map")]
        public ISpread<string> FAOMap;

        [Output("Alpha")]
        public ISpread<double> FAlpha;

        [Output("DepthStencilOrd")]
        public ISpread<double> FDepthStencil;

        [Import()]
        public ILogger Flogger;
        #endregion fields & pins

        //add some entries to the enum in the constructor
        [ImportingConstructor]
        public CNsynkMaterialDecoder()
        {
            var s = new string[] { "one", "two" };
            //Please rename your Enum Type to avoid 
            //numerous "MyDynamicEnum"s in the system
            EnumManager.UpdateEnum("superMaterial", "two", s);
        }

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {


            int materialCount = FMaterial.SliceCount;
            int modelCount = Math.Max(FInput.SliceCount, FDraw.SliceCount);

            if (FEvaluate[0])
            {

                if (FChangeEnum[0] && (materialCount > 0))
                {
                    string[] enums = new string[materialCount];
                    for (int i = 0; i < materialCount; i++)
                    {
                        enums[i] = FMaterial[i].name;
                    }
                    EnumManager.UpdateEnum("superMaterial",
                        enums[0], enums);
                }

                if (FInput.IsChanged || FDraw.IsChanged)
                {
                    List<int> noGlass = new List<int>();
                    List<int> glass = new List<int>();

                    //re-sort: glass last
                    for (int i = 0; i < modelCount; i++)
                    {
                        if (FDraw[i] && !FMaterial[FInput[i].Index].isGlass) noGlass.Add(i);
                        if (FDraw[i] && FMaterial[FInput[i].Index].isGlass) glass.Add(i);
                    }
                    List<int> reorder = new List<int>(noGlass.Concat(glass));

                    int drawCount = reorder.Count;

                    FReSort.SliceCount = drawCount;
                    FTex.SliceCount = drawCount;
                    FMet.SliceCount = drawCount;
                    FMetMap.SliceCount = drawCount;
                    FRough.SliceCount = drawCount;
                    FRoughMap.SliceCount = drawCount;
                    FBump.SliceCount = drawCount;
                    FAO.SliceCount = drawCount;
                    FAOMap.SliceCount = drawCount;
                    FAlpha.SliceCount = drawCount;
                    FDepthStencil.SliceCount = drawCount;

                    //                FReSort = reorder.ToSpread<int>();

                    for (int i = 0; i < drawCount; i++)
                    {
                        int reOrder = reorder[i];
                        FReSort[i] = reOrder;
                        FTex[i] = FMaterial[FInput[reOrder]].texture;
                        FMet[i] = FMaterial[FInput[reOrder]].metallic;
                        FMetMap[i] = FMaterial[FInput[reOrder]].metallicMap;
                        FBump[i] = FMaterial[FInput[reOrder]].bumpiness;
                        FRough[i] = FMaterial[FInput[reOrder]].roughness;
                        FRoughMap[i] = FMaterial[FInput[reOrder]].roughnessMap;
                        FAO[i] = FMaterial[FInput[reOrder]].ambientOcclusion;
                        FAOMap[i] = FMaterial[FInput[reOrder]].ambientOcclusionMap;
                        FAlpha[i] = FMaterial[FInput[reOrder]].alpha;

                        if (FMaterial[FInput[reOrder]].isGlass)
                        {
                            FDepthStencil[i] = 0; //less read
                        }
                        else
                        {
                            FDepthStencil[i] = 1; //less readwrite
                        }
                    }

                    Flogger.Log(LogType.Debug, "Input was changed");
                }
            }
        }
    }


    #region PluginInfo
    [PluginInfo(Name = "ColorEncoder", Category = "Nsynk", Author = "digitalWannabe", Credits = "lichterloh", Help = "Define Colors for Superphysical shader", Tags = "superphysical, maetrials")]
    #endregion PluginInfo
    public class CNsynkColorEncoder : IPluginEvaluate
    {


        #region fields & pins

        [Input("Name")]
        public ISpread<string> FName;

        [Input("Color")]
        public ISpread<RGBAColor> FCol;



        [Output("Nsynk SuperColor")]
        public ISpread<superColor> FColor;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        //        string importDLL = pathToPlatformDLL();


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            FColor.SliceCount = SpreadMax;

            for (int i = 0; i < SpreadMax; i++)
            {
                superColor col = new superColor();
                col.name = FName[i];
                col.color = FCol[i];

                FColor[i] = col;
            }


        }

    }


    #region PluginInfo
    [PluginInfo(Name = "ColorDecoder",
                Category = "Nsynk",
                Author = "digitalWannabe",
                Help = "Select colors for Superphysical shader",
                Credits = "lichterloh",
                Tags = "superphysical, colors")]
    #endregion PluginInfo
    public class CNsynkColorDecoder : IPluginEvaluate
    {
        #region fields & pins


        [Input("UpdateEnum", IsBang = true, IsSingle = true)]
        public ISpread<bool> FChangeEnum;

        [Input("Nsynk SuperColor")]
        public ISpread<superColor> FColor;

        [Input("Type", EnumName = "superColor")]
        public IDiffSpread<EnumEntry> FInput;

        [Input("Evaluate", IsSingle = true)]
        public IDiffSpread<bool> FEvaluate;



        [Output("Color")]
        public ISpread<RGBAColor> FColorOut;


        [Import()]
        public ILogger Flogger;
        #endregion fields & pins

        //add some entries to the enum in the constructor
        [ImportingConstructor]
        public CNsynkColorDecoder()
        {
            var s = new string[] { "one", "two" };
            //Please rename your Enum Type to avoid 
            //numerous "MyDynamicEnum"s in the system
            EnumManager.UpdateEnum("superColor", "two", s);
        }

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {


            int colorCount = FColor.SliceCount;
            int modelCount = FInput.SliceCount;

            FColorOut.SliceCount = modelCount;

            if (FEvaluate[0])
            {

                if (FChangeEnum[0] && (colorCount > 0))
                {
                    string[] enums = new string[colorCount];
                    for (int i = 0; i < colorCount; i++)
                    {
                        enums[i] = FColor[i].name;
                    }
                    EnumManager.UpdateEnum("superColor",
                        enums[0], enums);
                }

                if (FInput.IsChanged)
                {


                    for (int i = 0; i < modelCount; i++)
                    {
                        FColorOut[i] = FColor[FInput[i].Index].color;
                    }

                    Flogger.Log(LogType.Debug, "Input was changed");
                }
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

    }


}

