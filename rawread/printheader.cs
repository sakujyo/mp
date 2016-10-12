using System;
using System.IO;

class a {
	public static void Main(string[] args) {
		if (args.Length < 1) args = new [] { "sc.raw" };

		var fn = args[0];
		Console.WriteLine(fn);
		using (var fs = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read)) {
			for (var i = 0; i < 28; i++) {
				var b = fs.ReadByte();
				Console.Write("{0:X2} ", b);
				//Console.Write(b.ToString("X2"));
			}
		}
		Console.WriteLine();
	}
}
