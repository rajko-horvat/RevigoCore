using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using IRB.Collections.Generic;
using IRB.Revigo.Core.Databases;
using IRB.Revigo.Core;

namespace IRB.Revigo.Core.Worker
{
	/// <summary>
	/// 
	/// Authors:
	/// 	Fran Supek (https://github.com/FranSupek)
	/// 	Rajko Horvat (https://github.com/rajko-horvat)
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
	public class RevigoWorker
	{
		private int iJobID = -1; // Job ID is used as unique Job ID for database updates
		private TimeSpan tsDefaultTimeout = new TimeSpan(0, 20, 0);
		private const int MaxNonEliminatedTerms = 300;

		private GeneOntology oOntology;
		private SpeciesAnnotations oAnnotations;
		private RequestSourceEnum eRequestSource = RequestSourceEnum.WebPage;
		private double dCutOff = 0.7;
		private ValueTypeEnum eValueType = ValueTypeEnum.PValue;
		private SemanticSimilarityTypeEnum eSemanticSimilarity = SemanticSimilarityTypeEnum.SIMREL;
		private bool bRemoveObsolete = true;
		private string sData;

		private double dProgress = 0.0;
		private double dProgressPos = 0.0;
		private double dProgressSlice = 0.0;
		private string sProgressText = "";

		private bool bRunning = false;
		private bool bFinished = false;
		private bool bWorkerTimeout = false;
		private System.Timers.Timer? oWorkerTimer = null;
		private Thread? oWorkerThread = null;
		private object oWorkerLock = new object();

		// for debugging purposes
		//private string sLogPath = null;

		// user level warnings and errors
		private List<string> aWarnings = new List<string>();
		private List<string> aErrors = new List<string>();

		// developer level warnings and errorss
		private List<string> aDevWarnings = new List<string>();
		private List<string> aDevErrors = new List<string>();

		// Results
		private bool bPinningJob = false;   // prevent OnFinish event when pinning the term
		private bool bDataParsed = false;
		private bool bRecalculateBP = false;
		private bool bRecalculateMF = false;
		private bool bRecalculateCC = false;
		private bool bRecalculateMixed = false;
		private int iTermsWithValuesCount = 0;
		private int iMinNumColsPerGoTerm = 0;

		// these results need to persist (pinning...)
		private BHashSet<GeneOntologyTerm> oAllTerms = new BHashSet<GeneOntologyTerm>();
		private RevigoTermCollection oBPTerms = new RevigoTermCollection();
		private RevigoTermCollection oMFTerms = new RevigoTermCollection();
		private RevigoTermCollection oCCTerms = new RevigoTermCollection();

		// results
		private BDictionary<string, double> oEnrichments = new BDictionary<string, double>();
		private BDictionary<string, double> oCorrelations = new BDictionary<string, double>();
		private NamespaceVisualizer oBPVisualizer = NamespaceVisualizer.Empty;
		private NamespaceVisualizer oMFVisualizer = NamespaceVisualizer.Empty;
		private NamespaceVisualizer oCCVisualizer = NamespaceVisualizer.Empty;

		private DateTime dtCreateDateTime = DateTime.Now;
		private TimeSpan tsExecutingTime = new TimeSpan(0);

		public event EventHandler OnFinish = delegate { };

		private CancellationTokenSource oToken = new CancellationTokenSource();

		public RevigoWorker(int jobID, GeneOntology ontology, SpeciesAnnotations annotations, TimeSpan timeout, RequestSourceEnum requestSource,
			string data, double cutOff, ValueTypeEnum valueType, SemanticSimilarityTypeEnum similarity, bool removeObsolete)
		{
			if (annotations == null)
				throw new Exception("Species annotations can't be null");

			this.iJobID = jobID;
			this.oOntology = ontology;
			this.oAnnotations = annotations;
			this.tsDefaultTimeout = timeout;
			this.eRequestSource = requestSource;
			this.sData = data;
			this.dCutOff = cutOff;
			this.eValueType = valueType;
			this.eSemanticSimilarity = similarity;
			this.bRemoveObsolete = removeObsolete;

			ValidateCutOff();
		}

		#region Parameters

		public int JobID
		{
			get { return this.iJobID; }
		}

		public GeneOntology Ontology
		{
			get
			{
				return this.oOntology;
			}
		}

