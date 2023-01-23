using System;
using IRB.Revigo.Databases;
using IRB.Revigo.Core;

namespace IRB.Revigo.Visualizer
{
	/// <summary>
	/// 
	/// Authors:
	/// 	Fran Supek (fsupek at irb.hr)
	/// 	Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License:
	/// 	MIT
	/// 	Copyright (c) 2021, Ruđer Bošković Institute
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
	public class TermSimilarityMatrix
	{
		private TermListVisualizer oParent = null;

		private double[,] oMatrix = null;

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public TermSimilarityMatrix(TermListVisualizer parent)
			: this(parent, null)
		{ }

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public TermSimilarityMatrix(TermListVisualizer parent, ProgressEventHandler progress)
		{
			this.oParent = parent;
			ConstructMatrix(progress);
		}

		public TermListVisualizer Parent
		{
			get
			{
				return this.oParent;
			}
		}

		public double[,] Matrix
		{
			get
			{
				return this.oMatrix;
			}
		}

		private void ConstructMatrix(ProgressEventHandler progress)
		{
			SemanticSimilarityScoreEnum simScore = this.oParent.Parent.Measure;
			GeneOntology myGO = this.oParent.Parent.Ontology;
			SpeciesAnnotations oAnnotations = this.oParent.Parent.Annotations;
			GOTerm[] terms = this.oParent.Terms;
			int iTermCount = terms.Length;
			double dProgressStep = 100.0 / (double)iTermCount;
			double dOldProgress = 0.0;
			this.oMatrix = new double[iTermCount, iTermCount];

			for (int i = 0; i < iTermCount; i++)
			{
				GOTerm go1 = terms[i];

				this.oMatrix[i, i] = 1.0;

				for (int j = i + 1; j < iTermCount; j++)
				{
					GOTerm go2 = terms[j];
					double simil =
						SemanticSimilarityScore.GetSemanticSimilarityScore(simScore).calculateDistance(go1, go2, oAnnotations, myGO);

					this.oMatrix[i, j] = simil;
					this.oMatrix[j, i] = simil;
				}

				double dProgress = (double)(i + 1) * dProgressStep;
				if ((dProgress - dOldProgress) >= 0.1 && progress != null)
				{
					dOldProgress = dProgress;
					progress(this, new ProgressEventArgs(dProgress));
				}
			}
		}

		/// <summary>
		/// Use TermSimilaryMatrix to calculate "uniqueness" of each GO term,
		/// i.e.its average semantic distance from all other GO terms in the matrix.
		/// 
		/// The "commonness" will be stored as a property in the provided
		/// GoTermProperties object (this will overwrite old values of "uniqueness", if present).
		/// </summary>
		public void CalculateUniqueness()
		{
			GOTerm[] terms = this.oParent.Terms;
			int iTermCount = terms.Length;
			GOTermProperties[] properties = this.oParent.Properties;

			for (int i = 0; i < iTermCount; i++)
			{
				double sum = 0.0;
				int count = 0;

				for (int j = 0; j < iTermCount; j++)
				{
					if (i != j && !double.IsNaN(this.oMatrix[i, j]))
					{
						sum += this.oMatrix[i, j];
						count++;
					}
				}

				if (count <= 1)
				{
					properties[i].Uniqueness = 1.0;
				}
				else
				{
					// this assumes the semantic similarity measure in the matrix ranges
					// from 0 to 1, so that 1-x can be used to convert 'similarity' to
					// 'distance'
					properties[i].Uniqueness = Math.Pow(1.0 - (sum / (double)count), 2.0);
				}
			}
		}

		public TermSimilarityMatrix Clone()
		{
			TermSimilarityMatrix result = new TermSimilarityMatrix(this.oParent);

			int iLength = this.oMatrix.GetLength(0);
			result.oMatrix = new double[iLength, iLength];

			Array.Copy(this.oMatrix, result.oMatrix, this.oMatrix.Length);

			return result;
		}
	}
}
