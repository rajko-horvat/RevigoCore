using System;

namespace IRB.Revigo.Core.MDS
{
	/// <summary>
	/// The MDS algorithm implementation, utilities
	/// 
	/// Authors:
	/// 	Unknown Author (Java)
	/// 	Rajko Horvat (C#, rhorvat at irb.hr)
	/// 
	/// License:
	/// 	MIT
	/// 	Copyright (c) 2011-2023, Ruđer Bošković Institute
	///		
	/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
	/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
	/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
	/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	/// 	subject to the following conditions: 
	/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
	/// 	The names of authors and contributors may not be used to endorse or promote Software products derived from this software 
	/// 	without specific prior written permission.
	/// 	
	/// 	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	/// 	INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
	/// 	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	/// 	IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
	/// 	DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
	/// 	ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
	/// </summary>
	internal static class Utilities
	{
		public static void Randomize(RandomMT19937 rnd, double[,] matrix)
		{
			for (int row = 0; row < matrix.GetLength(0); row++)
			{
				for (int col = 0; col < matrix.GetLength(1); col++)
				{
					matrix[row, col] = 0.5D - rnd.NextDouble();
				}
			}
		}

		public static double Product(double[] array, double[] array1)
		{
			double result = 0.0D;
			int length = Math.Min(array.Length, array1.Length);

			for (int i = 0; i < length; i++)
			{
				result += array[i] * array1[i];
			}

			return result;
		}

		public static double Product(double[,] matrix, int matrixRow, double[] multipliers)
		{
			double result = 0.0D;
			int length = Math.Min(matrix.GetLength(1), multipliers.Length);

			for (int i = 0; i < length; i++)
			{
				result += matrix[matrixRow, i] * multipliers[i];
			}

			return result;
		}

		public static double Product(double[,] matrix, int matrixRow, double[,] matrix1, int matrix1Row)
		{
			double result = 0.0D;

			int length = Math.Min(matrix.GetLength(1), matrix1.GetLength(1));

			for (int i = 0; i < length; i++)
			{
				result += matrix[matrixRow, i] * matrix1[matrix1Row, i];
			}

			return result;
		}

		public static void Normalize(double[] array)
		{
			double norm = Math.Sqrt(Product(array, array));

			for (int i = 0; i < array.Length; i++)
			{
				array[i] /= norm;
			}
		}

		public static void Normalize(double[,] matrix, int row)
		{
			double norm = Math.Sqrt(Product(matrix, row, matrix, row));

			for (int col = 0; col < matrix.GetLength(1); col++)
			{
				matrix[row, col] /= norm;
			}
		}

		public static void SquareEntries(double[,] matrix)
		{
			for (int row = 0; row < matrix.GetLength(0); row++)
			{
				for (int col = 0; col < matrix.GetLength(1); col++)
				{
					matrix[row, col] = Math.Pow(matrix[row, col], 2.0D);
				}
			}
		}

		public static int[] LandmarkIndices(double[,] matrix)
		{
			int[] result = new int[matrix.GetLength(0)];

			for (int row = 0; row < matrix.GetLength(0); row++)
			{
				for (int col = 0; col < matrix.GetLength(1); col++)
				{
					if (matrix[row, col] == 0.0D)
						result[row] = col;
				}
			}

			return result;
		}

		public static double[,] LandmarkMatrix(double[,] matrix)
		{
			int mRows = matrix.GetLength(0);
			double[,] result = new double[mRows, mRows];
			int[] index = LandmarkIndices(matrix);

			for (int row = 0; row < mRows; row++)
			{
				for (int col = 0; col < mRows; col++)
					result[row, col] = matrix[row, index[col]];

			}

			return result;
		}

		public static void DoubleCenter(double[,] matrix)
		{
			int mRows = matrix.GetLength(0);
			int mCols = matrix.GetLength(1);

			for (int row = 0; row < mRows; row++)
			{
				double avg = 0.0D;
				for (int col = 0; col < mCols; col++)
				{
					avg += matrix[row, col];
				}

				avg /= mCols;
				for (int col = 0; col < mCols; col++)
				{
					matrix[row, col] -= avg;
				}
			}

			for (int col = 0; col < mCols; col++)
			{
				double avg = 0.0D;
				for (int row = 0; row < mRows; row++)
				{
					avg += matrix[row, col];
				}

				avg /= mRows;
				for (int row = 0; row < mRows; row++)
				{
					matrix[row, col] -= avg;
				}
			}
		}

		public static void Multiply(double[,] matrix, double factor)
		{
			for (int row = 0; row < matrix.GetLength(0); row++)
			{
				for (int col = 0; col < matrix.GetLength(1); col++)
				{
					matrix[row, col] *= factor;
				}
			}
		}

		public static double[,] WeightMatrix(double[,] matrix, double exponent)
		{
			int mRows = matrix.GetLength(0);
			int mCols = matrix.GetLength(1);
			double[,] result = new double[mRows, mCols];

			for (int row = 0; row < mRows; row++)
			{
				for (int col = 0; col < mCols; col++)
				{
					if (matrix[row, col] > 0.0D)
					{
						result[row, col] = Math.Pow(matrix[row, col], exponent);
					}
				}
			}

			return result;
		}
	}
}