		public SpeciesAnnotations Annotations
		{
			get
			{
				return this.oAnnotations;
			}
		}

		public RequestSourceEnum RequestSource
		{
			get
			{
				return this.eRequestSource;
			}
		}

		public string Data
		{
			get
			{
				return this.sData;
			}
			set
			{
				if (!this.bRunning)
				{
					this.sData = value;
				}
			}
		}

		public double CutOff
		{
			get
			{
				return this.dCutOff;
			}
			set
			{
				if (!this.bRunning)
				{
					this.dCutOff = value;

					ValidateCutOff();
				}
			}
		}

		public ValueTypeEnum ValueType
		{
			get
			{
				return this.eValueType;
			}
			set
			{
				if (!this.bRunning)
				{
					this.eValueType = value;
				}
			}
		}

		public SemanticSimilarityTypeEnum SemanticSimilarity
		{
			get
			{
				return this.eSemanticSimilarity;
			}
			set
			{
				if (!this.bRunning)
				{
					this.eSemanticSimilarity = value;
				}
			}
		}

		public bool RemoveObsolete
		{
			get
			{
				return this.bRemoveObsolete;
			}
			set
			{
				if (!this.bRunning)
				{
					this.bRemoveObsolete = value;
				}
			}
		}

		// statuses

		public double Progress
		{
			get
			{
				return this.dProgress;
			}
		}

		public string ProgressText
		{
			get
			{
				return this.sProgressText;
			}
		}

		public bool IsRunning
		{
			get
			{
				return this.bRunning;
			}
		}

		public bool IsFinished
		{
			get
			{
				return this.bFinished;
			}
		}

		public bool IsTimeout
		{
			get
			{
				return this.bWorkerTimeout;
			}
		}

		public DateTime CreateDateTime
		{
			get
			{
				return this.dtCreateDateTime;
			}
		}

		public TimeSpan ExecutingTime
		{
			get
			{
				return this.tsExecutingTime;
			}
		}

		// messages, warnings and errors

		public bool HasUserWarnings
		{
			get
			{
				return this.aWarnings.Count > 0;
			}
		}

		public List<string> UserWarnings
		{
			get
			{
				return this.aWarnings;
			}
		}

		public bool HasUserErrors
		{
			get
			{
				return this.aErrors.Count > 0;
			}
		}

		public List<string> UserErrors
		{
			get
			{
				return this.aErrors;
			}
		}

		// developer level warnings and errors
		public bool HasDeveloperWarnings
		{
			get
			{
				return this.aDevWarnings.Count > 0;
			}
		}

		public List<string> DeveloperWarnings
		{
			get
			{
				return this.aDevWarnings;
			}
		}

		public bool HasDeveloperErrors
		{
			get
			{
				return this.aDevErrors.Count > 0;
			}
		}

		public List<string> DeveloperErrors
		{
			get
			{
				return this.aDevErrors;
			}
		}

		// results
		public int TermsWithValuesCount
		{
			get
			{
				return this.iTermsWithValuesCount;
			}
		}

		public int MinNumColsPerGoTerm
		{
			get
			{
				return this.iMinNumColsPerGoTerm;
			}
		}

		public NamespaceVisualizer BPVisualizer
		{
			get
			{
				return this.oBPVisualizer;
			}
		}

		public NamespaceVisualizer MFVisualizer
		{
			get
			{
				return this.oMFVisualizer;
			}
		}

		public NamespaceVisualizer CCVisualizer
		{
			get
			{
				return this.oCCVisualizer;
			}
		}

		public bool HasClouds
		{
			get
			{
				return this.oEnrichments.Count > 0 || this.oCorrelations.Count > 0;
			}
		}

		public BDictionary<string, double> Enrichments
		{
			get
			{
				return this.oEnrichments;
			}
		}

		public BDictionary<string, double> Correlations
		{
			get
			{
				return this.oCorrelations;
			}
		}
		#endregion

		private void ValidateCutOff()
		{
			if (this.dCutOff <= 0.4)
			{
				this.dCutOff = 0.4;
			}
			else if (this.dCutOff > 0.4 && this.dCutOff <= 0.5)
			{
				this.dCutOff = 0.5;
			}
			else if (this.dCutOff > 0.5 && this.dCutOff <= 0.7)
			{
				this.dCutOff = 0.7;
			}
			else if (this.dCutOff > 0.7)
			{
				this.dCutOff = 0.9;
			}
		}

