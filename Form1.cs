using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ModernTextViewer
{
    public partial class Form1 : Form
    {
        private TextBox textBox;
        private Panel titleBar;
        private Button closeButton;
        private Button maximizeButton;
        private Button minimizeButton;
        private const int RESIZE_BORDER = 8;
        private const int TITLE_BAR_WIDTH = 24;
        private const float MIN_FONT_SIZE = 6f;
        private const float MAX_FONT_SIZE = 72f;
        private float currentFontSize = 10f;

        public Form1()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.Padding = new Padding(3);
            this.DoubleBuffered = true;

            titleBar = new Panel
            {
                Width = TITLE_BAR_WIDTH,
                Dock = DockStyle.Left,
                BackColor = Color.WhiteSmoke
            };

            minimizeButton = new Button
            {
                Text = "−",
                Size = new Size(TITLE_BAR_WIDTH, TITLE_BAR_WIDTH),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Top,
                ForeColor = Color.Gray,
                Font = new Font("Arial", 8),
                Cursor = Cursors.Hand
            };

            maximizeButton = new Button
            {
                Text = "□",
                Size = new Size(TITLE_BAR_WIDTH, TITLE_BAR_WIDTH),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Top,
                ForeColor = Color.Gray,
                Font = new Font("Arial", 8),
                Cursor = Cursors.Hand
            };

            closeButton = new Button
            {
                Text = "×",
                Size = new Size(TITLE_BAR_WIDTH, TITLE_BAR_WIDTH),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Top,
                ForeColor = Color.Gray,
                Font = new Font("Arial", 10),
                Cursor = Cursors.Hand
            };

            textBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", currentFontSize),
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical
            };

            textBox.MouseWheel += TextBox_MouseWheel;

            // Add controls in the correct order (top to bottom)
            titleBar.Controls.Add(closeButton);
            titleBar.Controls.Add(maximizeButton);
            titleBar.Controls.Add(minimizeButton);

            this.Controls.Add(textBox);
            this.Controls.Add(titleBar);

            closeButton.Click += (s, e) => this.Close();
            maximizeButton.Click += (s, e) => {
                this.WindowState = this.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
            minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            foreach (Button button in new[] { closeButton, maximizeButton, minimizeButton })
            {
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Color.LightGray;
                button.TextAlign = ContentAlignment.MiddleCenter;
            }

            titleBar.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 2)
                    {
                        maximizeButton.PerformClick();
                    }
                    else
                    {
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                    }
                }
            };

            this.LocationChanged += Form1_LocationChanged;
            this.MinimumSize = new Size(200, 100);
        }

        private void TextBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                float newSize = currentFontSize + (e.Delta > 0 ? 1f : -1f);

                if (newSize >= MIN_FONT_SIZE && newSize <= MAX_FONT_SIZE)
                {
                    currentFontSize = newSize;
                    textBox.Font = new Font(textBox.Font.FontFamily, currentFontSize);
                }
            }
        }

        private void Form1_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                EnsureFormIsWithinScreenBounds();
            }
        }

        private void EnsureFormIsWithinScreenBounds()
        {
            Screen[] screens = Screen.AllScreens;
            int lowestTaskbarPoint = screens.Max(s => s.WorkingArea.Bottom);

            if (this.Bottom > lowestTaskbarPoint)
            {
                this.Top = lowestTaskbarPoint - this.Height;
            }
        }

        // Constants for window messages
        private const int WM_MOVING = 0x0216;
        private const int WM_SIZING = 0x0214;
        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public void Offset(int x, int y)
            {
                Left += x;
                Right += x;
                Top += y;
                Bottom += y;
            }
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                Point pos = new Point(m.LParam.ToInt32());
                pos = this.PointToClient(pos);

                IntPtr result = IntPtr.Zero;

                if (pos.Y <= RESIZE_BORDER && pos.X <= RESIZE_BORDER)
                    result = (IntPtr)HTTOPLEFT;
                else if (pos.Y <= RESIZE_BORDER && pos.X >= ClientSize.Width - RESIZE_BORDER)
                    result = (IntPtr)HTTOPRIGHT;
                else if (pos.Y >= ClientSize.Height - RESIZE_BORDER && pos.X <= RESIZE_BORDER)
                    result = (IntPtr)HTBOTTOMLEFT;
                else if (pos.Y >= ClientSize.Height - RESIZE_BORDER && pos.X >= ClientSize.Width - RESIZE_BORDER)
                    result = (IntPtr)HTBOTTOMRIGHT;
                else if (pos.Y <= RESIZE_BORDER)
                    result = (IntPtr)HTTOP;
                else if (pos.Y >= ClientSize.Height - RESIZE_BORDER)
                    result = (IntPtr)HTBOTTOM;
                else if (pos.X <= RESIZE_BORDER)
                    result = (IntPtr)HTLEFT;
                else if (pos.X >= ClientSize.Width - RESIZE_BORDER)
                    result = (IntPtr)HTRIGHT;

                if (result != IntPtr.Zero)
                {
                    m.Result = result;
                    return;
                }
            }
            else if (m.Msg == WM_MOVING || m.Msg == WM_SIZING)
            {
                RECT rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                Screen[] screens = Screen.AllScreens;
                int lowestTaskbarPoint = screens.Max(s => s.WorkingArea.Bottom);

                if (rc.Bottom > lowestTaskbarPoint)
                {
                    if (m.Msg == WM_MOVING)
                        rc.Offset(0, lowestTaskbarPoint - rc.Bottom);
                    else // WM_SIZING
                        rc.Bottom = lowestTaskbarPoint;
                }

                Marshal.StructureToPtr(rc, m.LParam, true);
            }

            base.WndProc(ref m);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (WindowState == FormWindowState.Normal)
            {
                if (e.Y <= RESIZE_BORDER && e.X <= RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNWSE;
                else if (e.Y <= RESIZE_BORDER && e.X >= ClientSize.Width - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNESW;
                else if (e.Y >= ClientSize.Height - RESIZE_BORDER && e.X <= RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNESW;
                else if (e.Y >= ClientSize.Height - RESIZE_BORDER && e.X >= ClientSize.Width - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNWSE;
                else if (e.Y <= RESIZE_BORDER || e.Y >= ClientSize.Height - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNS;
                else if (e.X <= RESIZE_BORDER || e.X >= ClientSize.Width - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeWE;
                else
                    this.Cursor = Cursors.Default;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder(e.Graphics, this.ClientRectangle,
                Color.LightGray, 1, ButtonBorderStyle.Solid,
                Color.LightGray, 1, ButtonBorderStyle.Solid,
                Color.LightGray, 1, ButtonBorderStyle.Solid,
                Color.LightGray, 1, ButtonBorderStyle.Solid);
        }
    }
}