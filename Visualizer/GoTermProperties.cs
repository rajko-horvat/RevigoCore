using System;
using System.Collections.Generic;

namespace IRB.Revigo.Visualizer
{
	/// <summary>
	/// 
	/// Authors:
	///		Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License:
	///		MIT
	///		Copyright (c) 2011-2023, Ruđer Bošković Institute
	///		
	/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
	/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
	/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
	/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	/// 	subject to the following conditions: 
	/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
	/// 	The names of authors and contributors may not be used to endorse or promote products derived from this software 
	/// 	without specific prior written permission.
	/// 	
	///		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	///		INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
	///		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	///		IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
	///		DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
	///		ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
	/// </summary>
	public class GOTermProperties
	{		
		// Term properties
		private int iGOID = 0;
		private double dValue = double.NaN;
		private double dTransformedValue = double.NaN;
		private double dUniqueness = double.NaN;
		private double dDispensability = double.NaN;
		private double dAnnotationSize = double.NaN;
		private double dLogAnnotationSize = double.NaN;
		private double dAnnotationFrequency = double.NaN;
		private bool bPinned = false;
		private int iRepresentative = -1;
		private int iDispensedBy = -1;
		private List<double> aPC = new List<double>();
		private List<double> aPC3 = new List<double>();
		private List<double> aUserValues = new List<double>();

		public GOTermProperties(int goID)
		{
			this.iGOID = goID;
		}

		public int GOID
		{
			get
			{
				return this.iGOID;
			}
		}

		public double Value
		{
			get
			{
				return this.dValue;
			}
			set
			{
				this.dValue = value;
			}
		}

		public double TransformedValue
		{
			get
			{
				return this.dTransformedValue;
			}
			set
			{
				this.dTransformedValue = value;
			}
		}

		public double Uniqueness
		{
			get
			{
				return this.dUniqueness;
			}
			set
			{
				this.dUniqueness = value;
			}
		}

		public double Dispensability
		{
			get
			{
				return this.dDispensability;
			}
			set
			{
				this.dDispensability = value;
			}
		}

		public double AnnotationSize
		{
			get
			{
				return this.dAnnotationSize;
			}
			set
			{
				this.dAnnotationSize = value;
			}
		}

		public double LogAnnotationSize
		{
			get
			{
				return this.dLogAnnotationSize;
			}
			set
			{
				this.dLogAnnotationSize = value;
			}
		}

		public double AnnotationFrequency
		{
			get
			{
				return this.dAnnotationFrequency;
			}
			set
			{
				this.dAnnotationFrequency = value;
			}
		}

		public bool Pinned
		{
			get
			{
				return this.bPinned;
			}
			set
			{
				this.bPinned = value;
			}
		}

		public int Representative
		{
			get
			{
				return this.iRepresentative;
			}
			set
			{
				this.iRepresentative = value;
			}
		}

		public int DispensedBy
		{
			get
			{
				return this.iDispensedBy;
			}
			set
			{
				this.iDispensedBy = value;
			}
		}

		public List<double> PC
		{
			get
			{
				return this.aPC;
			}
		}

		public List<double> PC3
		{
			get
			{
				return this.aPC3;
			}
		}

		public List<double> UserValues
		{
			get
			{
				return this.aUserValues;
			}
		}
	}
}
