using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

class a {
	public static void Main(string[] args) {
		//d = 3Ç…ä‘à¯Ç´ÇµÇƒÇ‡1920x1200->640x400Ç≈è\ï™
		if (args.Length == 0) args = new [] { "sc.raw", "3" };
		var pb = new PictureBox();
		pb.Dock = DockStyle.Fill;
		pb.Image = FromRaw(args[0], int.Parse(args[1]));
		var f = new Form();
		f.Size = new Size(1920 / 2, 1200 / 2) + new Size(8, 28);
		f.Controls.Add(pb);
		Application.Run(f);
	}

	public static Bitmap FromRaw(string fn, int d) {
		using (var fs = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read)) {
			var width = fs.ReadByte() + fs.ReadByte() * 0x100 + fs.ReadByte() * 0x10000 + fs.ReadByte() * 0x1000000;
			var height = fs.ReadByte() + fs.ReadByte() * 0x100 + fs.ReadByte() * 0x10000 + fs.ReadByte() * 0x1000000;
			var bpp = fs.ReadByte() + fs.ReadByte() * 0x100 + fs.ReadByte() * 0x10000 + fs.ReadByte() * 0x1000000;
			Console.WriteLine("{0:X8} {1:X8} {2:X8} ", width, height, bpp);
			Console.WriteLine("{0}", fs.Length);
			return ReadBitmap(fs, width, height, d);
			//return new Bitmap(width, height);	// stub
		}
	}

	public static Bitmap ReadBitmap(FileStream fs, int width, int height, int d) {
	//public static Bitmap GetFBBitmap(string fn, int d) {
		var bmp = new Bitmap(width, height);
			//for (var i = 0; i < 12; i++) { fs.ReadByte(); }
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) { 
				//var c = fs.ReadByte();	//lower
				//var a = fs.ReadByte();	//upper
					//bmp.SetPixel(x, y, Color.FromArgb(255, 255, 255, 255));
					var r = fs.ReadByte();	//a & (255 - 7);
					var g = fs.ReadByte();	//((a & 0x07) << 5) | ((c & 224) >> 3);
					var b = fs.ReadByte();	//(c & 31) << 3;
					var a = fs.ReadByte();
				bmp.SetPixel(x / d, y / d, Color.FromArgb(a, r, g, b));
				//}
			}
		}
		return bmp;
	}
}
