using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp {
	partial class mp {
		const string deviceDefinitionFn = "devices.txt";

		public static void Main(string[] args) {
			var players = new List<Player>();
			using (var sr = new StreamReader(deviceDefinitionFn)) {
				while (!sr.EndOfStream) {
					players.Add(new Player(sr.ReadLine()));
				}
			}

			var f = InitComponents(players);
			f.Closed += (s, e) => {
				players.ForEach(x => x.Dispose());
			};

			var t = new Timer();
			t.Interval = 50;
			t.Tick += (s, e) => {
				foreach (var p in players) {
					if (p.IsRunning && p.IsExpired) {
						p.CaptureAndMatch();
					}
				}
			};

			t.Start();

			Application.Run(f);
		}
	}

	class Player {
		const string transferLogFn = "transferlog";
		const string matchLogFn = "matchlog";
		const string scfilename = "sc";
		private Process pctrl;

		public string Name	{ get; set; }
		public string Device { get; set; }
		public DateTime Next { get; set; }
		public bool IsRunning { get; set; }
		public bool IsObserved { get; set; }
		public bool IsExpired {
			get {
				return DateTime.Now > Next;
			}
		}
		private Form form = new Form();
		private PictureBox pb = new PictureBox();
		public Macro[] macros	{ get; private set; }	// readonly?
		private Size ScreenSize;
		private string[] MacroDirs { get; set; }

		public void Init() {
			foreach (var d in MacroDirs) { Console.WriteLine("{0}:MacroDir: {1}", Name, d); }

			var eMacroFiles= from d in MacroDirs
				select Directory.GetFiles(d, "*")
				into files
				select from f in files
				where f.EndsWith(".apm")
				select f;
			eMacroFiles.ToList().ForEach(x => x.ToList().ForEach(y => Console.WriteLine("{0}:{1}", Name, y)));
			macros = (from f in eMacroFiles.SelectMany(x => x)
				orderby Path.GetFileName(f)
				select new Macro(f)).ToArray();

			foreach(var m in macros) { Console.WriteLine("{0}:Macro: {1}", Name, m.Name); }
			AssureDelay(1000);
		}
		
		public void Dispose() {
			if (!pctrl.HasExited) pctrl.Kill();
		}

		public Player(string ddstr) {
			var arr = ddstr.Split(',');
			Name = arr[0];
			Device = arr[1];
			var ssStr = arr[2].Split(' ');
			ScreenSize = new Size(int.Parse(ssStr[0]), int.Parse(ssStr[1]));
			MacroDirs = arr[3].Split(' ');

			pb.Dock = DockStyle.Fill;
			form.Text = Name;
			form.Size = ScreenSize + new Size(8, 28);
			form.Controls.Add(pb);
			form.Show();
			form.Closed += (s, e) => {
				Dispose();
			};

			var pargs = string.Format("{0} shell", Device);
			var pinfo = new ProcessStartInfo("adb", pargs);
			pinfo.UseShellExecute = false;
			pinfo.RedirectStandardInput = true;
			pinfo.RedirectStandardOutput = true;

			var ppinfo = new ProcessStartInfo("adb", string.Format("{0} pull /data/local/tmp/{1}.png {1}{2}.png", Device, scfilename, Name));
			ppinfo.UseShellExecute = false;
			ppinfo.RedirectStandardInput = true;
			ppinfo.RedirectStandardOutput = true;
			ppinfo.RedirectStandardError = true;

			pctrl = Process.Start(pinfo);
			pctrl.OutputDataReceived += (s, e) => {
				if (e.Data == null) return;
				if (e.Data.ToString().Contains("capfined")) {
					//Console.WriteLine(e.Data);
					var ppull = Process.Start(ppinfo);
					ppull.OutputDataReceived += (s2, e2) => {};
					ppull.BeginOutputReadLine();
					ppull.ErrorDataReceived += (s2, e2) => {
						if (e2.Data != null) {
							WriteLog(transferLogFn, e2.Data);
							if (e2.Data.ToString().Contains("KB/s")) {
								ReadAndMatch();
							}
						}
					};
					ppull.BeginErrorReadLine();
				}
			};
			pctrl.BeginOutputReadLine();
		}

		public void WriteLog(string logfn, string msg) {
			var fn = string.Format("{0}{1}.txt", logfn, Name);
			using (var sw = new StreamWriter(fn, true)) {	// true: append
				sw.WriteLine(string.Format("{0}:{1}", Name, msg));
			}
		}

		public void SetTimeout(int ms) {
			Next = DateTime.Now + new TimeSpan(0, 0, 0, 0, ms);
		}

		public void AssureDelay(int ms) {
			if (Next - DateTime.Now < new TimeSpan(0, 0, 0, 0, ms)) SetTimeout(ms);
		}

		public void ReadAndMatch() {
			var bmp = DP.ReadBitmap(string.Format("{0}{1}.png", scfilename, Name));
			Match(bmp);
			pb.Image = bmp;
			pb.Refresh();
		}

		public void Match(Bitmap bmp) {
			foreach (var m in macros) {
				//TODO: threshold must be considered
				if (m.D2S(bmp) < 30000) {
					WriteLog(matchLogFn, string.Format("d2s = {0,6}, macro: {1}", m.D2S(bmp), m.Name));
				}
				if (m.IsMatch(bmp, 25000)) {
					Console.WriteLine("{2}:Match: {0}, {1}", m.Name, m.WaitTime, Name);
					Tap(m.TapPoint);
					SetTimeout(m.WaitTime);
					return;
				}
			}
			Notify();
		}

		public void Notify() {
			if (IsObserved) Console.Beep();
		}

		public void CaptureAndMatch() {
			SetTimeout(10 * 1000);
			pctrl.StandardInput.WriteLine("sh /data/local/tmp/screencap.sh");
			//screencap.sh
			//1: screencap /data/local/tmp/sc.png
			//2: echo capfined
		}

		public void Tap(Point p, int ms) {
			pctrl.StandardInput.WriteLine("input touchscreen swipe {0} {1} {0} {1} {2}", p.X, p.Y, ms);
		}
		public void Tap(Point p) {
			Tap(p, 100);
		}
	}

	class Macro {
		public string Name { get; private set; }
		public int[] Rect;
		public Point TapPoint	{ get; private set; }
		public int WaitTime { get; private set; }
		public Bitmap Bitmap;

		public Macro(string fn) {
			using (var sr = new StreamReader(fn)) {
				//TODO: DONE?
				Name = sr.ReadLine();
				if (Path.GetFileName(fn).Replace(".apm", "") != Name) Console.WriteLine("W: MacroName != filename");
				Rect = sr.ReadLine().Split(' ').Select(x => int.Parse(x)).ToArray();
				TapPoint = new Func<string[], Point>(x => new Point(int.Parse(x[0]), int.Parse(x[1])))(sr.ReadLine().Split(' '));
				WaitTime = int.Parse(sr.ReadLine().Split(' ')[0]);
				Bitmap = DP.ReadBitmap(Path.Combine(Path.GetDirectoryName(fn), string.Format("{0}.png", Name)));
			}
		}

		public bool IsMatch(Bitmap bmp, long capableDistanceSquare) {
			return D2S(bmp) < capableDistanceSquare;
		}

		public long D2S(Bitmap bmp) {
			var d2s = 0L;
			for (var y = 0; y < Bitmap.Height; y++) {
				for (var x = 0; x < Bitmap.Width; x++) {
					var c = Bitmap.GetPixel(x, y);
					var ct = bmp.GetPixel(Rect[0] + x, Rect[1] + y);
					var er = c.R - ct.R;
					var eg = c.G - ct.G;
					var eb = c.B - ct.B;
					d2s += er * er + eg * eg + eb * eb;
				}
			}
			return d2s;
		}
	}

	class DP {
		public static Bitmap ReadBitmap(string fn) {
			using (var fs = new FileStream(fn, FileMode.Open)) {
				var buf = new byte[fs.Length];
				var numBytesRead = 0;
				var numBytesToRead = (int)fs.Length;
				while (numBytesToRead > 0) {
					var n = fs.Read(buf, numBytesRead, numBytesToRead);
					numBytesRead += n;
					numBytesToRead -= n;
				}
				using (var ms = new MemoryStream(buf)) {
					return new Bitmap(ms);
				}
			}

		}
	}

	partial class mp {
		private static Form InitComponents(List<Player> players) {
			var f = new Form();
			f.Text = "MultiPlay 2";
			var fHeight = 104;
			f.Size = new Size(300, fHeight);
			for (var i = 0; i < players.Count(); i++) {
				var p = players[i];
				var panel = new Panel();
				panel.Location = new Point(i * 100, 8);
				panel.Size = new Size(100, fHeight);

				var lName = new Label();
				lName.Text = p.Name;
				lName.Location = new Point(0, 24 * 0 + 0);
				panel.Controls.Add(lName);

				var cbRunning = new CheckBox();
				cbRunning.Text = "Running";
				cbRunning.CheckedChanged += (s, e) => {
					p.IsRunning = cbRunning.Checked;
					if (p.IsRunning) p.Init();
				};
				panel.Controls.Add(cbRunning);
				cbRunning.Location = new Point(0, 24 * 1 - 4);

				var cbNotify = new CheckBox();
				cbNotify.Text = "Notify";
				cbNotify.CheckedChanged += (s, e) => {
					p.IsObserved = !p.IsObserved;
				};
				panel.Controls.Add(cbNotify);
				cbNotify.Location = new Point(0, 24 * 2 - 4);

				f.Controls.Add(panel);
			}
			return f;
		}
	}
}