		public void Start()
		{
			lock (this.oWorkerLock)
			{
				if (!this.bRunning && !this.HasUserErrors && !this.HasDeveloperErrors)
				{
					this.oToken = new CancellationTokenSource();
					this.bFinished = false;
					this.oWorkerThread = new Thread(StartRevigo);
					this.oWorkerThread.Start();
				}
			}
		}

		public bool ContainsTermID(int termID)
		{
			if (!this.bRunning && this.oAllTerms.Contains(this.oOntology.Terms.GetValueByKey(termID)))
			{
				return true;
			}

			return false;
		}

		public void PinTerm(int termID)
		{
			GeneOntologyTerm term;

			if (!this.bRunning && this.oOntology.Terms.ContainsKey(termID) && this.oAllTerms.Contains(term = this.oOntology.Terms.GetValueByKey(termID)))
			{
				// find the namespace of the term
				RevigoTerm? nsTerm;
				this.bRecalculateMixed = true;

				switch (term.Namespace)
				{
					case GeneOntologyNamespaceEnum.BiologicalProcess:
						nsTerm = this.oBPTerms.Find(termID);
						if (nsTerm != null)
						{
							if (nsTerm.Pinned)
							{
								nsTerm.Pinned = false;
							}
							else
							{
								// pin the term, but also unpin its representative
								nsTerm.Pinned = true;
								RevigoTerm? representative = this.oBPTerms.Find(nsTerm.RepresentativeID);
								if (representative != null)
								{

									representative.Pinned = false;
								}
							}

							this.bRecalculateBP = true;
						}
						break;
					case GeneOntologyNamespaceEnum.CellularComponent:
						nsTerm = this.oCCTerms.Find(termID);
						if (nsTerm != null)
						{
							if (nsTerm.Pinned)
							{
								nsTerm.Pinned = false;
							}
							else
							{
								// pin the term, but also unpin its representative
								nsTerm.Pinned = true;
								RevigoTerm? representative = this.oCCTerms.Find(nsTerm.RepresentativeID);
								if (representative != null)
								{

									representative.Pinned = false;
								}
							}

							this.bRecalculateCC = true;
						}
						break;
					case GeneOntologyNamespaceEnum.MolecularFunction:
						nsTerm = this.oMFTerms.Find(termID);
						if (nsTerm != null)
						{
							if (nsTerm.Pinned)
							{
								nsTerm.Pinned = false;
							}
							else
							{
								// pin the term, but also unpin its representative
								nsTerm.Pinned = true;
								RevigoTerm? representative = this.oMFTerms.Find(nsTerm.RepresentativeID);
								if (representative != null)
								{

									representative.Pinned = false;
								}
							}

							this.bRecalculateMF = true;
						}
						break;
				}

				this.bPinningJob = true;

				this.Start();
			}
		}

		public void Abort()
		{
			lock (this.oWorkerLock)
			{
				if (this.bRunning && !this.oToken.IsCancellationRequested)
				{
					try
					{
						this.oToken.Cancel();
						//this.oWorkerThread.Abort();
					}
					catch { }
				}
			}
		}

		void oWorkerTimer_Elapsed(object? sender, ElapsedEventArgs e)
		{
			if (this.bRunning && !oToken.IsCancellationRequested)
			{
				try
				{
					this.oToken.Cancel();
					//this.oWorkerThread.Abort();
				}
				catch { }
			}
		}

		private GeneOntologyNamespaceEnum eCurrentNamespace = GeneOntologyNamespaceEnum.None;

