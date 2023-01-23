namespace IRB.Revigo.Databases
{
	/// <summary>
	/// The enum that specifies to which namespace the GO term belongs
	/// 
	/// Authors:
	///		Rajko Horvat (rhorvat at irb.hr)
	/// 
	/// License:
	///		MIT
	///		Copyright (c) 2011-2023, Ruđer Bošković Institute
	///		
	/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
	/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
	/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
	/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	/// 	subject to the following conditions: 
	/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
	/// 	The names of authors and contributors may not be used to endorse or promote products derived from this software 
	/// 	without specific prior written permission.
	/// 	
	///		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	///		INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
	///		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	///		IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
	///		DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
	///		ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
	/// </summary>
	public enum GONamespaceEnum
	{
		None = 0,
		BIOLOGICAL_PROCESS = 1,
		CELLULAR_COMPONENT = 2,
		MOLECULAR_FUNCTION = 3,
		MIXED_NAMESPACE = 4
	}
}
