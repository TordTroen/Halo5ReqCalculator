using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Halo5ReqParser
{
	public class IO
	{
		public static void Printl()
		{
			Console.WriteLine();
		}

		public static void Printl(string s)
		{
			Console.WriteLine(s);
		}

		public static void Print(string s)
		{
			Console.Write(s);
		}

		public static void PrintBreak()
		{
			Printl("---------------------------------------------\n");
		}

		public static void PrintDivider()
		{
			Printl("=============================================\n");
		}

		public static string GetLine(string header)
		{
			string line = null;
			while (string.IsNullOrEmpty(line))
			{
				Console.WriteLine(header);
				line = Console.ReadLine();
			}
			return line;
		}
	}
}
