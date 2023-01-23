using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;
using IRB.Collections.Generic;
using System.Threading;

namespace IRB.Revigo.Databases
{
	/// <summary>
	/// A class holding a collection of annotations for all supported species.
	/// 
	/// This class is parallelized when constructing new species annotations.
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
	public class SpeciesAnnotationsList
	{
		private DateTime dtDate = new DateTime(0);
		private string sLink = null;
		private List<SpeciesAnnotations> aItems = new List<SpeciesAnnotations>();

		public SpeciesAnnotationsList()
		{ }

		public SpeciesAnnotationsList(ICollection<SpeciesAnnotations> collection)
		{
			this.aItems = new List<SpeciesAnnotations>(collection);
		}

		/// <summary>
		/// Makes serialized GoTermSizes objects for a number of model organisms
		/// used in REVIGO. Starts from the "goa_uniprot_gcrp.gaf.gz" file
		/// from the Uniprot GOA project.
		/// Loads the Gene Ontology category sizes by parsing an EBI GO Annotation
		/// file (GOA). These files are quite large so this will be slow. You may
		/// limit this loading to a set of species by their taxId.
		/// Use Serialize/Deserialize to instantiate GoTermSizes objects much faster.
		/// </summary>
		/// <param name="goaPath">
		/// Path to the GOA database file. The output directory will be: path to the GOA database + \Output\
		/// </param>
		/// <param name="goPath">Path to the GeneOntology database</param>
		/// <param name="ncbiPath">Path to the NCBI species database "names.dmp"</param>
		/// <param name="taxonIDs">Array of taxon IDs of the organism to process</param>
		public static SpeciesAnnotationsList FromGOA(string goaPath, string goPath, string ncbiPath, int[] taxonIDs)
		{
			SpeciesAnnotationsList result = new SpeciesAnnotationsList();

			// in how many batches we will distribute this work (physical CPU count)
			// take care if you have turned HyperThreading on!
			int iCPUCount = Environment.ProcessorCount;

			if (goaPath.EndsWith(".gz", StringComparison.CurrentCultureIgnoreCase))
			{
				throw new Exception("The GOA file can't be compressed");
			}

			StreamWriter log = new StreamWriter(string.Format("{0}{1}TermSizes.log", Path.GetDirectoryName(goaPath), Path.DirectorySeparatorChar));

			#region Parse GOA Link
			StreamReader linkReader = null;
			string sLinkPath = string.Format("{0}{1}link.txt", Path.GetDirectoryName(goaPath), Path.DirectorySeparatorChar);
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

					result.sLink = sLine;
					linkReader.Close();
				}
				catch { }
			}
			#endregion

			#region Parse NCBI species database
			Console.WriteLine("Parsing NCBI database");
			DateTime dtStart = DateTime.Now;
			BDictionary<int, string> oNCBINames = new BDictionary<int, string>();
			ParseNCBI(ncbiPath, oNCBINames);
			Console.WriteLine("Time: {0:hh\\:mm\\:ss}  \r", DateTime.Now - dtStart);
			#endregion

			#region Parse Gene ontology database
			Console.WriteLine("Parsing GeneOntology database");
			dtStart = DateTime.Now;
			GeneOntology go = new GeneOntology(goPath);
			Console.WriteLine("Time: {0:hh\\:mm\\:ss}  \r", DateTime.Now - dtStart);
			#endregion

			// process the GOA database
			Console.WriteLine("Parsing GOA database with {0} CPU(s)", iCPUCount);
			dtStart = DateTime.Now;

			// slice the goa input into batches
			long lGOAFilePosition = 0;
			long lGOAFileBatchPosition = 0;
			long lGOABatchSize = 50 * 1024 * 1024; // process input file in batches of 50MB

			// determine file size
			FileInfo info = new FileInfo(goaPath);
			long lGOAFileSize = info.Length;

			Console.WriteLine("GOA database size: {0} bytes", lGOAFileSize);