		private void StartRevigo()
		{
			this.bRunning = true;
			this.oWorkerTimer = new System.Timers.Timer(tsDefaultTimeout.TotalMilliseconds);
			this.oWorkerTimer.Elapsed += oWorkerTimer_Elapsed;
			this.oWorkerTimer.AutoReset = false;
			this.oWorkerTimer.Start();
			DateTime dtStartTime = DateTime.Now;
			this.tsExecutingTime = new TimeSpan(0);

			try
			{
				// is Gene ontology object initialized?
				if (this.oOntology == null || this.oOntology.Terms.Count == 0)
				{
					this.aErrors.Add("The Gene Ontology is not initialized.");
					this.aDevErrors.Add("The Gene Ontology is not initialized.");

					return;
				}

				if (this.oAnnotations == null)
				{
					this.aErrors.Add("Species annotations have not been selected.");

					return;
				}

				if (!this.bDataParsed)
				{
					this.oAllTerms.Clear();
					this.oBPTerms.Clear();
					this.oMFTerms.Clear();
					this.oCCTerms.Clear();
					BHashSet<RevigoTerm> aDuplicateTerms = new BHashSet<RevigoTerm>();
					BHashSet<RevigoTerm> aObsoleteTerms = new BHashSet<RevigoTerm>();

					this.iTermsWithValuesCount = 0;		// a check to see if user provided values for only some (but not all) of the terms
					this.iMinNumColsPerGoTerm = int.MaxValue;
					int termsWithNonSigPvalsCount = 0;	// any p-value (or FDR, for that matter) >0.50 is surely non-significant and will not be processed at all

					this.sProgressText = "Parsing data";
					this.dProgress = this.dProgressPos = 0.0;
					this.dProgressSlice = 5.0;

					StringReader oDataReader = new StringReader(this.sData);
					string? sLine;
					int iLineCount = 0;
					int iLinePos = 0;

					for (int i = 0; i < this.sData.Length; i++)
					{
						if (this.sData[i] == '\n')
						{
							iLineCount++;
						}
					}

					while ((sLine = oDataReader.ReadLine()) != null)
					{
						if (this.oToken.IsCancellationRequested)
						{
							this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
							return;
						}

						iLinePos++;
						this.dProgress = this.dProgressPos + (((double)iLinePos * this.dProgressSlice) / (double)iLineCount);

						sLine = sLine.Trim();
						if (string.IsNullOrEmpty(sLine) || sLine.StartsWith("%") || sLine.StartsWith("#") || sLine.StartsWith("!"))
							continue;
						string[] cols = sLine.Split(new char[] { ' ', '|', '\t', '\v' }, StringSplitOptions.RemoveEmptyEntries);

						int iGOID;
						double iValue;

						if (cols[0].StartsWith("GO:", StringComparison.CurrentCultureIgnoreCase))
							cols[0] = cols[0].Substring(3);
						if (cols[0].StartsWith("GO", StringComparison.CurrentCultureIgnoreCase))
							cols[0] = cols[0].Substring(2);

						if (!int.TryParse(cols[0], out iGOID))
						{
							this.aWarnings.Add(string.Format("Could not parse GO ID from line: '{0}'. Line will be skipped.", sLine));
							continue;
						}

						RevigoTerm? oTerm = null;
						if (this.oOntology.Terms.ContainsKey(iGOID))
						{
							oTerm = new RevigoTerm(this.oOntology.Terms.GetValueByKey(iGOID));
						}
						if (oTerm == null)
						{
							this.aWarnings.Add(string.Format("Go term {0} was not found in the " +
								"current version of the Gene Ontology dated {1}. GO term will be skipped.",
								iGOID, this.oOntology.Date.ToString("D", CultureInfo.GetCultureInfo("en-US"))));
							continue;
						}

						if (oTerm.GOTerm.IsObsolete)
						{
							if (!aObsoleteTerms.Contains(oTerm))
							{
								if (oTerm.GOTerm.GOConsiderIDs.Count > 0)
								{
									StringBuilder sbTemp = new StringBuilder();
									sbTemp.AppendFormat("The GO term {0} is obsolete ({1}). Consider replacing it with one of the alternative GO term(s): ",
										oTerm.GOTerm.ID, oTerm.GOTerm.Comment);
									for (int i = 0; i < oTerm.GOTerm.GOConsiderIDs.Count; i++)
									{
										if (i > 0)
											sbTemp.Append(", ");
										sbTemp.Append(oTerm.GOTerm.GOConsiderIDs[i]);
									}
									sbTemp.Append(".");
									this.aWarnings.Add(sbTemp.ToString());
								}
								else
								{
									this.aWarnings.Add(string.Format("The GO term {0} is obsolete ({1}) and there is no replacement term proposed.",
										oTerm.GOTerm.ID, oTerm.GOTerm.Comment));
								}

								aObsoleteTerms.Add(oTerm);
							}

							// do not add obsolete terms if required
							if (this.bRemoveObsolete)
								continue;
						}

						if (this.oAllTerms.Contains(oTerm.GOTerm))
						{
							if (!aDuplicateTerms.Contains(oTerm))
							{
								if (oTerm.GOTerm.AltIDs.Count > 0)
								{
									StringBuilder sbTemp = new StringBuilder();
									sbTemp.AppendFormat("A duplicate GO term {0} with alternative GO term ID(s) ", oTerm.GOTerm.ID);
									for (int i = 0; i < oTerm.GOTerm.AltIDs.Count; i++)
									{
										if (i > 0)
											sbTemp.Append(", ");
										sbTemp.Append(oTerm.GOTerm.AltIDs[i]);
									}
									sbTemp.Append(" was found in the submitted data. First will be used and other instance(s) will be skipped.");
									this.aWarnings.Add(sbTemp.ToString());
								}
								else
								{
									this.aWarnings.Add(string.Format("A duplicate GO term {0} was found " +
										"in the submitted data. First will be used and other instance(s) will be skipped.", iGOID));
								}
								aDuplicateTerms.Add(oTerm);
							}
							continue;
						}

						if (iGOID != oTerm.GOTerm.ID)
						{
							this.aWarnings.Add(string.Format("The GO term {0} has been replaced by or is an alternative of the GO term {1}. " +
								"The GO term {1} will be used in reports.",
								iGOID, oTerm.GOTerm.ID));
						}

						// if we found alternate ID, we need to keep the main GO ID
						iGOID = oTerm.GOTerm.ID;

						if (cols.Length == 1)
						{
							iValue = Double.NaN;
						}
						else
						{
							if (double.TryParse(cols[1], NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out iValue))
							{
								// new 4.7.2016. P-values>0.50 are very likely useless results (these result are from e.g. AgriGO)
								// and will be removed prior to calculation
								if (eValueType == ValueTypeEnum.PValue && iValue > 0.5)
								{
									termsWithNonSigPvalsCount++;
									continue; // will cause a term to be skipped & not reported later at all...
								}

								this.iTermsWithValuesCount++;
							}
							else
							{
								this.aWarnings.Add("Failed to parse number following GO ID in line: '"
									+ sLine + "'. This GO term will be treated as not having any value associated to it.");
								iValue = Double.NaN;
							}
						}
						this.iMinNumColsPerGoTerm = Math.Min(10, Math.Min(this.iMinNumColsPerGoTerm, cols.Length - 1));

						int iUserValueCount = Math.Min(10, cols.Length - 1);
						for (int c = 1; c < iUserValueCount; c++)
						{
							// all columns after first one
							double dUserValue;
							if (double.TryParse(cols[c], NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out dUserValue))
							{
								oTerm.UserValues.Add(dUserValue);
							}
							else
							{
								oTerm.UserValues.Add(double.NaN);
							}
						}

						// add 'value' attribute to terms (transform first if necessary, & do check for sensible p-values)
						// (note: it will not be added for terms where user did not supply something that can be parsed into a double)

						double transformedVal = Double.NaN;
						if (!double.IsNaN(iValue))
						{
							switch (eValueType)
							{
								case ValueTypeEnum.PValue:
									if (iValue < 0.0 || iValue > 1.0)
									{
										this.aErrors.Add("You can't specify a negative " +
											"p-value, or a p-value greater than 1.0. Go back to the input page and correct the error.");
										return;
									}

									iValue = Math.Max(1e-300, iValue);
									iValue = Math.Log10(iValue);
									transformedVal = -iValue;
									break;
								case ValueTypeEnum.Higher:
									transformedVal = iValue;
									break;
								case ValueTypeEnum.Lower:
									transformedVal = -iValue;
									break;
								case ValueTypeEnum.HigherAbsolute:
									transformedVal = Math.Abs(iValue);
									break;
								case ValueTypeEnum.HigherAbsLog2:
									if (iValue <= 0)
									{
										this.aErrors.Add("Please provide only positive values if you " +
											"choose the log-transform. Go back to the input page to correct the error.");
										return;
									}

									iValue = Math.Log(iValue) / Math.Log(2);
									transformedVal = Math.Abs(iValue);
									break;
							}
							oTerm.Value = iValue;
							oTerm.TransformedValue = transformedVal;
						}

						// if size not defined, attempts to guess using heuristics ... then add size and frequency attribute to terms
						oTerm.AnnotationSize = oAnnotations.GetTermSize(iGOID, this.oOntology);
						oTerm.LogAnnotationSize = Math.Log10(Math.Max(1.0, oTerm.AnnotationSize) + 1.0);
						oTerm.AnnotationFrequency = oAnnotations.GetTermFrequency(iGOID, this.oOntology);

						// And finally add term to appropriate namespace and to a common namespace
						switch (oTerm.GOTerm.Namespace)
						{
							case GeneOntologyNamespaceEnum.BiologicalProcess:
								this.oBPTerms.Add(oTerm);
								break;
							case GeneOntologyNamespaceEnum.MolecularFunction:
								this.oMFTerms.Add(oTerm);
								break;
							case GeneOntologyNamespaceEnum.CellularComponent:
								this.oCCTerms.Add(oTerm);
								break;
						}

						this.oAllTerms.Add(oTerm.GOTerm);
					} // line by line in user input file

					if (this.iTermsWithValuesCount > 0 && this.iTermsWithValuesCount < this.oAllTerms.Count)
					{
						this.aWarnings.Add("You have provided a numeric value only for some of the "
								+ "GO terms (" + this.iTermsWithValuesCount.ToString() + " out of " + this.oAllTerms.Count.ToString() + "); "
								+ "while REVIGO can still function, it is possible that you have"
								+ " an error in your input data.");
					}

					string sHugeListTemplate = "You have provided an extremely large list of GO terms. The list has {0} terms in the {1} namespace. " +
						"The maximum allowed number of terms in this namespace is {2}. Please reduce the list by an external criterion " +
						"(e.g. enrichment) before submitting to REVIGO.";

					if (this.oBPTerms.Count > NamespaceVisualizer.MaxAllowedGOListSize)
					{
						this.aErrors.Add(string.Format(sHugeListTemplate, this.oBPTerms.Count,
							GeneOntology.NamespaceToFriendlyString(GeneOntologyNamespaceEnum.BiologicalProcess),
							NamespaceVisualizer.MaxAllowedGOListSize));

						return;
					}
					if (this.oMFTerms.Count > NamespaceVisualizer.MaxAllowedGOListSize)
					{
						this.aErrors.Add(string.Format(sHugeListTemplate, this.oMFTerms.Count,
							GeneOntology.NamespaceToFriendlyString(GeneOntologyNamespaceEnum.MolecularFunction), NamespaceVisualizer.MaxAllowedGOListSize));

						return;
					}
					if (this.oCCTerms.Count > NamespaceVisualizer.MaxAllowedGOListSize)
					{
						this.aErrors.Add(string.Format(sHugeListTemplate, this.oCCTerms.Count,
							GeneOntology.NamespaceToFriendlyString(GeneOntologyNamespaceEnum.CellularComponent),
							NamespaceVisualizer.MaxAllowedGOListSize));

						return;
					}

					if (termsWithNonSigPvalsCount > 0)
					{
						this.aWarnings.Add(string.Format("You have provided {0} GO terms with p-values or FDRs >0.50. These non-significant entries have " +
							"been filtered out and will not appear in further analyses.", termsWithNonSigPvalsCount));
					}

					if (this.bRemoveObsolete && aObsoleteTerms.Count > 0)
					{
						this.aWarnings.Add(string.Format("{0} obsolete GO term(s) have been removed from your data set.", aObsoleteTerms.Count));
					}

					this.bDataParsed = true;
					this.bRecalculateBP = true;
					this.bRecalculateMF = true;
					this.bRecalculateCC = true;
					this.bRecalculateMixed = true;
				}

				// make Visualizers for each namespace separately (and one for all together)
				int iNamespaceCount = (this.bRecalculateBP ? 1 : 0) + (this.bRecalculateCC ? 1 : 0) + (this.bRecalculateMF ? 1 : 0);
				this.dProgress = this.dProgressPos = 5.0;
				this.dProgressSlice = 90.0 / (double)iNamespaceCount;

				if (this.bRecalculateBP)
				{
					this.eCurrentNamespace = GeneOntologyNamespaceEnum.BiologicalProcess;
					this.oBPVisualizer = new NamespaceVisualizer(this, GeneOntologyNamespaceEnum.BiologicalProcess, this.oBPTerms,
						this.oToken.Token, Visualizer_OnProgress);

					if (this.oToken.IsCancellationRequested)
					{
						this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
						return;
					}

					if (this.oBPVisualizer.MDSError)
					{
						this.aWarnings.Add("The Multidimensional Scaling (MDS) used for determining x and y semantic " +
							"space coordinates in the scatterplot did not converge for the namespace " +
							GeneOntology.NamespaceToFriendlyString(this.oBPVisualizer.Namespace) + ".");
						this.aDevWarnings.Add("The Multidimensional Scaling (MDS) used for determining x and y semantic " +
							"space coordinates in the scatterplot did not converge for the namespace " +
							GeneOntology.NamespaceToFriendlyString(this.oBPVisualizer.Namespace) + ".");
					}

					int iNumNonEliminatedTerms = 0;
					for (int i = 0; i < this.oBPTerms.Count; i++)
					{
						RevigoTerm term = this.oBPTerms[i];
						double dDispensability = term.Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.dCutOff)
						{
							continue;
						}
						iNumNonEliminatedTerms++;
					}

					if (iNumNonEliminatedTerms > MaxNonEliminatedTerms)
					{
						this.aWarnings.Add(
							string.Format("Your resulting list of GO terms for {0} namespace seems to be quite long. " +
							"If you want to reduce it further, go Back and choose a different size for your resulting list.",
							GeneOntology.NamespaceToFriendlyString(this.oBPVisualizer.Namespace)));
					}

					this.dProgress = this.dProgressPos += this.dProgressSlice;
					this.bRecalculateBP = false;
				}

				if (this.bRecalculateCC)
				{
					this.eCurrentNamespace = GeneOntologyNamespaceEnum.CellularComponent;
					this.oCCVisualizer = new NamespaceVisualizer(this, GeneOntologyNamespaceEnum.CellularComponent, this.oCCTerms,
						this.oToken.Token, Visualizer_OnProgress);

					if (this.oToken.IsCancellationRequested)
					{
						this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
						return;
					}

					if (this.oCCVisualizer.MDSError)
					{
						this.aWarnings.Add("The Multidimensional Scaling (MDS) used for determining x and y semantic " +
							"space coordinates in the scatterplot did not converge for the namespace " +
							GeneOntology.NamespaceToFriendlyString(this.oCCVisualizer.Namespace) + ".");
						this.aDevWarnings.Add("The Multidimensional Scaling (MDS) used for determining x and y semantic " +
							"space coordinates in the scatterplot did not converge for the namespace " +
							GeneOntology.NamespaceToFriendlyString(this.oCCVisualizer.Namespace) + ".");
					}

					int iNumNonEliminatedTerms = 0;
					for (int i = 0; i < this.oCCTerms.Count; i++)
					{
						RevigoTerm term = this.oCCTerms[i];
						double dDispensability = term.Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.dCutOff)
						{
							continue;
						}
						iNumNonEliminatedTerms++;
					}

					if (iNumNonEliminatedTerms > MaxNonEliminatedTerms)
					{
						this.aWarnings.Add(
							string.Format("Your resulting list of GO terms for {0} namespace seems to be quite long. " +
							"If you want to reduce it further, go Back and choose a different size for your resulting list.",
							GeneOntology.NamespaceToFriendlyString(this.oCCVisualizer.Namespace)));
					}

					this.dProgress = this.dProgressPos += this.dProgressSlice;
					this.bRecalculateCC = false;
				}

