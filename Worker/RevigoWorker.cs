using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using IRB.Collections.Generic;
using IRB.Revigo.Databases;
using IRB.Revigo.Core;

namespace IRB.Revigo.Worker
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
	public class RevigoWorker
	{
		private int iJobID = -1; // Job ID is used as unique Job ID for database updates
		private TimeSpan tsDefaultTimeout = new TimeSpan(0, 20, 0);
		private const int MaxNonEliminatedTerms = 300;

		private GeneOntology oOntology = null;
		private RequestSourceEnum eRequestSource = RequestSourceEnum.WebPage;
		private double dCutOff = 0.7;
		private ValueTypeEnum eValueType = ValueTypeEnum.PValue;
		private SpeciesAnnotations oAnnotations = null;
		private SemanticSimilarityScoreEnum eMeasure = SemanticSimilarityScoreEnum.SIMREL;
		private bool bRemoveObsolete = true;
		private string sData = null;

		private double dProgress = 0.0;
		private double dProgressPos = 0.0;
		private double dProgressSlice = 0.0;
		private string sProgressText = "";

		private bool bRunning = false;
		private bool bFinished = false;
		private bool bWorkerTimeout = false;
		private System.Timers.Timer oWorkerTimer = null;
		private Thread oWorkerThread = null;
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
		private bool bPinningJob = false;	// prevent OnFinish event when pinning the term
		private bool bDataParsed = false;
		private bool bRecalculateBP = false;
		private bool bRecalculateMF = false;
		private bool bRecalculateCC = false;
		private bool bRecalculateMixed = false;
		private int iTermsWithValuesCount = 0;
		private int iMinNumColsPerGoTerm = 0;

		// these results need to persist (pinning...)
		private BHashSet<GOTerm> oAllTerms = new BHashSet<GOTerm>();
		private BDictionary<int, GOTermProperties> oAllProperties = new BDictionary<int,GOTermProperties>();
		private List<GOTerm> oBPTerms = new List<GOTerm>();
		private List<GOTerm> oMFTerms = new List<GOTerm>();
		private List<GOTerm> oCCTerms = new List<GOTerm>();
		private BDictionary<string, double> oEnrichments = null;
		private BDictionary<string, double> oCorrelations = null;
		private TermListVisualizer oBPVisualizer = null;
		private TermListVisualizer oMFVisualizer = null;
		private TermListVisualizer oCCVisualizer = null;

		private DateTime dtCreateDateTime = DateTime.Now;
		private TimeSpan tsExecutingTime = new TimeSpan(0);

		public event EventHandler OnFinish = delegate { };

		private CancellationTokenSource oToken;

		public RevigoWorker(GeneOntology ontology, SpeciesAnnotations annotations, TimeSpan timeout, RequestSourceEnum requestSource,
			string data, double cutOff, ValueTypeEnum valueType, SemanticSimilarityScoreEnum measure, bool removeObsolete) :
			this(-1, ontology, annotations, timeout, requestSource,
			data, cutOff, valueType, measure, removeObsolete)
		{
		}

		public RevigoWorker(int jobID, GeneOntology ontology, SpeciesAnnotations annotations, TimeSpan timeout, RequestSourceEnum requestSource,
			string data, double cutOff, ValueTypeEnum valueType, SemanticSimilarityScoreEnum measure, bool removeObsolete)
		{
			this.iJobID = jobID;
			this.oOntology = ontology;
			this.oAnnotations = annotations;
			this.tsDefaultTimeout = timeout;
			this.eRequestSource = requestSource;
			this.sData = data;
			this.dCutOff = cutOff;
			this.eValueType = valueType;
			this.eMeasure = measure;
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

		public SemanticSimilarityScoreEnum Measure
		{
			get
			{
				return this.eMeasure;
			}
			set
			{
				if (!this.bRunning)
				{
					this.eMeasure = value;
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
		public BDictionary<int, GOTermProperties> AllProperties
		{
			get
			{
				return this.oAllProperties;
			}
		}

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

		public bool HasBPVisualizer
		{
			get
			{
				return this.oBPVisualizer != null && this.oBPVisualizer.Terms.Length > 0;
			}
		}

		public TermListVisualizer BPVisualizer
		{
			get
			{
				return this.oBPVisualizer;
			}
		}

		public bool HasMFVisualizer
		{
			get
			{
				return this.oMFVisualizer != null && this.oMFVisualizer.Terms.Length > 0;
			}
		}

		public TermListVisualizer MFVisualizer
		{
			get
			{
				return this.oMFVisualizer;
			}
		}

		public bool HasCCVisualizer
		{
			get
			{
				return this.oCCVisualizer != null && this.oCCVisualizer.Terms.Length > 0;
			}
		}

		public TermListVisualizer CCVisualizer
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
				return (this.oEnrichments != null && this.Enrichments.Count > 0) || 
					(this.oCorrelations != null && this.Correlations.Count > 0);
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
				if (!this.bRunning && !this.HasUserErrors && !this.HasDeveloperErrors && !oToken.IsCancellationRequested)
				{
					this.oToken = new CancellationTokenSource();
					this.bFinished = false;
					this.oWorkerThread = new Thread(StartRevigo);
					this.oWorkerThread.Start(oToken.Token);
				}
			}
		}

		public bool ContainsTermID(int termID)
		{
			if (!this.bRunning && this.oOntology != null && this.oOntology.Terms.ContainsKey(termID))
			{
				GOTerm term = this.oOntology.Terms.GetValueByKey(termID);
				if (this.oAllTerms.Contains(term))
				{
					return true;
				}
			}

			return false;
		}

		public void PinTerm(int termID)
		{
			if (!this.bRunning && this.oOntology != null && this.oOntology.Terms.ContainsKey(termID))
			{
				GOTerm term = this.oOntology.Terms.GetValueByKey(termID);
				if (this.oAllTerms.Contains(term))
				{
					GOTermProperties oProperties = this.oAllProperties.GetValueByKey(termID);

					if (oProperties.Pinned)
					{
						oProperties.Pinned = false;
					}
					else
					{
						// pin the term, but also unpin its representative
						oProperties.Pinned = true;
						int iRepresentative = oProperties.Representative;
						if (iRepresentative >= 0)
						{
							this.oAllProperties.GetValueByKey(iRepresentative).Pinned = false;
						}
					}

					// find the namespace of the term
					this.bRecalculateMixed = true;
					switch (term.Namespace)
					{
						case GONamespaceEnum.BIOLOGICAL_PROCESS:
							this.bRecalculateBP = true;
							break;
						case GONamespaceEnum.CELLULAR_COMPONENT:
							this.bRecalculateCC = true;
							break;
						case GONamespaceEnum.MOLECULAR_FUNCTION:
							this.bRecalculateMF = true;
							break;
					}

					this.bPinningJob = true;

					this.Start();
				}
			}
		}

		public void Abort()
		{
			lock (this.oWorkerLock)
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
		}

		void oWorkerTimer_Elapsed(object sender, ElapsedEventArgs e)
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

		private GONamespaceEnum eCurrentNamespace = GONamespaceEnum.None;

		private void StartRevigo(object token)
		{
			CancellationToken oToken = (CancellationToken)token;
			StreamWriter oLogWriter = null;

			this.bRunning = true;
			this.oWorkerTimer = new System.Timers.Timer(tsDefaultTimeout.TotalMilliseconds);
			this.oWorkerTimer.Elapsed += oWorkerTimer_Elapsed;
			this.oWorkerTimer.AutoReset = false;
			this.oWorkerTimer.Start();
			DateTime dtStartTime = DateTime.Now;
			this.tsExecutingTime = new TimeSpan(0);

			// is Gene ontology object initialized?
			if (this.oOntology == null || this.oOntology.Terms.Count == 0)
			{
				this.aErrors.Add("The Gene Ontology is not initialized.");
				this.aDevErrors.Add("The Gene Ontology is not initialized.");

				this.bFinished = true;
				this.bRunning = false;
				this.oWorkerTimer.Stop();
				this.oWorkerTimer.Dispose();
				this.oWorkerTimer = null;
				return;
			}

			try
			{
				if (!this.bDataParsed)
				{
					this.oAllTerms.Clear();
					this.oAllProperties.Clear();
					this.oBPTerms.Clear();
					this.oMFTerms.Clear();
					this.oCCTerms.Clear();
					BHashSet<GOTerm> aDuplicateTerms = new BHashSet<GOTerm>();
					BHashSet<GOTerm> aObsoleteTerms = new BHashSet<GOTerm>();

					this.iTermsWithValuesCount = 0;           // a check to see if user provided values for only some (but not all) of the terms
					this.iMinNumColsPerGoTerm = int.MaxValue;
					int termsWithNonSigPvalsCount = 0;    // any p-value (or FDR, for that matter) >0.50 is surely non-significant and will not be processed at all

					this.sProgressText = "Parsing data";
					this.dProgress = this.dProgressPos = 0.0;
					this.dProgressSlice = 5.0;

					StringReader oDataReader = new StringReader(this.sData);
					string sLine;
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
						if (oToken.IsCancellationRequested)
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

						GOTerm oGOTerm = null;
						if (this.oOntology.Terms.ContainsKey(iGOID))
						{
							oGOTerm = this.oOntology.Terms.GetValueByKey(iGOID);
						}
						if (oGOTerm == null)
						{
							this.aWarnings.Add(string.Format("Go term {0} was not found in the " +
								"current version of the GeneOntology dated {1}. GO term will be skipped.",
								iGOID, this.oOntology.Date.ToString("D", CultureInfo.GetCultureInfo("en-US"))));
							continue;
						}

						if (oGOTerm.IsObsolete)
						{
							if (!aObsoleteTerms.Contains(oGOTerm))
							{
								if (oGOTerm.ConsiderIDs.Count > 0)
								{
									StringBuilder sbTemp = new StringBuilder();
									sbTemp.AppendFormat("The GO term {0} is obsolete ({1}). Consider replacing it with one of the alternative GO term(s): ",
										oGOTerm.ID, oGOTerm.Comment);
									for (int i = 0; i < oGOTerm.ConsiderIDs.Count; i++)
									{
										if (i > 0)
											sbTemp.Append(", ");
										sbTemp.Append(oGOTerm.ConsiderIDs[i]);
									}
									sbTemp.Append(".");
									this.aWarnings.Add(sbTemp.ToString());
								}
								else
								{
									this.aWarnings.Add(string.Format("The GO term {0} is obsolete ({1}) and there is no replacement term proposed.",
										oGOTerm.ID, oGOTerm.Comment));
								}

								aObsoleteTerms.Add(oGOTerm);
							}

							// do not add obsolete terms if required
							if (this.bRemoveObsolete)
								continue;
						}

						if (this.oAllTerms.Contains(oGOTerm))
						{
							if (!aDuplicateTerms.Contains(oGOTerm))
							{
								if (oGOTerm.AltIDs.Count > 0)
								{
									StringBuilder sbTemp = new StringBuilder();
									sbTemp.AppendFormat("A duplicate GO term {0} with alternative GO term ID(s) ", oGOTerm.ID);
									for (int i = 0; i < oGOTerm.AltIDs.Count; i++)
									{
										if (i > 0)
											sbTemp.Append(", ");
										sbTemp.Append(oGOTerm.AltIDs[i]);
									}
									sbTemp.Append(" was found in the submitted data. First will be used and other instance(s) will be skipped.");
									this.aWarnings.Add(sbTemp.ToString());
								}
								else
								{
									this.aWarnings.Add(string.Format("A duplicate GO term {0} was found " +
										"in the submitted data. First will be used and other instance(s) will be skipped.", iGOID));
								}
								aDuplicateTerms.Add(oGOTerm);
							}
							continue;
						}

						if (iGOID != oGOTerm.ID)
						{
							this.aWarnings.Add(string.Format("The GO term {0} has been replaced by or is an alternative of the GO term {1}. The GO term {1} will be used in reports.",
								iGOID, oGOTerm.ID));
						}

						// if we found alternate ID, we need to keep the main GO ID
						iGOID = oGOTerm.ID;
						GOTermProperties oProperties = new GOTermProperties(iGOID);

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
								oProperties.UserValues.Add(dUserValue);
							}
							else
							{
								oProperties.UserValues.Add(double.NaN);
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
							oProperties.Value = iValue;
							oProperties.TransformedValue = transformedVal;
						}

						// if size not defined, attempts to guess using heuristics ... then add size and frequency attribute to terms
						oProperties.AnnotationSize = oAnnotations.GetTermSize(iGOID, this.oOntology);
						oProperties.LogAnnotationSize = Math.Log10(Math.Max(1.0, oProperties.AnnotationSize) + 1.0);
						oProperties.AnnotationFrequency = oAnnotations.GetTermFrequency(iGOID, this.oOntology);

						// And finally add term to appropriate namespace and to a common namespace
						switch (oGOTerm.Namespace)
						{
							case GONamespaceEnum.BIOLOGICAL_PROCESS:
								this.oBPTerms.Add(oGOTerm);
								break;
							case GONamespaceEnum.MOLECULAR_FUNCTION:
								this.oMFTerms.Add(oGOTerm);
								break;
							case GONamespaceEnum.CELLULAR_COMPONENT:
								this.oCCTerms.Add(oGOTerm);
								break;
						}

						this.oAllTerms.Add(oGOTerm);
						this.oAllProperties.Add(iGOID, oProperties);
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

					if (this.oBPTerms.Count > TermListVisualizer.MaxAllowedGOListSize)
					{
						this.aErrors.Add(string.Format(sHugeListTemplate, this.oBPTerms.Count,
							GeneOntology.NamespaceToFriendlyString(GONamespaceEnum.BIOLOGICAL_PROCESS), 
							TermListVisualizer.MaxAllowedGOListSize));

						return;
					}
					if (this.oMFTerms.Count > TermListVisualizer.MaxAllowedGOListSize)
					{
						this.aErrors.Add(string.Format(sHugeListTemplate, this.oMFTerms.Count,
							GeneOntology.NamespaceToFriendlyString(GONamespaceEnum.MOLECULAR_FUNCTION), TermListVisualizer.MaxAllowedGOListSize));

						return;
					}
					if (this.oCCTerms.Count > TermListVisualizer.MaxAllowedGOListSize)
					{
						this.aErrors.Add(string.Format(sHugeListTemplate, this.oCCTerms.Count,
							GeneOntology.NamespaceToFriendlyString(GONamespaceEnum.CELLULAR_COMPONENT), 
							TermListVisualizer.MaxAllowedGOListSize));

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
					this.eCurrentNamespace = GONamespaceEnum.BIOLOGICAL_PROCESS;
					this.oBPVisualizer = new TermListVisualizer(this, GONamespaceEnum.BIOLOGICAL_PROCESS, this.oBPTerms.ToArray(),
						this.oAllProperties, oToken, Visualizer_OnProgress);

					if (oToken.IsCancellationRequested)
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
						GOTerm curTerm = this.oBPTerms[i];
						double dDispensability = this.oAllProperties.GetValueByKey(curTerm.ID).Dispensability;
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
					this.eCurrentNamespace = GONamespaceEnum.CELLULAR_COMPONENT;
					this.oCCVisualizer = new TermListVisualizer(this, GONamespaceEnum.CELLULAR_COMPONENT, this.oCCTerms.ToArray(),
						this.oAllProperties, oToken, Visualizer_OnProgress);

					if (oToken.IsCancellationRequested)
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
						GOTerm curTerm = this.oCCTerms[i];
						double dDispensability = this.oAllProperties.GetValueByKey(curTerm.ID).Dispensability;
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
					this.eCurrentNamespace = GONamespaceEnum.MOLECULAR_FUNCTION;
					this.oMFVisualizer = new TermListVisualizer(this, GONamespaceEnum.MOLECULAR_FUNCTION, this.oMFTerms.ToArray(),
						this.oAllProperties, oToken, Visualizer_OnProgress);

					if (oToken.IsCancellationRequested)
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
						GOTerm curTerm = this.oMFTerms[i];
						double dDispensability = this.oAllProperties.GetValueByKey(curTerm.ID).Dispensability;
						if (!double.IsNaN(dDispensability) && dDispensability > this.dCutOff)
						{
							continue;
						}
						iNumNonEliminatedTerms++;
					}

					if (iNumNonEliminatedTerms > MaxNonEliminatedTerms)
					{
						this.aWarnings.Add(
							string.Format("Your resulting list of GO terms for {0} namespace seems to be quite long. If you want to reduce it further, go Back and choose a different size for your resulting list.",
							GeneOntology.NamespaceToFriendlyString(this.oMFVisualizer.Namespace)));
					}

					this.dProgress = this.dProgressPos += this.dProgressSlice;
					this.bRecalculateMF = false;
				}

				if (this.bRecalculateMixed)
				{
					this.dProgress = this.dProgressPos = 95.0;
					this.dProgressSlice = 4.0;
					this.sProgressText = "Calculating data for " + GeneOntology.NamespaceToFriendlyString(GONamespaceEnum.MIXED_NAMESPACE);

					GOTermWordCorpus corpus = new GOTermWordCorpus(this.oAllTerms, this.oOntology);
					this.oEnrichments = corpus.calculateWordEnrichment(this.oAnnotations.WordCorpus, 70, 0);

					if (oToken.IsCancellationRequested)
					{
						this.aErrors.Add("The Revigo didn't finish processing your data in a timely fashion.");
						return;
					}

					if (this.iTermsWithValuesCount != 0)
					{
						GOTermWordCorpus correlCorpus = new GOTermWordCorpus(this.oAllTerms, this.oAllProperties, this.oOntology);
						this.oCorrelations = correlCorpus.getMostFrequentWords(70);
					}

					if (oToken.IsCancellationRequested)
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

				if (this.oBPVisualizer.Terms.Length == 0 && this.oCCVisualizer.Terms.Length == 0 &&
					this.oMFVisualizer.Terms.Length == 0 && this.oAllTerms.Count == 0)
				{
					this.aErrors.Add("Your query has produced no results in any namespace, please return to the input page and correct the error.");
				}

				this.dProgress = 100.0;
				this.sProgressText = "Finished";
			}
			catch (Exception ex)
			{
				this.aErrors.Add("Unknown error has occured.");
				if (!oToken.IsCancellationRequested)
				{
					this.aDevErrors.Add(string.Format("Exception: {0}", ex.Message));
					this.aDevErrors.Add(string.Format("Stack trace: {0}", ex.StackTrace));
				}
			}
			finally
			{
				if (oLogWriter != null)
				{
					try
					{
						oLogWriter.Close();
					}
					catch { }
				}

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
				if (this.eCurrentNamespace != GONamespaceEnum.None)
				{
					sb.AppendFormat(" for {0} namespace", GeneOntology.NamespaceToFriendlyString(this.eCurrentNamespace));
				}
				this.sProgressText = sb.ToString();
			}
		}
	}
}
