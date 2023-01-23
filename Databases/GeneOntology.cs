using System;
using System.Text;
using System.IO.Compression;
using System.IO;
using IRB.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Collections.Generic;
using System.Globalization;

namespace IRB.Revigo.Databases
{
	/// <summary>
	/// A class representing the Gene Ontology.
	/// 
	/// Authors:
	/// 	Fran Supek (fsupek at irb.hr)
	/// 	Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License:
	/// 	MIT
	/// 	Copyright (c) 2021, Ruđer Bošković Institute
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
	public class GeneOntology : BDictionary<int, GOTerm>
	{
		// As given in the header of the OBO-XML file.
		private DateTime dtDate = DateTime.MinValue;
		private string sLink = null;

		public GeneOntology()
		{ }

		/// <summary>
		/// Reads in the structure of the whole Gene Ontology from a given file (NOT a serialized file).
		/// 
		/// The file is downloadable from http://purl.obolibrary.org/obo/go.obo
		/// Accepts also the old style GeneOntology file with .obo-xml extension
		/// </summary>
		/// <param name="stream"></param>
		public GeneOntology(string goPath)
		{
			StreamReader linkReader = null;
			string sLinkPath = string.Format("{0}{1}link.txt", Path.GetDirectoryName(goPath), Path.DirectorySeparatorChar);
			if (File.Exists(sLinkPath))
			{
				try
				{
					linkReader = new StreamReader(sLinkPath);
					string sLine = null;

					while (string.IsNullOrEmpty(sLine) && !linkReader.EndOfStream)
					{
						sLine = linkReader.ReadLine();
					}

					this.sLink = sLine;
					linkReader.Close();
				}
				catch { }
			}

			StreamReader goReader = null;
			string sGOFileName = Path.GetFileName(goPath);

			if (Path.GetExtension(sGOFileName).Equals(".gz", StringComparison.CurrentCultureIgnoreCase))
			{
				goReader = new StreamReader(new GZipStream(new BufferedStream(new FileStream(goPath, FileMode.Open, FileAccess.Read, FileShare.Read), 65536), CompressionMode.Decompress));
				sGOFileName = Path.GetFileNameWithoutExtension(sGOFileName);
			}
			else
			{
				goReader = new StreamReader(new BufferedStream(new FileStream(goPath, FileMode.Open, FileAccess.Read, FileShare.Read), 65536));
			}

			switch (Path.GetExtension(sGOFileName).ToLower())
			{ 
				case ".obo":
					ParseObo(goReader);
					break;
				case ".obo-xml":
					ParseOboXml(goReader);
					break;
				default:
					throw new Exception("Unknown GeneOntology file extension");
			}

			goReader.Close();

			// now, kill obsolete terms
			for (int i = 0; i < this.Count; i++)
			{
				GOTerm curTerm = this[i].Value;
				if (curTerm.IsObsolete && curTerm.ReplacedByID > 0)
				{
					curTerm.ParentIDs.Clear();

					GOTerm replTerm = this.GetValueByKey(curTerm.ReplacedByID);
					replTerm.AltIDs.Add(curTerm.ID);
					replTerm.AltIDs.AddRange((IEnumerable<int>)curTerm.AltIDs);

					// adjust references to replacement term
					this.Add(curTerm.ID, replTerm);
					for (int j = 0; j < curTerm.AltIDs.Count; j++)
					{
						this.Add(curTerm.AltIDs[j], replTerm);
					}
				}
				else
				{
					for (int j = 0; j < curTerm.AltIDs.Count; j++)
					{
						this.Add(curTerm.AltIDs[j], curTerm);
					}
				}
			}

			// Assign parents and children to all GOTerms
			for (int i = 0; i < this.Count; i++)
			{
				GOTerm curTerm = this[i].Value;

				// A is a child of B, and B is a parent of A
				for (int j = 0; j < curTerm.ParentIDs.Count; j++)
				{
					GOTerm parent = this.GetValueByKey(curTerm.ParentIDs[j]);

					curTerm.Parents.Add(parent);
					parent.Children.Add(curTerm);
				}

				// A is a child of B, but B is not always a parent of A
				for (int j = 0; j < curTerm.PartOfIDs.Count; j++)
				{
					GOTerm parent = this.GetValueByKey(curTerm.PartOfIDs[j]);

					curTerm.Parents.Add(parent);
				}

				// A is a parent of B, but B is not always a child of A
				for (int j = 0; j < curTerm.HasPartIDs.Count; j++)
				{
					GOTerm child = this.GetValueByKey(curTerm.HasPartIDs[j]);

					curTerm.Children.Add(child);
				}
			}

			// Cache Keywords, AllParents, TopmostParent properties
			for (int i = 0; i < this.Count; i++)
			{
				BHashSet<string> keyw = this[i].Value.Keywords;
				//BHashSet<int> parents = this[i].Value.AllParents;
				GOTerm top = this[i].Value.TopmostParent;
			}

			this.addKeywordsFromUniprotKeywords(string.Format("{0}{1}keywlist.txt",
				Path.GetDirectoryName(goPath), Path.DirectorySeparatorChar));
		}

