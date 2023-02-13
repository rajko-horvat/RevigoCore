using System;
using System.Collections.Generic;
using IRB.Collections.Generic;
using IRB.Revigo.Core;

namespace IRB.Revigo.Databases
{
	/// <summary>
	/// A class that holds Word Annotations from Gene Ontology
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
	[Serializable]
	public class GOTermWordCorpus : BDictionary<string, double>
	{
		private static BHashSet<string> stopwords = new BHashSet<string>(new string[] {
		"a", "the", "for", "and", "or", "in", "to", "an", "as", "at",
		"about", "by", "be", "is", "it", "how", "of", "on", "that", "this",
		"was", "what", "when", "where", "who", "will", "with", "during", "it",
		"its", "being", "find", "found", "acts", "terms", "term", "into", "etc",
		"their", "using", "use", "no", "are", "each", "contain", "certain",
		"but", "two", "one", "some", "acting", "comprises", "also", "include",
		"examples", "any", "may", "such", "more", "another", "known",
		"from", "relative", "entity", "involving", "which", "function", "usually",
		"includes", "together", "other", "moiety", "exclude", "excludes",
		"operate", "operates", "either", "conceptually", "actually",
		"essentially", "within", "involved", "containing", "wholly", "among",
		"given", "necessary", "singly", "unknown", "bears", "distinctive",
		"out", "consisting", "corresponds", "not", "comprising", "comprise",
		"whose", "involves", "involve", "either", "so", "relatively",
		"enables", "enable", "features", "all", "some", "normally",
		"several", "perform", "namely", "characteristically", "distinct",
		"characteristic", "hence", "already", "predominantly", "similar",
		"many", "distinguished", "encompasses", "aspect", "occupies", "gene's",
		"gene", "protein", "genes", "work", "pertinent", "biological",
		"consist", "mainly", "perform", "actual", "namely", "symbol",
		"especially", "regarded", "when", "involves", "ones", "besides",
		"derives", "derive", "typically", "goes", "serves", "produces", "similar",
		"whereas", "suitable", "comprises", "whose", "necessarily",
		"exist", "included", "classed", "especially", "highly", "resulting"});

		public GOTermWordCorpus()
		{ }

		/// <summary>
		/// A GoTermWordCorpus may be created from a SpeciesAnnotations object.
		/// </summary>
		/// <param name="annotations"></param>
		/// <param name="myGo"></param>
		public GOTermWordCorpus(SpeciesAnnotations annotations, GeneOntology myGo)
		{
			int sumOfSizes = 0;
			foreach (int termId in annotations.Annotations.Keys)
			{
				sumOfSizes += annotations.GetTermSize(termId, myGo);
			}

			foreach (int termId in annotations.Annotations.Keys)
			{
				// do not consider terms inapplicable to this organism
				// or terms with poor annotation coverage
				if (annotations.GetTermSize(termId, myGo) < Math.Min(2, sumOfSizes / 100000))
				{
					continue;
				}

				GOTerm term = myGo.Terms.GetValueByKey(termId);

				foreach (string keyword in term.Keywords)
				{
					if (!this.ContainsKey(keyword))
					{
						this.Add(keyword, 1.0);
					}
					else
					{
						this.Add(keyword, this.GetValueByKey(keyword) + 1.0);
					}
				}
			}

			removeStopWords();
			normalizeWordFreqs();
		}

		/// <summary>
		/// A GoTermWordCorpus may be created from a collection of GO terms.
		/// </summary>
		/// <param name="goTerms"></param>
		/// <param name="myGo"></param>
		public GOTermWordCorpus(ICollection<GOTerm> goTerms, GeneOntology myGo)
		{
			BHashSet<int> termsWithParents = new BHashSet<int>();

			foreach (GOTerm term in goTerms)
			{
				termsWithParents.Add(term.ID);
				foreach (int termID in term.AllParentIDs)
				{
					termsWithParents.Add(termID);
				}
			}

			foreach (int termID in termsWithParents)
			{
				BHashSet<string> wordSet = new BHashSet<string>();
				foreach (string key in myGo.Terms.GetValueByKey(termID).Keywords)
				{
					wordSet.Add(key);
				}

				foreach (string keyword in wordSet)
				{
					if (!this.ContainsKey(keyword))
					{
						this.Add(keyword, 1.0);
					}
					else
					{
						this.Add(keyword, this.GetValueByKey(keyword) + 1.0);
					}
				}
			}

			removeStopWords();
			normalizeWordFreqs();
		}

