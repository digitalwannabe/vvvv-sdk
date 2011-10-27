using System;
using System.ComponentModel.Composition;
using SlimDX;
using VVVV.Hosting.Pins;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.Streams;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

namespace VVVV.Nodes
{
	public abstract class ZipNode<T> : IPluginEvaluate
	{
		[Input("Input", IsPinGroup = true)]
		protected ISpread<ISpread<T>> FInputSpreads;
		
		[Output("Output")]
		protected ISpread<T> FOutput;
		
		private readonly T[] FBuffer = new T[128];
		
		public void Evaluate(int SpreadMax)
		{
			FOutput.SetSliceCountBy(FInputSpreads);

			var outputStream = FOutput.GetStream();
			var inputSpreadsStream = FInputSpreads.GetStream();
			var inputSpreadsCount = inputSpreadsStream.Length;
			
			int i = 0;
			
			while (!inputSpreadsStream.Eof)
			{
				var inputStream = inputSpreadsStream.Read().GetStream();
				int numSlicesToRead = Math.Min(inputStream.Length, FBuffer.Length);
				
				outputStream.WritePosition = i++;
				while (!outputStream.Eof)
				{
					inputStream.ReadCyclic(FBuffer, 0, numSlicesToRead);
					outputStream.Write(FBuffer, 0, numSlicesToRead, inputSpreadsCount);
				}
			}
		}
	}
	
	[PluginInfo(Name = "Zip", Category = "Value", Help = "Zips spreads together", Tags = "")]
	public class ValueZipNode : ZipNode<double>
	{

	}
	
	[PluginInfo(Name = "Zip", Category = "Spreads", Help = "Zips spreads together", Tags = "")]
	public class Value2dZipNode : ZipNode<ISpread<double>>
	{

	}
	
	[PluginInfo(Name = "Zip", Category = "2d", Help = "Zips spreads together", Tags = "")]
	public class Vector2DZipNode : ZipNode<Vector2D>
	{
		
	}
	
	[PluginInfo(Name = "Zip", Category = "3d", Help = "Zips spreads together", Tags = "")]
	public class Vector3DZipNode : ZipNode<Vector3D>
	{
		
	}
	
	[PluginInfo(Name = "Zip", Category = "4d", Help = "Zips spreads together", Tags = "")]
	public class Vector4DZipNode : ZipNode<Vector4D>
	{
		
	}
	
	[PluginInfo(Name = "Zip", Category = "Color", Help = "Zips spreads together", Tags = "")]
	public class ColorZipNode : ZipNode<RGBAColor>
	{
		
	}
	
	[PluginInfo(Name = "Zip", Category = "String", Help = "Zips spreads together", Tags = "")]
	public class StringZipNode : ZipNode<string>
	{
		
	}
	
	[PluginInfo(Name = "Zip", Category = "Transform", Help = "Zips spreads together", Tags = "")]
	public class TransformZipNode : ZipNode<Matrix>
	{
		
	}
	
	[PluginInfo(Name = "Zip", Category = "Enumerations", Help = "Zips spreads together", Tags = "")]
	public class EnumZipNode : ZipNode<EnumEntry>
	{
		
	}
}