			BDictionary<int, BDictionary<int, int>> oSpeciesAnnotations = new BDictionary<int, BDictionary<int, int>>();
			List<GOABatchWorker> aGOAWorkers = new List<GOABatchWorker>();

			double dProgressPos = 0.0;
			double dProgressStep = 0.0;

			while (lGOAFilePosition < lGOAFileSize || aGOAWorkers.Count > 0)
			{
				while (aGOAWorkers.Count < iCPUCount && lGOAFileBatchPosition < lGOAFileSize)
				{
					long lBatchStart = lGOAFileBatchPosition;
					long lBatchEnd = lGOAFileBatchPosition + lGOABatchSize;
					if (lBatchEnd > lGOAFileSize)
					{
						lBatchEnd = lGOAFileSize;
					}
					else
					{
						lBatchEnd = GOABatchWorker.AlignFilePosition(goaPath, lBatchEnd);
					}

					GOABatchWorker worker = new GOABatchWorker(go, oNCBINames, goaPath, lBatchStart, lBatchEnd);
					worker.Start();
					aGOAWorkers.Add(worker);
					lGOAFileBatchPosition = lBatchEnd;
				}

				double dProgressSum = 0.0;
				bool bFirst = true; // limit merge to one worker at a time

				for (int i = 0; i < aGOAWorkers.Count; i++)
				{
					if (bFirst && !aGOAWorkers[i].Working)
					{
						// Merge results
						GOABatchWorker worker = aGOAWorkers[i];

						if (!worker.GOADate.Equals(DateTime.MinValue))
						{
							result.dtDate = worker.GOADate;
						}

						BDictionary<int, BDictionary<int, int>> annotations = worker.SpeciesAnnotations;
						for (int j = 0; j < annotations.Count; j++)
						{
							if (oSpeciesAnnotations.ContainsKey(annotations[j].Key))
							{
								BDictionary<int, int> oldAnnotations = oSpeciesAnnotations.GetValueByKey(annotations[j].Key);
								BDictionary<int, int> newAnnotations = annotations[j].Value;

								for (int k = 0; k < newAnnotations.Count; k++)
								{
									int termID = newAnnotations[k].Key;
									int iAnnotationCount = newAnnotations[k].Value;

									int iIndex = oldAnnotations.IndexOfKey(termID);
									if (iIndex >= 0)
									{
										iAnnotationCount += oldAnnotations[iIndex].Value;
										oldAnnotations[iIndex] = new BKeyValuePair<int, int>(termID, iAnnotationCount);
									}
									else
									{
										oldAnnotations.Add(termID, iAnnotationCount);
									}
								}
							}
							else
							{
								oSpeciesAnnotations.Add(annotations[j]);
							}
						}

						lGOAFilePosition += worker.BatchSize;
						aGOAWorkers.RemoveAt(i);
						i--;
						bFirst = false;
					}
					else
					{
						dProgressSum += aGOAWorkers[i].Progress;
					}
				}

				dProgressSum /= (double)aGOAWorkers.Count;
				dProgressSum /= 100.0;
				double dProgress = (((double)lGOAFilePosition + (double)(lGOAFileBatchPosition - lGOAFilePosition) * dProgressSum) / (double)lGOAFileSize) * 100.0;
				if ((dProgress - dProgressPos) > 0.01)
				{
					dProgressPos = dProgress;
					Console.Write("{0:##0.00}% {1:hh\\:mm\\:ss}  \r", dProgressPos, DateTime.Now - dtStart);
				}
				Thread.Sleep(100);
			}

			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
			Console.WriteLine("Time: {0:hh\\:mm\\:ss}  \r", DateTime.Now - dtStart);

			// distibute creation of SpeciesAnnotations objects in batches
			Console.WriteLine("Creating species annotation objects with {0} CPU(s)", iCPUCount);
			dtStart = DateTime.Now;