		/// <summary>
		/// Creates a GoTermWordCorpus from a collection of GO terms.
		/// 
		/// The word 'frequencies' in this corpus are not actually frequencies, but
		/// instead they are a measure of correlation of a specific word to the 
		/// value of the designated property of GO Terms.
		/// 
		/// The GO terms that do not have the specified property defined will be skipped.
		/// 
		/// The measure is =2*AUC and therefore varies from 0.0 (anticorrelated) over
		/// 1.0 (no correlation) to 2.0 (correlated).
		/// </summary>
		/// <param name="goTerms"></param>
		/// <param name="termProps"></param>
		/// <param name="go"></param>
		public GOTermWordCorpus(ICollection<GOTerm> goTerms, BDictionary<int, GOTermProperties> termProps, GeneOntology go)
		{
			// first, copy the data to a LinkedHashMap, we need the predictable
			// iteration order 
			BDictionary<GOTerm, double> lhm = new BDictionary<GOTerm,double>();
			BHashSet<int> oIDs = new BHashSet<int>();

			foreach (GOTerm term in goTerms)
			{
				double dTransformedValue = termProps.GetValueByKey(term.ID).TransformedValue;
				if (!double.IsNaN(dTransformedValue))
				{
					lhm.Add(term, dTransformedValue);
					oIDs.Add(term.ID);
				}
			}

			double[] vals = lhm.Values.ToArray();

			// sort by values - ascending order, smallest first
			// and larger values of the GoTermPropery are better, by definition
			int[] sortedIndices = Utilities.QuickSort(vals);

			// now go through the key-value pairs in the sorted order, and compute for
			// each word:
			// (a) sum of ranks for all 1's - in "wordFreqs"
			// (b) count of 1's - in "countOf1s"
			// from those two, we can compute the AUC (or Mann-Whitney U)

			BDictionary<string, int> countOf1s = new BDictionary<string, int>();

			for (int j = 0; j < sortedIndices.Length; j++)
			{
				GOTerm term = lhm[sortedIndices[j]].Key;
				int iRankToAdd = j + 1;

				foreach (string word in term.Keywords)
				{
					if (!countOf1s.ContainsKey(word))
						countOf1s.Add(word, 1);
					else
						countOf1s.Add(word, countOf1s.GetValueByKey(word) + 1);
					
					if (!this.ContainsKey(word))
						this.Add(word, iRankToAdd);
					else
						this.Add(word, this.GetValueByKey(word) + iRankToAdd);
				}
			}

			// okay, now convert the sum of ranks to 2*AUC (which is the result of this function)
			for (int i = 0; i < this.Count; i++)
			{
				string word = this[i].Key;
				double n1 = countOf1s.GetValueByKey(word);
				double n2 = sortedIndices.Length - countOf1s.GetValueByKey(word);
				double u = (double)this.GetValueByKey(word) - (n1 * (n1 + 1.0)) / 2.0;
				double auc = u / (n1 * n2);

				this[i] = new BKeyValuePair<string,double>(word, 2.0 * auc);
			}

			removeStopWords();
		}

		/// <summary>
		/// Gets frequency of given word in corpus.
		/// </summary>
		/// <param name="word"></param>
		/// <returns></returns>
		public double getWordFreq(string word)
		{
			if (this.ContainsKey(word))
				return this.GetValueByKey(word);
			else
				return 0.0;
		}

		public BDictionary<string, double>.KeyCollection getAllWords()
		{
			return this.Keys;
		}

