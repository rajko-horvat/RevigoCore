using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRB.Revigo
{
	public class RevigoJob
	{
		private int iID = -1;
		private DateTime dtExpiration;
		private RevigoWorker oWorker;

		public RevigoJob(int id, DateTime expiration, RevigoWorker worker)
		{
			this.iID = id;
			this.dtExpiration = expiration;
			this.oWorker = worker;
		}

		public int ID
		{
			get
			{
				return this.iID;
			}
		}

		public DateTime Expiration
		{
			get
			{
				return this.dtExpiration;
			}
		}

		public RevigoWorker Worker
		{
			get
			{
				return this.oWorker;
			}
		}

		public void ExtendExpiration(int minutes)
		{
			DateTime newExpiration = DateTime.Now.AddMinutes(minutes);

			if (this.dtExpiration < newExpiration)
				this.dtExpiration = newExpiration;
		}
	}
}
