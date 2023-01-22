using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRB.JSON
{
	public class JSONNumber : JSONValue<double>
	{
		public JSONNumber()
		{
			this.oValue = 0.0;
		}

		public JSONNumber(double value)
		{
			this.oValue = value;
		}

		override public string Serialize()
		{
			// we have to handle number states as string here, because JSON is not aware of them
			// output them as string so the aware client is able to parse them properly
			if (double.IsNaN(this.oValue))
				return "\"NaN\"";
			if(double.IsNegativeInfinity(this.oValue))
				return "\"-Infinity\"";
			if (double.IsPositiveInfinity(this.oValue))
				return "\"Infinity\"";

			return (this.oValue).ToString(CultureInfo.InvariantCulture);
		}

		override public void Serialize(Stream stream)
		{
			byte[] aBuffer = Encoding.UTF8.GetBytes(this.Serialize());
			stream.Write(aBuffer, 0, aBuffer.Length);
		}
	}
}
