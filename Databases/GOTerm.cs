using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using IRB.Collections.Generic;

namespace IRB.Revigo.Databases
{
	/// <summary>
	/// Class representing a single Gene Ontology term.
	///
	/// The GOTerm must have at least one unique ID (of type int) specified at time
	/// of creation, but may also have many alternate unique IDs.
	/// 
	/// The GOTerm may have a name, and it may have a number of alternate names.
	/// 
	/// The GOTerm may have zero, one or many parent nodes.
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
	public class GOTerm : IComparable<GOTerm>, IComparer<GOTerm>, IEqualityComparer<GOTerm>
	{
		// The GeneOntology object this term belongs to.
		private GeneOntology oOntology = null;
		private int iID = -1; // The main unique ID for the node.
		private BHashSet<int> aAltIDs = new BHashSet<int>(); // List of alternate IDs for the node.
		private GONamespaceEnum eNamespace; // The namespace this term belongs to.
		private string sName = null; // The main name of the node.
		private List<string> aAltNames = new List<string>(); // List of alternate names for the node
		private string sDescription = null;
		private string sComment = null;
		private bool bObsolete = false;
		
		private BHashSet<int> aGOParentIDs = new BHashSet<int>(); // A list of all parent IDs of the node in the ontology
		private BHashSet<int> aGOPartOfIDs = new BHashSet<int>(); // A list of all conditional parent IDs of the node in the ontology
		private BHashSet<int> aGOHasPartIDs = new BHashSet<int>(); // A list of all conditional children IDs of the node in the ontology
		private BHashSet<int> aGOConsiderIDs = new BHashSet<int>(); // A list of all terms that should be considered a replacement for this term in the ontology
		private BHashSet<int> aGOReplacementIDs = new BHashSet<int>(); // For obsolete terms, here is a suitable replacement.

		// constructed from GO
		private bool bReferencesInitialized = false;
		private BHashSet<int> aParentIDs = new BHashSet<int>(); // A list of all parent IDs of the node
		private BHashSet<int> aChildrenIDs = new BHashSet<int>(); // A list of direct children of this node
		private BHashSet<int> aAllParentIDs = new BHashSet<int>(); // A list of all parent terms
		private int iRootNodeID = -1;

		private BHashSet<string> aKeywords = new BHashSet<string>();

		public GOTerm()
		{
		}

		/// <summary>
		/// A unique ID (integer) must be specified for the GOTerm at the time of construction.
		/// </summary>
		/// <param name="id">The main unique ID for the GOTerm.</param>
		public GOTerm(GeneOntology geneOntology, int id)
		{
			this.oOntology = geneOntology;
			this.iID = id;
		}

		internal void InitializeTerm()
		{
			// here we initialize first generation of parents, children and keywords

			this.aParentIDs.Clear();
			this.aChildrenIDs.Clear();

			// A is a child of B, and B is a parent of A
			for (int j = 0; j < this.aGOParentIDs.Count; j++)
			{
				if (this.oOntology.Terms.ContainsKey(this.aGOParentIDs[j]))
				{
					GOTerm parent = this.oOntology.Terms.GetValueByKey(this.aGOParentIDs[j]);

					this.aParentIDs.Add(parent.ID);
					parent.ChildrenIDs.Add(this.iID);
				}
			}

			// A is a child of B, but B is not always a parent of A
			for (int j = 0; j < this.aGOPartOfIDs.Count; j++)
			{
				if (this.oOntology.Terms.ContainsKey(this.aGOPartOfIDs[j]))
					this.aParentIDs.Add(this.aGOPartOfIDs[j]);
			}

			// A is a parent of B, but B is not always a child of A
			for (int j = 0; j < this.aGOHasPartIDs.Count; j++)
			{
				if (this.oOntology.Terms.ContainsKey(this.aGOHasPartIDs[j]))
					this.aChildrenIDs.Add(this.aGOHasPartIDs[j]);
			}

			this.aKeywords.Clear();
			MakeKeywords();
		}

		internal void InitializeReferences()
		{
			if (!bReferencesInitialized)
			{
				BHashSet<int> allParentIDs = new BHashSet<int>();

				foreach (int parentID in this.aParentIDs)
				{
					allParentIDs.Add(parentID);
					GOTerm parent = this.oOntology.Terms.GetValueByKey(parentID);
					if (!parent.ReferencesInitialized)
						parent.InitializeReferences();

					BHashSet<int> parentIDs = parent.AllParentIDs;
					for (int i = 0; i < parentIDs.Count; i++)
					{
						allParentIDs.Add(parentIDs[i]);
					}
				}

				this.aAllParentIDs = allParentIDs;
				bReferencesInitialized = true;

				GOTerm curNode = this;
				while (!curNode.IsRootNode)
				{
					curNode = this.oOntology.Terms.GetValueByKey(curNode.AllParentIDs[0]);
				}
				this.iRootNodeID = curNode.ID;
			}
		}

