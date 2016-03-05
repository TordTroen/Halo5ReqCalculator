using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Halo5ReqParser
{
	public class Program
	{
		static void Main(string[] args)
		{
			Parser parser = new Parser();

			IO.Printl("Welcome to the semi-automatic REQ tracker");
			IO.PrintDivider();
			IO.Printl();

			parser.reqFileCustomization = IO.GetLine("Input path/drag and drop file for customization page: ");
			parser.reqFileLoadout = IO.GetLine("Input path/drag and drop file for loadout page: ");
			parser.reqFilePower = IO.GetLine("Input path/drag and drop file for power6vehicle page: ");
			IO.PrintDivider();

			parser.ParseAllReqs();

			IO.Printl();
			IO.Printl("Any key to exit...");
			Console.ReadKey();
		}
	}
}
