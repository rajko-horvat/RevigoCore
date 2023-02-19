using IRB.Revigo.Core.Databases;

namespace IRB.Revigo.Core
{
	/// <summary>
	/// This class combines Gene Ontology Term with temporary properties used for clustering and visualizations
	/// </summary>
	public class RevigoTerm : IComparable<RevigoTerm>, IComparer<RevigoTerm>, IEqualityComparer<RevigoTerm>
	{
		private GeneOntologyTerm oGOTerm;

		private double dValue = double.NaN;
		private double dTransformedValue = double.NaN;
		private double dUniqueness = double.NaN;
		private double dDispensability = double.NaN;
		private double dAnnotationSize = double.NaN;
		private double dLogAnnotationSize = double.NaN;
		private double dAnnotationFrequency = double.NaN;
		private bool bPinned = false;
		private int iRepresentativeID = -1;
		private int iDispensedByID = -1;
		private List<double> aPC = new List<double>();
		private List<double> aPC3 = new List<double>();
		private List<double> aUserValues = new List<double>();

		public RevigoTerm( GeneOntologyTerm term)
		{
			this.oGOTerm = term;
		}

		public GeneOntologyTerm GOTerm
		{
			get { return this.oGOTerm; }
		}

		public double Value
		{
			get
			{
				return this.dValue;
			}
			set
			{
				this.dValue = value;
			}
		}

		public double TransformedValue
		{
			get
			{
				return this.dTransformedValue;
			}
			set
			{
				this.dTransformedValue = value;
			}
		}

		public double Uniqueness
		{
			get
			{
				return this.dUniqueness;
			}
			set
			{
				this.dUniqueness = value;
			}
		}

		public double Dispensability
		{
			get
			{
				return this.dDispensability;
			}
			set
			{
				this.dDispensability = value;
			}
		}

		public double AnnotationSize
		{
			get
			{
				return this.dAnnotationSize;
			}
			set
			{
				this.dAnnotationSize = value;
			}
		}

		public double LogAnnotationSize
		{
			get
			{
				return this.dLogAnnotationSize;
			}
			set
			{
				this.dLogAnnotationSize = value;
			}
		}

		public double AnnotationFrequency
		{
			get
			{
				return this.dAnnotationFrequency;
			}
			set
			{
				this.dAnnotationFrequency = value;
			}
		}

		public bool Pinned
		{
			get
			{
				return this.bPinned;
			}
			set
			{
				this.bPinned = value;
			}
		}

		public int RepresentativeID
		{
			get
			{
				return this.iRepresentativeID;
			}
			set
			{
				this.iRepresentativeID = value;
			}
		}

		public int DispensedByID
		{
			get
			{
				return this.iDispensedByID;
			}
			set
			{
				this.iDispensedByID = value;
			}
		}

		public List<double> PC
		{
			get
			{
				return this.aPC;
			}
		}

		public List<double> PC3
		{
			get
			{
				return this.aPC3;
			}
		}

		public List<double> UserValues
		{
			get
			{
				return this.aUserValues;
			}
		}

		/// <summary>
		/// Two nodes are equal if they have equal unique IDs
		/// </summary>
		/// <param name="obj">A GeneOntologyTerm to compare to</param>
		/// <returns>True if two nodes have the same unique ID</returns>
		public override bool Equals(object? obj)
		{
			if (obj == null || (!obj.GetType().Equals(typeof(RevigoTerm)) && !obj.GetType().IsSubclassOf(typeof(RevigoTerm))))
			{
				return false;
			}
			RevigoTerm other = (RevigoTerm)obj;
			if (this.GOTerm.ID != other.GOTerm.ID)
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
			return this.GOTerm.ID;
		}

		public override string ToString()
		{
			return string.Format("{0}:{1}", this.GOTerm.ID, this.GOTerm.Name);
		}

		#region IComparable<RevigoTerm> Members

		public int CompareTo(RevigoTerm? other)
		{
			return (other == null) ? 1 : this.GOTerm.ID.CompareTo(other.GOTerm.ID);
		}
		#endregion

		#region IComparer<RevigoTerm> Members

		public int Compare(RevigoTerm? x, RevigoTerm? y)
		{
			if (x == null && y == null)
				return 0;
			if (x == null)
				return -1;
			if (y == null)
				return 1;

			return x.GOTerm.ID.CompareTo(y.GOTerm.ID);
		}
		#endregion

		#region IEqualityComparer<RevigoTerm> Members

		public bool Equals(RevigoTerm? x, RevigoTerm? y)
		{
			if (x == null && y == null)
				return true;
			if (x == null || y == null)
				return false;

			return x.GOTerm.ID == y.GOTerm.ID;
		}

		public int GetHashCode(RevigoTerm obj)
		{
			return obj.GetHashCode();
		}

		#endregion
	}
}
