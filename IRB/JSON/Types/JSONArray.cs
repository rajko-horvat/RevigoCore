using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRB.JSON
{
	public class JSONArray : JSONValue<List<IJSONSerialize>>
	{
		public JSONArray()
		{
			this.oValue = new List<IJSONSerialize>();
		}

		public JSONArray(IEnumerable<IJSONSerialize> list)
		{
			this.oValue = new List<IJSONSerialize>();
			this.oValue.AddRange(list);
		}

		public override string Serialize()
		{
			StringBuilder sbValue = new StringBuilder();
			sbValue.Append("[");
			for (int i = 0; i < this.oValue.Count; i++)
			{
				if (i > 0)
					sbValue.Append(",");
				sbValue.Append(this.oValue[i].Serialize());
			}
			sbValue.Append("]");

			return sbValue.ToString();
		}

		public override void Serialize(Stream stream)
		{
			byte[] aBuffer = Encoding.UTF8.GetBytes(this.Serialize());
			stream.Write(aBuffer, 0, aBuffer.Length);
		}
	}
}
