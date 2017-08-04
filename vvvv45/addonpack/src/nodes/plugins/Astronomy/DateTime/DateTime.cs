#region usings
using System;
using System.ComponentModel.Composition;

using System.Linq;


using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;


using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
#endregion usings

namespace VVVV.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "DateTime", Category = "Astronomy", Author = "digitalWannabe", Credits = "lichterloh", Help = ".NET DateTime Format", Tags = "date,time,datetime")]
    #endregion PluginInfo
    public class DateTimeNode : IPluginEvaluate, IDisposable
    {

        #region fields & pins
        [Input("Year", BinVisibility = PinVisibility.OnlyInspector, MinValue = 0, MaxValue = 9999)]
        public IDiffSpread<ISpread<int>> FYear;

        [Input("Month", BinVisibility = PinVisibility.OnlyInspector, MinValue = 1, MaxValue = 12, DefaultValue =1)]
        public IDiffSpread<ISpread<int>> FMonth;

        [Input("Day", BinVisibility = PinVisibility.OnlyInspector, MinValue = 1, MaxValue = 31)]
        public IDiffSpread<ISpread<int>> FDay;

        [Input("Hour", BinVisibility = PinVisibility.OnlyInspector, MinValue = 0, MaxValue = 23)]
        public IDiffSpread<ISpread<int>> FHour;

        [Input("Minute", BinVisibility = PinVisibility.OnlyInspector, MinValue = 0, MaxValue = 59)]
        public IDiffSpread<ISpread<int>> FMinute;

        [Input("Second", BinVisibility = PinVisibility.OnlyInspector, MinValue = 0, MaxValue = 59)]
        public IDiffSpread<ISpread<int>> FSecond;

        [Input("Millisecond", BinVisibility = PinVisibility.OnlyInspector, MinValue = 0, MaxValue = 999)]
        public IDiffSpread<ISpread<int>> FMillsecond;


        [Output("DateTime", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<DateTime>> FDateTime;



 //       [Import()]
 //       public ILogger FLogger;
        #endregion fields & pins


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            SpreadMax = SpreadUtils.SpreadMax(FYear, FMonth, FDay, FHour, FMinute, FSecond, FMillsecond/*,FPA,FPM*/);
            FDateTime.SliceCount = SpreadMax;

                for (int binID = 0; binID < SpreadMax; binID++)
                {
                bool InChange = FYear[binID].IsChanged || FMonth[binID].IsChanged || FDay[binID].IsChanged || FHour[binID].IsChanged || FMinute[binID].IsChanged || FSecond[binID].IsChanged || FMillsecond[binID].IsChanged;
                if (InChange)
                {
                    int subCount = SpreadUtils.SpreadMax(FYear[binID], FMonth[binID], FDay[binID], FHour[binID], FMinute[binID], FSecond[binID], FMillsecond[binID]/*,FPA,FPM*/);
                    FDateTime[binID].SliceCount = subCount;
                    for (int dateID = 0; dateID < subCount; dateID++)
                    {
                        FDateTime[binID][dateID] = new DateTime(FYear[binID][dateID], FMonth[binID][dateID], FDay[binID][dateID], FHour[binID][dateID], FMinute[binID][dateID], FSecond[binID][dateID], FMillsecond[binID][dateID]);
                    }
                }
            }
        }



        public void Dispose()
        {
            //	Marshal.FreeHGlobal(Vptr);
        }
    }



}

