using System;
using System.Collections.Generic;
using System.Text;
using IRB.Collections.Generic;
using IRB.Database;

namespace IRB.Visualizer
{
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

			/*// now, reconstruct the whole chain, who gets dispensed by whom
			BDictionary<GOTerm, GOTerm> dispensedBy = new BDictionary<GOTerm, GOTerm>();
			foreach (GOTerm term in this)
			{
				int disp = termProperties.GetValueByKey(term.ID).DispensedBy;
				if (disp >= 0)
					dispensedBy.Add(term, ontology.GetValueByKey(disp));
			}

			List<GOTerm> oRepresentatives = new List<GOTerm>(aResultList);
			List<GOTerm> oDispensed = new List<GOTerm>();

			// now determine the representatives using the last two arrays
			foreach (GOTerm term in this)
			{
				if (!aResultList.Contains(term))
				{
					GOTerm myTerm = term;  // first, the term tries to represent itself
					oDispensed.Add(term);
					while (!aResultList.Contains(myTerm))
					{
						if (!dispensedBy.ContainsKey(myTerm))
						{
							// this term has no representative among representatives, add it to representatives
							//throw new Exception("Can't find term representative");
							myTerm = null;
							break;
						}
						myTerm = dispensedBy.GetValueByKey(myTerm);
					}
					if (myTerm != null)
					{
						termProperties.GetValueByKey(term.ID).Representative = myTerm.ID;
					}
					else
					{
						oRepresentatives.Add(term);
						oDispensed.Remove(term);
					}
				}
			}

			// sort representatives and dispensed separately
			oRepresentatives.Sort(new SortRepresentatives(termProperties));
			oDispensed.Sort(new SortDispensed(termProperties));

			// insert dispensed into representatives
			int iRepresentative = -1;
			int iPosition = -1;
			for (int i = 0; i < oDispensed.Count; i++)
			{
				GOTerm term = oDispensed[i];
				int iTermRepresentative = termProperties.GetValueByKey(term.ID).Representative;

				if (iRepresentative != iTermRepresentative)
				{
					iRepresentative = iTermRepresentative;

					iPosition = -1;
					for (int j = 0; j < oRepresentatives.Count; j++)
					{
						if (oRepresentatives[j].ID == iRepresentative)
						{
							iPosition = j + 1;
							break;
						}
					}
					if (iPosition < 0)
						throw new Exception("Can't find representative " + iRepresentative.ToString());
				}
				if (iPosition >= oRepresentatives.Count)
				{
					oRepresentatives.Add(term);
				}
				else
				{
					oRepresentatives.Insert(iPosition, term);
				}
				iPosition++;
			}

			// update this collection
			this.Clear();
			this.AddRange(oRepresentatives);*/
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