		private void ParseObo(StreamReader reader)
		{
			Regex rxSectionTag = new Regex(@"^\s*\[\s*([\w\-]+)\s*\]\s*$", RegexOptions.Compiled);
			Regex rxItemTag = new Regex(@"^\s*([\w\-]+)\s*\:\s*(.*?)\s*$", RegexOptions.Compiled);
			Regex rxValueString = new Regex("^\"((?:[^\"]|\\.)+)\"\\s*(.*?)\\s*$", RegexOptions.Compiled);
			GOTerm oCurrentTerm = null;
			bool bInTerm = false;
			bool bInHeader = true;

			while (!reader.EndOfStream)
			{
				string sLine = reader.ReadLine();

				if (sLine != null)
					sLine = sLine.Trim();

				if (string.IsNullOrEmpty(sLine) || sLine.StartsWith("!"))
				{
					continue;
				}

				Match mResult = rxItemTag.Match(sLine);
				if (mResult.Success && bInTerm)
				{
					string sItem = mResult.Groups[1].Value.ToLower();
					string sItemValue = mResult.Groups[2].Value.Trim();

					// Item Value can have string value and/or parameters and/or comment
					string sItemParameter = null;

					if (sItemValue.StartsWith("\""))
					{
						mResult = rxValueString.Match(sItemValue);
						if (mResult.Success)
						{
							sItemValue = mResult.Groups[1].Value;
							string[] aParameters = mResult.Groups[2].Value.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
							if (aParameters.Length > 0 && !aParameters[0].StartsWith("!") && !aParameters[0].StartsWith("["))
							{
								sItemParameter = aParameters[0];
							}
						}
						else
						{
							sItemValue = sItemValue.Substring(1);
						}
					}
					else if (sItemValue.IndexOf('!') >= 0)
					{
						sItemValue = sItemValue.Substring(0, sItemValue.IndexOf('!')).Trim();
					}

					if (!sItem.Equals("id") && oCurrentTerm == null)
					{
						throw new ArgumentException("The term has undefined ID, Name or Namespace in OBO file.");
					}

					string[] asTemp;

					switch (sItem)
					{
						case "id":
							oCurrentTerm = new GOTerm(this, ParseGOID(sItemValue));
							break;
						case "name":
							if (oCurrentTerm.Name == null)
							{
								oCurrentTerm.Name = HttpUtility.HtmlDecode(sItemValue);
							}
							break;
						case "namespace":
							if (oCurrentTerm.Namespace == GONamespaceEnum.None)
							{
								oCurrentTerm.Namespace = (GONamespaceEnum)Enum.Parse(typeof(GONamespaceEnum),
									sItemValue, true);
							}
							break;
						case "def":
							if (oCurrentTerm.Description == null)
							{
								oCurrentTerm.Description = HttpUtility.HtmlDecode(sItemValue);
							}
							break;
						case "comment":
							if (oCurrentTerm.Comment == null)
							{
								oCurrentTerm.Comment = HttpUtility.HtmlDecode(sItemValue);
							}
							break;
						case "alt_id":
							oCurrentTerm.AltIDs.Add(ParseGOID(sItemValue));
							break;
						case "synonym":
							// EXACT, RELATED, NARROW...
							oCurrentTerm.AltNames.Add(HttpUtility.HtmlDecode(sItemValue));
							break;
						case "is_obsolete":
							if (sItemValue.Equals("true") && !oCurrentTerm.IsObsolete)
							{
								oCurrentTerm.IsObsolete = true;
								if (oCurrentTerm.Name.StartsWith("obsolete "))
								{
									oCurrentTerm.Name = "(obsolete) " + oCurrentTerm.Name.Substring(9);
								}
								else
								{
									oCurrentTerm.Name = "(obsolete) " + oCurrentTerm.Name;
								}
							}
							break;
						case "consider":
							if (sItemValue.StartsWith("go:", StringComparison.CurrentCultureIgnoreCase))
							{
								oCurrentTerm.ConsiderIDs.Add(ParseGOID(sItemValue));
							}
							break;
						case "replaced_by":
							oCurrentTerm.ReplacedByID = ParseGOID(sItemValue);
							break;
						case "is_a":
						case "to":
							oCurrentTerm.ParentIDs.Add(ParseGOID(sItemValue));
							break;
						case "relationship":
							// we currently care only for part_of relation
							asTemp = sItemValue.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
							if (asTemp.Length < 2)
							{
								throw new Exception("Invalid relationship");
							}
							switch (asTemp[0].ToLower())
							{
								case "part_of":
									oCurrentTerm.PartOfIDs.Add(ParseGOID(asTemp[1]));
									break;
								case "has_part":
									oCurrentTerm.HasPartIDs.Add(ParseGOID(asTemp[1]));
									break;
								case "regulates":
								case "negatively_regulates":
								case "positively_regulates":
								case "occurs_in":
								case "ends_during":
								case "happens_during":
									break;
								default:
									//throw new Exception(string.Format("Unknown relationship operator: {0}", asTemp[0]));
									break;
							}
							break;
						case "subset":
						case "xref":
						case "disjoint_from":
						case "created_by":
						case "creation_date":
						case "intersection_of":
						case "property_value":
							break;
						default:
							//throw new Exception("Unknown tag");
							break;
					}
					continue;
				}
				else if (mResult.Success && bInHeader)
				{
					string sItem = mResult.Groups[1].Value.ToLower();
					string sItemValue = mResult.Groups[2].Value;

					if (sItem.Equals("data-version"))
					{
						if (sItemValue.StartsWith("releases/", StringComparison.CurrentCultureIgnoreCase))
							sItemValue = sItemValue.Substring(9);
						DateTime.TryParseExact(sItemValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
							DateTimeStyles.NoCurrentDateDefault, out this.dtDate);
					}
					continue;
				}

				mResult = rxSectionTag.Match(sLine);
				if (mResult.Success)
				{
					string sSection = mResult.Groups[1].Value.ToLower();

					switch (sSection)
					{
						case "header":
							bInHeader = true;
							bInTerm = false;
							break;

						case "term":
							bInHeader = false;
							bInTerm = true;
							if (oCurrentTerm != null)
							{
								this.Add(oCurrentTerm.ID, oCurrentTerm);
								oCurrentTerm = null;
							}
							break;

						default:
							bInHeader = false;
							bInTerm = false;
							break;
					}
					continue;
				}
			}
		}

