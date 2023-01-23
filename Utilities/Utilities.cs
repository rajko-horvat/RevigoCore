using System;

namespace IRB.Revigo
{
	/// <summary>
	/// 
	/// Authors:
	///		Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License:
	///		MIT
	///		Copyright (c) 2021, Ruđer Bošković Institute
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
	public static class Utilities
	{
		public static int[] QuickSort(double[] values)
		{
			int[] index = new int[values.Length];

			for (int i = 0; i < index.Length; i++)
			{
				index[i] = i;
			}
			QuickSortInternal(ref values, ref index, 0, values.Length - 1);

			return index;
		}

		private static void QuickSortInternal(ref double[] values, ref int[] index, int left, int right)
		{
			if (left < right)
			{
				int middle = partition(ref values, ref index, left, right);
				QuickSortInternal(ref values, ref index, left, middle);
				QuickSortInternal(ref values, ref index, middle + 1, right);
			}
		}

		private static int partition(ref double[] values, ref int[] index, int left, int right)
		{
			double pivot = values[index[(left + right) / 2]];
			int tmp;

			while (left < right)
			{
				while ((values[index[left]] < pivot) && (left < right))
				{
					left++;
				}
				while ((values[index[right]] > pivot) && (left < right))
				{
					right--;
				}
				if (left < right)
				{
					tmp = index[left];
					index[left] = index[right];
					index[right] = tmp;
					left++;
					right--;
				}
			}
			if ((left == right) && (values[index[right]] > pivot))
			{
				right--;
			}

			return right;
		}
	}
}
