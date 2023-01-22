using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Serialization;
using IRB.Collections.Generic;
using System.Globalization;

namespace IRB.Database
{
	/// <summary>
	/// A class holding annotations for a species.
	/// 
	/// Authors: Fran Supek (fsupek at irb.hr)
	///          Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License: MIT
	///		Copyright (c) 2021 Ruđer Bošković Institute
	///		
	/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
	/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
	/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
	/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	/// 	subject to the following conditions: 
	/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
	/// 	
	///		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	///		INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
	///		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	///		IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
	///		DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
	///		ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
	/// </summary>
	[Serializable]
	public class SpeciesAnnotations
	{
		private int iTaxonID = -1;
		private string sSpeciesName = null;

		// A dictionary, mapping from GO id to size (# of proteins annotated with it).
		private BDictionary<int, int> oAnnotations = new BDictionary<int, int>();
		private int iSumOfAnnotations = 0;

		// Category sizes, divided by the size of the root node in that particular namespace.
		private BDictionary<int, double> oNormalizedAnnotations = new BDictionary<int, double>();
		private double dSumOfNormalizedAnnotations = 0.0;

		private GOTermWordCorpus oWordCorpus = new GOTermWordCorpus();

		public SpeciesAnnotations()
		{ }

		public SpeciesAnnotations(GeneOntology go, int taxonID, string speciesName, BDictionary<int, int> annotations, BDictionary<int, double> normalizedAnnotations)
		{
			this.iTaxonID = taxonID;
			this.sSpeciesName = speciesName;
			this.oAnnotations = annotations;
			this.oNormalizedAnnotations = normalizedAnnotations;

			for (int i = 0; i < this.oAnnotations.Count; i++)
			{
				this.iSumOfAnnotations += annotations[i].Value;
				this.dSumOfNormalizedAnnotations += this.oNormalizedAnnotations[i].Value;
			}

			this.oWordCorpus = new GOTermWordCorpus(this, go);
		}
		
		public int TaxonID
		{
			get
			{
				return this.iTaxonID;
			}
			set
			{
				this.iTaxonID = value;
			}
		}

		public string SpeciesName
		{
			get
			{
				return this.sSpeciesName;
			}
			set
			{
				this.sSpeciesName = value;
			}
		}

		public BDictionary<int, int> Annotations
		{
			get
			{
				return this.oAnnotations;
			}
		}

		public int SumOfAnnotations
		{
			get
			{
				return this.iSumOfAnnotations;
			}
			set
			{
				this.iSumOfAnnotations = value;
			}
		}

		public BDictionary<int, double> NormalizedAnnotations
		{
			get
			{
				return this.oNormalizedAnnotations;
			}
		}

		public double SumOfNormalizedAnnotations
		{
			get
			{
				return this.dSumOfNormalizedAnnotations;
			}
			set
			{
				this.dSumOfNormalizedAnnotations = value;
			}
		}

		public GOTermWordCorpus WordCorpus
		{
			get
			{
				return this.oWordCorpus;
			}
		}

		public int GetTermSize(int goId, GeneOntology myGo)
		{
			if (this.oAnnotations.ContainsKey(goId))
			{
				return this.oAnnotations.GetValueByKey(goId);
			}
			else
			{
				// look at sizes of all siblings, and average them to guess the
				// frequency of the term
				GOTerm curTerm = myGo.GetValueByKey(goId);
				BHashSet<GOTerm> sibs = curTerm.GetSiblings();
				int numUsableSibs = 0;
				int sumOfSizesOfUsableSibs = 0;

				foreach (GOTerm sib in sibs)
				{
					if (this.oAnnotations.ContainsKey(sib.ID)
							&& this.oAnnotations.GetValueByKey(sib.ID) > 0)
					{
						numUsableSibs++;
						sumOfSizesOfUsableSibs += this.oAnnotations.GetValueByKey(sib.ID);
					}
				}
				if (numUsableSibs >= 1)
				{
					this.oAnnotations.Add(goId, sumOfSizesOfUsableSibs / numUsableSibs);
					return sumOfSizesOfUsableSibs / numUsableSibs;
				}

				// no usable siblings? try frequency of largest child
				BHashSet<GOTerm> children = curTerm.Children;
				int sizeOfBiggestChild = 0;

				foreach (GOTerm child in children)
				{
					if (this.oAnnotations.ContainsKey(child.ID))
					{
						if (this.oAnnotations.GetValueByKey(child.ID) > sizeOfBiggestChild)
							sizeOfBiggestChild = this.oAnnotations.GetValueByKey(child.ID);
					}
				}
				if (sizeOfBiggestChild > 0)
				{
					this.oAnnotations.Add(goId, sizeOfBiggestChild);
					return sizeOfBiggestChild;
				}

				// no usable children? try frequency of smallest parent (EXCLUDING GO ROOT TERMS!!!)
				BHashSet<int> parentIDs = curTerm.AllParents;
				int sizeOfSmallestParent = int.MaxValue;

				foreach (int parentID in parentIDs)
				{
					if (myGo.GetValueByKey(parentID).IsTopmost)
						continue;

					if (this.oAnnotations.ContainsKey(parentID))
					{
						if (this.oAnnotations.GetValueByKey(parentID) > 0
								&& this.oAnnotations.GetValueByKey(parentID) < sizeOfSmallestParent)
							sizeOfSmallestParent = this.oAnnotations.GetValueByKey(parentID);
					}
				}
				if (sizeOfSmallestParent < int.MaxValue)
				{
					this.oAnnotations.Add(goId, sizeOfSmallestParent); // cache for next time
					return sizeOfSmallestParent;
				}

				// No more options to try... this happens in very rare cases with terms
				// that have been made obsolete before being officially introduced
				// and that don't have any parents or children assigned. In that case,
				// return the average size of all GO Terms.
				return this.iSumOfAnnotations / this.oAnnotations.Count;
			}
		}

