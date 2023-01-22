using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IRB.JSON
{
	public class JSONBoolean : JSONValue<bool>
	{
		public JSONBoolean()
		{
			this.oValue = false;
		}

		public JSONBoolean(bool value)
		{
			this.oValue = value;
		}

		override public string Serialize()
		{
			if (this.oValue)
			{
				return "true";
			}

			return "false";
		}

		override public void Serialize(Stream stream)
		{
			byte[] aBuffer = Encoding.UTF8.GetBytes(this.Serialize());
			stream.Write(aBuffer, 0, aBuffer.Length);
		}
	}
}
