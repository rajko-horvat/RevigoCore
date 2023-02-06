using System;
using System.Collections.Generic;
using IRB.Collections.Generic;
using IRB.Revigo.Worker;
using IRB.Revigo.Databases;

namespace IRB.Revigo.Core
{
	/// <summary>
	/// Takes a list of GO terms and prepares them for visualization in 1-, 2- or 3-
	/// dimensional space, where coordinates are derived from term semantic
	/// distances summarized by principal component analysis (PCA).
	/// 
	/// First, the pairwise distances of all listed GO terms are determined -
	/// currently, the XML-RPC interface of FunSimMat service is used 
	/// ( http://funsimmat.bioinf.mpi-inf.mpg.de/ ). In the future, the distances
	/// might also be read from a local copy of the FSST database, or computed 
	/// using GO4J.
	/// 
	/// Then the myInst is stored into an Instances object (from Weka) and principal
	/// component analysis (PCA) is performed, retaining the first three PCs.
	/// Alternatively, other dimensionality reduction methods (e.g. SOM, MDS)
	/// Whatever myInst is stored as "properties" in each GO term is added to the
	/// Instances afterwards (can be used in visualization - disc size, color etc.)
	/// 
	/// Finally, some GO terms are indicated as 'disposable'. This is determined by 
	/// recursively eliminating one term from a pair of currently closest GO terms 
	/// in the set, until the distance of the closest pair drops below a given 
	/// threshold. Which GO term is eliminated from a pair is determined by checking
	/// the value of a term's property, if exists (if not, one is removed at random).
	/// (Future plan: if no property is supplied, the term with the smaller minimum
	/// distance to all other terms in list is kept.)
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
	public class TermListVisualizer
	{
		private RevigoWorker oParent = null;
		private GONamespaceEnum eNamespace;

		private GOTerm[] aTerms;
		private GOTermProperties[] aProperties;

		// Two values (x and y) of the supplied property are considered equal
		// if abs(x-y)/((x+y)/2) is less than the confidenceInterval.
		// It's recommended to use "absLogProp" on p-values if you want this to work
		// correctly.
		private static double ConfidenceInterval = 0.10;  // 10 % of average value

		// A genegroup is considered too general if it contains more than this
		// proportion of total genes. Such genegroups are considered to be uninteresting.
		private static double GeneGroupGeneralityThreshold = 0.05;  // suggestions: 1.5 % or 5 %

		// Maximum allowed List size
		public static int MaxAllowedGOListSize = 2000;

		// similarity matrix
		private TermSimilarityMatrix oMatrix = null;

		// results
		private bool bMDSError = false;
		private OntoloGraph oOntoloGraph = null;

		// progress
		private double dProgressPos = 0.0;
		private double dProgressSlice = 0.0;

		// events
		private ProgressEventHandler OnProgress = null;

		/// <summary>
		/// Requires that a List of GOTerms is provided, as
		/// well as the GeneOntology instance they were taken from. <p>
		///
		/// The TermListVisualizer will use the provided GoTermSizesObject to
		/// calculate semantic distances. <p>
		/// </summary>
		/// <param name="termList">A list of selected GO terms.</param>
		/// <param name="go">A GeneOntology object with all GO terms.</param>
		/// <param name="termSizes">A GoTermSizes object necessary to compute semantic distances.</param>
		/// <param name="desc">A free-text description of the visualization. Used for 
		/// header in output arff files, and in reporting of errors. Optional.</param>
		/// <param name="orgsInTotal">I don't remember what this was for; it isn't used for
		/// any calculations, it is just output as-is to Weka Instances.</param>
		public TermListVisualizer(RevigoWorker parent, GONamespaceEnum goNamespace,
			GOTerm[] terms, BDictionary<int, GOTermProperties> properties, CancellationToken token, ProgressEventHandler progressHandler)
		{
			this.oParent = parent;
			this.eNamespace = goNamespace;
			this.OnProgress = progressHandler;
			this.aTerms = terms;
			Array.Sort(this.aTerms);

			// properties array coresponds to term array
			this.aProperties = new GOTermProperties[this.aTerms.Length];

			for (int i = 0; i < this.aTerms.Length; i++)
			{
				this.aProperties[i] = properties.GetValueByKey(this.aTerms[i].ID);
			}

			if (this.aTerms.Length > 0)
			{
				ConstructMatrix(token);
				MakeOntologram(token);
				MakeSimpleThresholdOntolograph(0.97, token);
			}
		}

