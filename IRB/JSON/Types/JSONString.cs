using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRB.JSON
{
	public class JSONString : JSONValue<string>
	{
		public JSONString()
		{
			this.oValue = null;
		}

		public JSONString(string value)
		{
			base.oValue = value;
		}

		override public string Serialize()
		{
			StringBuilder sbValue = new StringBuilder();
			string sValue = (string)this.oValue;
			sbValue.Append("\"");

			for (int i = 0; i < sValue.Length; i++)
			{
				char ch = sValue[i];
				// do we preserve compatibility or use UTF-8?
				// for now preserve compatibility
				if (ch < '\x020' || ch > '\x07f')
				{
					sbValue.AppendFormat("\\u{0:x4}", ch);
				}
				else
				{
					switch (ch)
					{
						case '\"':
							sbValue.Append("\\\"");
							break;
						case '\\':
							sbValue.Append("\\\\");
							break;
						case '/':
							sbValue.Append("\\/");
							break;
						case '\b':
							sbValue.Append("\\b");
							break;
						case '\f':
							sbValue.Append("\\f");
							break;
						case '\n':
							sbValue.Append("\\n");
							break;
						case '\r':
							sbValue.Append("\\r");
							break;
						case '\t':
							sbValue.Append("\\t");
							break;
						default:
							sbValue.Append(ch);
							break;
					}
				}
			}

			sbValue.Append("\"");
			return sbValue.ToString();
		}

		override public void Serialize(Stream stream)
		{
			byte[] aBuffer = Encoding.UTF8.GetBytes(this.Serialize());
			stream.Write(aBuffer, 0, aBuffer.Length);
		}
	}
}
