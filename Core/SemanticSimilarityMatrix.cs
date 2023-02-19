using System;
using IRB.Collections.Generic;
using IRB.Revigo.Core.Databases;
using IRB.Revigo.Core.Worker;

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
	public class SemanticSimilarityMatrix
	{
		// we will preserve a copy of the terms
		RevigoTermCollection aTerms;
		// we also want to cache GOTermID and Index pairs
		BDictionary<int, int> aGOTermIDIndexes = new BDictionary<int, int>();

		//private double[,] aMatrix = null;
		private double[] aMatrix;

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public SemanticSimilarityMatrix(SemanticSimilarityTypeEnum similarityType, GeneOntology ontology, SpeciesAnnotations annotations, 
			RevigoTermCollection terms)
			: this(similarityType, ontology, annotations, terms, null, null)
		{ }

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public SemanticSimilarityMatrix(SemanticSimilarityTypeEnum similarityType, GeneOntology ontology, SpeciesAnnotations annotations, 
			RevigoTermCollection terms, CancellationTokenSource? token)
			: this(similarityType, ontology, annotations, terms, token, null)
		{ }

		/// <summary>
		/// Constructs a distance matrix of all the GO terms present in termList.
		/// </summary>
		/// <param name="parent"></param>
		public SemanticSimilarityMatrix(SemanticSimilarityTypeEnum similarityType, GeneOntology ontology, SpeciesAnnotations annotations, 
			RevigoTermCollection terms,	CancellationTokenSource? token, ProgressEventHandler? progress)
		{
			this.aTerms = new RevigoTermCollection(terms);
			for (int i = 0; i < this.aTerms.Count; i++)
			{
				this.aGOTermIDIndexes.Add(this.aTerms[i].GOTerm.ID, i);
			}
			this.aMatrix = ConstructMatrix(similarityType, ontology, annotations, token, progress);
		}

		/*public double[,] Matrix
		{
			get
			{
				return this.aMatrix;
			}
		}*/

		public RevigoTermCollection Terms
		{
			get { return this.aTerms; }
		}

		public double GetSimilarityByGOTermID(int term1ID, int term2ID)
		{
			if (!this.aGOTermIDIndexes.ContainsKey(term1ID) || !this.aGOTermIDIndexes.ContainsKey(term2ID))
				return 0.0;

			if (term1ID == term2ID)
				return 1.0;

			int row = this.aGOTermIDIndexes.GetValueByKey(term1ID);
			int column = this.aGOTermIDIndexes.GetValueByKey(term2ID);

			int iRow = row;
			int iColumn = column - 1;

			if (iRow > iColumn)
			{
				iRow = column;
				iColumn = row - 1;
			}

			return this.aMatrix[(iColumn * (iColumn + 1)) / 2 + iRow];
		}

		public double GetSimilarity(int row, int column)
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

			return this.aMatrix[(iColumn * (iColumn + 1)) / 2 + iRow];
		}

		private double[] ConstructMatrix(SemanticSimilarityTypeEnum similarityType, GeneOntology ontology, SpeciesAnnotations annotations,
			CancellationTokenSource? token, ProgressEventHandler? progressEvent)
		{
			int iTermCount = this.aTerms.Count;
			double dProgressStep = 100.0 / (double)iTermCount;
			double dOldProgress = 0.0;
			//this.aMatrix = new double[iTermCount, iTermCount];
			double[] aMatrix = new double[((iTermCount - 1) * iTermCount) / 2];

			// reduces memory consumption by half
			for (int i = 1; i < iTermCount; i++)
			{
				RevigoTerm term1 = this.aTerms[i];

				for (int j = 0; j < i; j++)
				{
					RevigoTerm term2 = this.aTerms[j];
					aMatrix[((i - 1) * i) / 2 + j] = CalculateSemanticSimilarity(similarityType, term1.GOTerm, term2.GOTerm, annotations, ontology);
				}

				if (token != null && token.IsCancellationRequested)
					break;

				double dProgress = (double)i * dProgressStep;
				if ((dProgress - dOldProgress) >= 0.1 && progressEvent != null)
				{
					dOldProgress = dProgress;
					progressEvent(this, new ProgressEventArgs(dProgress));
				}
			}

			/*for (int i = 0; i < iTermCount; i++)
			{
				GeneOntologyTerm go1 = terms[i];

				this.aMatrix[i, i] = 1.0;

				for (int j = i + 1; j < iTermCount; j++)
				{
					// do we need to gracefuly exit?
					if (token.HasValue && token.Value.IsCancellationRequested)
					{
						return;
					}

					GeneOntologyTerm go2 = terms[j];
					double simil = CalculateSemanticSimilarity(similarityType, go1, go2, oAnnotations, oOntology);

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

			return aMatrix;
		}

		/// <summary>
		/// See http://www.biomedcentral.com/1471-2105/7/302 <br/>
		/// (Schlicker et al. BMC Bioinformatics 2006)
		/// </summary>
		/// <returns></returns>
		private double CalculateSemanticSimilarity(SemanticSimilarityTypeEnum similarityType, GeneOntologyTerm term1, GeneOntologyTerm term2, 
			SpeciesAnnotations annotations, GeneOntology ontology)
		{
			if (term1.ID == term2.ID)
				return 1.0;

			if (!ontology.Terms.ContainsKey(term1.ID) || !ontology.Terms.ContainsKey(term2.ID))
				return 0.0;

			double term1Freq = annotations.GetTermFrequency(term1.ID, ontology);
			double term2Freq = annotations.GetTermFrequency(term2.ID, ontology);

			// first find frequency of most informative parent
			// (also called MIA or Most Informative Common Ancestor)
			BHashSet<int> commonParents = term1.GetAllCommonParents(term2);

			double freqOfMostInfoParent = 1.0;
			foreach (int termID in commonParents)
			{
				freqOfMostInfoParent = Math.Min(freqOfMostInfoParent, annotations.GetTermFrequency(termID, ontology));
			}

			double curValue;

			switch (similarityType)
			{
				case SemanticSimilarityTypeEnum.RESNIK: // normalized to [0,1]; we assume 4 is a 'very large' value
					curValue = Math.Min(-Math.Log10(freqOfMostInfoParent), 4.0) / 4.0;
					break;
				case SemanticSimilarityTypeEnum.LIN:
					curValue = 2.0 * Math.Log10(freqOfMostInfoParent) /
						(Math.Log10(term1Freq) + Math.Log10(term2Freq));
					break;
				case SemanticSimilarityTypeEnum.SIMREL:
					curValue = 2.0 * Math.Log10(freqOfMostInfoParent) /
						(Math.Log10(term1Freq) + Math.Log10(term2Freq));
					curValue *= (1.0 - freqOfMostInfoParent);
					break;
				case SemanticSimilarityTypeEnum.JIANG:
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
		public void CalculateUniqueness(CancellationTokenSource token)
		{
			int iTermCount = this.aTerms.Count;

			for (int i = 0; i < iTermCount; i++)
			{
				double sum = 0.0;
				int count = 0;

				for (int j = 0; j < iTermCount; j++)
				{
					if (token.IsCancellationRequested)
					{
						return;
					}

					double dValue = this.GetSimilarity(i, j);
					if (i != j && !double.IsNaN(dValue))
					{
						sum += dValue;
						count++;
					}
				}

				if (count <= 1)
				{
					this.aTerms[i].Uniqueness = 1.0;
				}
				else
				{
					// this assumes the semantic similarity measure in the matrix ranges
					// from 0 to 1, so that 1-x can be used to convert 'similarity' to
					// 'distance'
					this.aTerms[i].Uniqueness = Math.Pow(1.0 - (sum / (double)count), 2.0);
				}
			}
		}
	}
}