		private void ParseOboXml(StreamReader reader)
		{
			Regex rxOpenHTMLTag = new Regex(@"^\s*\<\s*([\w\-]+)(?:(?:\s*\>)|(?:\s+[^\>]+\>))\s*$", RegexOptions.Compiled);
			Regex rxClosedHTMLTag = new Regex(@"^\s*\<\s*/([\w\-]+)\s*\>\s*$", RegexOptions.Compiled);
			Regex rxHTMLTag = new Regex(@"^\s*\<\s*([\w\-]+)(?:(?:\s*\>)|(?:\s+[^\>]+\>))\s*([^\<]*)\s*<\s*/([\w\-]+)\s*\>\s*$", RegexOptions.Compiled);
			Stack<string> oOpenTags = new Stack<string>();
			GOTerm oCurrentTerm = null;
			bool bInTerm = false;
			bool bInHeader = false;

			while (!reader.EndOfStream)
			{
				string line = reader.ReadLine();

				if (!string.IsNullOrEmpty(line))
				{
					Match mResult = rxHTMLTag.Match(line);
					if (mResult.Success && bInTerm)
					{
						string sOpeningTag = mResult.Groups[1].Value.ToLower();
						string sTagValue = mResult.Groups[2].Value;
						string sClosingTag = mResult.Groups[3].Value.ToLower();

						if (sOpeningTag.Equals(sClosingTag))
						{
							if (!sOpeningTag.Equals("id") && oCurrentTerm == null)
							{
								throw new ArgumentException("The term has undefined ID, Name or Namespace in OBO-XML file.");
							}

							switch (sOpeningTag)
							{
								case "id":
									oCurrentTerm = new GOTerm(this, ParseGOID(sTagValue));
									break;
								case "name":
									if (oCurrentTerm.Name == null)
									{
										oCurrentTerm.Name = HttpUtility.HtmlDecode(sTagValue);
									}
									break;
								case "namespace":
									if (oCurrentTerm.Namespace == GONamespaceEnum.None)
									{
										oCurrentTerm.Namespace = (GONamespaceEnum)Enum.Parse(typeof(GONamespaceEnum),
											sTagValue, true);
									}
									break;
								case "defstr":
									if (oCurrentTerm.Description == null)
									{
										oCurrentTerm.Description = HttpUtility.HtmlDecode(sTagValue);
									}
									break;
								case "alt_id":
									oCurrentTerm.AltIDs.Add(ParseGOID(sTagValue));
									break;
								case "synonym_text":
									oCurrentTerm.AltNames.Add(HttpUtility.HtmlDecode(sTagValue));
									break;
								case "is_obsolete":
									if (sTagValue.Equals("1") && !oCurrentTerm.IsObsolete)
									{
										oCurrentTerm.IsObsolete = true;
										if (oCurrentTerm.Name.StartsWith("obsolete "))
										{
											oCurrentTerm.Name = "(obsolete) " + oCurrentTerm.Name.Substring(9);
										}
										else
										{
											oCurrentTerm.Name = "(obsolete) " + oCurrentTerm.Name;
										}
									}
									break;
								case "replaced_by":
									if (oCurrentTerm.ReplacedByID < 0)
									{
										oCurrentTerm.ReplacedByID = ParseGOID(sTagValue);
									}
									break;
								case "is_a":
								case "to":
									oCurrentTerm.ParentIDs.Add(ParseGOID(sTagValue));
									break;
							}
						}
						continue;
					}
					else if (mResult.Success && bInHeader)
					{
						string sOpeningTag = mResult.Groups[1].Value.ToLower();
						string sTagValue = mResult.Groups[2].Value;
						string sClosingTag = mResult.Groups[3].Value.ToLower();

						if (sOpeningTag.Equals(sClosingTag))
						{
							if (sOpeningTag.Equals("data-version"))
							{
								if (sTagValue.StartsWith("releases/", StringComparison.CurrentCultureIgnoreCase))
									sTagValue = sTagValue.Substring(9);
								DateTime.TryParseExact(sTagValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
									DateTimeStyles.NoCurrentDateDefault, out this.dtDate);
							}
						}
						continue;
					}

					mResult = rxOpenHTMLTag.Match(line);
					if (mResult.Success)
					{
						string sOpeningTag = mResult.Groups[1].Value.ToLower();

						oOpenTags.Push(sOpeningTag);

						switch (sOpeningTag)
						{
							case "header":
								if (bInHeader)
									throw new ArgumentException("The OBO-XML file has header in header.");
								bInHeader = true;
								break;
							case "term":
								if (bInTerm)
									throw new ArgumentException("The OBO-XML file has term in term.");
								bInTerm = true;
								break;
						}
						continue;
					}

					mResult = rxClosedHTMLTag.Match(line);
					if (mResult.Success)
					{
						string sClosingTag = mResult.Groups[1].Value.ToLower();

						string sTemp = oOpenTags.Pop();
						if (!sClosingTag.Equals(sTemp))
						{
							throw new ArgumentException("The closing tag doesn't match opening tag in OBO-XML file.");
						}

						switch (sClosingTag)
						{
							case "header":
								if (!bInHeader)
									throw new ArgumentException("The OBO-XML has mismatched header tags.");
								bInHeader = false;
								break;
							case "term":
								if (!bInTerm)
									throw new ArgumentException("The OBO-XML has mismatched term tags.");
								bInTerm = false;
								if (oCurrentTerm == null || oCurrentTerm.Name == null || oCurrentTerm.Namespace == GONamespaceEnum.None)
									throw new ArgumentException("The term has undefined ID, Name or Namespace in OBO-XML file.");
								this.Add(oCurrentTerm.ID, oCurrentTerm);
								oCurrentTerm = null;
								break;
						}
						continue;
					}
				}
			}
		}

