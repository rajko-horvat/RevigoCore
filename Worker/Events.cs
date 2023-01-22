using System;
using System.Collections.Generic;
using System.Text;

namespace IRB.Revigo
{
	public class ProgressEventArgs : EventArgs
	{
		private double dProgress;
		private string sDescription = null;

		public ProgressEventArgs(double progress)
			: this(progress, null)
		{ }

		public ProgressEventArgs(double progress, string description)
		{
			this.dProgress = progress;
			this.sDescription = description;
		}

		public double Progress
		{
			get
			{
				return this.dProgress;
			}
		}

		public string Description
		{
			get
			{
				return this.sDescription;
			}
		}
	}

	public delegate void ProgressEventHandler(object sender, ProgressEventArgs e);
}
