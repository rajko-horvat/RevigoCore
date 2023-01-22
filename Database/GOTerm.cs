using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using IRB.Collections.Generic;

namespace IRB.Database
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
	public class GOTerm : IComparable<GOTerm>, IComparer<GOTerm>, IEqualityComparer<GOTerm>
	{
		// The GeneOntology object this term belongs to.
		private GeneOntology oOntology = null;
		private int iID = -1; // The main unique ID for the node.
		private BHashSet<int> aAltIDs = new BHashSet<int>(); // List of alternate IDs for the node.
		private string sName = null; // The main name of the node.
		private List<string> aAltNames = new List<string>(); // List of alternate names for the node
		private string sDescription = null;
		private string sComment = null;
		private bool bObsolete = false;
		// For obsolete terms, here is a suitable replacement. (Note: just one 
		// replacement term is stored here while the OBO-XML file may offer more than one.
		private int iReplaceByID = -1;
		private GoNamespaceEnum eNamespace; // The namespace this term belongs to.
		private BHashSet<string> aKeywords = null;

		private BHashSet<int> aParentIDs = new BHashSet<int>(); // A list of all parent IDs of the node.
		private BHashSet<int> aPartOfIDs = new BHashSet<int>(); // A list of all conditional parent IDs of the node.
		private BHashSet<int> aHasPartIDs = new BHashSet<int>(); // A list of all conditional children IDs of the node.
		private BHashSet<int> aConsiderIDs = new BHashSet<int>(); // A list of all terms that should be considered a replacement for this term.
		private BHashSet<GOTerm> aParents = new BHashSet<GOTerm>(); // A list of all parents of the node.
		private BHashSet<GOTerm> aChildren = new BHashSet<GOTerm>(); // A list of all children of the node.

		private BHashSet<int> oAllParentsCached = null; // This speeds up all parents lookups
		private GOTerm oTopmostCached = null; // This speeds up topmost parent lookups

		/// <summary>
		/// A unique ID (integer) must be specified for the GOTerm at the time of construction.
		/// </summary>
		/// <param name="id">The main unique ID for the GOTerm.</param>
		public GOTerm(GeneOntology geneOntology, int id)
		{
			this.oOntology = geneOntology;
			this.iID = id;
		}

		[XmlIgnore]
		public GeneOntology Ontology
		{
			get
			{
				return this.oOntology;
			}
		}

		/// <summary>
		/// Returns the main unique ID of the GOTerm.
		/// </summary>
		public int ID
		{
			get
			{
				return this.iID;
			}
			set
			{
				this.iID = value;
			}
		}

		/// <summary>
		/// Returns GO Term ID in the format "GO:0006915".
		/// </summary>
		/// <returns></returns>
		[XmlIgnore]
		public string FormattedID
		{
			get
			{
				return string.Format("GO:{0:d7}", this.iID);
			}
		}

		public GoNamespaceEnum Namespace
		{
			get
			{
				return this.eNamespace;
			}
			set
			{
				this.eNamespace = value;
			}
		}

		public string Description
		{
			get
			{
				return this.sDescription;
			}
			set
			{
				this.sDescription = value;
			}
		}

		public string Comment
		{
			get
			{
				return this.sComment;
			}
			set
			{
				this.sComment = value;
			}
		}

		/// <summary>
		/// Returns a list of alternate IDs for the GOTerm.
		/// </summary>
		public BHashSet<int> AltIDs
		{
			get
			{
				return this.aAltIDs;
			}
		}

		/// <summary>
		/// Gets or sets the name of the GOTerm.
		/// </summary>
		/// <param name="name"></param>
		public string Name
		{
			get
			{
				return this.sName;
			}
			set
			{
				this.sName = value;
			}
		}

		/// <summary>
		/// Gives back the name of the GOTerm, but without any illegal characters,
		/// commas and spaces replaced with "_", and double quotes with single quotes.
		/// </summary>
		[XmlIgnore]
		public string SafeName
		{
			get
			{
				string result = this.sName;

				if (result != null)
				{
					result = result.Replace('\\', '_');
					result = result.Replace('/', '_');
					result = result.Replace('*', '_');
					result = result.Replace('?', '_');
					result = result.Replace('"', '\'');
					result = result.Replace('<', '_');
					result = result.Replace('>', '_');
					result = result.Replace('|', '_');
					result = result.Replace(':', '_');
					result = result.Replace(' ', '_');
					result = result.Replace(',', '_');
				}

				return result;
			}
		}

		/// <summary>
		/// Returns a list of alternate names for the GOTerm.
		/// </summary>
		public List<string> AltNames
		{
			get
			{
				return this.aAltNames;
			}
		}

		public bool IsObsolete
		{
			get
			{
				return this.bObsolete;
			}
			set
			{
				this.bObsolete = value;
			}
		}

		public int ReplacedByID
		{
			get
			{
				return this.iReplaceByID;
			}
			set
			{
				this.iReplaceByID = value;
			}
		}

		/// <summary>
		/// Provides a set of all words (in lowercase) used in any of the term names,
		/// or in the term's definition.
		/// </summary>
		[XmlIgnore]
		public BHashSet<string> Keywords
		{
			get
			{
				if (this.aKeywords == null)
					MakeKeywords();

				return this.aKeywords;
			}
		}

		private void MakeKeywords()
		{
			if (this.aKeywords != null)
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

			BHashSet<string> oKeywords = new BHashSet<string>();

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
					oKeywords.Add(token);
				}
			}

			this.aKeywords = oKeywords;
		}

		/// <summary>
		/// Returns all the parent IDs of this GO Term
		/// </summary>
		public BHashSet<int> ParentIDs
		{
			get
			{
				return this.aParentIDs;
			}
		}

		/// <summary>
		/// Returns all the conditional parent IDs of this GO Term 
		/// (example: this GO Term is a child of another GO Term, but another GO Term is not a parent of this GO Term)
		/// </summary>
		public BHashSet<int> PartOfIDs
		{
			get
			{
				return this.aPartOfIDs;
			}
		}

		/// <summary>
		/// Returns all the conditional children IDs of this GO Term 
		/// (example: this GO Term is a parent of another GO Term, but another GO Term is not a child of this GO Term)
		/// </summary>
		public BHashSet<int> HasPartIDs
		{
			get
			{
				return this.aHasPartIDs;
			}
		}

		/// <summary>
		/// Returns all terms that should be considered as a replacement for this term 
		/// </summary>
		public BHashSet<int> ConsiderIDs
		{
			get
			{
				return this.aConsiderIDs;
			}
		}

		/// <summary>
		/// Returns the parents of this GOTerm object.
		/// </summary>
		[XmlIgnore]
		public BHashSet<GOTerm> Parents
		{
			get
			{
				return this.aParents;
			}
		}

		/// <summary>
		/// Returns the children of this GOTerm object.
		/// </summary>
		[XmlIgnore]
		public BHashSet<GOTerm> Children
		{
			get
			{
				return this.aChildren;
			}
		}

		/// <summary>
		/// Get a list of all parents for this GOTerm,
		/// all of their parents and so on, searching recursively through the ontology.
		/// </summary>
		/// <returns></returns>
		public BHashSet<int> AllParents
		{
			get
			{
				if (this.oAllParentsCached == null)
				{
					BHashSet<int> parents = new BHashSet<int>();

					foreach (GOTerm curNode in this.aParents)
					{
						parents.Add(curNode.ID);
						BHashSet<int> parentIDs = curNode.AllParents;
						for (int i = 0; i < parentIDs.Count; i++)
						{
							parents.Add(parentIDs[i]);
						}
					}
					this.oAllParentsCached = parents;
				}

				return this.oAllParentsCached;
			}
		}

		/// <summary>
		/// Returns true if the node is a topmost node (if it has no parents).
		/// </summary>
		[XmlIgnore]
		public bool IsTopmost
		{
			get
			{
				return this.aParents.Count == 0;
			}
		}

		/// <summary>
		/// Finds the topmost parent of the given node (i.e. the one that has no parents.)
		/// </summary>
		/// <returns></returns>
		public GOTerm TopmostParent
		{
			get
			{
				if (this.oTopmostCached == null)
				{
					GOTerm curNode = this;
					while (!curNode.IsTopmost)
					{
						curNode = curNode.aParents[0];
					}
					this.oTopmostCached = curNode;
				}

				return this.oTopmostCached;
			}
		}

		public void ResetAllParents()
		{
			this.oAllParentsCached = null;
			ResetAllParentsHelper();
		}

		/// <summary>
		/// A helper function for AllParents recursive search.
		/// </summary>
		private void ResetAllParentsHelper()
		{
			foreach (GOTerm curNode in this.aChildren)
			{
				curNode.ResetAllParentsHelper();
			}
		}

		public bool IsChildOf(int parentID)
		{
			return IsChildOfHelper(parentID);
		}

		private bool IsChildOfHelper(int parentID)
		{
			for (int i = 0; i < this.aParents.Count; i++)
			{
				GOTerm parent = this.aParents[i];
				if (parent.ID == parentID || parent.IsChildOfHelper(parentID))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns a new set with references to all of the parent GOTerms that this GoTerm has in common with another GOTerm
		/// </summary>
		/// <param name="anotherTerm"></param>
		/// <returns></returns>
		public BHashSet<int> GetAllCommonParents(GOTerm anotherTerm)
		{
			BHashSet<int> result = new BHashSet<int>();
			BHashSet<int> myParentIDs = this.AllParents;
			BHashSet<int> anotherParentIDs = anotherTerm.AllParents;

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
		public BHashSet<GOTerm> GetSiblings()
		{
			BHashSet<GOTerm> mySibs = new BHashSet<GOTerm>();

			foreach (GOTerm n in this.Parents)
			{
				foreach (GOTerm child in n.Children)
				{
					mySibs.Add(child);
				}
			}
			mySibs.Remove(this);

			return mySibs;
		}

		public BDictionary<GOTerm, int> GetAllTopNodesWithDepth()
		{
			BDictionary<GOTerm, int> result = new BDictionary<GOTerm, int>();
			GetAllTopNodesWithDepthHelper(result, 0);

			return result;
		}

		private void GetAllTopNodesWithDepthHelper(BDictionary<GOTerm, int> nodes, int depth)
		{
			if (this.IsTopmost)
			{
				if (nodes.ContainsKey(this))
				{
					if (depth < nodes.GetValueByKey(this))
					{
						nodes.SetValueByKey(this, depth);
					}
				}
				else
				{
					nodes.Add(this, depth);
				}

				return;
			}

			foreach (GOTerm curNode in this.aParents)
			{
				curNode.GetAllTopNodesWithDepthHelper(nodes, depth + 1);
			}
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
