using System;
using System.Collections.Generic;
using System.Text;

namespace MDS
{
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