				if (this.bRecalculateMF)
				{
					this.eCurrentNamespace = GeneOntologyNamespaceEnum.MolecularFunction;
					this.oMFVisualizer = new NamespaceVisualizer(this, GeneOntologyNamespaceEnum.MolecularFunction, this.oMFTerms.ToArray(),
						this.oToken.Token, Visualizer_OnProgress);

					if (this.oToken.IsCancellationRequested)
					{
						this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
						return;
					}

					if (this.oMFVisualizer.MDSError)
					{
						this.aWarnings.Add("The Multidimensional Scaling (MDS) used for determining x and y semantic " +
							"space coordinates in the scatterplot did not converge for the namespace " +
							GeneOntology.NamespaceToFriendlyString(this.oMFVisualizer.Namespace) + ".");
						this.aDevWarnings.Add("The Multidimensional Scaling (MDS) used for determining x and y semantic " +
							"space coordinates in the scatterplot did not converge for the namespace " +
							GeneOntology.NamespaceToFriendlyString(this.oMFVisualizer.Namespace) + ".");
					}

					int iNumNonEliminatedTerms = 0;
					for (int i = 0; i < this.oMFTerms.Count; i++)
					{
						RevigoTerm term = this.oMFTerms[i];
						double dDispensability = term.Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.dCutOff)
						{
							continue;
						}
						iNumNonEliminatedTerms++;
					}

