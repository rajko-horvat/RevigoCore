using System;
using IRB.Revigo.Core.Databases;

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
	public class RevigoTermCollection : List<RevigoTerm>
	{
		public RevigoTermCollection()
		{ }

		public RevigoTermCollection(IEnumerable<RevigoTerm> collection)
			: base(collection)
		{ }

		public RevigoTermCollection(IEnumerable<GeneOntologyTerm> collection)
		{
			foreach (GeneOntologyTerm term in collection)
			{
				this.Add(new RevigoTerm(term));
			}
		}

		public RevigoTerm? Find(int goTermID)
		{
			if (goTermID <= 0)
				return null;

			for (int i = 0; i < this.Count; i++)
			{
				if (this[i].GOTerm.ID == goTermID)
				{
					return this[i];
				}
			}

			return null;
		}

		public RevigoTermCollection Clone()
		{
			RevigoTermCollection result = new RevigoTermCollection(this);

			return result;
		}

		public RevigoTermCollection FindClustersAndSortByThem(GeneOntology ontology, double dispensabilityCutoff)
		{
			// make a copy of terms
			RevigoTermCollection aTermBag = this.Clone();

			// first make a set of "untouchables" - those GO terms that are below dispensability cutoff
			RevigoTermCollection aNewCollection = new RevigoTermCollection();

			for (int i = 0; i < aTermBag.Count; i++)
			{
				RevigoTerm term = aTermBag[i];

				term.RepresentativeID = -1;

				if (term.Dispensability < dispensabilityCutoff)
				{
					aNewCollection.Add(term);

					aTermBag.RemoveAt(i);
					i--;
				}
			}

			// In next stage attach children to representatives.
			// There can be terms without representative (DispensedBy <= 0), ignore those!
			// This can happen if CutOff is too restrictive, and some representatives
			// don't pass dispensability threshold, for example in TreeMap
			for (int i = 0; i < aNewCollection.Count; i++)
			{
				int iRepresentativeID = aNewCollection[i].GOTerm.ID;

				i += AddChildrenRecursive(aNewCollection, i, iRepresentativeID, iRepresentativeID, aTermBag);
			}

			// sort the list by Term ID
			aNewCollection.Sort(new SortByID());

			return aNewCollection;
		}

		private int AddChildrenRecursive(RevigoTermCollection collection, int parentIndex, int parentID, int representativeID, RevigoTermCollection termBag)
		{
			int iChildCount = 0;
			RevigoTermCollection aChildren = new RevigoTermCollection();

			// do this in two passes so term bag maintains order
			for (int i = 0; i < termBag.Count; i++)
			{
				RevigoTerm child = termBag[i];

				if (child.DispensedByID == parentID)
				{
					child.RepresentativeID = representativeID;
					aChildren.Add(child);

					collection.Insert(parentIndex + iChildCount + 1, child);
					iChildCount++;

					termBag.RemoveAt(i);
					i--;
				}
			}

			// And now add children of children also
			for (int i = 0; i < aChildren.Count; i++)
			{
				iChildCount += AddChildrenRecursive(collection, parentIndex + iChildCount, aChildren[i].GOTerm.ID, representativeID, termBag);
			}

			return iChildCount;
		}

		private class SortByID : IComparer<RevigoTerm>
		{
			public SortByID()
			{
			}

			public int Compare(RevigoTerm? o1, RevigoTerm? o2)
			{
				// -1 - o1 <  o2
				// 0  - o1 == o2
				// 1  - o1 >  o2

				// handle nulls
				if (o1 == null && o2 == null)
					return 0;
				if (o1 == null)
					return -1;
				if (o2 == null)
					return 1;

				// they are equal if ID is the same
				if (o1.GOTerm.ID == o2.GOTerm.ID)
					return 0;

				int o1_d = o1.RepresentativeID;
				int o2_d = o2.RepresentativeID;

				if (o1_d <= 0 && o2_d <= 0)
				{
					// both are representatives
					return o1.GOTerm.ID.CompareTo(o2.GOTerm.ID);
				}

				if (o1_d <= 0)
				{
					// first is representative
					if (o1.GOTerm.ID <= o2_d)
						return -1;

					return 1;
				}

				if (o2_d <= 0)
				{
					// second is representative
					if (o1_d < o2.GOTerm.ID)
						return -1;

					return 1;
				}

				if (o1_d == o2_d)
				{
					// both have same representative
					return o1.GOTerm.ID.CompareTo(o2.GOTerm.ID);
				}

				// both are children and have different representative
				return o1_d.CompareTo(o2_d);
			}
		}
	}
}
