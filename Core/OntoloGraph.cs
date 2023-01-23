using System;
using System.Collections.Generic;
using System.Text;
using IRB.Collections.Generic;
using System.Globalization;
using System.IO;

namespace IRB.Revigo.Core
{
	/// <summary>
	/// 
	/// Authors:
	/// 	Fran Supek (fsupek at irb.hr)
	/// 	Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License:
	/// 	MIT
	/// 	Copyright (c) 2011-2023, Ruđer Bošković Institute
	///		
	/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
	/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
	/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
	/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	/// 	subject to the following conditions: 
	/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
	/// 	The names of authors and contributors may not be used to endorse or promote Software products derived from this software 
	/// 	without specific prior written permission.
	/// 	
	/// 	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	/// 	INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
	/// 	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	/// 	IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
	/// 	DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
	/// 	ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
	/// </summary>
	public class GraphEdge
	{
		public int sourceID;
		public int destinationID;
		public BDictionary<string, object> properties = new BDictionary<string, object>();
	}

	public class GraphNode
	{
		public int ID;
		public BDictionary<string, object> properties = new BDictionary<string, object>();
	}

	public class OntoloGraph
	{
		public List<GraphEdge> edges = new List<GraphEdge>();
		public List<GraphNode> nodes = new List<GraphNode>();

		public void writeAdditionalAttributes()
		{
			double dValueMin = double.PositiveInfinity;
			double dValueMax = double.NegativeInfinity;

			for (int i = 0; i < nodes.Count; ++i)
			{
				double dValue = (double)nodes[i].properties.GetValueByKey("value");
				if (dValue < dValueMin)
					dValueMin = dValue;
				if (dValue > dValueMax)
					dValueMax = dValue;
			}
			double dMaxColor = 255.0 * 0.9;
			double dValueMult = dMaxColor / (dValueMax - dValueMin);

			for (int i = 0; i < nodes.Count; ++i)
			{
				double dValue = (double)nodes[i].properties.GetValueByKey("value");
				string color = "";
				if (dValue < 0)
				{
					color = string.Format("#ff{0:x2}{0:x2}", (int)(dMaxColor - ((dValue - dValueMin) * dValueMult)));
				}
				else
				{
					color = string.Format("#{0:x2}ff{0:x2}", (int)(dMaxColor - ((dValue - dValueMin) * dValueMult)));
				}
				nodes[i].properties.Add("color", color);
			}

			dValueMin = double.PositiveInfinity;
			dValueMax = double.NegativeInfinity;

			for (int i = 0; i < edges.Count; ++i)
			{
				double dValue = (double)edges[i].properties.GetValueByKey("similarity");

				if (dValue < dValueMin)
					dValueMin = dValue;
				if (dValue > dValueMax)
					dValueMax = dValue;
			}
			dValueMult = 4.0 / (dValueMax - dValueMin);

			for (int i = 0; i < edges.Count; i++)
			{
				double dValue = (double)edges[i].properties.GetValueByKey("similarity");
				dValue = 1.0 + (dValue - dValueMin) * dValueMult;
				edges[i].properties.Add("thickness", dValue);
			}
		}

		// Format used as input for Cytoscape JS.
		public string GraphToJsObject(string varName)
		{
			double minSize = double.MaxValue, maxSize = double.MinValue, sizeMult = 0.0;
			double minWeight = double.MaxValue, maxWeight = double.MinValue, weightMult = 0.0;

			// determine min and max value for size
			for (int i = 0; i < nodes.Count; ++i)
			{
				double dTemp = (double)nodes[i].properties.GetValueByKey("LogSize");
				if (dTemp < minSize)
					minSize = dTemp;
				if (dTemp > maxSize)
					maxSize = dTemp;
			}
			sizeMult = 60.0 / (maxSize - minSize);

			// determine min and max value for weight
			for (int i = 0; i < edges.Count; ++i)
			{
				double dTemp = (double)edges[i].properties.GetValueByKey("similarity");
				if (dTemp < minWeight)
					minWeight = dTemp;
				if (dTemp > maxWeight)
					maxWeight = dTemp;
			}
			weightMult = 5.0 / (maxWeight - minWeight);

			StringBuilder writer = new StringBuilder();

			writer.AppendFormat("var {0}=[", varName);
			for (int i = 0; i < nodes.Count; ++i)
			{
				if (i > 0)
					writer.Append(",");

				writer.AppendFormat("{{data:{{id:'GO:{0:d7}',label:'{1}',value:{2},color:'{3}',log_size:{4}}}}}",
					nodes[i].ID,
					nodes[i].properties.GetValueByKey("description").ToString().Replace("'", ""),
					Convert.ToString(Math.Round((double)nodes[i].properties.GetValueByKey("value"), 3), CultureInfo.InvariantCulture),
					nodes[i].properties.GetValueByKey("color"),
					10 + (int)Math.Floor(((double)nodes[i].properties.GetValueByKey("LogSize") - minSize) * sizeMult));
			}
			for (int i = 0; i < edges.Count; ++i)
			{
				writer.Append(",");
				writer.AppendFormat("{{data:{{source:'GO:{0:d7}',target:'GO:{1:d7}',weight:{2}}}}}",
					edges[i].sourceID, edges[i].destinationID,
					1 + Math.Floor(((double)edges[i].properties.GetValueByKey("similarity") - minWeight) * weightMult));
			}
			writer.Append("];");

			return writer.ToString();
		}