		private void removeStopWords()
		{
			for (int i = 0; i < this.Count; i++)
			{
				string word = this[i].Key;
				if (word.Length < 2 || stopwords.Contains(word))
				{
					this.RemoveAt(i);
					i--;
				}
			}
		}

		/// <summary>
		/// Normalizes by dividing with average frequency.
		/// </summary>
		private void normalizeWordFreqs()
		{
			double sum = 0.0;
			foreach (string word in this.Keys)
			{
				sum += this.GetValueByKey(word);
			}
			foreach (string word in this.Keys)
			{
				this.Add(word, (this.GetValueByKey(word) * (double)this.Count) / sum);
			}
		}

		/// <summary>
		/// For each word present in the baselineWordCorpus, calculates how many times
		/// more or less frequently that word is used here (in this WordCorpus).
		/// </summary>
		/// <param name="baselineWordCorpus"></param>
		/// <param name="numFromTop"></param>
		/// <param name="numFromBottom"></param>
		/// <returns></returns>
		public BDictionary<string, double> calculateWordEnrichment(
				GOTermWordCorpus baselineWordCorpus,
				int numFromTop, int numFromBottom)
		{
			BDictionary<string, double> result = new BDictionary<string, double>(
					baselineWordCorpus.Count);

			foreach (string word in baselineWordCorpus.Keys)
			{
				if (numFromBottom <= 0  // if we're not interested in depletion, only enrichment
						&& this.getWordFreq(word) < 0.5)  // the frequency of word must be above 50% of average
					continue;

				result.Add(word,
						this.getWordFreq(word) / Math.Pow(baselineWordCorpus.getWordFreq(word), 0.66));
			}

			// now, select a subset of results
			double[] enrichments = result.Values.ToArray();
			Array.Sort(enrichments); // ascending order
			double thresholdTop;

			if (numFromTop <= 0 || enrichments.Length == 0)  // sometimes had exceptions for empty enrichments[]... could be the case when all words are rare? (<50% of average) 
				thresholdTop = double.MaxValue;
			else
				thresholdTop = enrichments[enrichments.Length - Math.Min(numFromTop, enrichments.Length)];

			double thresholdBottom;
			if (numFromBottom <= 0 || enrichments.Length == 0)
				thresholdBottom = double.MinValue;
			else
				thresholdBottom = enrichments[Math.Min(numFromBottom, enrichments.Length) - 1];

			// keep only the ones conforming to either of the thresholds
			// (or both, in the case the array of enrichments is really short)
			for (int i = 0; i < result.Count; i++)
			{
				string key = result[i].Key;
				double val = result[i].Value;
				if (val > thresholdBottom && val < thresholdTop)
				{
					result.RemoveAt(i);
					i--;
				}
			}

			return result;
		}

		public BDictionary<string, double> getMostFrequentWords(int numFromTop)
		{
			BDictionary<string, double> result = new BDictionary<string, double>();
			if (numFromTop <= 0)  // why would someone want this, I don't know
				return result;

			double[] allWordFreqs = this.Values.ToArray();

			// sort by values
			//int[] sortedIndices = qsort(allWordFreqs);
			//double cutoff = allWordFreqs[sortedIndices[Math.Max(allWordFreqs.Length - numFromTop, 0)]];

			Array.Sort(allWordFreqs);
			double cutoff = 0.0;
			if (allWordFreqs.Length > 0)
			{
				cutoff = allWordFreqs[Math.Max(allWordFreqs.Length - numFromTop, 0)];
			}

			foreach (string word in this.Keys)
			{
				if (this.GetValueByKey(word) >= cutoff)
					result.Add(word, this.GetValueByKey(word));
			}

			BDictionary<string, double> result1 = new BDictionary<string, double>();
			RandomMT19937 oRND = new RandomMT19937(26012021);
			while (result.Count > 0)
			{
				int iPos = oRND.Next(result.Count);
				result1.Add(result[iPos]);
				result.RemoveAt(iPos);
			}

			return result1;
		}
	}
}
