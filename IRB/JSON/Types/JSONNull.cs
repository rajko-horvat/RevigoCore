using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRB.JSON
{
	public class JSONNull : JSONValue<object>
	{
		public JSONNull()
		{
			base.oValue = null;
		}

		override public string Serialize()
		{
			return "null";
		}

		override public void Serialize(Stream stream)
		{
			byte[] aBuffer = Encoding.UTF8.GetBytes(this.Serialize());
			stream.Write(aBuffer, 0, aBuffer.Length);
		}

		public override object Value { get { return this.oValue; } set { } }
	}
}