		/// <summary>
		/// Parses the Uniprot keywords file
		/// </summary>
		private void addKeywordsFromUniprotKeywords(string fileName)
		{
			StreamReader file = new StreamReader(fileName);
			StringBuilder curText = new StringBuilder();

			while (!file.EndOfStream)
			{
				string line = file.ReadLine();

				if (line.StartsWith("DE"))
				{
					curText.Append(line.Substring(4));
					if (!line.EndsWith("-") && !line.EndsWith(" "))
						curText.Append(" ");
					continue;
				}

				if (line.StartsWith("ID"))
				{
					curText.Append(line.Substring(4));
					continue;
				}

				if (line.StartsWith("SY"))
				{
					curText.Append(line.Substring(4));
					continue;
				}

				if (line.StartsWith("//"))
				{
					curText = new StringBuilder();
					continue;
				}

				if (line.StartsWith("GO"))
				{
					int termId = Convert.ToInt32(line.Substring(8, 7));

					BHashSet<string> newKeywords = new BHashSet<string>();

					// old delimiters: ',', ':', '=', ';', '.', ' '
					// some compounds have ',' in their name
					string oneBigString = HttpUtility.HtmlDecode(curText.ToString()).ToLower().Replace(", ", " ");
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

						// the word "protein" is very overrepresented in Uniprot definitions
						// we ignore tokens of 2 characters or less
						if (!string.IsNullOrEmpty(token) && token.Length > 2 && !token.Equals("protein", StringComparison.CurrentCultureIgnoreCase))
						{
							newKeywords.Add(token);
						}
					}

					foreach (string keyword in newKeywords)
					{
						this.GetValueByKey(termId).Keywords.Add(keyword);
					}

					curText = new StringBuilder();
					continue;
				}
			}