		public RevigoWorker Parent
		{
			get
			{
				return this.oParent;
			}
		}

		public GONamespaceEnum Namespace
		{
			get
			{
				return this.eNamespace;
			}
		}

		public GOTerm[] Terms
		{
			get
			{
				return this.aTerms;
			}
		}

		public GOTermProperties[] Properties
		{
			get
			{
				return this.aProperties;
			}
		}

		// results
		public TermSimilarityMatrix Matrix
		{
			get
			{
				return this.oMatrix;
			}
		}

		public bool MDSError
		{
			get
			{
				return this.bMDSError;
			}
		}

		public OntoloGraph SimpleOntologram
		{
			get
			{
				return this.oOntoloGraph;
			}
		}

		private void ConstructMatrix(CancellationToken token)
		{
			this.dProgressPos = 0.0;
			this.dProgressSlice = 20.0;

			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(0.0, "Constructing similarity matrix"));
			this.oMatrix = new TermSimilarityMatrix(this, token, myMatrix_OnProgress);
		}

		void myMatrix_OnProgress(object sender, ProgressEventArgs e)
		{
			double dProgress = this.dProgressPos + ((e.Progress * this.dProgressSlice) / 100.0);
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(dProgress));
		}

		/// <summary>
		/// Makes a ontologram by performing these steps:
		/// <ul>
		///  <li> computes 'uniqueness' of each GO term (stored into GOTerm properties)</li>
		///  <li> computes 'dispensability' of each GO term (stored into GOTerm properties)</li>
		///  <li> removes all dispensable GO Terms from similarity matrix (you can skip this step
		///       by passing 1.0 as dispensabilityCutoff)</li>
		///  <li> performs MDS on the matrix</li>
		/// </ul>
		/// </summary>
		/// <returns>True if MDS error was encountered</returns>
		private void MakeOntologram(CancellationToken token)
		{
			RandomMT19937 oRnd = new RandomMT19937(18012021);
			GeneOntology oOntology = this.oParent.Ontology;
			SpeciesAnnotations oAnnotations = this.oParent.Annotations;

			if (this.oMatrix == null)
				throw new Exception("The matrix for the TermListVisualizer is not contructed");

			// Trim huge term lists
			/*this.dProgressPos = 0.0;
			this.dProgressSlice = 10.0;
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(this.dProgressPos, "Trimming huge term lists"));
			this.TrimHugeTermLists();*/

			// Calculate term uniqueness
			this.dProgressPos = 20.0;
			this.dProgressSlice = 10.0;
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(this.dProgressPos, "Calculating term uniqueness"));
			this.oMatrix.CalculateUniqueness(token);

			// Mark dispensable terms
			// This step adds/alters properties of GO Terms in the TermSimilarityMatrix
			// Properties changed: "dispensability", "dispensedBy"
			this.dProgressPos = 30.0;
			this.dProgressSlice = 50.0;
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(this.dProgressPos, "Building dispensable term list"));
			this.BuildDispensableTermList(oRnd, token);

			// these Instances have no class attribute and generally no other attributes
			// other than the pairwise distances themselves
			this.dProgressPos = 80.0;
			this.dProgressSlice = 10.0;
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(this.dProgressPos, "Performing Multi Dimensional Scaling"));

			this.bMDSError = this.DoMDS(token);
		}

		/**
		 * !!! This function is not supported anymore !!!
		 * 
		 * Takes a list of GO terms from the current TermListVisualizer object,
		 * checks if the list is 'huge' (i.e. above a certain threshold) and if it 
		 * is, makes sure that it fits within that size by throwing out GO terms that:
		 * 
		 * (a) have 'bad' p-values/enrichments/whatever - note: the GoTermProperties
		 *     must have been transformed prior to invoking TermListVisualizer so that
		 *     larger is always better
		 * 
		 * OR, if the values above were not supplied by the user
		 * 
		 * (b) are very general.
		 */
		private void TrimHugeTermLists()
		{
			if (this.aTerms.Length <= MaxAllowedGOListSize)
				return;

			GeneOntology oOntology = this.oParent.Ontology;
			SpeciesAnnotations oAnnotations = this.oParent.Annotations;

			bool allTermsHaveProperty = true;
			List<double> propValues = new List<double>();
			for (int i = 0; i < this.aTerms.Length; i++)
			{
				double dTransformedValue = this.aProperties[i].TransformedValue;
				if (!double.IsNaN(dTransformedValue))
				{
					propValues.Add(dTransformedValue);
				}
				else
				{
					allTermsHaveProperty = false; //uh-oh, this won't work; use generality instead
					break;
				}
			}

			if (!allTermsHaveProperty)
			{
				// resort to using Go Term generality instead
				propValues.Clear();
				foreach (GOTerm curTerm in this.aTerms)
				{
					propValues.Add(-oAnnotations.GetTermFrequency(curTerm.ID, oOntology));
				}
			}

			propValues.Sort();

			// keep maxAllowedGoListSize biggest values
			double threshold = propValues[this.aTerms.Length - MaxAllowedGOListSize];

			for (int i = 0; i < this.aTerms.Length; i++)
			{
				GOTerm curTerm = this.aTerms[i];
				double valToTest;

				if (allTermsHaveProperty)
				{
					// remove 'undesirable' GO categories, e.g. with large p-values
					valToTest = this.aProperties[i].TransformedValue;
				}
				else
				{
					// remove general (i.e. not highly informative) GO categories
					valToTest = -oAnnotations.GetTermFrequency(curTerm.ID, oOntology);
				}
				if (valToTest < threshold)
				{
					//this.aTerms.RemoveAt(i);
					i--;
				}
			}
		}

		/// <summary>
		/// Determines 'dispensability' of each GO term in the list. This is done by 
		/// recursively eliminating one term from a pair of currently most similar GO
		/// terms in the set, and remembering the similarity of the pair as the
		/// 'dispensability' of the eliminated term.
		/// 
		/// Which GO term is eliminated from a pair is determined by
		/// checking the value of a term's property, if exists (if not, one is removed
		/// at random).
		/// </summary>
		/// <param name="rnd"></param>
		private void BuildDispensableTermList(RandomMT19937 rnd, CancellationToken token)
		{
			GeneOntology oOntology = this.oParent.Ontology;
			SpeciesAnnotations oAnnotations = this.oParent.Annotations;

			bool keepGreater = true;
			bool absLogProp = false;

			// this is to keep the original matrix from getting destroyed
			//TermSimilarityMatrix oSimilarityMatrix = this.oMatrix.Clone();

			// set "dispensability" of all terms to 0, because otherwise one term
			// could be left without a dispensability value
			for (int i = 0; i < this.aTerms.Length; i++)
			{
				GOTermProperties property = this.aProperties[i];
				property.Dispensability = 0.0;
				property.DispensedBy = -1;

				if (token.IsCancellationRequested)
					return;
			}

			int iTermCount = this.aTerms.Length;
			double dSlice = this.dProgressSlice / 2.0;
			double dProgressStep = dSlice / (double)iTermCount;
			int iPairCount = 0;

			BDictionary<double, List<int[]>> oTermPairsGroupedBySimilarity = new BDictionary<double, List<int[]>>();
			for (int i = 0; i < iTermCount; i++)
			{
				double propX = this.aProperties[i].TransformedValue;

				if (token.IsCancellationRequested)
					return;

				// we are symmetrical
				for (int j = i + 1; j < iTermCount; j++)
				{
					double propY = this.aProperties[j].TransformedValue;
					double dSimilarity = Math.Round(oMatrix.Matrix[i, j], 8);

					if ((double.IsNaN(propX) || double.IsNaN(propY) || Math.Sign(propX) == Math.Sign(propY)) && dSimilarity > -1.0)
					{
						if (oTermPairsGroupedBySimilarity.ContainsKey(dSimilarity))
						{
							oTermPairsGroupedBySimilarity.GetValueByKey(dSimilarity).Add(new int[] { i, j });
						}
						else
						{
							List<int[]> aPairs = new List<int[]>();
							aPairs.Add(new int[] { i, j });
							oTermPairsGroupedBySimilarity.Add(dSimilarity, aPairs);
						}
						iPairCount++;
					}
				}
				double dProgress = this.dProgressPos + dProgressStep * (double)i;
				if (this.OnProgress != null)
					this.OnProgress(this, new ProgressEventArgs(dProgress));
			}

			double[] aSimilarities = oTermPairsGroupedBySimilarity.Keys.ToArray();
			Array.Sort(aSimilarities);

			int iRemovedPairCount = 0;
			BHashSet<int> oRemovedTerms = new BHashSet<int>();
			dProgressStep = dSlice / (double)iPairCount;

			for (int i = aSimilarities.Length - 1; i >= 0; i--)
			{
				double dSimilarity = aSimilarities[i];
				List<int[]> aPairs = oTermPairsGroupedBySimilarity.GetValueByKey(aSimilarities[i]);

				while (aPairs.Count > 0)
				{
					if (token.IsCancellationRequested)
						return;

					int iSelected = (aPairs.Count > 1) ? rnd.Next(aPairs.Count) : 0;
					int iXIndex = aPairs[iSelected][0];
					int iYIndex = aPairs[iSelected][1];
					int iXID = this.aTerms[iXIndex].ID;
					int iYID = this.aTerms[iYIndex].ID;

					if (oRemovedTerms.Contains(iXID) || oRemovedTerms.Contains(iYID))
					{
						aPairs.RemoveAt(iSelected);
					}
					else
					{
						// now, either XID or YID have to be "removed" (i.e. marked as having high dispensability)
						int iRemoveID;
						GOTermProperties oPropertiesX = this.aProperties[iXIndex];
						GOTermProperties oPropertiesY = this.aProperties[iYIndex];

						// decideWhichToRemove block

						// first condition - if one of the GO categories is "pinned" by the
						// user, it automatically wins the contest
						bool bXPinned = oPropertiesX.Pinned;
						bool bYPinned = oPropertiesY.Pinned;
						if (bXPinned && !bYPinned)
						{
							iRemoveID = iYID;
							goto decideWhichToRemove;
						}
						if (bYPinned && !bXPinned)
						{
							iRemoveID = iXID;
							goto decideWhichToRemove;
						}

						// second condition - if one GO category is 'very general' and the other
						// GO category is not, the 'very general' category automatically loses
						double dFreqX = oPropertiesX.AnnotationFrequency;
						double dFreqY = oPropertiesY.AnnotationFrequency;
						if (!double.IsNaN(dFreqX) && !double.IsNaN(dFreqY))
						{
							if (dFreqX > GeneGroupGeneralityThreshold &&
									dFreqY < GeneGroupGeneralityThreshold)
							{
								iRemoveID = iXID;
								goto decideWhichToRemove;
							}
							else if (dFreqY > GeneGroupGeneralityThreshold &&
									dFreqX < GeneGroupGeneralityThreshold)
							{
								iRemoveID = iYID;
								goto decideWhichToRemove;
							}
						}

						// now, check the properties to see which term we like best
						double dTValueX = oPropertiesX.TransformedValue;
						double dTValueY = oPropertiesY.TransformedValue;

						if (double.IsNaN(dTValueX) || double.IsNaN(dTValueY))
						{
							dTValueX = oPropertiesX.Uniqueness;
							dTValueY = oPropertiesY.Uniqueness;
						}

						if (double.IsNaN(dTValueX) || double.IsNaN(dTValueY))
						{
							dTValueX = 0.0; dTValueY = 0.0;
						}

						/* AbsLogProp transformation is necessary for eliminating by enrichment;
						 * this makes 0.001 more desirable than 0.01, while 100 is more desirable
						 * by 10 (i.e. keeps the number further away from 1).
						 *
						 * AbsLogProp also doesn't hurt when eliminating by p-value, which is by
						 * definition between 0 and 1, and the greater absolute logarithm is
						 * always desirable: e.g. 1e-20 is better than 1e-10.
						 */
						if (absLogProp)
						{
							dTValueX = Math.Abs(Math.Log10(Math.Max(dTValueX, 1e-300)));
							dTValueY = Math.Abs(Math.Log10(Math.Max(dTValueY, 1e-300)));
						}

						// if propX and propY are within the confidence interval, force equality
						bool xWasGreaterBeforeForcedEquality = false;
						if (Math.Abs(dTValueX - dTValueY) / ((dTValueX + dTValueY) / 2) < ConfidenceInterval)
						{
							xWasGreaterBeforeForcedEquality = (dTValueX > dTValueY);
							dTValueY = dTValueX;
						}

						if (dTValueX > dTValueY)
						{
							iRemoveID = keepGreater ? iYID : iXID;
						}
						else if (dTValueX < dTValueY)
						{
							iRemoveID = keepGreater ? iXID : iYID;
						}
						else
						{
							// they are equal.. here the fun begins :)
							// check if the two are linked by a parent-child relationship
							//BHashSet<GOTerm> parentsOfX = oOntology[iXSelectedID].getAllParents();
							//BHashSet<GOTerm> parentsOfY = oOntology[iYSelectedID].getAllParents();

							// also get sizes of X and Y, we'll need this to decide which to keep
							double sizeOfX = oPropertiesX.AnnotationSize;
							double sizeOfY = oPropertiesY.AnnotationSize;

							// parentsOfX.Contains(oOntology[iYSelectedID])
							if (oOntology.Terms.GetValueByKey(iXID).IsChildOf(iYID))
							{
								/* Y is a parent of X. Now check if Y is constituted mostly of X
								 * by comparing their log-sizes - if yes, keep X (the more
								 * specific term). Otherwise, keep Y, the parent term.
								 *
								 * TODO: This should be reworked into something like "if X has most of
								 * the flagged/non-flagged genes of Y, keep X". Flagged genes
								 * would be more interesting in enriched categories, and non-flagged
								 * in depleted ones. See example:
								 *
								 * GO:0015986	1674 flagged, 1865 not; p=-300; enr=3.795; ATP_synthesis_coupled_proton_transport
								 * GO:0006818	1804 flagged, 3170 not; p=-300; enr=2.909; hydrogen_transport (parent)
								 *
								 * Currently, we keep the parent here, and we obviously shoud not.
								 */
								if ((sizeOfY - sizeOfX) / sizeOfY < 0.25)
									iRemoveID = iYID; // Y constituted mostly of X
								else
									iRemoveID = iXID; // Y constituted only partly of X
							}
							else if (oOntology.Terms.GetValueByKey(iYID).IsChildOf(iXID)) // parentsOfY.Contains(oOntology[iXSelectedID])
							{
								// X is a parent of Y. Rule as above, only reversed.
								if ((sizeOfX - sizeOfY) / sizeOfX < 0.25)
									iRemoveID = iXID; // X constituted mostly of Y
								else
									iRemoveID = iYID; // X constituted only partly of Y
							}
							else
							{
								// X and Y are not parent-child related - keep the more
								// desirable value regardless of the confidence interval

								if (xWasGreaterBeforeForcedEquality)
									iRemoveID = keepGreater ? iYID : iXID;
								else
									iRemoveID = keepGreater ? iXID : iYID;
							}
						}
					// end block "decideWhichToRemove:"
					decideWhichToRemove:
						int iRemoveIndex = (iRemoveID == iXID) ? iXIndex : iYIndex;
						GOTermProperties oRemoveProperties = (iRemoveID == iXID) ? oPropertiesX : oPropertiesY;
						oRemoveProperties.Dispensability = dSimilarity;
						oRemoveProperties.DispensedBy = (iRemoveID == iXID) ? iYID : iXID;

						oRemovedTerms.Add(iRemoveID);
						aPairs.RemoveAt(iSelected);
					}

					iRemovedPairCount++;
					double dProgress = this.dProgressPos + dSlice + dProgressStep * (double)iRemovedPairCount;
					if (this.OnProgress != null)
						this.OnProgress(this, new ProgressEventArgs(dProgress));
				}
			}
		}

		private bool DoMDS(CancellationToken token)
		{
			double dProgress;

			double dCutoff = this.oParent.CutOff;
			List<int> aMDSTerms = new List<int>();
			int iTermsCount = this.aTerms.Length;

			for (int i = 0; i < iTermsCount; i++)
			{
				if (token.IsCancellationRequested)
					return false;

				GOTerm term = this.aTerms[i];
				double dPropertyValue = this.aProperties[i].Dispensability;

				if (double.IsNaN(dPropertyValue) || dPropertyValue <= dCutoff)
				{
					aMDSTerms.Add(i);
				}
			}

			iTermsCount = aMDSTerms.Count;
			double[,] oMatrixArray = new double[iTermsCount, iTermsCount];

			for (int i = 0; i < iTermsCount; i++)
			{
				oMatrixArray[i, i] = 0.0;

				for (int j = i + 1; j < iTermsCount; j++)
				{
					double value = this.oMatrix.Matrix[aMDSTerms[i], aMDSTerms[j]];

					if (token.IsCancellationRequested)
						return false;

					// no NaN in matrix for MDS otherwise it will break! We can safely assume no similarity between these terms
					// NaNs can happen if the same or different base terms are submitted with annotation of 1
					if (double.IsNaN(value))
					{
						oMatrixArray[i, j] = 0.0;
						// matrix is symmetrical
						oMatrixArray[j, i] = 0.0;
					}
					else
					{
						oMatrixArray[i, j] = 1.0 / Math.Max(0.1, value);
						// matrix is symmetrical
						oMatrixArray[j, i] = oMatrixArray[i, j];
					}
				}
			}

			dProgress = this.dProgressPos + 2.0;
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(dProgress));

			// 2D MDS

			int iMDSAxes = 2;
			bool bError = true;

			// skip MDS if we have insufficient data
			if (iTermsCount > iMDSAxes)
			{
				double[,] mdsResult = MDS.MDS.StressMinimization(oMatrixArray, iMDSAxes, 0, 5, 10000); // really try to converge, iterate for 10 seconds

				if (token.IsCancellationRequested)
					return false;

				dProgress = this.dProgressPos + 5.0;
				if (this.OnProgress != null)
					this.OnProgress(this, new ProgressEventArgs(dProgress));

				int iAxes = mdsResult.GetLength(0);
				// for the first PCs, add new properties to the GO Terms with their values.
				for (int i = 0; i < aMDSTerms.Count; i++)
				{
					GOTerm curTerm = this.aTerms[aMDSTerms[i]];  // order of terms in TermList should correspond to order of instances
					GOTermProperties oProperties = this.aProperties[aMDSTerms[i]];
					oProperties.PC.Clear();

					for (int j = 0; j < iMDSAxes; j++)
					{
						if (token.IsCancellationRequested)
							return false;

						if (j < iAxes)
						{
							// PC is available (generally, it should be, except for very small distance matrices)
							oProperties.PC.Add(mdsResult[j, i]);
						}
						else
						{
							// PC is not available (the distance matrix was very small)
							oProperties.PC.Add(0.0);
						}
					}
				}

				dProgress = this.dProgressPos + 6.0;
				if (this.OnProgress != null)
					this.OnProgress(this, new ProgressEventArgs(dProgress));

				bError = false;
			}
			else
			{
				// if we don't have enough columns to do MDS, don't report it as error
				bError = false;

				int iAxes = oMatrixArray.GetLength(1);
				// for the first PCs, add new properties to the GO Terms with their values.
				for (int i = 0; i < aMDSTerms.Count; i++)
				{
					GOTerm curTerm = this.aTerms[aMDSTerms[i]];  // order of terms in TermList should correspond to order of instances
					GOTermProperties oProperties = this.aProperties[aMDSTerms[i]];
					oProperties.PC.Clear();

					for (int j = 0; j < iMDSAxes; j++)
					{
						if (token.IsCancellationRequested)
							return false;

						if (j < iAxes)
						{
							// PC is available (generally, it should be, except for very small distance matrices)
							oProperties.PC.Add(oMatrixArray[i, j]);
						}
						else
						{
							// PC is not available (the distance matrix was very small)
							oProperties.PC.Add(0.0);
						}
					}
				}
			}

			// 3D MDS
			iMDSAxes = 3;

			// skip MDS if we have insufficient data
			if (iTermsCount > iMDSAxes)
			{
				double[,] mdsResult = MDS.MDS.StressMinimization(oMatrixArray, iMDSAxes, 0, 5, 10000); // really try to converge, iterate for 10 seconds

				if (token.IsCancellationRequested)
					return false;

				dProgress = this.dProgressPos + 9.0;
				if (this.OnProgress != null)
					this.OnProgress(this, new ProgressEventArgs(dProgress));

				int iAxes = mdsResult.GetLength(0);
				// for the first PCs, add new properties to the GO Terms with their values.
				for (int i = 0; i < aMDSTerms.Count; i++)
				{
					GOTerm curTerm = this.aTerms[aMDSTerms[i]];  // order of terms in TermList should correspond to order of instances
					GOTermProperties oProperties = this.aProperties[aMDSTerms[i]];
					oProperties.PC3.Clear();

					for (int j = 0; j < iMDSAxes; j++)
					{
						if (token.IsCancellationRequested)
							return false;

						if (j < iAxes)
						{
							// PC is available (generally, it should be, except for very small distance matrices)
							oProperties.PC3.Add(mdsResult[j, i]);
						}
						else
						{
							// PC is not available (the distance matrix was very small)
							oProperties.PC3.Add(0.0);
						}
					}
				}

				dProgress = this.dProgressPos + 10.0;
				if (this.OnProgress != null)
					this.OnProgress(this, new ProgressEventArgs(dProgress));

				bError = false;
			}
			else
			{
				// if we don't have enough columns to do MDS, don't report it as error
				bError = false;

				int iAxes = oMatrixArray.GetLength(1);
				// for the first PCs, add new properties to the GO Terms with their values.
				for (int i = 0; i < aMDSTerms.Count; i++)
				{
					GOTerm curTerm = this.aTerms[aMDSTerms[i]];  // order of terms in TermList should correspond to order of instances
					GOTermProperties oProperties = this.aProperties[aMDSTerms[i]];
					oProperties.PC3.Clear();

					for (int j = 0; j < iMDSAxes; j++)
					{
						if (token.IsCancellationRequested)
							return false;

						if (j < iAxes)
						{
							// PC is available (generally, it should be, except for very small distance matrices)
							oProperties.PC3.Add(oMatrixArray[i, j]);
						}
						else
						{
							// PC is not available (the distance matrix was very small)
							oProperties.PC3.Add(0.0);
						}
					}
				}
			}

			this.dProgressPos += this.dProgressSlice;
			if (this.OnProgress != null)
				this.OnProgress(this, new ProgressEventArgs(this.dProgressPos));

			return bError;
		}

		private void MakeSimpleThresholdOntolograph(double similarityThreshold, CancellationToken token)
		{
			OntoloGraph result = new OntoloGraph();
			GeneOntology oOntology = this.oParent.Ontology;
			SpeciesAnnotations oAnnotations = this.oParent.Annotations;
			BHashSet<GOTerm> outerTermsProcessed = new BHashSet<GOTerm>();
			BHashSet<int> termIdsWithConnections = new BHashSet<int>();
			int iTermCount = this.aTerms.Length;

			if (iTermCount == 0)
				this.oOntoloGraph = null; // empty if no input data

			double dProgressOld = this.dProgressPos;
			this.dProgressSlice = 10.0 / 3.0;
			double dProgressStep = this.dProgressSlice / (double)iTermCount;

			if (this.oMatrix == null)
				throw new Exception("The matrix for the TermListVisualizer is not contructed");


			// this is all to find the threshold for the terms we export to graph
			int iNoOfEdges = iTermCount * (iTermCount - 1) / 2;
			int iSimilarityCount = 0;
			double[] aSimilarities = new double[iNoOfEdges];

			for (int i = 0; i < iTermCount; i++)
			{
				GOTerm outerGoTerm = this.aTerms[i];
				int outerGoId = outerGoTerm.ID;
				outerTermsProcessed.Add(outerGoTerm);

				for (int j = 0; j < iTermCount; j++)
				{
					GOTerm innerGoTerm = this.aTerms[j];
					int innerGoId = innerGoTerm.ID;

					if (token.IsCancellationRequested)
						return;

					// skip connections to self, and two-way connections
					if (innerGoId == outerGoId || outerTermsProcessed.Contains(innerGoTerm))
						continue;

					if (!this.aProperties[i].Pinned)
					{
						double dDispensability = this.aProperties[i].Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.oParent.CutOff)
							continue;
					}
					if (!this.aProperties[j].Pinned)
					{
						double dDispensability = this.aProperties[j].Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.oParent.CutOff)
							continue;
					}

					double simil = this.oMatrix.Matrix[i, j];
					aSimilarities[iSimilarityCount++] = simil;
				}

				double dProgress = this.dProgressPos + (double)(i + 1) * dProgressStep;
				if ((dProgress - dProgressOld) >= 0.1 && this.OnProgress != null)
				{
					dProgressOld = dProgress;
					this.OnProgress(this, new ProgressEventArgs(dProgress));
				}
			}

			// find the threshold for the terms
			Array.Sort(aSimilarities);
			double threshold;
			if (iNoOfEdges == 0)
			{
				threshold = double.NaN; // in this case, it won't matter what this value is anyhow..
			}
			else
			{
				threshold = aSimilarities[Math.Max((int)(iNoOfEdges * similarityThreshold) - 1, 0)];
			}

			outerTermsProcessed.Clear();
			termIdsWithConnections.Clear();

			// construct graph edges
			this.dProgressPos += this.dProgressSlice;
			dProgressOld = this.dProgressPos;

			for (int i = 0; i < iTermCount; i++)
			{
				GOTerm outerGoTerm = this.aTerms[i];
				int outerGoId = outerGoTerm.ID;

				outerTermsProcessed.Add(outerGoTerm);

				for (int j = 0; j < iTermCount; j++)
				{
					if (token.IsCancellationRequested)
						return;

					GOTerm innerGoTerm = this.aTerms[j];
					int innerGoId = innerGoTerm.ID;

					// skip connections to self, and two-way connections
					if (innerGoId == outerGoId || outerTermsProcessed.Contains(innerGoTerm))
						continue;

					if (!this.aProperties[i].Pinned)
					{
						double dDispensability = this.aProperties[i].Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.oParent.CutOff)
							continue;
					}

					if (!this.aProperties[j].Pinned)
					{
						double dDispensability = this.aProperties[j].Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.oParent.CutOff)
							continue;
					}

					double simil = this.oMatrix.Matrix[i, j];

					if (simil >= threshold)
					{
						GraphEdge edge = new GraphEdge();
						edge.sourceID = outerGoId;
						edge.destinationID = innerGoId;
						edge.properties.Add("similarity", simil);
						result.edges.Add(edge);
						termIdsWithConnections.Add(innerGoId);
						termIdsWithConnections.Add(outerGoId);
					}
				}

				double dProgress = this.dProgressPos + (double)(i + 1) * dProgressStep;
				if ((dProgress - dProgressOld) >= 0.1 && this.OnProgress != null)
				{
					dProgressOld = dProgress;
					this.OnProgress(this, new ProgressEventArgs(dProgress));
				}
			}

			// Before this point,
			// it is expected that makeOntolograph() was called, where the uniqueness
			// and dispensability were computed

			// Create graph nodes. Start by enumerating all properties
			// of GO terms, then iterate term by term and write out all its properties
			// into appropriate columns.
			this.dProgressPos += this.dProgressSlice;
			dProgressOld = this.dProgressPos;

			for (int i = 0; i < iTermCount; i++)
			{
				GOTerm curGoTerm = this.aTerms[i];
				GOTermProperties oProperties = this.aProperties[i];

				if (token.IsCancellationRequested)
					return;

				if (!oProperties.Pinned)
				{
					double dDispensability = oProperties.Dispensability;
					if (!double.IsNaN(dDispensability) && dDispensability > this.oParent.CutOff)
						continue;
				}

				GraphNode node = new GraphNode();
				node.ID = curGoTerm.ID;
				node.properties.Add("description", curGoTerm.Name);

				// string[] props = { "value", "LogSize", "PC_1", "PC_2", "dispensability", "uniqueness" };
				if (!double.IsNaN(oProperties.Value))
				{
					node.properties.Add("value", oProperties.Value);
				}
				else
				{
					node.properties.Add("value", 0.0);
				}

				if (!double.IsNaN(oProperties.LogAnnotationSize))
				{
					node.properties.Add("LogSize", oProperties.LogAnnotationSize);
				}
				else
				{
					node.properties.Add("LogSize", 0.0);
				}

				if (oProperties.PC.Count > 0 && !double.IsNaN(oProperties.PC[0]))
				{
					node.properties.Add("PC_1", oProperties.PC[0]);
				}
				else
				{
					node.properties.Add("PC_1", 0.0);
				}

				if (oProperties.PC.Count > 1 && !double.IsNaN(oProperties.PC[1]))
				{
					node.properties.Add("PC_2", oProperties.PC[1]);
				}
				else
				{
					node.properties.Add("PC_2", 0.0);
				}

				if (!double.IsNaN(oProperties.Dispensability))
				{
					node.properties.Add("dispensability", oProperties.Dispensability);
				}
				else
				{
					node.properties.Add("dispensability", 0.0);
				}

				if (!double.IsNaN(oProperties.Uniqueness))
				{
					node.properties.Add("uniqueness", oProperties.Uniqueness);
				}
				else
				{
					node.properties.Add("uniqueness", 0.0);
				}

				result.nodes.Add(node);

				double dProgress = this.dProgressPos + (double)(i + 1) * dProgressStep;
				if ((dProgress - dProgressOld) >= 0.1 && this.OnProgress != null)
				{
					dProgressOld = dProgress;
					this.OnProgress(this, new ProgressEventArgs(dProgress));
				}
			}

			this.dProgressPos += this.dProgressSlice;
			result.writeAdditionalAttributes();
			this.oOntoloGraph = result;
		}
	}
}
