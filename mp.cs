using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp {
	class mp {
		const string deviceDefinitionFn = "devices.txt";

		public static void Main(string[] args) {
			/*
			var p1 = new Player();
			p1.Device = "-s 192.168.225.101:5555";
			p1.MacroDirs = new [] { @"macro\rx3", @"macro\rx3a" };
			p1.Init();
			p1.SetTimeout(1500);
			//p1.Next = DateTime.Now + new TimeSpan(0, 0, 0, 10, 500);
			//Console.WriteLine(DateTime.Now); //Console.WriteLine(p1.Next);
			//p1.CaptureAndMatch();

			//var players = new [] { p1 };
			*/
			var players = (from l in Lines(deviceDefinitionFn)
					select new Player(l)).ToList();
			players.ToList().ForEach(x => {
					x.Init();
					x.SetTimeout(1500);
					});

			var f = new Form(); //var f = InitComponents("MultiPlay 2");
			f.Text = "MultiPlay 2";	//f.Text = formTitle;
			f.Size = new Size(200, 200);
			f.Closed += (s, e) => {
				players.ToList().ForEach(x => x.Dispose());
			};

			var t = new Timer();
			t.Interval = 50;
			t.Tick += (s, e) => {
				foreach (var p in players) {
					if (p.IsExpired) {
						p.CaptureAndMatch();
					}
				}
			};
			var bStop = new Button();
			bStop.Text = "Stop";
			bStop.Click += (s, e) => {
				t.Stop();
			};
			f.Controls.Add(bStop);

			t.Start();

			Application.Run(f);
		}

		public static List<string> Lines(string fn) {
			var l = new List<string>();
			using (var sr = new StreamReader(fn)) {
				while (!sr.EndOfStream) {
					l.Add(sr.ReadLine());
				}
			}
			return l;
		}

		//private static Form InitComponents(string formTitle) { }
	}

	class Player {
		const string scfilename = "sc.png";
		private Process pctrl;
		//private Process ppull;

		public string Device { get; set; }
		public DateTime Next { get; set; }
		public bool IsExpired {
			get {
				return DateTime.Now > Next;
			}
		}
		private Form form = new Form();
		private PictureBox pb = new PictureBox();
		private IEnumerable<Macro> macros;
		public IEnumerable<string> MacroDirs { get; set; }

		public void Init() {
			foreach (var d in MacroDirs) { Console.WriteLine("MacroDir: {0}", d); }

			var eMacroFiles= from d in MacroDirs
				select Directory.GetFiles(d, "*")
				into files
				select from f in files
				where f.EndsWith(".apm")
				select f;
			eMacroFiles.ToList().ForEach(x => x.ToList().ForEach(y => Console.WriteLine(y)));
			macros = from f in eMacroFiles.SelectMany(x => x)
				orderby Path.GetFileName(f)
				select new Macro(f);

			macros.ToList().ForEach(x => Console.WriteLine("Macro: {0}", x.Name));
		}
		
		public void Dispose() {
			if (!pctrl.HasExited) pctrl.Kill();
		}

		public Player(string ddstr) {
			var arr = ddstr.Split(',');
			Device = arr[0];
			MacroDirs = arr[1].Split(' ');

			pb.Dock = DockStyle.Fill;
			form.Text = "Pulled PNG";
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

			var ppinfo = new ProcessStartInfo("adb", string.Format("{0} pull /data/local/tmp/{1}", Device, scfilename));
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
						if (e2.Data == null) {
							//Console.WriteLine("e2.Data == null");
						} else {
							Console.WriteLine(e2.Data);
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

		public void SetTimeout(int ms) {
			Next = DateTime.Now + new TimeSpan(0, 0, 0, 0, ms);
		}

		public void ReadAndMatch() {
			var bmp = DP.ReadBitmap(scfilename);
			//Console.WriteLine(bmp.Size);
			pb.Image = bmp;
			pb.Refresh();
			form.Size = bmp.Size + new Size(8, 28);
			Match(bmp);
			//Application.Run(f);
		}

		public void Match(Bitmap bmp) {
			foreach (var m in macros) {
				//TODO: threshold must be considered
				if (m.IsMatch(bmp, 30000)) {
					Console.WriteLine("Match: {0}", m.Name);
					Tap(m.TapPoint);
					SetTimeout(m.WaitTime);
					break;
				}
			}
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
			Tap(p, 50);
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
				//var rect = new Func<string[], Rectangle>(x => new Rectangle(int.Parse(x[0]), int.Parse(x[1]), int.Parse(x[2]), int.Parse(x[3])))(sr.ReadLine().Split(' '));
				TapPoint = new Func<string[], Point>(x => new Point(int.Parse(x[0]), int.Parse(x[1])))(sr.ReadLine().Split(' '));
				WaitTime = int.Parse(sr.ReadLine());
				Bitmap = DP.ReadBitmap(Path.Combine(Path.GetDirectoryName(fn), string.Format("{0}.png", Name)));
			}
		}

		public bool IsMatch(Bitmap bmp, long capableDistanceSquare) {
			//TODO:
			var d2s = 0;
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
			//return true;	//STUB:
			Console.WriteLine("DEBUG: d2s = {0}", d2s);
			return d2s < capableDistanceSquare;
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
}
