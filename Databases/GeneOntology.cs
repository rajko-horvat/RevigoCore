using System;
using System.Text;
using System.IO.Compression;
using System.IO;
using IRB.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

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
	/// 	Copyright (c) 2011-2023, Ruđer Bošković Institute
	///		
	/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
	/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
	/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
	/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	/// 	subject to the following conditions: 
	/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
	/// 	
	/// 	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	/// 	INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
	/// 	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	/// 	IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
	/// 	DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
	/// 	ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
	/// </summary>
	[Serializable]
	public class GeneOntology
	{
		// As given in the header of the OBO-XML file.
		private BDictionary<int, GOTerm> aTerms = new BDictionary<int, GOTerm>();
		private DateTime dtDate = DateTime.MinValue;
		private string? sLink = null;

		public GeneOntology()
		{ }

		/// <summary>
		/// Reads in the structure of the whole Gene Ontology from a given file (NOT a serialized file).
		/// 
		/// The file is downloadable from http://purl.obolibrary.org/obo/go.obo
		/// Accepts also the old style GeneOntology file with .obo-xml extension
		/// </summary>
		/// <param name="stream"></param>
		public GeneOntology(string path)
		{
			string sLinkPath = string.Format("{0}.{1}link.txt", Path.GetDirectoryName(path), Path.DirectorySeparatorChar);
			if (File.Exists(sLinkPath))
			{
				try
				{
					StreamReader linkReader = new StreamReader(sLinkPath);
					string? sLine = null;

					while (string.IsNullOrEmpty(sLine) && !linkReader.EndOfStream)
					{
						sLine = linkReader.ReadLine();
					}

					this.sLink = sLine;
					linkReader.Close();
				}
				catch { }
			}

			StreamReader goReader;
			string sGOFileName = Path.GetFileName(path);

			if (Path.GetExtension(sGOFileName).Equals(".gz", StringComparison.CurrentCultureIgnoreCase))
			{
				goReader = new StreamReader(new GZipStream(new BufferedStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), 65536), CompressionMode.Decompress));
				sGOFileName = Path.GetFileNameWithoutExtension(sGOFileName);
			}
			else
			{
				goReader = new StreamReader(new BufferedStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), 65536));
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
			for (int i = 0; i < this.aTerms.Count; i++)
			{
				GOTerm curTerm = this.aTerms[i].Value;
				if (curTerm.IsObsolete && curTerm.GOReplacementIDs.Count > 0)
				{
					// use first replacement term
					GOTerm replTerm = this.aTerms.GetValueByKey(curTerm.GOReplacementIDs[0]);
					replTerm.AltIDs.Add(curTerm.ID);
					replTerm.AltIDs.AddRange((IEnumerable<int>)curTerm.AltIDs);

					// adjust references to replacement term
					this.aTerms.Add(curTerm.ID, replTerm);
					for (int j = 0; j < curTerm.AltIDs.Count; j++)
					{
						this.aTerms.Add(curTerm.AltIDs[j], replTerm);
					}
				}
				else
				{
					for (int j = 0; j < curTerm.AltIDs.Count; j++)
					{
						this.aTerms.Add(curTerm.AltIDs[j], curTerm);
					}
				}
			}

			InitializeGO(path);
		}

		private void InitializeGO(string path)
		{
			// Assign parents and children to all GOTerms
			for (int i = 0; i < this.aTerms.Count; i++)
			{
				this.aTerms[i].Value.InitializeTerm();
			}

			this.AddKeywordsFromUniprotKeywords(string.Format("{0}.{1}keywlist.txt", Path.GetDirectoryName(path), Path.DirectorySeparatorChar));

			// Cache AllParents, RootNode properties
			for (int i = 0; i < this.aTerms.Count; i++)
			{
				this.aTerms[i].Value.InitializeReferences();
			}
		}

		private void ParseObo(StreamReader reader)
		{
			Regex rxSectionTag = new Regex(@"^\s*\[\s*([\w\-]+)\s*\]\s*$", RegexOptions.Compiled);
			Regex rxItemTag = new Regex(@"^\s*([\w\-]+)\s*\:\s*(.*?)\s*$", RegexOptions.Compiled);
			Regex rxValueString = new Regex("^\"((?:[^\"]|\\.)+)\"\\s*(.*?)\\s*$", RegexOptions.Compiled);
			GOTerm? oCurrentTerm = null;
			bool bInTerm = false;
			bool bInHeader = true;

			while (!reader.EndOfStream)
			{
				string? sLine = reader.ReadLine();

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
					if (sItemValue.StartsWith("\""))
					{
						mResult = rxValueString.Match(sItemValue);
						if (mResult.Success)
						{
							sItemValue = mResult.Groups[1].Value;
							string[] aParameters = mResult.Groups[2].Value.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
							if (aParameters.Length > 0 && !aParameters[0].StartsWith("!") && !aParameters[0].StartsWith("["))
							{
								sItemValue = aParameters[0];
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
							if (oCurrentTerm != null && oCurrentTerm.Name == null)
							{
								oCurrentTerm.Name = HttpUtility.HtmlDecode(sItemValue);
							}
							break;
						case "namespace":
							if (oCurrentTerm != null && oCurrentTerm.Namespace == GONamespaceEnum.None)
							{
								oCurrentTerm.Namespace = (GONamespaceEnum)Enum.Parse(typeof(GONamespaceEnum),
									sItemValue, true);
							}
							break;
						case "def":
							if (oCurrentTerm != null && oCurrentTerm.Description == null)
							{
								oCurrentTerm.Description = HttpUtility.HtmlDecode(sItemValue);
							}
							break;
						case "comment":
							if (oCurrentTerm != null && oCurrentTerm.Comment == null)
							{
								oCurrentTerm.Comment = HttpUtility.HtmlDecode(sItemValue);
							}
							break;
						case "alt_id":
							if (oCurrentTerm != null)
							{
								oCurrentTerm.AltIDs.Add(ParseGOID(sItemValue));
							}
							break;
						case "synonym":
							// EXACT, RELATED, NARROW...
							if (oCurrentTerm != null)
							{
								oCurrentTerm.AltNames.Add(HttpUtility.HtmlDecode(sItemValue));
							}
							break;
						case "is_obsolete":
							if (oCurrentTerm != null && sItemValue.Equals("true") && !oCurrentTerm.IsObsolete)
							{
								oCurrentTerm.IsObsolete = true;
								if (oCurrentTerm.Name != null && oCurrentTerm.Name.StartsWith("obsolete "))
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
							if (oCurrentTerm != null && sItemValue.StartsWith("go:", StringComparison.CurrentCultureIgnoreCase))
							{
								oCurrentTerm.GOConsiderIDs.Add(ParseGOID(sItemValue));
							}
							break;
						case "replaced_by":
							if (oCurrentTerm != null)
							{
								oCurrentTerm.GOReplacementIDs.Add(ParseGOID(sItemValue));
							}
							break;
						case "is_a":
						case "to":
							if (oCurrentTerm != null)
							{
								oCurrentTerm.GOParentIDs.Add(ParseGOID(sItemValue));
							}
							break;
						case "relationship":
							if (oCurrentTerm != null)
							{
								// we currently care only for part_of relation
								asTemp = sItemValue.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
								if (asTemp.Length < 2)
								{
									throw new Exception("Invalid relationship");
								}
								switch (asTemp[0].ToLower())
								{
									case "part_of":
										oCurrentTerm.GOPartOfIDs.Add(ParseGOID(asTemp[1]));
										break;
									case "has_part":
										oCurrentTerm.GOHasPartIDs.Add(ParseGOID(asTemp[1]));
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
								this.aTerms.Add(oCurrentTerm.ID, oCurrentTerm);
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
			GOTerm? oCurrentTerm = null;
			bool bInTerm = false;
			bool bInHeader = false;

			while (!reader.EndOfStream)
			{
				string? line = reader.ReadLine();

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
									if (oCurrentTerm != null && oCurrentTerm.Name == null)
									{
										oCurrentTerm.Name = HttpUtility.HtmlDecode(sTagValue);
									}
									break;
								case "namespace":
									if (oCurrentTerm != null && oCurrentTerm.Namespace == GONamespaceEnum.None)
									{
										oCurrentTerm.Namespace = (GONamespaceEnum)Enum.Parse(typeof(GONamespaceEnum),
											sTagValue, true);
									}
									break;
								case "defstr":
									if (oCurrentTerm != null && oCurrentTerm.Description == null)
									{
										oCurrentTerm.Description = HttpUtility.HtmlDecode(sTagValue);
									}
									break;
								case "alt_id":
									if (oCurrentTerm != null)
									{
										oCurrentTerm.AltIDs.Add(ParseGOID(sTagValue));
									}
									break;
								case "synonym_text":
									if (oCurrentTerm != null)
									{
										oCurrentTerm.AltNames.Add(HttpUtility.HtmlDecode(sTagValue));
									}
									break;
								case "is_obsolete":
									if (sTagValue.Equals("1") && oCurrentTerm != null && !oCurrentTerm.IsObsolete)
									{
										oCurrentTerm.IsObsolete = true;
										if (oCurrentTerm.Name != null && oCurrentTerm.Name.StartsWith("obsolete "))
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
									if (oCurrentTerm != null)
									{
										oCurrentTerm.GOReplacementIDs.Add(ParseGOID(sTagValue));
									}
									break;
								case "is_a":
								case "to":
									if (oCurrentTerm != null)
									{
										oCurrentTerm.GOParentIDs.Add(ParseGOID(sTagValue));
									}
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
								this.aTerms.Add(oCurrentTerm.ID, oCurrentTerm);
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
		private void AddKeywordsFromUniprotKeywords(string fileName)
		{
			if (File.Exists(fileName))
			{
				StreamReader file = new StreamReader(fileName);
				StringBuilder curText = new StringBuilder();

				while (!file.EndOfStream)
				{
					string? line = file.ReadLine();

					if (line == null)
						continue;

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
							this.aTerms.GetValueByKey(termId).Keywords.Add(keyword);
						}

						curText = new StringBuilder();
					}
				}

				file.Close();
			}
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
		public string? Link
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

		/// <summary>
		/// The Terms that constitue Gene Ontology
		/// </summary>
		public BDictionary<int, GOTerm> Terms
		{
			get { return this.aTerms; }
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

		/// <summary>
		/// Deserializes a GeneOntology object.
		/// </summary>
		/// <param name="path">A path to the Gene Ontology xml</param>
		/// <returns>A deserialized GeneOntology object.</returns>
		public static GeneOntology Deserialize(string path, bool gzipped)
		{
			return Deserialize(path + (gzipped ? ".gz" : ""));
		}

		/// <summary>
		/// Deserializes a GeneOntology object.
		/// Assumes file iz gzipped, if the filename ends with .gz
		/// </summary>
		/// <param name="path">A full path to a object xml file.</param>
		/// <returns>A deserialized GeneOntology object.</returns>
		public static GeneOntology Deserialize(string path)
		{
			StreamReader reader;
			if (path.EndsWith(".gz"))
			{
				reader = new StreamReader(new GZipStream(new BufferedStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), 65536), CompressionMode.Decompress));
			}
			else
			{
				reader = new StreamReader(new BufferedStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), 65536));
			}

			GeneOntology oOntology = Deserialize(reader);

			reader.Close();

			return oOntology;
		}

		/// <summary>
		/// Deserializes a GeneOntology object.
		/// </summary>
		/// <param name="reader">A stream to read the object from.</param>
		/// <returns>A deserialized GeneOntology object.</returns>
		public static GeneOntology Deserialize(StreamReader reader)
		{
			XmlSerializer ser = new XmlSerializer(typeof(GeneOntology));
			object? obj = ser.Deserialize(reader);
			if (obj == null)
				throw new Exception("Can't deserialize GeneOntology object");

			GeneOntology newObj = (GeneOntology)obj;
			for (int i = 0; i < newObj.Terms.Count; i++)
			{
				GOTerm term = newObj.Terms[i].Value;
				term.ReferencesInitialized = true;
				term.OntologyInternal = newObj;
			}

			return newObj;
		}

		/// <summary>
		/// Serializes a GeneOntology object.
		/// </summary>
		/// <param name="path">Can be a path ending in "goa_yyyy_mm_dd"</param>
		public void Serialize(string path, bool gzipped)
		{
			Serialize(path + (gzipped ? ".gz" : ""));
		}

		/// <summary>
		/// Serializes a GeneOntology object.
		/// Assumes file iz gzipped, if the filename ends with .gz
		/// </summary>
		/// <param name="filePath">A full path to a object xml file.</param>
		public void Serialize(string filePath)
		{
			StreamWriter writer;
			if (filePath.EndsWith(".gz"))
			{
				writer = new StreamWriter(new GZipStream(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read), CompressionMode.Compress));
			}
			else
			{
				writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read));
			}

			Serialize(writer);

			writer.Flush();
			writer.Close();
		}

		/// <summary>
		/// Serializes a GeneOntology object.
		/// </summary>
		/// <param name="writer">A stream to serialize the object to.</param>
		public void Serialize(StreamWriter writer)
		{
			XmlSerializer ser = new XmlSerializer(typeof(GeneOntology));
			ser.Serialize(writer, this);
		}
	}
}
