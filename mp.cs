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
					if (!p.IsMatching && p.IsRunning && p.IsExpired) {
						p.CaptureAndMatch();
					}
				}
			};

			t.Start();

			Application.Run(f);
		}
	}

	enum CapImageFormat {
		Png,
		Raw,
	}

	class Player {
		const string transferLogFn = "transferlog";
		const string matchLogFn = "matchlog";
		const string scfilename = "sc";
		private Process pctrl;

		public string Name	{ get; set; }
		public string Device { get; set; }
		public CapImageFormat Format { get; set; }
		public int Scaling { get; set; }
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

		public Bitmap Bitmap { get; private set; }
		const int defaultWaitTime = 10 * 1000;
		public bool IsMatching	{ get; private set; }

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
			Format = arr[2] == "Raw" ? CapImageFormat.Raw : CapImageFormat.Png;
			Scaling = int.Parse(arr[3]);
			var ssStr = arr[4].Split(' ');
			ScreenSize = new Size(int.Parse(ssStr[0]), int.Parse(ssStr[1]));
			MacroDirs = arr[5].Split(' ');

			pb.Dock = DockStyle.Fill;
			form.Text = Name;
			form.Size = ScreenSize + new Size(8, 28);
			form.Controls.Add(pb);
			form.Show();
			form.Closed += (s, e) => {
				Dispose();
			};
			pb.MouseUp += new MouseEventHandler(pbMouseUp);

			var pargs = string.Format("{0} shell", Device);
			var pinfo = new ProcessStartInfo("adb", pargs);
			pinfo.UseShellExecute = false;
			pinfo.RedirectStandardInput = true;
			pinfo.RedirectStandardOutput = true;

			var ppinfo = Format == CapImageFormat.Png ?
				new ProcessStartInfo("adb", string.Format("{0} pull /data/local/tmp/{1}.png {1}{2}.png", Device, scfilename, Name)) :
				new ProcessStartInfo("adb", string.Format("{0} pull /data/local/tmp/{1}.raw {1}{2}.raw", Device, scfilename, Name));
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

		private void pbMouseUp(Object sender, MouseEventArgs eargs) {
			if ((eargs.Button & MouseButtons.Right) == MouseButtons.Right) {
				//https://msdn.microsoft.com/ja-jp/library/system.windows.forms.control.pointtoscreen(v=vs.110).aspx
				var f = new Form();
				f.Text = "Matching Rectangle";
				f.StartPosition = FormStartPosition.Manual;
				var pbLocToScreen = Point.Empty;
				f.Location = pb.PointToScreen(pb.Location);
				f.Size = new Size(200, 100);
				f.Opacity = 0.5;
				f.BackColor = Color.FromArgb(255, 64, 32, 255);
				f.FormBorderStyle = FormBorderStyle.None;
				var isDrag = false;
				var startPointForm = Point.Empty;
				var startPointCursor = Point.Empty;
				var startSize = Size.Empty;
				f.MouseDown += (s, e) => {
					isDrag = true;
					startPointCursor = f.PointToScreen(new Point(e.X, e.Y));
					startPointForm = f.PointToScreen(Point.Empty);
					startSize = f.Size;
				};
				f.MouseUp += (s, e) => {
					isDrag = false;
				};
				f.MouseMove += (s, e) => {
					if (isDrag) {
						if (e.Button == MouseButtons.Left) {
							f.Location = startPointForm + (Size)(f.PointToScreen(new Point(e.X, e.Y)) - (Size)startPointCursor);
						} else if (e.Button == MouseButtons.Right) {
							f.Size = startSize + (Size)(f.PointToScreen(new Point(e.X, e.Y)) - (Size)startPointCursor);
						}
					}
				};
				var rectStr = "";
				var waitTimeStr = defaultWaitTime.ToString();
				f.KeyDown += (s, e) => {
					//var tapPointStr = "";
					switch (e.KeyCode) {
						case Keys.W:
							f.Location += new Size(0, -1);
							break;
						case Keys.S:
							f.Location += new Size(0, +1);
							break;
						case Keys.A:
							f.Location += new Size(-1, 0);
							break;
						case Keys.D:
							f.Location += new Size(+1, 0);
							break;
						case Keys.T:
							f.Size += new Size(0, -1);
							break;
						case Keys.G:
							f.Size += new Size(0, +1);
							break;
						case Keys.F:
							f.Size += new Size(-1, 0);
							break;
						case Keys.H:
							f.Size += new Size(+1, 0);
							break;
						case Keys.Z:
							f.BackColor = Color.FromArgb(255, 255, 64, 96);
							pbLocToScreen = pb.PointToScreen(pb.Location);
							Console.WriteLine("Point = {0}, Size = {1}", f.Location - (Size)pbLocToScreen, f.Size);
							rectStr = string.Format("{0} {1} {2} {3}", f.Left - pbLocToScreen.X, f.Top - pbLocToScreen.Y, f.Width, f.Height);
							break;
						case Keys.C:
							pbLocToScreen = pb.PointToScreen(pb.Location);
							if (rectStr == "")
								rectStr = string.Format("{0} {1} {2} {3}", f.Left - pbLocToScreen.X, f.Top - pbLocToScreen.Y, f.Width, f.Height);
							var tapPointStr = string.Format("{0} {1}", f.Left - pbLocToScreen.X + f.Width / 2, f.Top - pbLocToScreen.Y + f.Height / 2);
							var newMacroName = "";
							var textDialog = new TextDialog();

							Console.WriteLine(rectStr);

							var macroNameDialog = new OpenFileDialog();
							if (textDialog.ShowDialog() == DialogResult.OK) {
								newMacroName = textDialog.Text;
								using (var sw = new StreamWriter(Path.Combine(MacroDirs.Last(), string.Format("{0}.apm", newMacroName)))) {
									sw.WriteLine(newMacroName);
									sw.WriteLine(rectStr);
									sw.WriteLine(tapPointStr);
									sw.WriteLine(waitTimeStr);
								}
								var rectArr = rectStr.Split(' ');
								var newMacroBitmap = new Bitmap(int.Parse(rectArr[2]), int.Parse(rectArr[3]));
								var g = Graphics.FromImage(newMacroBitmap);
								g.DrawImage(
										Bitmap,
										0, 0,
										new Rectangle(int.Parse(rectArr[0]), int.Parse(rectArr[1]), int.Parse(rectArr[2]), int.Parse(rectArr[3])),
										GraphicsUnit.Pixel
									   );
								newMacroBitmap.Save(Path.Combine(MacroDirs.Last(), string.Format("{0}.png", newMacroName)));
							}
							f.Close();
							break;
						default:
							break;
					}
				};
				f.Show();
			}
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
			if (Format == CapImageFormat.Png) {
				Bitmap = DP.ReadBitmap(string.Format("{0}{1}.png", scfilename, Name));
			} else {
				Bitmap = DP.ReadRawBitmap(string.Format("{0}{1}.raw", scfilename, Name), ScreenSize.Width, ScreenSize.Height);
			}
			Match(Bitmap);
			pb.Image = Bitmap;
			pb.Refresh();
		}

		public void Match(Bitmap bmp) {
			foreach (var m in macros) {
				//TODO: threshold must be considered
				if (m.D2S(bmp) < 30000) {
					WriteLog(matchLogFn, string.Format("d2s = {0,6}, macro: {1}", m.D2S(bmp), m.Name));
				}
				if (m.IsMatch(bmp, 25000)) {
					Console.WriteLine("{2}:Match: {0}, {1}", m.Name, m.WaitTime / 1000d, Name);
					Tap(m.TapPoint);
					SetTimeout(m.WaitTime);
					//return;
					goto END_MATCHING;
				}
			}
			Notify();
			SetTimeout(10 * 1000);
END_MATCHING:
			IsMatching = false;
		}

		public void Notify() {
			if (IsObserved) Console.Beep();
		}

		public void CaptureAndMatch() {
			IsMatching = true;
			if (Format == CapImageFormat.Png) {
				pctrl.StandardInput.WriteLine("sh /data/local/tmp/screencap.sh");
			} else {
				pctrl.StandardInput.WriteLine("sh /data/local/tmp/shrinkcap.sh");
			}
			//screencap.sh
			//1: screencap /data/local/tmp/sc.png
			//2: echo capfined
		}

		public void Tap(Point p, int ms) {
			Console.WriteLine("Scaling: {0}", Scaling);
			pctrl.StandardInput.WriteLine("input touchscreen swipe {0} {1} {0} {1} {2}", p.X * Scaling, p.Y * Scaling, ms);
		}
		public void Tap(Point p) {
			Tap(p, 100);
		}
	}

	class TextDialog : CommonDialog {
		public string Text { get; private set; }
		public TextDialog() {
		}
		protected override bool RunDialog(IntPtr hwndOwner) {
			var f = new Form();
			f.Size = new Size(212, 56);
			var tbText = new TextBox();
			tbText.Width = 200;
			tbText.KeyUp += (s, e) => {
				if (e.KeyCode == Keys.Enter) {
					Text = tbText.Text;
					f.Close();
				}
			};
			f.Controls.Add(tbText);
			f.ShowDialog();
			return true;
		}
		public override void Reset() {
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
			if (Rect[0] + Bitmap.Width > bmp.Width) return false;
			if (Rect[1] + Bitmap.Height > bmp.Height) return false;
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
		public static Bitmap ReadRawBitmap(string fn, int width, int height) {
			using (var fs = new FileStream(fn, FileMode.Open)) {
				var bmp = new Bitmap(width, height);
				for (int y = 0; y < height; y++) {
					for (int x = 0; x < width; x++) { 
						var r = fs.ReadByte();	//a & (255 - 7);
						var g = fs.ReadByte();	//((a & 0x07) << 5) | ((c & 224) >> 3);
						var b = fs.ReadByte();	//(c & 31) << 3;
						var a = 255;
						//var a = fs.ReadByte();
						bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
					}
				}
				return bmp;
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
				panel.BackColor = Color.FromArgb(255, 64, 64, 64);

				var lName = new Label();
				lName.Text = p.Name;
				lName.Location = new Point(0, 24 * 0 + 0);
				panel.Controls.Add(lName);

				var cbRunning = new CheckBox();
				cbRunning.Text = "Running";
				cbRunning.CheckedChanged += (s, e) => {
					p.IsRunning = cbRunning.Checked;
					if (p.IsRunning) p.Init();
					if (p.IsRunning) {
						panel.BackColor = Color.FromArgb(255, 96, 64, 255);
					} else {
						panel.BackColor = Color.FromArgb(255, 64, 64, 64);
					}
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
