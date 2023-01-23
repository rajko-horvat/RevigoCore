using System;

namespace IRB.MDS
{
	/// <summary>
	/// The MDS algorithm implementation
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
	public static class MDS
	{
		public static double[,] ClassicalMDSScaling(double[,] dist, int dim)
		{
			return ClassicalMDSScaling(new RandomMT19937(), dist, dim);
		}

		public static double[,] ClassicalMDSScaling(RandomMT19937 rnd, double[,] dist, int dim)
		{
			double[,] result = new double[dim, dist.GetLength(1)];
			Utilities.Randomize(rnd, result);
			lmds(rnd, dist, result);

			return result;
		}

		public static double[,] StressMinimization(double[,] dist, int dim, int maxIter, double threshold, int timeout)
		{
			return StressMinimization(new RandomMT19937(), dist, dim, maxIter, threshold, timeout);
		}

		public static double[,] StressMinimization(RandomMT19937 rnd, double[,] dist, int dim, int maxIter, double threshold, int timeout)
		{
			double[,] positions = ClassicalMDSScaling(rnd, dist, dim);
			double[,] weights = Utilities.WeightMatrix(dist, 0.0D);

			MinimizeStress(dist, positions, weights, maxIter, threshold, timeout);

			return positions;
		}

		#region Stress minimization
		private static void MinimizeStress(double[,] dist, double[,] positions, double[,] weights, int iter, double threshold, double timeout)
		{
			int n = positions.GetLength(1);
			int mRows = dist.GetLength(0);
			int dim = positions.GetLength(0);
			int[] index = Utilities.LandmarkIndices(dist);
			double[] wSum = new double[n];

			for (int i = 0; i < n; i++)
			{
				for (int j = 0; j < mRows; j++)
				{
					wSum[i] += weights[j, i];
				}
			}

			double eps = Math.Pow(10D, -threshold);
			DateTime time = DateTime.Now;
			if (iter == 0)
				iter = 0x989680;
			for (int c = 0; c < iter; c++)
			{
				double change = 0.0D;
				double magnitude = 0.0D;
				for (int i = 0; i < n; i++)
				{
					double[] xnew = new double[dim];
					for (int j = 0; j < mRows; j++)
					{
						double inv = 0.0D;
						for (int m = 0; m < dim; m++)
						{
							inv += Math.Pow(positions[m, i] - positions[m, index[j]], 2D);
						}

						if (inv != 0.0D)
							inv = Math.Pow(inv, -0.5D);
						for (int m = 0; m < dim; m++)
						{
							xnew[m] += weights[j, i] * (positions[m, index[j]] + dist[j, i] * (positions[m, i] - positions[m, index[j]]) * inv);
						}
					}

					if (wSum[i] != 0.0D)
					{
						for (int m = 0; m < dim; m++)
						{
							change += Math.Pow(xnew[m] / wSum[i] - positions[m, i], 2D);
							magnitude += Math.Pow(positions[m, i], 2D);
							positions[m, i] = xnew[m] / wSum[i];
						}
					}
				}

				change = Math.Sqrt(change / magnitude);
				double timediff = (DateTime.Now - time).TotalMilliseconds;
				if ((timeout > 0 && timediff > timeout) || (eps > 0 && change < eps) || (iter > 0 && c >= iter - 1))
					break;
			}
		}
		#endregion

		#region Classical scalling
		private static void lmds(RandomMT19937 rnd, double[,] dist, double[,] result)
		{
			int mRows = dist.GetLength(0);
			int mCols = dist.GetLength(1);
			int dim = result.GetLength(0);
			double[,] distCopy = new double[dist.GetLength(0), dist.GetLength(1)];

			for (int row = 0; row < mRows; row++)
			{
				for (int col = 0; col < mCols; col++)
				{
					distCopy[row, col] = dist[row, col];
				}
			}

			Utilities.SquareEntries(distCopy);

			double[] mean = new double[mCols];

			for (int row = 0; row < mRows; row++)
			{
				for (int col = 0; col < mCols; col++)
				{
					mean[col] += distCopy[row, col];
				}
			}

			for (int col = 0; col < mCols; col++)
			{
				mean[col] /= mRows;
			}

			double[] lambda = new double[dim];
			double[,] temp = new double[dim, mRows];

			Utilities.Randomize(rnd, temp);
			double[,] landmark = Utilities.LandmarkMatrix(dist);
			Utilities.SquareEntries(landmark);
			Utilities.DoubleCenter(landmark);
			Utilities.Multiply(landmark, -0.5D);
			Eigen(rnd, landmark, temp, lambda);

			for (int d = 0; d < dim; d++)
			{
				double dTemp = Math.Sqrt(Math.Abs(lambda[d]));// *Math.Sign(lambda[i]);

				for (int row = 0; row < mRows; row++)
				{
					temp[d, row] *= dTemp;
				}
			}

			for (int d = 0; d < dim; d++)
			{
				for (int col = 0; col < mCols; col++)
				{
					result[d, col] = 0.0D;
					for (int row = 0; row < mRows; row++)
					{
						result[d, col] -= (0.5D * (distCopy[row, col] - mean[col]) * temp[d, row]) / lambda[d];
					}
				}
			}
		}

		private static void Eigen(RandomMT19937 rnd, double[,] matrix, double[,] evecs, double[] lambda)
		{
			int dim = lambda.Length;
			int mRows = matrix.GetLength(0);

			for (int d = 0; d < dim; d++)
			{
				if (d > 0)
				{
					for (int row = 0; row < mRows; row++)
					{
						for (int col = 0; col < mRows; col++)
						{
							matrix[row, col] -= lambda[d - 1] * evecs[d - 1, row] * evecs[d - 1, col];
						}
					}

				}
				for (int row = 0; row < mRows; row++)
				{
					evecs[d, row] = rnd.NextDouble();
				}

				Utilities.Normalize(evecs, d);

				double threshold = 0.0D;
				for (int iter = 0; Math.Abs(1.0D - threshold) > 9.9999999999999995E-007D && iter < 100; iter++)
				{
					double[] productSum = new double[mRows];
					for (int row = 0; row < mRows; row++)
					{
						for (int col = 0; col < mRows; col++)
						{
							productSum[row] += matrix[row, col] * evecs[d, col];
						}
					}

					lambda[d] = Utilities.Product(evecs, d, productSum);
					Utilities.Normalize(productSum);
					threshold = Math.Abs(Utilities.Product(evecs, d, productSum));
					for (int row = 0; row < mRows; row++)
					{
						evecs[d, row] = productSum[row];
					}
				}
			}
		}
		#endregion
	}
}
