using System;
using System.Drawing;

class a {
	public static void Main(string[] args) {
		var bmp = new Bitmap("scA.png");
		Console.WriteLine(bmp.Size);
		var b2 = new Bitmap(200, 100);
		var g = Graphics.FromImage(b2);
		g.DrawImage(
				bmp,
				0, 0,
				new Rectangle(1600, 300, 200, 100),
				GraphicsUnit.Pixel
			   );
		b2.Save("testDrawImage.png");
	}
}
