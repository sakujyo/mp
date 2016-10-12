using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

class a {
	public static void Main(string[] args) {
		//d = 3Ç…ä‘à¯Ç´ÇµÇƒÇ‡1920x1200->640x400Ç≈è\ï™
		if (args.Length == 0) args = new [] { "sh.raw", "1" };
		var pb = new PictureBox();
		pb.Dock = DockStyle.Fill;
		using (var fs = new FileStream(args[0], FileMode.Open)) {
			pb.Image = ReadBitmap(fs, 640, 400, 1);
		}
		//FromRaw(args[0], int.Parse(args[1]));
		var f = new Form();
		f.Text = "shrink";
		f.Size = new Size(1920 / 3, 1200 / 3) + new Size(8, 28);
		f.Controls.Add(pb);
		Application.Run(f);
	}

	public static Bitmap ReadBitmap(FileStream fs, int width, int height, int d) {
		var bmp = new Bitmap(width, height);
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) { 
				var r = fs.ReadByte();	//a & (255 - 7);
				var g = fs.ReadByte();	//((a & 0x07) << 5) | ((c & 224) >> 3);
				var b = fs.ReadByte();	//(c & 31) << 3;
				var a = 255;
				//var a = fs.ReadByte();
				bmp.SetPixel(x / d, y / d, Color.FromArgb(a, r, g, b));
			}
		}
		return bmp;
	}
}
