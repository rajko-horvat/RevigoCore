using IRB.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRB.JSON
{
	public static class JSON
	{
		public static IJSONSerialize ToJSONValue(object value)
		{
			// boolean
			if (value is bool)
			{
				return new JSONBoolean((bool)value);
			}
			else if (value is JSONBoolean)
			{
				return (IJSONSerialize)value;
			}
			// null
			else if (value == null)
			{
				return new JSONNull();
			}
			else if (value is JSONNull)
			{
				return (IJSONSerialize)value;
			}
			// number
			else if (value is double || value is float || value is short || value is ushort ||
				value is int || value is uint || value is long || value is ulong)
			{
				return new JSONNumber(Convert.ToDouble(value));
			}
			else if (value is JSONNumber)
			{
				return (IJSONSerialize)value;
			}
			// string
			else if (value is string)
			{
				return new JSONString((string)value);
			}
			else if (value is JSONString)
			{
				return (IJSONSerialize)value;
			}
			// object
			else if (value is JSONObject)
			{
				return (IJSONSerialize)value;
			}
			// array
			else if (value is JSONArray)
			{
				return (IJSONSerialize)value;
			}
			else
			{
				throw new Exception(string.Format("The JSON value provided is of type {0}, but can be only one of the following types: " +
					"[{1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}]",
					value.GetType().Name, typeof(bool).Name, typeof(JSONBoolean).Name,
					"null", typeof(JSONNull).Name,
					typeof(double).Name, typeof(JSONNumber).Name,
					typeof(string).Name, typeof(JSONString).Name,
					typeof(JSONObject).Name, typeof(JSONArray).Name));
			}
		}

		public static string Serialize(IJSONSerialize value)
		{
			return value.Serialize();
		}

		public static void Serialize(Stream stream, IJSONSerialize value)
		{
			value.Serialize(stream);
		}

		private static string sWhitespace = " \n\r\t";

		private enum JSONParserStateEnum
		{
			None,
			String,
			Number,
			Object,
			Array,
			NativeValue
		}

		public static IJSONSerialize Deserialize(string value)
		{
			Stack<BKeyValuePair<JSONParserStateEnum, IJSONSerialize>> oStack = new Stack<BKeyValuePair<JSONParserStateEnum, IJSONSerialize>>();
			JSONParserStateEnum eState = JSONParserStateEnum.None;
			StringBuilder sbValue = new StringBuilder();

			// and the real work begins now
			for (int i = 0; i < value.Length; i++)
			{
				char ch = value[i];

				switch (ch)
				{
					case ' ':
					case '\n':
					case '\r':
					case '\t':
						if (oStack.Peek().Key == JSONParserStateEnum.NativeValue)
						{
							oStack.Pop();
							if (sbValue.Length > 0)
							{
								// native values
								switch (sbValue.ToString().ToLower())
								{
									case "true":

									case "false":
									case "null":
										break;
									default:
										break;
								}
							}
							sbValue.Clear();
						}
						continue;
					case '{':
						break;
					case '[':
						break;
					default:
						oStack.Push(new BKeyValuePair<JSONParserStateEnum, IJSONSerialize>(JSONParserStateEnum.NativeValue, null));
						sbValue.Append(ch);
						break;
				}
			}

			return new JSONNull();
		}

		public static IJSONSerialize Deserialize(Stream stream)
		{
			if (stream.Position >= stream.Length)
			{
				// empty stream equals JSON null
				return new JSONNull();
			}

			// read the remaining stream and convert to UTF-8 string
			byte[] buffer = new byte[stream.Length - stream.Position];
			stream.Read(buffer, 0, buffer.Length);

			return JSON.Deserialize(Encoding.UTF8.GetString(buffer));
		}
	}
}
