using System;
using IRB.Collections.Generic;
using IRB.Revigo.Databases;

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
	public class SemanticSimilarityScore
	{
		private static SemanticSimilarityScore[] aValues = new SemanticSimilarityScore[]
		{
			new SemanticSimilarityScore(SemanticSimilarityScoreEnum.SIMREL,
			// biolProcNullDist
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.05, 0.05, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.07, 0.07, 0.07, 0.07, 0.07, 0.08, 0.08, 0.08, 0.08, 0.08, 0.09, 0.09, 0.09, 0.09, 0.1, 0.1, 0.1, 0.11, 0.11, 0.12, 0.13, 0.14, 0.15, 0.16, 0.17, 0.18, 0.19, 0.2, 0.21, 0.22, 0.23, 0.24, 0.25, 0.26, 0.27, 0.28, 0.29, 0.29, 0.3, 0.32, 0.33, 0.34, 0.35, 0.37, 0.39, 0.41, 0.44, 0.47, 0.54, 1.0},
			// biolProcNullDistProkaryotes: 99th_percentile = 0.55; 95th_percentile = 0.35; 90th_percentile = 0.29; 80th percentile = 0.19
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.04, 0.07, 0.07, 0.07, 0.07, 0.07, 0.07, 0.07, 0.07, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.1, 0.1, 0.1, 0.1, 0.11, 0.11, 0.11, 0.11, 0.12, 0.12, 0.12, 0.12, 0.12, 0.13, 0.13, 0.14, 0.14, 0.15, 0.16, 0.16, 0.17, 0.18, 0.18, 0.19, 0.2, 0.22, 0.22, 0.23, 0.24, 0.25, 0.25, 0.26, 0.27, 0.29, 0.3, 0.31, 0.31, 0.33, 0.35, 0.38, 0.4, 0.45, 0.55, 1.0},
			// cellCompNullDist
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.04, 0.04, 0.04, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05, 0.06, 0.06, 0.06, 0.06, 0.06, 0.07, 0.07, 0.07, 0.08, 0.08, 0.08, 0.09, 0.09, 0.09, 0.1, 0.1, 0.11, 0.12, 0.13, 0.14, 0.14, 0.15, 0.16, 0.16, 0.16, 0.17, 0.17, 0.18, 0.18, 0.19, 0.19, 0.19, 0.19, 0.2, 0.2, 0.21, 0.21, 0.22, 0.22, 0.22, 0.23, 0.23, 0.23, 0.24, 0.24, 0.25, 0.25, 0.26, 0.26, 0.27, 0.27, 0.27, 0.28, 0.28, 0.28, 0.29, 0.29, 0.3, 0.3, 0.31, 0.31, 0.32, 0.33, 0.34, 0.34, 0.35, 0.36, 0.37, 0.38, 0.39, 0.4, 0.42, 0.44, 0.46, 0.49, 0.54, 0.61, 1.0},
			// cellCompNullDistProkaryotes: 99th percentile = 0.73; 95th_percentile = 0.50; 90th_percentile = 0.43; 80th percentile = 0.32
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.04, 0.04, 0.04, 0.05, 0.05, 0.05, 0.05, 0.05, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.07, 0.07, 0.07, 0.08, 0.08, 0.08, 0.08, 0.09, 0.09, 0.09, 0.09, 0.1, 0.1, 0.1, 0.1, 0.11, 0.11, 0.11, 0.12, 0.12, 0.13, 0.13, 0.14, 0.16, 0.17, 0.18, 0.19, 0.2, 0.21, 0.22, 0.23, 0.23, 0.23, 0.25, 0.26, 0.27, 0.28, 0.29, 0.29, 0.3, 0.3, 0.3, 0.31, 0.31, 0.32, 0.32, 0.33, 0.34, 0.35, 0.37, 0.38, 0.39, 0.4, 0.42, 0.43, 0.44, 0.46, 0.47, 0.49, 0.5, 0.52, 0.55, 0.6, 0.73, 1.0},
			// molFunNullDist
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.05, 0.05, 0.05, 0.05, 0.05, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.07, 0.07, 0.07, 0.07, 0.07, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.09, 0.09, 0.09, 0.09, 0.1, 0.1, 0.1, 0.1, 0.11, 0.12, 0.12, 0.12, 0.13, 0.14, 0.15, 0.16, 0.17, 0.18, 0.19, 0.2, 0.23, 0.26, 0.29, 0.35, 0.43, 1.0},
			// molFunNullDistProkaryotes: 99th percentile = 0.52; 95th_percentile = 0.30; 90th_percentile = 0.22; 80th percentile = 0.12
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.06, 0.06, 0.06, 0.06, 0.07, 0.07, 0.08, 0.08, 0.08, 0.08, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.09, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.11, 0.11, 0.11, 0.11, 0.11, 0.11, 0.11, 0.12, 0.12, 0.12, 0.12, 0.13, 0.14, 0.14, 0.16, 0.18, 0.2, 0.21, 0.22, 0.23, 0.24, 0.25, 0.27, 0.3, 0.36, 0.43, 0.52, 1.0},
			// mixedNamespaceNullDist
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.013, 0.013, 0.013, 0.017, 0.017, 0.017, 0.017, 0.017, 0.017, 0.017, 0.020, 0.020, 0.020, 0.020, 0.020, 0.023, 0.023, 0.023, 0.027, 0.027, 0.027, 0.030, 0.030, 0.030, 0.033, 0.033, 0.037, 0.040, 0.043, 0.047, 0.063, 0.067, 0.073, 0.073, 0.073, 0.077, 0.093, 0.097, 0.100, 0.103, 0.103, 0.107, 0.107, 0.113, 0.113, 0.117, 0.117, 0.120, 0.123, 0.123, 0.127, 0.130, 0.133, 0.137, 0.137, 0.143, 0.147, 0.153, 0.157, 0.163, 0.167, 0.170, 0.180, 0.183, 0.187, 0.193, 0.200, 0.207, 0.210, 0.217, 0.223, 0.233, 0.240, 0.247, 0.253, 0.260, 0.270, 0.283, 0.293, 0.303, 0.313, 0.330, 0.353, 0.377, 0.407, 0.453, 0.527, 1.000},
			// mixedNamespaceNullDistProkaryotes: 99th_percentile = 0.600; 95th_percentile = 0.373; 90th percentile = 0.310; 80th percentile = 0.210
			new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.013, 0.013, 0.013, 0.017, 0.017, 0.017, 0.017, 0.017, 0.020, 0.020, 0.020, 0.020, 0.033, 0.043, 0.043, 0.043, 0.047, 0.047, 0.047, 0.050, 0.050, 0.053, 0.053, 0.077, 0.077, 0.077, 0.077, 0.087, 0.087, 0.090, 0.090, 0.093, 0.093, 0.097, 0.100, 0.100, 0.103, 0.107, 0.110, 0.117, 0.120, 0.127, 0.130, 0.137, 0.140, 0.147, 0.150, 0.150, 0.150, 0.157, 0.163, 0.167, 0.173, 0.180, 0.183, 0.190, 0.190, 0.193, 0.200, 0.200, 0.210, 0.213, 0.223, 0.227, 0.237, 0.250, 0.257, 0.267, 0.280, 0.297, 0.310, 0.320, 0.333, 0.340, 0.357, 0.373, 0.400, 0.437, 0.493, 0.600, 1.000}
			),
			new SemanticSimilarityScore(SemanticSimilarityScoreEnum.LIN, null, null, null, null, null, null, null, null),
			new SemanticSimilarityScore(SemanticSimilarityScoreEnum.RESNIK, null, null, null, null, null, null, null, null),
			new SemanticSimilarityScore(SemanticSimilarityScoreEnum.JIANG, null, null, null, null, null, null, null, null)
		};

		public static SemanticSimilarityScore[] Values
		{
			get
			{
				return aValues;
			}
		}

		public static SemanticSimilarityScore GetSemanticSimilarityScore(SemanticSimilarityScoreEnum similarity)
		{
			for (int i = 0; i < aValues.Length; i++)
			{
				if (aValues[i].EnumValue == similarity)
				{
					return aValues[i];
				}
			}
			return null;
		}

		// Index of the column in the results of the FunSimMat XML-RPC server.
		private SemanticSimilarityScoreEnum columnIndex;

		// Null distribution for the Biological Process GO Namespace.
		private double[] biolProcNullDist;
		// Null distribution for the Biological Process GO Namespace (prokaryotic subset only).
		private double[] biolProcNullDistProkaryotes;
		// Null distribution for the Cellular Component GO Namespace.
		private double[] cellCompNullDist;
		// Null distribution for the Cellular Component GO Namespace (prokaryotic subset only).
		private double[] cellCompNullDistProkaryotes;
		// Null distribution for the Molecular Function GO Namespace.
		private double[] molFunNullDist;
		// Null distribution for the Molecular Function GO Namespace (prokaryotic subset only).
		private double[] molFunNullDistProkaryotes;
		// Null distribution for mixed GO Namespaces.
		private double[] mixedNamespaceNullDist;
		// Null distribution for mixed GO Namespaces (prokaryotic subset only).
		private double[] mixedNamespaceNullDistProkaryotes;

		// Construtor. Fills all fields.
		private SemanticSimilarityScore(
			SemanticSimilarityScoreEnum columnIndexPar,
			double[] biolProcNullDist,
			double[] biolProcNullDistProkaryotes,
			double[] cellCompNullDist,
			double[] cellCompNullDistProkaryotes,
			double[] molFunNullDist,
			double[] molFunNullDistProkaryotes,
			double[] mixedNamespaceNullDist,
			double[] mixedNamespaceNullDistProkaryotes)
		{
			this.columnIndex = columnIndexPar;
			this.biolProcNullDist = biolProcNullDist;
			this.biolProcNullDistProkaryotes = biolProcNullDistProkaryotes;
			this.cellCompNullDist = cellCompNullDist;
			this.cellCompNullDistProkaryotes = cellCompNullDistProkaryotes;
			this.molFunNullDist = molFunNullDist;
			this.molFunNullDistProkaryotes = molFunNullDistProkaryotes;
			this.mixedNamespaceNullDist = mixedNamespaceNullDist;
			this.mixedNamespaceNullDistProkaryotes = mixedNamespaceNullDistProkaryotes;
		}

		public SemanticSimilarityScoreEnum EnumValue
		{
			get
			{
				return this.columnIndex;
			}
		}

		// Index of the column in the results of the FunSimMat XML-RPC server.
		public int getColumnIndex
		{
			get
			{
				return (int)columnIndex;
			}
		}

		/// <summary>
		/// See http://www.biomedcentral.com/1471-2105/7/302 <br/>
		/// (Schlicker et al. BMC Bioinformatics 2006)
		/// </summary>
		/// <param name="term1"></param>
		/// <param name="term2"></param>
		/// <param name="annotations"></param>
		/// <param name="myGo"></param>
		/// <returns></returns>
		public double calculateDistance(GOTerm term1, GOTerm term2, SpeciesAnnotations annotations, GeneOntology myGo)
		{
			if (term1.ID == term2.ID)
				return 1.0;

			if (!myGo.ContainsKey(term1.ID) || !myGo.ContainsKey(term2.ID))
				return 0.0;

			double term1Freq = annotations.GetTermFrequency(term1.ID, myGo);
			double term2Freq = annotations.GetTermFrequency(term2.ID, myGo);

			// first find frequency of most informative parent
			// (also called MIA or Most Informative Ancestor)
			BHashSet<int> commonParents = term1.GetAllCommonParents(term2);
			if (term1.Equals(term2))
				commonParents.Add(term1.ID);
			double freqOfMostInfoParent = 1.0;
			foreach (int termID in commonParents)
			{
				freqOfMostInfoParent = Math.Min(freqOfMostInfoParent, annotations.GetTermFrequency(termID, myGo));
			}

			double curValue;

			switch (this.columnIndex)
			{
				case SemanticSimilarityScoreEnum.RESNIK: // normalized to [0,1]; we assume it 4 is a 'very large' value
					curValue = Math.Min(-Math.Log10(freqOfMostInfoParent), 4.0) / 4.0;
					break;
				case SemanticSimilarityScoreEnum.LIN:
					curValue = 2.0 * Math.Log10(freqOfMostInfoParent) /
						(Math.Log10(term1Freq) + Math.Log10(term2Freq));
					break;
				case SemanticSimilarityScoreEnum.SIMREL:
					curValue = 2.0 * Math.Log10(freqOfMostInfoParent) /
						(Math.Log10(term1Freq) + Math.Log10(term2Freq));
					curValue *= (1.0 - freqOfMostInfoParent);
					break;
				case SemanticSimilarityScoreEnum.JIANG:
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

		public override string ToString()
		{
			switch (this.columnIndex)
			{
				case SemanticSimilarityScoreEnum.RESNIK: // normalized to [0,1]
					return "Resnik (normalized)";
				case SemanticSimilarityScoreEnum.LIN:
					return "Lin";
				case SemanticSimilarityScoreEnum.SIMREL:
					return "SimRel (default)";
				case SemanticSimilarityScoreEnum.JIANG:
					return "Jiang and Conrath";
				default:
					throw new ArgumentException("Not yet implemented.");
			}
		}
	}
}