			BDictionary<int, SpeciesAnnotations> oSpeciesAnnotationsObjects = new BDictionary<int, SpeciesAnnotations>();
			List<SpeciesAnnotationsBatchWorker> aSpeciesAnnotationsWorkers = new List<SpeciesAnnotationsBatchWorker>();
			// determine list of all the taxons stored in the Species annotation object
			int[] aSpeciesTaxons = oSpeciesAnnotations.Keys.ToArray();
			int iTaxonPosition = 0;
			int iBatchTaxonPosition = 0;

			
			dProgressPos = 0.0;
			dProgressStep = 100.0 / aSpeciesTaxons.Length;

			while (iTaxonPosition < aSpeciesTaxons.Length || aSpeciesAnnotationsWorkers.Count > 0)
			{
				while (aSpeciesAnnotationsWorkers.Count < iCPUCount && iBatchTaxonPosition < aSpeciesTaxons.Length)
				{
					int iTaxon = aSpeciesTaxons[iBatchTaxonPosition];
					string sSpeciesName;

					if (iTaxon == 0)
					{
						sSpeciesName = "Whole UniProt database (default)";
					}
					else
					{
						sSpeciesName = oNCBINames.GetValueByKey(iTaxon);
					}
					SpeciesAnnotationsBatchWorker worker = new SpeciesAnnotationsBatchWorker(go, iTaxon, sSpeciesName, oSpeciesAnnotations[iBatchTaxonPosition].Value);
					worker.Start();
					aSpeciesAnnotationsWorkers.Add(worker);
					iBatchTaxonPosition++;
				}

				double dProgressSum = 0.0;
				bool bFirst = true; // limit merge to one worker at a time

				for (int i = 0; i < aSpeciesAnnotationsWorkers.Count; i++)
				{
					if (bFirst && !aSpeciesAnnotationsWorkers[i].Working)
					{
						// Merge results
						SpeciesAnnotations speciesAnnotations = aSpeciesAnnotationsWorkers[i].SpeciesAnnotations;
						oSpeciesAnnotationsObjects.Add(speciesAnnotations.TaxonID, speciesAnnotations);

						iTaxonPosition++;
						aSpeciesAnnotationsWorkers.RemoveAt(i);
						i--;
						bFirst = false;
					}
					else
					{
						dProgressSum += aSpeciesAnnotationsWorkers[i].Progress;
					}
				}

				dProgressSum /= (double)aSpeciesAnnotationsWorkers.Count;
				dProgressSum /= 100.0;
				double dProgress = (((double)iTaxonPosition + (double)(iBatchTaxonPosition - iTaxonPosition) * dProgressSum) / (double)aSpeciesTaxons.Length) * 100.0;
				if ((dProgress - dProgressPos) > 0.01)
				{
					dProgressPos = dProgress;
					TimeSpan tsElapsed = DateTime.Now - dtStart;
					Console.Write("{0:##0.00}% {1:hh\\:mm\\:ss}  \r", dProgressPos, tsElapsed);
				}

				Thread.Sleep(100);
			}
			Console.WriteLine("Time: {0:hh\\:mm\\:ss}  \r", DateTime.Now - dtStart);

			Console.WriteLine("Finishing");
			int iMaxAnnotations = 0;
			for (int i = 0; i < oSpeciesAnnotationsObjects.Count; i++)
			{
				int iSpecies = oSpeciesAnnotationsObjects[i].Key;
				if (iSpecies > 0)
				{
					BDictionary<int, int> annotations = oSpeciesAnnotationsObjects[i].Value.Annotations;

					for (int j = 0; j < annotations.Count; j++)
					{
						iMaxAnnotations = Math.Max(iMaxAnnotations, annotations[j].Value);
					}
				}
			}

