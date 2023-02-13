using System;
using System.Data.Common;
using IRB.Collections.Generic;
using IRB.Revigo.Databases;
using IRB.Revigo.Worker;

namespace IRB.Revigo.Core
{
	/// <summary>
	/// 
	/// Authors:
	/// 	Fran Supek (fsupek at irb.hr)
	/// 	Rajko Horvat (rhorvat at irb.hr)
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

		//private double[,] aMatrix = null;
		private double[] aMatrix1 = null;

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public TermSimilarityMatrix(TermListVisualizer parent)
			: this(parent, null, null)
		{ }

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public TermSimilarityMatrix(TermListVisualizer parent, CancellationToken? token)
			: this(parent, token, null)
		{ }

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public TermSimilarityMatrix(TermListVisualizer parent, CancellationToken? token, ProgressEventHandler progress)
		{
			this.oParent = parent;
			ConstructMatrix(token, progress);
		}

		public TermListVisualizer Parent
		{
			get
			{
				return this.oParent;
			}
		}

		/*public double[,] Matrix
		{
			get
			{
				return this.aMatrix;
			}
		}*/

		public double GetValue(int row, int column)
		{
			if (row == column)
				return 1.0;

			int iRow = row;
			int iColumn = column - 1;

			if (iRow > iColumn)
			{
				iRow = column;
				iColumn = row - 1;
			}

			return this.aMatrix1[(iColumn * (iColumn + 1)) / 2 + iRow];
		}

		private void ConstructMatrix(CancellationToken? token, ProgressEventHandler progress)
		{
			SemanticSimilarityEnum similarityType = this.oParent.Parent.SemanticSimilarity;
			GeneOntology myGO = this.oParent.Parent.Ontology;
			SpeciesAnnotations oAnnotations = this.oParent.Parent.Annotations;
			GOTerm[] terms = this.oParent.Terms;
			int iTermCount = terms.Length;
			double dProgressStep = 100.0 / (double)iTermCount;
			double dOldProgress = 0.0;
			//this.aMatrix = new double[iTermCount, iTermCount];
			this.aMatrix1 = new double[((iTermCount - 1) * iTermCount) / 2];

			// reduces memory consumption by half
			for (int i = 1; i < iTermCount; i++)
			{
				GOTerm go1 = terms[i];

				for (int j = 0; j < i; j++)
				{
					GOTerm go2 = terms[j];
					this.aMatrix1[((i - 1) * i) / 2 + j] = CalculateSemanticSimilarity(similarityType, go1, go2, oAnnotations, myGO);
				}

				double dProgress = (double)i * dProgressStep;
				if ((dProgress - dOldProgress) >= 0.1 && progress != null)
				{
					dOldProgress = dProgress;
					progress(this, new ProgressEventArgs(dProgress));
				}
			}

			/*for (int i = 0; i < iTermCount; i++)
			{
				GOTerm go1 = terms[i];

				this.aMatrix[i, i] = 1.0;

				for (int j = i + 1; j < iTermCount; j++)
				{
					// do we need to gracefuly exit?
					if (token.HasValue && token.Value.IsCancellationRequested)
					{
						return;
					}

					GOTerm go2 = terms[j];
					double simil = CalculateSemanticSimilarity(similarityType, go1, go2, oAnnotations, myGO);

					this.aMatrix[i, j] = simil;
					this.aMatrix[j, i] = simil;
				}

				double dProgress = (double)(i + 1) * dProgressStep;
				if ((dProgress - dOldProgress) >= 0.1 && progress != null)
				{
					dOldProgress = dProgress;
					progress(this, new ProgressEventArgs(dProgress));
				}
			}*/

			// compare two matrices
			/*for (int i = 0; i < iTermCount; i++)
			{
				for (int j = 0; j < iTermCount; j++)
				{
					if (this.aMatrix[i, j] != this.GetValue(i, j))
					{
						Console.WriteLine("Matrices not equal");
					}
				}
			}*/
		}

		/// <summary>
		/// See http://www.biomedcentral.com/1471-2105/7/302 <br/>
		/// (Schlicker et al. BMC Bioinformatics 2006)
		/// </summary>
		/// <returns></returns>
		private double CalculateSemanticSimilarity(SemanticSimilarityEnum similarityType, GOTerm term1, GOTerm term2, SpeciesAnnotations annotations, GeneOntology myGo)
		{
			if (term1.ID == term2.ID)
				return 1.0;

			if (!myGo.Terms.ContainsKey(term1.ID) || !myGo.Terms.ContainsKey(term2.ID))
				return 0.0;

			double term1Freq = annotations.GetTermFrequency(term1.ID, myGo);
			double term2Freq = annotations.GetTermFrequency(term2.ID, myGo);

			// first find frequency of most informative parent
			// (also called MIA or Most Informative Common Ancestor)
			BHashSet<int> commonParents = term1.GetAllCommonParents(term2);

			double freqOfMostInfoParent = 1.0;
			foreach (int termID in commonParents)
			{
				freqOfMostInfoParent = Math.Min(freqOfMostInfoParent, annotations.GetTermFrequency(termID, myGo));
			}

			double curValue;

			switch (similarityType)
			{
				case SemanticSimilarityEnum.RESNIK: // normalized to [0,1]; we assume 4 is a 'very large' value
					curValue = Math.Min(-Math.Log10(freqOfMostInfoParent), 4.0) / 4.0;
					break;
				case SemanticSimilarityEnum.LIN:
					curValue = 2.0 * Math.Log10(freqOfMostInfoParent) /
						(Math.Log10(term1Freq) + Math.Log10(term2Freq));
					break;
				case SemanticSimilarityEnum.SIMREL:
					curValue = 2.0 * Math.Log10(freqOfMostInfoParent) /
						(Math.Log10(term1Freq) + Math.Log10(term2Freq));
					curValue *= (1.0 - freqOfMostInfoParent);
					break;
				case SemanticSimilarityEnum.JIANG:
					curValue = 1.0 / (-Math.Log10(term1Freq) - Math.Log10(term2Freq) +
						2 * Math.Log10(freqOfMostInfoParent) + 1.0);
					break;
				default:
					throw new ArgumentException("Not yet implemented.");
			}
			//if (curValue == -0.0)
			//	curValue = 0.0;

			return curValue;
		}

		/// <summary>
		/// Use TermSimilaryMatrix to calculate "uniqueness" of each GO term,
		/// i.e.its average semantic distance from all other GO terms in the matrix.
		/// 
		/// The "commonness" will be stored as a property in the provided
		/// GoTermProperties object (this will overwrite old values of "uniqueness", if present).
		/// </summary>
		public void CalculateUniqueness(CancellationToken? token)
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
					if (token.HasValue && token.Value.IsCancellationRequested)
					{
						return;
					}

					double dValue = this.GetValue(i, j);
					if (i != j && !double.IsNaN(dValue))
					{
						sum += dValue;
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
	}
}
