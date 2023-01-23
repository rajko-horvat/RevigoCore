using System;
using System.Collections.Generic;
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
	public class GOTermList : List<GOTerm>
	{
		public GOTermList()
		{ }

		public GOTermList(IEnumerable<GOTerm> collection)
			: base(collection)
		{ }

		public GOTermList Clone()
		{
			GOTermList result = new GOTermList(this);

			return result;
		}

		public void FindClustersAndSortByThem(GeneOntology ontology, BDictionary<int, GOTermProperties> termProperties, double dispensabilityCutoff)
		{
			// make a copy of terms
			List<GOTerm> aTermBag = new List<GOTerm>(this);

			// first make a set of "untouchables" - those GO terms that are below dispensability cutoff
			List<GOTerm> aResultList = new List<GOTerm>();

			for (int i = 0; i < aTermBag.Count; i++)
			{
				GOTerm term = aTermBag[i];
				GOTermProperties properties = termProperties.GetValueByKey(term.ID);

				properties.Representative = -1;

				if (properties.Dispensability < dispensabilityCutoff)
				{
					aResultList.Add(term);

					aTermBag.RemoveAt(i);
					i--;
				}
			}

			// In next stage attach children to representatives.
			// There can be terms without representative (DispensedBy <= 0), ignore those!
			// This can happen if CutOff is too restrictive, and some representatives
			// don't pass dispensability threshold, for example in TreeMap
			for (int i = 0; i < aResultList.Count; i++)
			{
				int iRepresentativeID = aResultList[i].ID;

				i += AddChildrenRecursive(aResultList, i, iRepresentativeID, iRepresentativeID, aTermBag, termProperties);
			}

			// sort the list by Term ID
			aResultList.Sort(new SortByTermID(termProperties));

			// update this collection
			this.Clear();
			this.AddRange(aResultList);
		}

		private int AddChildrenRecursive(List<GOTerm> resultList, int parentIndex, int parentID, int representativeID,
			List<GOTerm> termBag, BDictionary<int, GOTermProperties> termProperties)
		{
			int iChildCount = 0;
			List<GOTerm> aChildren = new List<GOTerm>();

			// do this in two passes so term bag maintains order
			for (int i = 0; i < termBag.Count; i++)
			{
				GOTerm child = termBag[i];
				GOTermProperties properties = termProperties.GetValueByKey(child.ID);

				if (properties.DispensedBy == parentID)
				{
					properties.Representative = representativeID;
					aChildren.Add(child);

					resultList.Insert(parentIndex + iChildCount + 1, child);
					iChildCount++;

					termBag.RemoveAt(i);
					i--;
				}
			}

			// And now add children of children also
			for (int i = 0; i < aChildren.Count; i++)
			{
				iChildCount += AddChildrenRecursive(resultList, parentIndex + iChildCount, aChildren[i].ID, 
					representativeID, termBag, termProperties);
			}

			return iChildCount;
		}

		private class SortByTermID : IComparer<GOTerm>
		{
			private BDictionary<int, GOTermProperties> properties;

			public SortByTermID(BDictionary<int, GOTermProperties> properties)
			{
				this.properties = properties;
			}

			public int Compare(GOTerm o1, GOTerm o2)
			{
				// -1 - o1 <  o2
				// 0  - o1 == o2
				// 1  - o1 >  o2

				// they are equal if ID is the same
				if (o1.ID == o2.ID)
					return 0;

				int o1_d = properties.GetValueByKey(o1.ID).Representative;
				int o2_d = properties.GetValueByKey(o2.ID).Representative;

				if (o1_d <= 0 && o2_d <= 0)
				{
					// both are representatives
					return o1.ID.CompareTo(o2.ID);
				}

				if (o1_d <= 0)
				{
					// first is representative
					if (o1.ID <= o2_d)
						return -1;

					return 1;
				}

				if (o2_d <= 0)
				{
					// second is representative
					if (o1_d < o2.ID)
						return -1;

					return 1;
				}

				if (o1_d == o2_d)
				{
					// both have same representative
					return o1.ID.CompareTo(o2.ID);
				}

				// both are children and have different representative
				return o1_d.CompareTo(o2_d);
			}
		}

		private class SortRepresentatives : IComparer<GOTerm>
		{
			private BDictionary<int, GOTermProperties> properties;

			public SortRepresentatives(BDictionary<int, GOTermProperties> properties)
			{
				this.properties = properties;
			}

			public int Compare(GOTerm o1, GOTerm o2)
			{
				// -1 - o1 <  o2
				// 0  - o1 == o2
				// 1  - o1 >  o2

				double o1_d = properties.GetValueByKey(o1.ID).Dispensability;
				double o2_d = properties.GetValueByKey(o2.ID).Dispensability;

				// sort them by dispensability and if both have equal dispensability by ID
				if (o1_d < o2_d)
				{
					return -1;
				}
				else if (o1_d > o2_d)
				{
					return 1;
				}

				// if both representatives are equally dispensible, the one
				// with lower GO number goes first
				return o1.ID.CompareTo(o2.ID);
			}
		}

		private class SortDispensed : IComparer<GOTerm>
		{
			private BDictionary<int, GOTermProperties> properties;

			public SortDispensed(BDictionary<int, GOTermProperties> properties)
			{
				this.properties = properties;
			}

			public int Compare(GOTerm o1, GOTerm o2)
			{
				// -1 - o1 <  o2
				// 0  - o1 == o2
				// 1  - o1 >  o2

				double repId1 = properties.GetValueByKey(o1.ID).Representative;
				double repId2 = properties.GetValueByKey(o2.ID).Representative;
				double o1_d = properties.GetValueByKey(o1.ID).Dispensability;
				double o2_d = properties.GetValueByKey(o2.ID).Dispensability;

				if (repId1 != repId2)
				{
					// first sort them by representative
					return repId1.CompareTo(repId2);
				}

				// if terms have equal representatives, the one with the less
				// dispensability goes first
				if (o1_d < o2_d)
				{
					return -1;
				}
				else if (o1_d > o2_d)
				{
					return 1;
				}

				return o1.ID.CompareTo(o2.ID);
			}
		}
	}
}