					if (iNumNonEliminatedTerms > MaxNonEliminatedTerms)
					{
						this.aWarnings.Add(
							string.Format("Your resulting list of GO terms for {0} namespace seems to be quite long. " +
							"If you want to reduce it further, go Back and choose a different size for your resulting list.",
							GeneOntology.NamespaceToFriendlyString(this.oMFVisualizer.Namespace)));
					}

					this.dProgress = this.dProgressPos += this.dProgressSlice;
					this.bRecalculateMF = false;
				}

				if (this.bRecalculateMixed)
				{
					this.dProgress = this.dProgressPos = 95.0;
					this.dProgressSlice = 4.0;
					this.sProgressText = "Calculating other data";

					GeneOntologyWordCorpus corpus = new GeneOntologyWordCorpus(this.oAllTerms, this.oOntology);
					this.oEnrichments = corpus.calculateWordEnrichment(this.oAnnotations.WordCorpus, 70, 0);

					if (this.oToken.IsCancellationRequested)
					{
						this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
						return;
					}

					if (this.iTermsWithValuesCount != 0)
					{
						GeneOntologyWordCorpus correlCorpus = new GeneOntologyWordCorpus(this.oAllTerms, this.oOntology);
						this.oCorrelations = correlCorpus.getMostFrequentWords(70);
					}

					if (this.oToken.IsCancellationRequested)
					{
						this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
						return;
					}

					this.bRecalculateMixed = false;
				}

