using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConsoleApp {
	class a {
		public static void Main(string[] args) {
			var textDialog = new TextDialog();
			if (textDialog.ShowDialog() == DialogResult.OK) {
				Console.WriteLine("TextDialog Exited.");
				Console.WriteLine(textDialog.Text);
			}
		}
	}

	class TextDialog : CommonDialog {
		public string Text { get; private set; }
		public TextDialog() {
		}
		protected override bool RunDialog(IntPtr hwndOwner) {
			var f = new Form();
			f.Size = new Size(212, 56);	//
			var tbText = new TextBox();
			tbText.Width = 200;
			tbText.KeyUp += (s, e) => {
				if (e.KeyCode == Keys.Enter) {
					Text = tbText.Text;
					f.Close();
				}
			};
			f.Controls.Add(tbText);
			f.ShowDialog();	//
			return true;
		}
		public override void Reset() {
		}
	}
}
