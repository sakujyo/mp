using System;
using System.Diagnostics;
using System.Linq;

class a {
	public static void Main() {
		var procs = Process.GetProcessesByName("adb");
		Console.WriteLine(procs.Length);
		foreach (var p in procs.OrderBy(x => x.StartTime)) {
			Console.WriteLine(p.Id + ":" + p.ProcessName + ", " + p.StartTime);
			p.Kill();
		}
	}
}