		public double GetTermFrequency(int goId, GeneOntology myGo)
		{
			if (this.oNormalizedAnnotations.ContainsKey(goId))
			{
				return this.oNormalizedAnnotations.GetValueByKey(goId);
			}
			else
			{
				return CalculateTermFrequency(goId, myGo, new List<int>());
			}
		}

		private double CalculateTermFrequency(int goId, GeneOntology myGo, List<int> missingTerms)
		{
			// look at frequencies of all siblings, and average them to guess the 
			// frequency of the term

			// prevent stack overflow
			if (missingTerms.Count > 200)
			{
				return this.dSumOfNormalizedAnnotations / this.oNormalizedAnnotations.Count;
			}

			GOTerm curTerm = myGo.GetValueByKey(goId);
			BHashSet<GOTerm> sibs = curTerm.GetSiblings();
			int numUsableSibs = 0;
			double sumOfFreqsOfUsableSibs = 0.0;

			foreach (GOTerm sib in sibs)
			{
				// Added by rhorvat at 5.3.2022. - For this to work properly we have to define Normalized annotations for all siblings and all children
				// prevent recursion with missingTerms array
				if (!this.oNormalizedAnnotations.ContainsKey(sib.ID) && !missingTerms.Contains(sib.ID))
				{
					missingTerms.Add(sib.ID);
					CalculateTermFrequency(sib.ID, myGo, missingTerms);
				}

				if (this.oNormalizedAnnotations.ContainsKey(sib.ID) &&
					this.oNormalizedAnnotations.GetValueByKey(sib.ID) > 0 &&
					!Double.IsNaN(this.oNormalizedAnnotations.GetValueByKey(sib.ID)))
				{
					numUsableSibs++;
					sumOfFreqsOfUsableSibs += this.oNormalizedAnnotations.GetValueByKey(sib.ID);
				}
			}
			if (numUsableSibs >= 1)
			{
				this.oNormalizedAnnotations.Add(goId, sumOfFreqsOfUsableSibs / numUsableSibs);
				return sumOfFreqsOfUsableSibs / numUsableSibs;
			}

			// no usable siblings? try frequency of largest child
			BHashSet<GOTerm> children = curTerm.Children;
			double freqOfBiggestChild = 0.0;

			foreach (GOTerm child in children)
			{
				// Added by rhorvat at 5.3.2022. - For this to work properly we have to define Normalized annotations for all siblings and all children
				// prevent recursion with missingTerms array
				if (!this.oNormalizedAnnotations.ContainsKey(child.ID) && !missingTerms.Contains(child.ID))
				{
					missingTerms.Add(child.ID);
					CalculateTermFrequency(child.ID, myGo, missingTerms);
				}

				if (this.oNormalizedAnnotations.ContainsKey(child.ID) &&
					!Double.IsNaN(this.oNormalizedAnnotations.GetValueByKey(child.ID)))
				{
					if (this.oNormalizedAnnotations.GetValueByKey(child.ID) > freqOfBiggestChild)
						freqOfBiggestChild = this.oNormalizedAnnotations.GetValueByKey(child.ID);
				}
			}
			if (freqOfBiggestChild > 0.0)
			{
				this.oNormalizedAnnotations.Add(goId, freqOfBiggestChild);
				return freqOfBiggestChild;
			}

			// no usable children? try frequency of smallest parent (EXCLUDING GO ROOT TERMS!!!)
			BHashSet<int> parentIDs = curTerm.AllParents;
			double freqOfSmallestParent = double.MaxValue;

			foreach (int parentID in parentIDs)
			{
				if (myGo.GetValueByKey(parentID).IsTopmost)
					continue;

				// Added by rhorvat at 5.3.2022. - For this to work properly we have to define Normalized annotations for all siblings and all children
				// prevent recursion with missingTerms array
				if (!this.oNormalizedAnnotations.ContainsKey(parentID) && !missingTerms.Contains(parentID))
				{
					missingTerms.Add(parentID);
					CalculateTermFrequency(parentID, myGo, missingTerms);
				}

				if (this.oNormalizedAnnotations.ContainsKey(parentID)
					&& !Double.IsNaN(this.oNormalizedAnnotations.GetValueByKey(parentID)))
				{
					if (this.oNormalizedAnnotations.GetValueByKey(parentID) < freqOfSmallestParent)
						freqOfSmallestParent = this.oNormalizedAnnotations.GetValueByKey(parentID);
				}
			}
			if (freqOfSmallestParent < double.MaxValue)
			{
				// this is a very weird situation when the non-root nodes (eg. 5623 'cell') have the 
				// same relative frequency as the root node, ie 1.0. They will be force-assigned a lower
				// frequency, for the purposes of propagating it downwards to their children (which were 
				// of undefined size/freq). Note that we do this only for the relative frequencies, 
				// but not for the absolute ones (=sizes), which is determined in getSizeGuessIfUnknown() 
				if (freqOfSmallestParent > 0.75)
				{
					freqOfSmallestParent = 0.75;
				}
				this.oNormalizedAnnotations.Add(goId, freqOfSmallestParent);
				return freqOfSmallestParent;
			}
			// No more options to try... this happens in very rare cases with terms
			// that have been made obsolete before being officially introduced
			// and that don't have any parents or children assigned. In that case,
			// return the average frequency of all GO Terms

			return this.dSumOfNormalizedAnnotations / this.oNormalizedAnnotations.Count;
		}
	}
}