			log.WriteLine("\"Taxon ID\"\tSpecies\t\"Gene count\"\t\"Maximum annotations\"\tScore\t\"Overall score\"");
			for (int i = 0; i < oSpeciesAnnotationsObjects.Count; i++)
			{
				int iSpecies = oSpeciesAnnotationsObjects[i].Key;
				if (iSpecies > 0)
				{
					BDictionary<int, int> annotations = oSpeciesAnnotationsObjects[i].Value.Annotations;
					int iLocalMaxAnnotations = 0;
					for (int j = 0; j < annotations.Count; j++)
					{
						iLocalMaxAnnotations = Math.Max(iLocalMaxAnnotations, annotations[j].Value);
					}

					double dSum = 0.0;
					for (int j = 0; j < annotations.Count; j++)
					{
						dSum += (double)annotations[j].Value / (double)iLocalMaxAnnotations;
					}
					dSum /= (double)annotations.Count;

					log.WriteLine("{0}\t{1}\t{2}\t{3}\t{4:##0.000000}\t{5:##0.000000}", iSpecies, oNCBINames.GetValueByKey(iSpecies), annotations.Count,
						iLocalMaxAnnotations, dSum, dSum * ((double)iLocalMaxAnnotations / (double)iMaxAnnotations));
				}
			}

			log.WriteLine("--- Obsolete terms ---");
			for (int i = 0; i < oSpeciesAnnotationsObjects.Count; i++)
			{
				int iSpecies = oSpeciesAnnotationsObjects[i].Key;
				BDictionary<int, int> annotations = oSpeciesAnnotationsObjects[i].Value.Annotations;

				for (int j = 0; j < annotations.Count; j++)
				{
					GOTerm term = go.GetValueByKey(annotations[j].Key);
					if (term.IsObsolete)
					{
						log.WriteLine("{0}\t{1}\t{2}", iSpecies, term.FormattedID, annotations[j].Value);
					}
				}
			}

			for (int i = 0; i < taxonIDs.Length; i++)
			{
				if (oSpeciesAnnotationsObjects.ContainsKey(taxonIDs[i]))
				{
					result.aItems.Add(oSpeciesAnnotationsObjects.GetValueByKey(taxonIDs[i]));
				}
			}

			log.Close();

