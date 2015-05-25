#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
using VVVV.Utils.SlimDX;
using SlimDX;
#endregion usings

/*
TODO:
choose for freeze progress/time when delay/time changing
*/

namespace  VVVV.Nodes.Tweening
{
	#region PluginInfo
	[PluginInfo(Name = "Lerp", AutoEvaluate = true, Category = "3d", Version = "Catmull-Rom Uniform Spline", Help = "", Tags = "")]
	#endregion PluginInfo
	public class LerpCatmullRomSplineNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", MinValue = 0.0, MaxValue = 1.0, Order = 0)]
		public IDiffSpread<double> FInput;
		
		[Input("Points" , Order = 1)]
		public IDiffSpread<Vector3D> FInPoints;
		
		[Input("Devider", IsSingle = true, MinValue = 1, DefaultValue = 100, Order = 2)]
		public IDiffSpread<int> FInDivider;
		
		[Output("Output", Order = 0)]
		public ISpread<Vector3D> FOutput;
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		struct Curve
		{
			public Vector3D m0;
			public Vector3D m1;
			public Vector3D m2;
			public Vector3D m3;
			public double[] percent;
			public double length;
		}
		Curve[] curve;
		
		private Vector3D Catmull(double t, Curve curve )
		{
			
			double t2 = t * t, t3 = t2 * t;
			return 0.5 * (curve.m0 + curve.m1 * t + curve.m2 * t2 + curve.m3 * t3);
		}
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if(FInPoints.IsChanged || FInDivider.IsChanged)
			{
				curve = new Curve[FInPoints.SliceCount-1];
				
				double step = 1.0 / FInDivider[0];
				
				for(int i=0; i<FInPoints.SliceCount-1; i++)
				{
					Vector3D p0 = i>0? FInPoints[i-1] : FInPoints[0];
					Vector3D p1 = FInPoints[i];
					Vector3D p2 = FInPoints[i+1];
					Vector3D p3 = i+2<FInPoints.SliceCount ? FInPoints[i+2] : FInPoints[FInPoints.SliceCount-1];
					
					curve[i].m0 = 2 * p1;
					curve[i].m1 =  -p0 + p2;
					curve[i].m2 = 2 * p0 - 5 * p1 + 4 * p2 - p3;
					curve[i].m3 = -p0 + 3 * p1 - 3 * p2 + p3;
					
					curve[i].percent = new double[FInDivider[0]];
					
					/*
int k = 0;
for (double j = 0; j <= 1.0; j += step, k++) {
curve[i].length += curve[i].percent[k] = VMath.Dist(Catmull(j , curve[i]), Catmull(j+step, curve[i]));
}
*/
					// /*
					for (int k = 0, j = 0; j < FInDivider[0]; j ++, k++) {
						curve[i].length += curve[i].percent[k] = VMath.Dist(Catmull(j * step, curve[i]), Catmull((j + 1)* step, curve[i]));
					}
					// */
					
					for (int l = 0; l < FInDivider[0]; l++) {
						if(l>0) curve[i].percent[l] += curve[i].percent[l-1];
					}
					for (int l = 0; l < FInDivider[0]; l++) {
						curve[i].percent[l] /= curve[i].length;
					}
					
					if(i>0) curve[i].length += curve[i-1].length;
				}
				
			}
			