			file.Close();
		}

		private int ParseGOID(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				throw new Exception("Parameter 'value' cannot be null or empty");
			}
			if (value.StartsWith("go:", StringComparison.CurrentCultureIgnoreCase))
			{
				value = value.Substring(3);
			}

			return Convert.ToInt32(value);
		}

		/// <summary>
		/// Gets or sets the link from which the Gene Ontology database has been downloaded.
		/// </summary>
		public string Link
		{
			get
			{
				return this.sLink;
			}
			set
			{
				this.sLink = value;
			}
		}

		/// <summary>
		/// Gets or sets the date of Gene Ontology database
		/// </summary>
		public DateTime Date
		{
			get
			{
				return this.dtDate;
			}
			set
			{
				this.dtDate = value;
			}
		}

		public static string NamespaceToFriendlyString(GONamespaceEnum name)
		{
			string friendlyName = name.ToString().Replace('_', ' ').Trim().ToLower();
			int iPos = 0;
			friendlyName = friendlyName.Substring(0, 1).ToUpper() + friendlyName.Substring(1);

			while (iPos < friendlyName.Length && (iPos = friendlyName.IndexOf(' ', iPos + 1)) >= 0)
			{
				friendlyName = friendlyName.Substring(0, iPos + 1) + friendlyName.Substring(iPos + 1, 1).ToUpper() + friendlyName.Substring(iPos + 2);
			}

			return friendlyName;
		}
	}
}
