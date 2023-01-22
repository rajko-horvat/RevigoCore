using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IRB.JSON
{
	public abstract class JSONValue<T>: IJSONSerialize
	{
		protected T oValue;

		public abstract string Serialize();

		public abstract void Serialize(Stream stream);

		public virtual T Value
		{
			get { return this.oValue; }
			set { this.oValue = value; }
		}
	}
}