		[XmlIgnore]
		internal bool ReferencesInitialized 
		{
			get { return bReferencesInitialized; }
			set { this.bReferencesInitialized = value; }
		}

		private void MakeKeywords()
		{
			if (this.aKeywords.Count > 0)
				return;

			StringBuilder sb = new StringBuilder();

			sb.Append(this.sName);
			foreach (string curAltName in this.aAltNames)
			{
				sb.Append(" ");
				sb.Append(curAltName);
			}
			sb.Append(" ");
			sb.Append(this.sDescription);

			// some compounds have ',' in their name
			string oneBigString = sb.ToString().ToLower().Replace(", ", " ");
			string[] aTokens = oneBigString.Split(new char[] { ':', ';', '=', '.', '\"', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries);

			for (int i = 0; i < aTokens.Length; i++)
			{
				string token = aTokens[i];

				// parenthesis removal
				if (token.StartsWith("(") && token.EndsWith(")"))
				{
					token = token.Substring(1, token.Length - 2);
				}
				// remove starting parenthesis only if there is no ending parenthesis
				if (token.StartsWith("(") && token.IndexOf(')') < 0)
				{
					token = token.Substring(1);
				}
				// remove ending parenthesis only if there is no starting parenthesis
				if (token.EndsWith(")") && token.IndexOf('(') < 0)
				{
					token = token.Substring(0, token.Length - 1);
				}

				// square brackets removal
				if (token.StartsWith("[") && token.EndsWith("]"))
				{
					token = token.Substring(1, token.Length - 2);
				}
				// remove starting square bracket only if there is no ending square bracket
				if (token.StartsWith("[") && token.IndexOf(']') < 0)
				{
					token = token.Substring(1);
				}
				// remove ending square bracket only if there is no starting square bracket
				if (token.EndsWith("]") && token.IndexOf('[') < 0)
				{
					token = token.Substring(0, token.Length - 1);
				}

				// we ignore tokens of 2 characters or less
				if (!string.IsNullOrEmpty(token) && token.Length > 2)
				{
					aKeywords.Add(token);
				}
			}
		}

		[XmlIgnore]
		public GeneOntology Ontology
		{
			get
			{
				return this.oOntology;
			}
		}

		[XmlIgnore]
		internal GeneOntology OntologyInternal
		{
			get { return this.oOntology; }
			set { this.oOntology = value; }
		}

		/// <summary>
		/// Returns the main unique ID of the GOTerm.
		/// </summary>
		public int ID
		{
			get { return this.iID; }
			set { this.iID = value; }
		}

		/// <summary>
		/// Returns GO Term ID in the format "GO:0006915".
		/// </summary>
		/// <returns></returns>
		[XmlIgnore]
		public string FormattedID
		{
			get { return string.Format("GO:{0:d7}", this.iID); }
		}

		public GONamespaceEnum Namespace
		{
			get { return this.eNamespace; }
			set { this.eNamespace = value; }
		}

		public string Description
		{
			get { return this.sDescription; }
			set { this.sDescription = value; }
		}

		public string Comment
		{
			get { return this.sComment; }
			set { this.sComment = value; }
		}

		/// <summary>
		/// Returns a list of alternate IDs for the GOTerm.
		/// </summary>
		public BHashSet<int> AltIDs
		{
			get { return this.aAltIDs; }
		}

		/// <summary>
		/// Gets or sets the name of the GOTerm.
		/// </summary>
		/// <param name="name"></param>
		public string Name
		{
			get { return this.sName; }
			set { this.sName = value; }
		}

		/// <summary>
		/// Returns a list of alternate names for the GOTerm.
		/// </summary>
		public List<string> AltNames
		{
			get { return this.aAltNames; }
		}

		public bool IsObsolete
		{
			get { return this.bObsolete; }
			set { this.bObsolete = value; }
		}

		/// <summary>
		/// Returns all the parent IDs of this GO Term as defined in the ontology
		/// </summary>
		public BHashSet<int> GOParentIDs
		{
			get { return this.aGOParentIDs; }
		}

		/// <summary>
		/// Returns all the conditional parent IDs of this GO Term as defined in the ontology
		/// (example: this GO Term is a child of another GO Term, but another GO Term is not a parent of this GO Term)
		/// </summary>
		public BHashSet<int> GOPartOfIDs
		{
			get { return this.aGOPartOfIDs; }
		}

		/// <summary>
		/// Returns all the conditional children IDs of this GO Term as defined in the ontology
		/// (example: this GO Term is a parent of another GO Term, but another GO Term is not a child of this GO Term)
		/// </summary>
		public BHashSet<int> GOHasPartIDs
		{
			get { return this.aGOHasPartIDs; }
		}

		/// <summary>
		/// Returns all terms that should be considered as a replacement for this term as defined in the ontology
		/// </summary>
		public BHashSet<int> GOConsiderIDs
		{
			get { return this.aGOConsiderIDs; }
		}

		/// <summary>
		/// Returns all terms that are offered as a replacement as defined in the ontology
		/// </summary>
		public BHashSet<int> GOReplacementIDs
		{
			get { return this.aGOReplacementIDs; }
		}

		/// <summary>
		/// Returns all the parent IDs of this GO Term
		/// </summary>
		public BHashSet<int> ParentIDs
		{
			get { return this.aParentIDs; }
		}

		/// <summary>
		/// Returns the children of this GOTerm object.
		/// </summary>
		public BHashSet<int> ChildrenIDs
		{
			get { return this.aChildrenIDs; }
		}

		public BHashSet<int> AllParentIDs
		{
			get { return this.aAllParentIDs; }
		}

		/// <summary>
		/// Returns true if the node is a top node (if it has no parents).
		/// </summary>
		[XmlIgnore]
		public bool IsRootNode
		{
			get { return this.aParentIDs.Count == 0; }
		}

		/// <summary>
		/// Finds the root node of the given node (i.e. the one that has no parents.)
		/// </summary>
		/// <returns></returns>
		public int RootNodeID
		{
			get { return this.iRootNodeID; }
			set { this.iRootNodeID = value; }
		}

		/// <summary>
		/// Provides a set of all words (in lowercase) used in any of the term names,
		/// or in the term's definition.
		/// </summary>
		public BHashSet<string> Keywords
		{
			get { return this.aKeywords; }
		}

		public bool IsChildOf(int parentID)
		{
			return this.aAllParentIDs.Contains(parentID);
		}

		/// <summary>
		/// Returns a new set with references to all of the parent GOTerms that this GoTerm has in common with another GOTerm
		/// </summary>
		/// <param name="anotherTerm"></param>
		/// <returns></returns>
		public BHashSet<int> GetAllCommonParents(GOTerm anotherTerm)
		{
			BHashSet<int> result = new BHashSet<int>();
			BHashSet<int> myParentIDs = this.AllParentIDs;
			BHashSet<int> anotherParentIDs = anotherTerm.AllParentIDs;

			foreach (int termID in anotherParentIDs)
			{
				if (myParentIDs.Contains(termID))
					result.Add(termID);
			}

			return result;
		}

		/// <summary>
		/// Creates a new Set containing references to all the children of any parent 
		/// of the given Node. When looking for children, looks only one level deep.
		/// The returned collection does not include the given node.
		/// </summary>
		/// <returns></returns>
		public BHashSet<int> GetSiblings()
		{
			BHashSet<int> siblings = new BHashSet<int>();

			foreach (int parentID in this.aParentIDs)
			{
				GOTerm parent = this.oOntology.Terms.GetValueByKey(parentID);

				foreach (int child in parent.ChildrenIDs)
				{
					siblings.Add(child);
				}
			}
			siblings.Remove(this.iID);

			return siblings;
		}

		/// <summary>
		/// Two nodes are equal if they have equal unique IDs
		/// </summary>
		/// <param name="obj">A GOTerm to compare to</param>
		/// <returns>True if two nodes have the same unique ID</returns>
		public override bool Equals(object obj)
		{
			if (obj == null || (!obj.GetType().Equals(typeof(GOTerm)) && !obj.GetType().IsSubclassOf(typeof(GOTerm))))
			{
				return false;
			}
			GOTerm other = (GOTerm)obj;
			if (this.iID != other.iID)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Hash code is the unique ID of a node
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return this.iID;
		}

		public override string ToString()
		{
			return string.Format("{0}:{1}", this.iID, this.sName);
		}

		#region IComparable<GOTerm> Members

		public int CompareTo(GOTerm other)
		{
			return this.iID.CompareTo(other.iID);
		}

		#endregion

		#region IComparer<GOTerm> Members

		public int Compare(GOTerm x, GOTerm y)
		{
			return x.ID.CompareTo(y.ID);
		}

		#endregion

		#region IEqualityComparer<GOTerm> Members

		public bool Equals(GOTerm x, GOTerm y)
		{
			return x.ID == y.ID;
		}

		public int GetHashCode(GOTerm obj)
		{
			return obj.GetHashCode();
		}

		#endregion
	}
}