			if(FInPoints.IsChanged || FInDivider.IsChanged || FInput.IsChanged)
			{
				FOutput.SliceCount = FInput.SliceCount;
				for(int i=0; i<FInput.SliceCount ; i++)
				{
					double length = FInput[i] * curve[FInPoints.SliceCount-2].length;
					for (int j = 0; j < FInPoints.SliceCount-1; j++)
					{
						if(  length <= curve[j].length)//  length - curve[j].length <= 1E-15) //
						{
							// uniform
							double t = j >0 ? (length - curve[j-1].length)/(curve[j].length - curve[j-1].length) : length / curve[0].length;
							
							for (int l = 0; l< FInDivider[0] ; l++) // && t >0 && t<1
							{
								if(curve[j].percent[l] >= t  )
								{
									//if(l!=0)
									t = VMath.Map(t, l > 0 ?  curve[j].percent[l-1] : 0, curve[j].percent[l], l, l+1, TMapMode.Clamp) / ( FInDivider[0]) ;
									
									break;
								}
							}
							
							FOutput[i] = Catmull( t, curve[j]);
							break;
						}
					}
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Lerp", AutoEvaluate = true, Category = "Color", Help = "", Tags = "")]
	#endregion PluginInfo
	public class LerpColorNode : IPluginEvaluate
	{
		
		#region fields & pins
		[Input("Input", MinValue = 0.0, MaxValue = 1.0, Order = 0)]
		public IDiffSpread<double> FInput;
		
		[Input("From", DefaultColor = new double[] { 0.0, 0.0, 0.0, 1.0 }, Order = 1)]
		public IDiffSpread<RGBAColor> FInFrom;
		
		[Input("To", DefaultColor = new double[] { 0.0, 1.0, 0.0, 1.0 }, Order = 2)]
		public IDiffSpread<RGBAColor> FInTo;
		
		[Input("Mode", DefaultEnumEntry = "RGB", Order = 3)]
		public IDiffSpread<Mode> FInMode;
		
		[Output("Output", Order = 0)]
		public ISpread<RGBAColor> FOutput;
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		public enum Mode
		{
			RGB = 0,
			HSV
		}
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			//VColor.RGBtoHSV
			if(FInput.IsChanged || FInFrom.IsChanged || FInTo.IsChanged || FInMode.IsChanged)
			{
				FOutput.SliceCount = SpreadMax;
				for (int i = 0; i < SpreadMax; i++)
				{
					switch(FInMode[i])
					{
						case Mode.RGB:
						{
							FOutput[i] = VColor.LerpRGBA(FInFrom[i], FInTo[i], FInput[i]);
						}
						break;
						case Mode.HSV:
						{
							Vector4D vFrom, vTo, vRes;
							
							VColor.RGBtoHSV(FInFrom[i].R,FInFrom[i].G,FInFrom[i].B,out vFrom.x,out vFrom.y,out vFrom.z);
							VColor.RGBtoHSV(FInTo[i].R,FInTo[i].G,FInTo[i].B,out vTo.x,out vTo.y,out vTo.z);
							
							vFrom.w = FInFrom[i].A;
							vTo.w = FInTo[i].A;
							
							vRes = VMath.Lerp(vFrom, vTo, FInput[i]);
							
							FOutput[i] = VColor.FromHSVA(vRes.x,vRes.y,vRes.z,vRes.w);
						}
						break;
						default:
						break;
					}
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Lerp", AutoEvaluate = true, Category = "Transform", Help = "", Tags = "")]
	#endregion PluginInfo
	public class LerpTransformNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", MinValue = 0.0, MaxValue = 1.0, Order = 0)]
		public IDiffSpread<double> FInput;
		
		[Input("From", Order = 1)]
		public IDiffSpread<Matrix4x4> FInFrom;
		
		[Input("To", Order = 2)]
		public IDiffSpread<Matrix4x4> FInTo;
		
		[Output("Output", Order = 0)]
		public ISpread<Matrix4x4> FOutput;
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		
		
		private Vector3[] scaleFrom, translationFrom;
		private Quaternion[] rotationFrom;
		private Vector3[] scaleTo, translationTo;
		private Quaternion[] rotationTo;
		private int spreadMax;
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if(FInFrom.IsChanged || FInTo.IsChanged)
			{
				if(spreadMax != SpreadMax)
				{
					Array.Resize(ref scaleFrom, SpreadMax);
					Array.Resize(ref translationFrom, SpreadMax);
					Array.Resize(ref rotationFrom, SpreadMax);
					Array.Resize(ref scaleTo, SpreadMax);
					Array.Resize(ref translationTo, SpreadMax);
					Array.Resize(ref rotationTo, SpreadMax);
					
					spreadMax = SpreadMax;
				}
				
				for (int i = 0; i < SpreadMax; i++)
				{
					Matrix4x4Extensions.ToSlimDXMatrix(FInFrom[i]).Decompose(out scaleFrom[i], out rotationFrom[i], out translationFrom[i]);
					Matrix4x4Extensions.ToSlimDXMatrix(FInTo[i]).Decompose(out scaleTo[i], out rotationTo[i], out translationTo[i]);
				}
			}
			
			
			if(FInput.IsChanged || FInFrom.IsChanged || FInTo.IsChanged)
			{
				FOutput.SliceCount = SpreadMax;
				for (int i = 0; i < SpreadMax; i++)
				{
					Matrix m = Matrix.Transformation(
					Vector3.Zero,
					Quaternion.Identity,
					Vector3.Lerp(scaleFrom[i],scaleTo[i],(float)FInput[i]),
					Vector3.Zero,
					Quaternion.Lerp(rotationFrom[i],rotationTo[i],(float)FInput[i]),
					Vector3.Lerp(translationFrom[i],translationTo[i],(float)FInput[i])
					);
					
					FOutput[i] =  MatrixExtensions.ToMatrix4x4(m);
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Lerp", AutoEvaluate = true, Category = "Value", Help = "", Tags = "")]
	#endregion PluginInfo
	public class LerpValueNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", MinValue = 0.0, MaxValue = 1.0, Order = 0)]
		public IDiffSpread<double> FInput;
		
		[Input("From", DefaultValue = 0.0, Order = 1)]
		public IDiffSpread<double> FInFrom;
		
		[Input("To", DefaultValue = 1.0, Order = 2)]
		public IDiffSpread<double> FInTo;
		
		[Output("Output", Order = 0)]
		public ISpread<double> FOutput;
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if(FInput.IsChanged || FInFrom.IsChanged || FInTo.IsChanged)
			{
				FOutput.SliceCount = SpreadMax;
				for (int i = 0; i < SpreadMax; i++)
				{
					FOutput[i] = VMath.Lerp(FInFrom[i],FInTo[i],FInput[i]);
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Animator", AutoEvaluate = true, Category = "Animation", Version = "", Help = "", Tags = "")]
	#endregion PluginInfo
	public class AnimatorNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("From", DefaultValue = 0.0, Order = 0)]
		public IDiffSpread<double> FInFrom;
		
		[Input("To", DefaultValue = 1.0, Order = 1)]
		public IDiffSpread<double> FInTo;
		
		[Input("Time", DefaultValue = 1.0, MinValue = 0.01, Order = 2)]
		public IDiffSpread<double> FInTime;
		
		
		[Input("Delay", Order = 3, MinValue = 0.0)]
		public IDiffSpread<double> FInDelay;
		
		[Input("IsReverse", Order = 4)]
		public IDiffSpread<bool> FInReverse;
		
		[Input("IsRepeat", Order = 5)]
		public ISpread<bool> FInRepeat;
		
		[Input("Repeat Count", Order = 6, MinValue = 0)]
		public ISpread<int> FInRepeatCount;
		
		[Input("IsPingpong", Order = 7)]
		public IDiffSpread<bool> FInPingpong;
		
		[Input("Repeat Delay", Order = 8, MinValue = 0.0)]
		public IDiffSpread<double> FInRepeatDelay;
		
		
		
		[Input("IsPause", Order = 9)]
		public IDiffSpread<bool> FInPause;
		
		[Input("DoSeek", IsBang = true, Order = 10)]
		public ISpread<bool> FInDoSeek;
		
		[Input("Seek", Order = 11, MinValue = 0.0, MaxValue = 1.0)]
		public ISpread<double> FInSeek;
		
		[Input("Seek Type", DefaultEnumEntry = "Local", Order = 12)]
		public IDiffSpread<SeekType> FInSeekType;
		
		[Input("DoStop", IsBang = true, Order = 13)]
		public ISpread<bool> FInStop;
		
		[Input("DoStart", IsBang = true, Order = 14)]
		public ISpread<bool> FInStart;
		
		
		
		[Output("Output", Order = 0)]
		public ISpread<double> FOutput;
		
		[Output("IsRunning", Order = 1)]
		public ISpread<bool> FOutRunning;
		
		[Output("IsDelaying", Order = 2)]
		public ISpread<bool> FOutDelaying;
		
		
		[Output("OnStart", IsBang = true, Order = 3)]
		public ISpread<bool> FOutStart;
		
		[Output("IsReversed", Order = 4)]
		public ISpread<bool> FOutReversed;
		
		[Output("OnRepeat", IsBang = true, Order = 5)]
		public ISpread<bool> FOutRepeat;
		
		[Output("Repeat Count", Order = 6)]
		public ISpread<int> FOutRepeatCount;
		
		
		
		[Output("Percent", Order = 7)]
		public ISpread<double> FOutPercent;
		
		[Output("PercentSpread", IsSingle = true, Order = 8)]
		public ISpread<double> FOutPercentSpread;
		
		
		
		
		[Output("OnFinish", IsBang = true, Order = 9)]
		public ISpread<bool> FOutFinish;
		
		[Output("OnFinishSpread", IsSingle = true, IsBang = true, Order = 10)]
		public ISpread<bool> FOutFinishSpread;
		
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		
		public enum SeekType
		{
			Local = 0,
			Global
		}
		
		private IHDEHost hde;
		
		private double[] time;
		private double[] percent;
		private double[] prevPeriod;
		private bool[] isStarted;
		private int[] cycles;
		
		private bool isCompletedSpread;
		private int spreadMax;
		
		private double pFT;
		private double dFT;
		
		
		
		
		[ImportingConstructor()]
		public AnimatorNode(IHDEHost hde, ILogger logger)
		{
			this.hde = hde;
			
			spreadMax = 0;
			pFT = hde.FrameTime;
		}
		
		public static void Swap<T> (ref T lhs, ref T rhs)
		{
			T temp = lhs;
			lhs = rhs;
			rhs = temp;
		}
		
		public void Evaluate(int SpreadMax)
		{
			if(hde.FrameTime < pFT) pFT = hde.FrameTime; // overflow fix
			else
			{
				dFT = hde.FrameTime - pFT;
				pFT = hde.FrameTime;
			}
			
			if(spreadMax != SpreadMax)
			{
				FOutput.SliceCount = SpreadMax;
				FOutRunning.SliceCount = SpreadMax;
				FOutDelaying.SliceCount = SpreadMax;
				FOutStart.SliceCount = SpreadMax;
				FOutReversed.SliceCount = SpreadMax;
				FOutRepeat.SliceCount = SpreadMax;
				FOutRepeatCount.SliceCount = SpreadMax;
				FOutFinish.SliceCount = SpreadMax;
				FOutPercent.SliceCount = SpreadMax;
				
				Array.Resize(ref time, SpreadMax);
				Array.Resize(ref percent, SpreadMax);
				Array.Resize(ref prevPeriod, SpreadMax);
				Array.Resize(ref isStarted, SpreadMax);
				Array.Resize(ref cycles, SpreadMax);
				
				spreadMax = SpreadMax;
			}
			
			FOutFinishSpread[0] = false;
			
			bool isAnyChanged = false;
			
			for (int i = 0; i < SpreadMax; i++)
			{
				double period = FInTime[i]  + ( cycles[i] == 0 ? FInDelay[i] : FInRepeatDelay[i] );
				if(period<=0) continue;
				
				bool isParamChanged = false;
				bool isReversed = false;
				double from = FInFrom[i];
				double to = FInTo[i];
				
				if (FOutRunning[i] && !FInPause[i])
				{
					time[i] += dFT;
				}
				
				if(FInDoSeek[i])
				{
					isCompletedSpread = false;
					
					if(FInSeekType[i] == SeekType.Local)
					{
						time[i] = FInSeek[i] * period;
						cycles[i] = FInRepeat[i] ? cycles[i] : 0;
					}
					else
					{
						// seek global (and local)
						if(FInRepeat[i] && FInRepeatCount[i] > 0)
						{
							double prd = FInTime[i] + FInDelay[i];
							double prdRepeat = FInTime[i] + FInRepeatDelay[i];
							
							double t = FInSeek[i] * (prd + FInRepeatCount[i] * prdRepeat);
							
							if(t <= prd)
							{
								cycles[i] = 0;
								time[i] = t;
								period = prd;
							}
							else
							{
								cycles[i] = (int)(1 + (t - prd) / prdRepeat);
								
								if(FInSeek[i] < 1)
								{
									time[i] = (t - prd) % prdRepeat;
									period = prdRepeat;
								}
								else
								{
									time[i] =  prdRepeat;
									cycles[i] = FInRepeatCount[i];
								}
							}
						}
						else
						{
							time[i] = FInSeek[i] *  period;
							cycles[i] = FInRepeat[i] ? cycles[i] : 0;
						}
						
					}
					FOutRepeatCount[i] = cycles[i];
					
					isParamChanged = true;
				}
				
				if(FInTime.IsChanged || FInDelay.IsChanged || FInRepeatDelay.IsChanged)
				{
					if(time[i]>0) time[i] = time[i] / prevPeriod[i] * period ;
					
					
					// если повторы есть.
					
					prevPeriod[i] = period;
					isParamChanged = true;
				}
				
				if(FInStart[i] || FInStop[i])
				{ 
					isCompletedSpread = false;
					
					isStarted[i] = false;
					cycles[i] = 0;
					time[i] = 0;
					
					FOutRepeatCount[i] = 0;
					
					if(FInStart[i])
					{
						FOutRunning[i] = true;
					}
					if(FInStop[i]) // priority
					{
						FOutRunning[i] = false;
					}
					
					isParamChanged = true;
				}
				
				if(FInReverse[i])
				{
					Swap(ref from, ref to);
					isReversed = !isReversed;
				}
				if(FInPingpong[i] && cycles[i] % 2 != 0)
				{
					Swap(ref from, ref to);
					isReversed = !isReversed;
				}
				if(FInReverse.IsChanged || FInPingpong.IsChanged)
				{
					isParamChanged = true;
				}
				
				if(FInFrom.IsChanged || FInTo.IsChanged)
				{
					isParamChanged = true;
				}
				
				FOutStart[i] = false;
				FOutRepeat[i] = false;
				FOutFinish[i] = false;
				FOutDelaying[i] = false;
				FOutReversed[i] = isReversed;
				
				if((FOutRunning[i] && !FInPause[i]) || isParamChanged)
				{
					percent[i] = time[i] / period;
					int count = (int)(percent[i]);
					isAnyChanged = true;
					
					// percent of animation (without delaying)
					double t = time[i] - ( cycles[i] == 0 ? FInDelay[i] : FInRepeatDelay[i] );
					if(FOutRunning[i] && !FInPause[i]) // if running
					{
						FOutDelaying[i] = t<0;
					}
					if(t>=0 || isStarted[i] == false) // time is comming or start/stop bang
					{
						FOutput[i] = VMath.Clamp(VMath.Lerp(from, to,  t / FInTime[i] ), from, to);
					}
					
					
					if(time[i] >= 0 && isStarted[i] == false && FOutRunning[i]) // start bang
					{
						isStarted[i] = true;
						FOutStart[i] = true;
					}
					
					if(count >= 1)
					{
						FOutput[i] = to;
						
						if(FInRepeat[i] && (FInRepeatCount[i] == 0 || cycles[i] < FInRepeatCount[i]) && !FInDoSeek[i])
						{
							percent[i] = 0;
							cycles[i] += count;
							time[i] = time[i] % period;
							
							/*
time[i] = time[i] % period;

// flip if next reversed
bool isReversedNext = false;
if(FInReverse[i])
{
isReversedNext = !isReversedNext;
}
if(FInPingpong[i] && cycles[i] % 2 != 0)
{
isReversedNext = !isReversedNext;
}
if(isReversedNext != isReversed) FOutput[i] = VMath.Clamp(VMath.Lerp(to, from,  t / FInTime[i] ), to, from);
*/
							
							if(FOutRunning[i] && !FInPause[i])
							{
								FOutRepeat[i] = true;
								FOutRepeatCount[i] = cycles[i];
							}
						}
						else
						{
							percent[i] = 1;
							if(FOutRunning[i] && !FInPause[i])
							{
								FOutRunning[i] = false;
								FOutFinish[i] = true;
							}
						}
					}
					
				}
				
			}
			
			// calculate percentages
			if(isAnyChanged)
			{
				FOutPercentSpread[0] = 0;
				
				int completedCount = 0;
				bool hasInfinite = false;
				for (int i = 0; i < SpreadMax; i++)
				{
					if(FInRepeat[i] && FInRepeatCount[i] <= 0)
					{
						FOutPercent[i] = 0;
						FOutPercentSpread[0] = 0;
						hasInfinite = true;
					}
					else
					{
						double period = FInTime[i] + FInDelay[i];
						double periodRepeat = FInTime[i] + FInRepeatDelay[i];
						
						// global uniform percent
						FOutPercent[i] = (cycles[i]==0) ? percent[i] * period : period + (cycles[i]-1 + percent[i]) * periodRepeat;
						FOutPercent[i] /=  !FInRepeat[i] ? period : period + FInRepeatCount[i] * periodRepeat;
						
						// fast non-unform percent
						//FOutPercent[i] =  VMath.Clamp( !FInRepeat[i] ? percent[i]  : (cycles[i] + percent[i]) / (FInRepeatCount[i]+1) ,0, 1); // ...
					}
					if(!hasInfinite) FOutPercentSpread[0] += FOutPercent[i] / SpreadMax;
					
					if(FOutPercent[i] == 1) completedCount++;
				}
				
				if(completedCount == SpreadMax && isCompletedSpread == false)
				{
					isCompletedSpread = true;
					FOutFinishSpread[0] = true;
				}
				
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Ease", AutoEvaluate = true, Category = "Animation", Help = "", Tags = "")]
	#endregion PluginInfo
	public class EaseNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", MinValue = 0,  MaxValue = 1, Order = 0)]
		public ISpread<double> FInput;
		
		[Input("Type", Order = 1)]
		public ISpread<Type> FInType;
		
		[Input("InOut", DefaultEnumEntry  = "InOut", Order = 2)]
		public ISpread<TypeInOut> FInTypeInOut;
		
		[Input("Reverse", Order = 3)]
		public ISpread<bool> FInReverse;
		
		[Output("Output", Order = 0)]
		public ISpread<double> FOutput;
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		public enum Type
		{
			Linear = 0,
			Quad,
			Cubic,
			Quart,
			Quint,
			Circ,
			Expo,
			Sine,
			Back,
			Bounce,
			Elastic,
			Swift
		}
		public enum TypeInOut
		{
			In = 0,
			Out,
			InOut
		}
		
		private Func<double,double,double,double,double>[,] easingFunctions = {
			{ EasingFunctions.Linear, EasingFunctions.Linear, EasingFunctions.Linear },
			{ EasingFunctions.QuadIn, EasingFunctions.QuadOut, EasingFunctions.QuadInOut },
			{ EasingFunctions.CubicIn, EasingFunctions.CubicOut, EasingFunctions.CubicInOut },
			{ EasingFunctions.QuartIn, EasingFunctions.QuartOut, EasingFunctions.QuartInOut },
			{ EasingFunctions.QuintIn, EasingFunctions.QuintOut, EasingFunctions.QuintInOut },
			{ EasingFunctions.CircIn, EasingFunctions.CircOut, EasingFunctions.CircInOut },
			{ EasingFunctions.ExpoIn, EasingFunctions.ExpoOut, EasingFunctions.ExpoInOut },
			{ EasingFunctions.SineIn, EasingFunctions.SineOut, EasingFunctions.SineInOut },
			{ EasingFunctions.BackIn, EasingFunctions.BackOut, EasingFunctions.BackInOut },
			{ EasingFunctions.BounceIn, EasingFunctions.BounceOut, EasingFunctions.BounceInOut },
			{ EasingFunctions.ElasticIn, EasingFunctions.ElasticOut, EasingFunctions.ElasticInOut },
			{ EasingFunctions.SwiftIn, EasingFunctions.SwiftOut, EasingFunctions.SwiftInOut }
		};
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FOutput.SliceCount = SpreadMax;
			for (int i = 0; i < SpreadMax; i++)
			{
				FOutput[i] = easingFunctions[(int)FInType[i],(int)FInTypeInOut[i]](VMath.Clamp(FInReverse[i] ? 1 - FInput[i] : FInput[i],0,1),0,1,1);
				if(FInReverse[i])
				{
					FOutput[i] = 1 - FOutput[i];
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Ease", AutoEvaluate = true, Category = "Animation", Version = "Custom", Help = "", Tags = "")]
	#endregion PluginInfo
	public class EaseCustomNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", MinValue = 0,  MaxValue = 1, Order = 0)]
		public ISpread<double> FInput;
		
		[Input("A", MinValue = 0,  MaxValue = 1, Order = 1)]
		public ISpread<double> FInA;
		
		[Input("B", MinValue = 0,  MaxValue = 1, Order = 2)]
		public ISpread<double> FInB;
		
		[Input("C", MinValue = 0,  MaxValue = 1, Order = 3)]
		public ISpread<double> FInC;
		
		[Input("D", MinValue = 0,  MaxValue = 1, Order = 4)]
		public ISpread<double> FInD;
		
		[Input("Reverse", Order = 5)]
		public ISpread<bool> FInReverse;
		
		
		
		[Output("Output", Order = 0)]
		public ISpread<double> FOutput;
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FOutput.SliceCount = SpreadMax;
			for (int i = 0; i < SpreadMax; i++)
			{
				FOutput[i] = EasingFunctions.CubicBezier(VMath.Clamp(FInReverse[i] ? 1 - FInput[i] : FInput[i],0,1),FInA[i],FInB[i],FInC[i],FInD[i]);
				if(FInReverse[i])
				{
					FOutput[i] = 1 - FOutput[i];
				}
			}
			
		}
	}
	
	public class EasingFunctions
	{
		// from: https://github.com/armadillu/ofxAnimatable
		
		static public double CubicBezier(double x, double a, double b, double c, double d)
		{
			double y0a = 0.00; // initial y
			double x0a = 0.00; // initial x
			double y1a = b;    // 1st influence y
			double x1a = a;    // 1st influence x
			double y2a = d;    // 2nd influence y
			double x2a = c;    // 2nd influence x
			double y3a = 1.00; // final y
			double x3a = 1.00; // final x
			
			double A = x3a - 3.0 * x2a + 3.0 * x1a - x0a;
			double B = 3.0 * x2a - 6.0 * x1a + 3.0 * x0a;
			double C = 3.0 * x1a - 3.0 * x0a;
			double D = x0a;
			
			double E = y3a - 3.0 * y2a + 3.0 * y1a - y0a;
			double F = 3.0 * y2a - 6.0 * y1a + 3.0 * y0a;
			double G = 3.0 * y1a - 3.0 * y0a;
			double H = y0a;
			
			// Solve for t given x (using Newton-Raphelson), then solve for y given t.
			// Assume for the first guess that t = x.
			double t = x;
			int nRefinementIterations = 5;
			for (int i = 0; i < nRefinementIterations; i++){
				double currentx = A * (t * t * t) + B * (t * t) + C * t + D  ;
				double currentslope = 1.0 / (3.0 * A * t * t + 2.0 * B * t + C);
				t -= (currentx - x)*(currentslope);
				t = VMath.Clamp(t, 0.0, 1.0);
			}
			
			return E * (t * t * t) + F * (t * t) + G * t + H;
		}
		
		
		// In Out InOut the same
		static public double Linear(double t,double b , double c, double d){
			return c*t/d + b;
		}
		
		static public double QuadIn(double t,double b , double c, double d){
			return c*(t/=d)*t + b;
		}
		static public double QuadOut(double t,double b , double c, double d){
			return -c *(t/=d)*(t-2) + b;
		}
		static public double QuadInOut(double t,double b , double c, double d){
			if ((t/=d/2) < 1) return ((c/2)*(t*t)) + b;
			return -c/2 * (((--t)*(t-2)) - 1) + b;
		}
		
		static public double CubicIn(double t,double b , double c, double d){
			return c*(t/=d)*t*t + b;
		}
		static public double CubicOut(double t,double b , double c, double d){
			return c*((t=t/d-1)*t*t + 1) + b;
		}
		static public double CubicInOut(double t,double b , double c, double d){
			if ((t/=d/2) < 1) return c/2*t*t*t + b;
			return c/2*((t-=2)*t*t + 2) + b;
		}
		
		static public double QuartIn(double t,double b , double c, double d){
			return c*(t/=d)*t*t*t + b;
		}
		static public double QuartOut(double t,double b , double c, double d){
			return -c * ((t=t/d-1)*t*t*t - 1) + b;
		}
		static public double QuartInOut(double t,double b , double c, double d){
			if ((t/=d/2) < 1) return c/2*t*t*t*t + b;
			return -c/2 * ((t-=2)*t*t*t - 2) + b;
		}
		
		static public double QuintIn(double t,double b , double c, double d){
			return c*(t/=d)*t*t*t*t + b;
		}
		static public double QuintOut(double t,double b , double c, double d){
			return c*((t=t/d-1)*t*t*t*t + 1) + b;
		}
		static public double QuintInOut(double t,double b , double c, double d){
			if ((t/=d/2) < 1) return c/2*t*t*t*t*t + b;
			return c/2*((t-=2)*t*t*t*t + 2) + b;
		}
		
		static public double CircIn(double t,double b , double c, double d){
			return -c * (Math.Sqrt(1 - (t/=d)*t) - 1) + b;
		}
		static public double CircOut(double t,double b , double c, double d){
			return c * Math.Sqrt(1 - (t=t/d-1)*t) + b;
		}
		static public double CircInOut(double t,double b , double c, double d){
			if ((t/=d/2) < 1) return -c/2 * (Math.Sqrt(1 - t*t) - 1) + b;
			return c/2 * (1+Math.Sqrt(1- (t-=2)*t)) + b;
		}
		
		static public double ExpoIn(double t,double b , double c, double d){
			return (t==0) ? b : c * Math.Pow(2, 10 * (t/d - 1)) + b;
		}
		static public double ExpoOut(double t,double b , double c, double d){
			return (t==d) ? b+c : c * (-Math.Pow(2, -10 * t/d) + 1) + b;
		}
		static public double ExpoInOut(double t,double b , double c, double d){
			if (t==0) return b;
			if (t==d) return b+c;
			if ((t/=d/2) < 1) return c/2 * Math.Pow(2, 10 * (t - 1)) + b;
			return c/2 * (-Math.Pow(2, -10 * --t) + 2) + b;
		}
		
		static public double SineIn(double t,double b , double c, double d){
			return -c * Math.Cos(t/d * (Math.PI/2)) + c + b;
		}
		static public double SineOut(double t,double b , double c, double d){
			return c * Math.Sin(t/d * (Math.PI/2)) + b;
		}
		static public double SineInOut(double t,double b , double c, double d){
			return -c/2 * (Math.Cos(Math.PI*t/d) - 1) + b;
		}
		
		static public double BackIn(double t,double b , double c, double d){
			double s = 1.70158;
			double postFix = t/=d;
			return c*(postFix)*t*((s+1)*t - s) + b;
		}
		static public double BackOut(double t,double b , double c, double d){
			double s = 1.70158;
			return c*((t=t/d-1)*t*((s+1)*t + s) + 1) + b;
		}
		static public double BackInOut(double t,double b , double c, double d){
			double s = 1.70158;
			if ((t/=d/2) < 1) return c/2*(t*t*(((s*=(1.525))+1)*t - s)) + b;
			double postFix = t-=2;
			return c/2*((postFix)*t*(((s*=(1.525))+1)*t + s) + 2) + b;
		}
		
		static public double BounceIn(double t,double b , double c, double d){
			return c - BounceOut (d-t, 0, c, d) + b;
		}
		
		static public double BounceOut(double t,double b , double c, double d){
			if ((t/=d) < (1/2.75)) {
				return c*(7.5625*t*t) + b;
			} else if (t < (2/2.75)) {
				double postFix = t-=(1.5/2.75);
				return c*(7.5625*(postFix)*t + .75) + b;
			} else if (t < (2.5/2.75)) {
				double postFix = t-=(2.25/2.75);
				return c*(7.5625*(postFix)*t + .9375) + b;
			} else {
				double postFix = t-=(2.625/2.75);
				return c*(7.5625*(postFix)*t + .984375) + b;
			}
		}
		
		static public double BounceInOut(double t,double b , double c, double d){
			if (t < d/2) return BounceIn (t*2, 0, c, d) * .5 + b;
			else return BounceOut (t*2-d, 0, c, d) * .5 + c*.5 + b;
		}
		
		static public double ElasticIn(double t,double b , double c, double d){
			if (t==0) return b;  if ((t/=d)==1) return b+c;
			double p=d*.3;
			double a=c;
			double s=p/4;
			double postFix =a*Math.Pow(2,10*(t-=1));
			return -(postFix * Math.Sin((t*d-s)*(2*Math.PI)/p )) + b;
		}
		static public double ElasticOut(double t,double b , double c, double d){
			if (t==0) return b;  if ((t/=d)==1) return b+c;
			double p=d*.3;
			double a=c;
			double s=p/4;
			return (a*Math.Pow(2,-10*t) * Math.Sin( (t*d-s)*(2*Math.PI)/p ) + c + b);
		}
		static public double ElasticInOut(double t,double b , double c, double d){
			if (t==0) return b;  if ((t/=d/2)==2) return b+c;
			double p=d*(.3*1.5);
			double a=c;
			double s=p/4;
			
			if (t < 1) {
				double postFix =a*Math.Pow(2,10*(t-=1));
				return -.5*(postFix* Math.Sin( (t*d-s)*(2*Math.PI)/p )) + b;
			}
			else{
				double postFix =  a*Math.Pow(2,-10*(t-=1));
				return postFix * Math.Sin( (t*d-s)*(2*Math.PI)/p )*.5 + c + b;
			}
		}
		
		// from: https://github.com/jenius/axis
		static public double SwiftIn(double t,double b , double c, double d)
		{
			return CubicBezier(t, 0.900,  0.000, 0.450, 1.000);
		}
		
		static public double SwiftOut(double t,double b , double c, double d)
		{
			return CubicBezier(t, 0.550,  0.000, 0.100, 1.000);
		}
		
		static public double SwiftInOut(double t,double b , double c, double d)
		{
			return CubicBezier(t, 0.900,  0.000, 0.100, 1.000);
		}
		
		
	}
	
	
}