		// Format used as input for Cytoscape
		public void GraphToXGMML(StreamWriter writer)
		{
			writer.WriteLine("<graph label=\"Cytoscape JS\" directed=\"0\" Graphic=\"1\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:cy=\"http://www.cytoscape.org\" xmlns=\"http://www.cs.rpi.edu/XGMML\">");
			writer.WriteLine("<att name=\"documentVersion\" value=\"0.1\"/>");

			for (int i = 0; i < nodes.Count; ++i)
			{
				writer.WriteLine("<node id=\"GO:{0:d7}\" label=\"{1}\">",
					nodes[i].ID, nodes[i].properties.GetValueByKey("description"));
				writer.WriteLine("\t<att type=\"string\" name=\"description\" value=\"{0}\"/>",
					nodes[i].properties.GetValueByKey("description"));
				writer.WriteLine("\t<att type=\"real\" name=\"value\" value=\"{0}\"/>",
					((double)nodes[i].properties.GetValueByKey("value")).ToString("#####0.000", CultureInfo.InvariantCulture));
				writer.WriteLine("\t<att type=\"real\" name=\"log_size\" value=\"{0}\"/>",
					((double)nodes[i].properties.GetValueByKey("LogSize")).ToString("#####0.000", CultureInfo.InvariantCulture));
				writer.WriteLine("\t<att type=\"real\" name=\"uniqueness\" value=\"{0}\"/>",
					((double)nodes[i].properties.GetValueByKey("uniqueness")).ToString("#####0.000", CultureInfo.InvariantCulture));
				writer.WriteLine("\t<att type=\"real\" name=\"dispensability\" value=\"{0}\"/>",
					((double)nodes[i].properties.GetValueByKey("dispensability")).ToString("#####0.000", CultureInfo.InvariantCulture));
				writer.WriteLine("\t<graphics type=\"ELLIPSE\" x=\"{0}\" y=\"{1}\" fill=\"{2}\"/>",
					(25.0 * (double)nodes[i].properties.GetValueByKey("PC_1")).ToString("#####0.000", CultureInfo.InvariantCulture),
					(25.0 * (double)nodes[i].properties.GetValueByKey("PC_2")).ToString("#####0.000", CultureInfo.InvariantCulture),
					nodes[i].properties.GetValueByKey("color"));
				writer.WriteLine("</node>");
			}
			for (int i = 0; i < edges.Count; ++i)
			{
				writer.WriteLine("<edge id=\"e{0}\" target=\"GO:{1:d7}\" source=\"GO:{2:d7}\" directed=\"false\" label=\"e104\">",
					i, edges[i].sourceID, edges[i].destinationID);
				writer.WriteLine("\t<att type=\"real\" name=\"weight\" value=\"{0}\"/>",
					((double)edges[i].properties.GetValueByKey("similarity")).ToString("#####0.000", CultureInfo.InvariantCulture));
				writer.WriteLine("\t<graphics width=\"{0}\" fill=\"#edecec\"/>",
					((double)edges[i].properties.GetValueByKey("thickness")).ToString("#####0.000", CultureInfo.InvariantCulture));
				writer.WriteLine("</edge>");
			}
			writer.WriteLine("</graph>");
		}
	}
}