			return result;
		}

		private static void ParseNCBI(string path, BDictionary<int, string> names)
		{
			int iOldTaxonID = -1;
			string sSpeciesName = null;
			bool bScientificName = false;

			StreamReader ncbiReader = new StreamReader(path);

			while (!ncbiReader.EndOfStream)
			{
				string sLine = ncbiReader.ReadLine();
				if (string.IsNullOrEmpty(sLine))
					continue;

				string[] aColumns = sLine.Split('|');
				int iTaxonID;
				if (aColumns.Length < 4 || !int.TryParse(aColumns[0].Trim(), out iTaxonID))
					continue;

				if (iTaxonID != iOldTaxonID)
				{
					if (iOldTaxonID > 0 && !string.IsNullOrEmpty(sSpeciesName))
					{
						names.Add(iOldTaxonID, sSpeciesName);
					}
					iOldTaxonID = iTaxonID;
					sSpeciesName = null;
					bScientificName = false;
				}
				switch (aColumns[3].Trim().ToLower())
				{
					case "scientific name":
						if (!bScientificName)
						{
							bScientificName = true;
							sSpeciesName = aColumns[1].Trim();
						}
						break;
					case "synonym":
						if (!bScientificName)
						{
							sSpeciesName = aColumns[1].Trim();
						}
						break;
					default:
						break;
				}
			}
			if (iOldTaxonID > 0 && !string.IsNullOrEmpty(sSpeciesName))
			{
				names.Add(iOldTaxonID, sSpeciesName);
			}

			ncbiReader.Close();
		}

		private class GOABatchWorker
		{
			private GeneOntology oOntology;
			private BDictionary<int, string> oNCBINames;
			private BDictionary<int, BDictionary<int, int>> oSpeciesAnnotations = new BDictionary<int, BDictionary<int, int>>();
			private BDictionary<int, BHashSet<int>> oSpeciesAlreadyAnnotated = new BDictionary<int, BHashSet<int>>();
			private string sPath;
			private long lBatchStart;
			private long lBatchEnd;

			private bool bWorking = false;
			private Thread oWorker = null;

			private DateTime dtGoaDate = DateTime.MinValue;
			private double dProgress = 0.0;

			public GOABatchWorker(GeneOntology ontology, BDictionary<int, string> ncbiNames, string path, long batchStart, long batchEnd)
			{
				this.oOntology = ontology;
				this.oNCBINames = ncbiNames;
				this.sPath = path;
				this.lBatchStart = batchStart;
				this.lBatchEnd = batchEnd;
			}

			public static long AlignFilePosition(string path, long initialPosition)
			{
				long lAlignedPosition = initialPosition;
				FileStream goaStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				goaStream.Seek(initialPosition, SeekOrigin.Begin);
				BufferedReader goaReader = new BufferedReader(goaStream, 65536);

				// synchronize with new line, discard the first line as its start is unpredictable
				goaReader.ReadLine();

				string sLastObjectID = null;
				string sLastLine = null;

				while (!goaReader.EndOfStream)
				{
					long lGOAPosition = goaReader.Position; // get the position of the current line
					string sLine = goaReader.ReadLine();
					sLastLine = sLine;

					if (sLine != null)
						sLine = sLine.Trim();

					if (string.IsNullOrEmpty(sLine))
						continue;

					if (sLine.StartsWith("!") || !sLine.StartsWith("UniProtKB", StringComparison.CurrentCultureIgnoreCase))
						continue;

					// cols[1] is the protein ID
					// cols[4] is the GO ID (only a single one)
					// cols[12] is the species taxon(s)
					string[] aColumns = sLine.Split(new char[] { '\t' });

					// for this to work, the annotations for different genes have to be contiguous
					if (!aColumns[1].Equals(sLastObjectID, StringComparison.CurrentCultureIgnoreCase))
					{
						if (sLastObjectID != null)
						{
							lAlignedPosition = lGOAPosition;
							break;
						}
						sLastObjectID = aColumns[1];
					}
				}

				if (goaReader.EndOfStream)
					lAlignedPosition = goaReader.Length;

				goaReader.Close();
				goaStream.Close();

				return lAlignedPosition;
			}

			public bool Working
			{
				get
				{
					return this.bWorking;
				}
			}

			public double Progress
			{
				get
				{
					return this.dProgress;
				}
			}

			public BDictionary<int, BDictionary<int, int>> SpeciesAnnotations
			{
				get
				{
					return this.oSpeciesAnnotations;
				}
			}

			public DateTime GOADate
			{
				get
				{
					return this.dtGoaDate;
				}
			}

			public long BatchSize
			{
				get
				{
					return this.lBatchEnd - this.lBatchStart;
				}
			}

			public void Start()
			{
				if (!this.bWorking)
				{
					this.bWorking = true;

					this.oWorker = new Thread(new ThreadStart(ThreadWorker));
					this.oWorker.Start();
				}
			}

			private void ThreadWorker()
			{
				this.dProgress = 0.0;

				FileStream goaStream = new FileStream(this.sPath, FileMode.Open, FileAccess.Read, FileShare.Read);
				goaStream.Seek(this.lBatchStart, SeekOrigin.Begin);
				BufferedReader goaReader = new BufferedReader(goaStream, 524288);
				
				double dProgressStep = 100.0 / (double)(((this.lBatchEnd < 0) ? goaReader.Length : this.lBatchEnd) - this.lBatchStart);

				bool bHeader = this.lBatchStart == 0;
				string sLastObjectID = null;
				int iLastGOID = -1;

				this.oSpeciesAnnotations.Clear();
				this.oSpeciesAlreadyAnnotated.Clear();

				while (!goaReader.EndOfStream && (this.lBatchEnd < 0 || goaReader.Position < this.lBatchEnd))
				{
					string sLine = goaReader.ReadLine();

					if (sLine != null)
						sLine = sLine.Trim();

					if (string.IsNullOrEmpty(sLine))
						continue;

					if (bHeader && sLine.StartsWith("!Generated:", StringComparison.CurrentCultureIgnoreCase))
					{
						DateTime dtGoaDate;
						if (DateTime.TryParseExact(sLine.Substring(12), "yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture,
							DateTimeStyles.NoCurrentDateDefault, out dtGoaDate))
						{
							this.dtGoaDate = dtGoaDate;
						}
					}

					if (bHeader && sLine.StartsWith("!date-generated:", StringComparison.CurrentCultureIgnoreCase))
					{
						DateTime dtGoaDate;
						if (DateTime.TryParseExact(sLine.Substring(17), "yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture,
							DateTimeStyles.NoCurrentDateDefault, out dtGoaDate))
						{
							this.dtGoaDate = dtGoaDate;
						}
					}

					if (sLine.StartsWith("!") || !sLine.StartsWith("UniProtKB", StringComparison.CurrentCultureIgnoreCase))
						continue;

					bHeader = false;

					// cols[1] is the protein ID
					// cols[4] is the GO ID (only a single one)
					// cols[12] is the species taxon(s)
					string[] aColumns = sLine.Split(new char[] { '\t' });
					int iGOID;
					int iSpeciesTaxon;
					string[] aSpeciesTaxons = aColumns[12].Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
					bool bError = false;

					// for debugging, remove afterwards
					//if (aSpeciesTaxons.Length > 1)
						//continue;

					if (!int.TryParse(aColumns[4].Substring(3), out iGOID))
					{
						bError = true;
					}
					if (!int.TryParse(aSpeciesTaxons[0].Substring(6), out iSpeciesTaxon))
					{
						bError = true;
					}
					if (bError)
					{
						Console.WriteLine("STRANGE ROW: '{0}'", sLine);
						continue;
					}

					// sometimes lines with annotation repeat to account for different
					// sources of annotation (we don't care about the source here)
					if (aColumns[1].Equals(sLastObjectID, StringComparison.CurrentCultureIgnoreCase) && iGOID == iLastGOID)
						continue;

					// for this to work, the annotations for different genes have to be contiguous
					if (!aColumns[1].Equals(sLastObjectID, StringComparison.CurrentCultureIgnoreCase))
					{
						for (int i = 0; i < oSpeciesAlreadyAnnotated.Count;i++ )
						{
							BHashSet<int> aBuffer = oSpeciesAlreadyAnnotated[i].Value;

							if (aBuffer != null && aBuffer.Count > 0)
							{
								aBuffer.Clear();
							}
						}

						sLastObjectID = aColumns[1];
					}

					iLastGOID = iGOID;

					if (this.oOntology.ContainsKey(iGOID))
					{
						// translate term ID to current ID (alternate ID, replacement, obsolete...)
						GOTerm term = this.oOntology.GetValueByKey(iGOID);

						AnnotateTerm(0, term.ID);

						if (iSpeciesTaxon > 0 && oNCBINames.ContainsKey(iSpeciesTaxon))
						{
							AnnotateTerm(iSpeciesTaxon, term.ID);
						}
					}

					this.dProgress = dProgressStep * (goaReader.Position - this.lBatchStart);
				}

				goaReader.Close();
				goaStream.Close();

				this.bWorking = false;
			}

			private void AnnotateTerm(int speciesTaxon, int termID)
			{
				BDictionary<int, int> annotations;
				BHashSet<int> alreadyAnnotated;

				if (this.oSpeciesAnnotations.ContainsKey(speciesTaxon))
				{
					annotations = this.oSpeciesAnnotations.GetValueByKey(speciesTaxon);
				}
				else
				{
					annotations = new BDictionary<int, int>();
					this.oSpeciesAnnotations.Add(speciesTaxon, annotations);
				}

				if (this.oSpeciesAlreadyAnnotated.ContainsKey(speciesTaxon))
				{
					alreadyAnnotated = this.oSpeciesAlreadyAnnotated.GetValueByKey(speciesTaxon);
				}
				else
				{
					alreadyAnnotated = new BHashSet<int>();
					this.oSpeciesAlreadyAnnotated.Add(speciesTaxon, alreadyAnnotated);
				}

				// do our thing, assign annotations
				BHashSet<int> allTermParents = this.oOntology.GetValueByKey(termID).AllParents;

				if (!alreadyAnnotated.Contains(termID))
				{
					int iAnnotationCount = 1;
					int iIndex = annotations.IndexOfKey(termID);
					if (iIndex >= 0)
					{
						iAnnotationCount += annotations[iIndex].Value;
						annotations[iIndex] = new BKeyValuePair<int, int>(termID, iAnnotationCount);
					}
					else
					{
						annotations.Add(termID, iAnnotationCount);
					}
					alreadyAnnotated.Add(termID);
				}

				for (int k = 0; k < allTermParents.Count; k++)
				{
					int iParentGOID = allTermParents[k];

					if (!alreadyAnnotated.Contains(iParentGOID))
					{
						int iAnnotationCount = 1;
						int iIndex = annotations.IndexOfKey(iParentGOID);
						if (iIndex >= 0)
						{
							iAnnotationCount += annotations[iIndex].Value;
							annotations[iIndex] = new BKeyValuePair<int, int>(iParentGOID, iAnnotationCount);
						}
						else
						{
							annotations.Add(iParentGOID, iAnnotationCount);
						}
						alreadyAnnotated.Add(iParentGOID);
					}
				}
			}
		}

		private class SpeciesAnnotationsBatchWorker
		{
			private GeneOntology oOntology;
			private int iTaxonID;
			private string sSpeciesName;
			private BDictionary<int, int> oAnnotations;
			private BDictionary<int, double> oNormalizedAnnotations;

			// resulting object
			private SpeciesAnnotations oSpeciesAnnotations = null;

			private bool bWorking = false;
			private Thread oWorker = null;
			private double dProgress = 0.0;

			public SpeciesAnnotationsBatchWorker(GeneOntology ontology, int taxonID, string speciesName, BDictionary<int, int> annotations)
			{
				this.oOntology = ontology;
				this.iTaxonID = taxonID;
				this.sSpeciesName = speciesName;
				this.oAnnotations = annotations;
				this.oNormalizedAnnotations = new BDictionary<int, double>(this.oAnnotations.Count);
			}

			public SpeciesAnnotations SpeciesAnnotations
			{
				get
				{
					return this.oSpeciesAnnotations;
				}
			}

			public bool Working
			{
				get
				{
					return this.bWorking;
				}
			}

			public double Progress
			{
				get
				{
					return this.dProgress;
				}
			}

			public void Start()
			{
				if (!this.bWorking)
				{
					this.bWorking = true;

					this.oWorker = new Thread(new ThreadStart(ThreadWorker));
					this.oWorker.Start();
				}
			}

			private void ThreadWorker()
			{
				this.dProgress = 0.0;
				double dProgressStep = 99.0 / (double)this.oAnnotations.Count;

				for (int i = 0; i < this.oAnnotations.Count; i++)
				{
					int termID = this.oAnnotations[i].Key;
					GOTerm rootGOTerm = this.oOntology.GetValueByKey(termID).TopmostParent;

					this.oNormalizedAnnotations.Add(termID, (double)this.oAnnotations[i].Value / (double)this.oAnnotations.GetValueByKey(rootGOTerm.ID));
					this.dProgress = (double)i * dProgressStep;
				}

				this.dProgress = 99.0;
				this.oSpeciesAnnotations = new SpeciesAnnotations(this.oOntology, this.iTaxonID, this.sSpeciesName, this.oAnnotations, this.oNormalizedAnnotations);
				this.dProgress = 100.0;

				this.bWorking = false;
			}
		}

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

		public List<SpeciesAnnotations> Items
		{
			get
			{
				return this.aItems; 
			}
		}

		public SpeciesAnnotations GetByID(int id)
		{
			for (int i = 0; i < this.aItems.Count; i++)
			{
				if (this.aItems[i].TaxonID == id)
				{
					return this.aItems[i];
				}
			}

			return null;
		}

		public bool ContainsID(int id)
		{
			for (int i = 0; i < this.aItems.Count; i++)
			{
				if (this.aItems[i].TaxonID == id)
				{
					return true;
				}
			}

			return false;
		}

		public void SortByName()
		{
			this.aItems.Sort(CompareSpeciesAnnotationsByName);
		}

		private static int CompareSpeciesAnnotationsByName(SpeciesAnnotations obj1, SpeciesAnnotations obj2)
		{
			if (obj1 == null && obj2 == null)
			{
				return 0;
			}
			if (obj1 == null)
			{
				return -1;
			}
			if (obj2 == null)
			{
				return 1;
			}

			if (obj1.TaxonID == 0 && obj2.TaxonID == 0)
			{
				return 0;
			}
			if (obj1.TaxonID == 0)
			{
				return -1;
			}
			if (obj2.TaxonID == 0)
			{
				return 1;
			}

			return obj1.SpeciesName.CompareTo(obj2.SpeciesName);
		}

		/// <summary>
		/// Deserializes a GoTermSizes object.
		/// </summary>
		/// <param name="path">Can be a path ending in "goa_yyyy_mm_dd"</param>
		/// <param name="taxonID">A NCBI Taxonomy ID for the organism to load; pass 0 for the
		/// entire Uniprot database.</param>
		/// <returns>A deserialized GoTermSizes object.</returns>
		public static SpeciesAnnotationsList Deserialize(string path, bool gzipped)
		{
			return Deserialize(path + (gzipped ? ".gz" : ""));
		}

		/// <summary>
		/// Deserializes a GoTermSizes object.
		/// Assumes file iz gzipped, if the filename ends with .gz
		/// </summary>
		/// <param name="filePath">A full path to a object xml file.</param>
		/// <returns>A deserialized GoTermSizes object.</returns>
		public static SpeciesAnnotationsList Deserialize(string filePath)
		{
			StreamReader reader;
			if (filePath.EndsWith(".gz"))
			{
				reader = new StreamReader(new GZipStream(new BufferedStream(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), 65536), CompressionMode.Decompress));
			}
			else
			{
				reader = new StreamReader(new BufferedStream(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), 65536));
			}

			return Deserialize(reader);
		}

		/// <summary>
		/// Deserializes a GoTermSizes object.
		/// </summary>
		/// <param name="reader">A stream to read the object from.</param>
		/// <returns>A deserialized GoTermSizes object.</returns>
		public static SpeciesAnnotationsList Deserialize(StreamReader reader)
		{
			XmlSerializer ser = new XmlSerializer(typeof(SpeciesAnnotationsList));
			SpeciesAnnotationsList newObj = (SpeciesAnnotationsList)ser.Deserialize(reader);

			return newObj;
		}

		/// <summary>
		/// Serializes a GoTermSizes object.
		/// </summary>
		/// <param name="path">Can be a path ending in "goa_yyyy_mm_dd"</param>
		/// <param name="taxonID">A NCBI Taxonomy ID of the organism to save; 0 for the
		/// entire Uniprot database.</param>
		public void Serialize(string path, bool gzipped)
		{
			Serialize(path + (gzipped ? ".gz" : ""));
		}

		/// <summary>
		/// Serializes a GoTermSizes object.
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
		}

		/// <summary>
		/// Serializes a GoTermSizes object.
		/// </summary>
		/// <param name="writer">A stream to serialize the object to.</param>
		public void Serialize(StreamWriter writer)
		{
			XmlSerializer ser = new XmlSerializer(typeof(SpeciesAnnotationsList));
			ser.Serialize(writer, this);
		}
	}
}