				this.dProgress = this.dProgressPos = 99.0;
				this.dProgressSlice = 1.0;
				this.sProgressText = "Finishing...";

				if (this.iMinNumColsPerGoTerm > 1 && this.oAllTerms.Count > 0)
				{
					this.aWarnings.Add("You have provided more than one number alongside each GO term. Note that only the first value following the GO term will be used to select and cluster GO terms, although others values will be available in the scatterplot and the table view.");
				}

				if (this.oBPVisualizer.IsEmpty && this.oCCVisualizer.IsEmpty && this.oMFVisualizer.IsEmpty)
				{
					this.aErrors.Add("Your query has produced no results in any namespace, please return to the input page and correct the error.");
				}

				this.dProgress = 100.0;
				this.sProgressText = "Finished";
			}
			catch (Exception ex)
			{
				this.aErrors.Add("Unknown error has occured.");
				if (!this.oToken.IsCancellationRequested)
				{
					this.aDevErrors.Add(string.Format("Exception: {0}", ex.Message));
					this.aDevErrors.Add(string.Format("Stack trace: {0}", ex.StackTrace));
				}
			}
			finally
			{
				this.tsExecutingTime = DateTime.Now - dtStartTime;

				if (!this.bPinningJob && this.OnFinish != null)
				{
					this.OnFinish(this, EventArgs.Empty);
				}

				this.bPinningJob = false;
				this.bRunning = false;
				this.bFinished = true;
				this.oWorkerTimer.Stop();
				this.oWorkerTimer.Dispose();
				this.oWorkerTimer = null;
			}
		}

		void Visualizer_OnProgress(object sender, ProgressEventArgs e)
		{
			this.dProgress = this.dProgressPos + ((e.Progress * this.dProgressSlice) / 100.0);
			if (!string.IsNullOrEmpty(e.Description))
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(e.Description);
				if (this.eCurrentNamespace != GeneOntologyNamespaceEnum.None)
				{
					sb.AppendFormat(" for {0} namespace", GeneOntology.NamespaceToFriendlyString(this.eCurrentNamespace));
				}
				this.sProgressText = sb.ToString();
			}
		}
	}
}
