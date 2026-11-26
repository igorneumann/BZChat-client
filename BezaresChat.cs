// Bezares Chat — cliente de Windows portable (un solo .exe, sin instalación).
// Requiere .NET Framework 4.x (incluido en Windows 10/11). Compilar con build.bat.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Bezares Chat")]
[assembly: System.Reflection.AssemblyProduct("Bezares Chat")]
[assembly: System.Reflection.AssemblyVersion("2026.11.26.1")]

namespace BezaresChat
{
    using J = Dictionary<string, object>;

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        public static void LogError(Exception ex)
        {
            if (ex == null) return;
            try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "BezaresChat-errores.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine); } catch { }
        }

        [STAThread]
        static void Main()
        {
            // Uso interno de build.bat: genera icon.ico a partir del anagrama y termina.
            var cli = Environment.GetCommandLineArgs();
            bool updated = cli.Any(a => a.Equals("--updated", StringComparison.OrdinalIgnoreCase));
            int ei = Array.FindIndex(cli, a => a.Equals("--export-ico", StringComparison.OrdinalIgnoreCase));
            if (ei >= 0 && ei + 1 < cli.Length) { BrandIcon.ExportIco(cli[ei + 1], BrandIcon.Red); return; }
            bool created;
            using (var mutex = new Mutex(true, "BezaresChat_SingleInstance", out created))
            {
                // Recién actualizado: se espera a que se cierre la versión anterior.
                if (!created && updated) { try { created = mutex.WaitOne(20000); } catch (AbandonedMutexException) { created = true; } }
                if (!created) { MessageBox.Show("Bezares Chat ya está abierto (mira junto al reloj, en la bandeja del sistema).", "Bezares Chat"); return; }
                try { SetProcessDPIAware(); } catch { }
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288; // TLS 1.2 / 1.3
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // Un error inesperado no debe cerrar el programa (se perderían los avisos): se registra y se continúa.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => LogError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => LogError(e.ExceptionObject as Exception);
                TaskScheduler.UnobservedTaskException += (s, e) => { LogError(e.Exception); e.SetObserved(); };
                bool minimized = Environment.GetCommandLineArgs().Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                Config.RefreshStartupPath();
                Updater.Cleanup(Application.ExecutablePath);
                Application.Run(new MainForm(minimized) { JustUpdated = updated });
            }
        }
    }

    /* =============================================================== utilidades */

    static class U
    {
        // Paleta: rojo Bezares como color principal (igual que el icono), grises cálidos y colores de estado.
        public static readonly Color Brand = Color.FromArgb(226, 75, 74);
        public static readonly Color BrandL = Color.FromArgb(252, 235, 235), BrandD = Color.FromArgb(163, 45, 45);
        public static readonly Color Line = Color.FromArgb(211, 209, 199), Line2 = Color.FromArgb(241, 239, 232), Paper = Color.FromArgb(250, 250, 248);
        public static readonly Color Muted = Color.FromArgb(136, 135, 128), Ink2 = Color.FromArgb(95, 94, 90), Ink = Color.FromArgb(44, 44, 42);
        public static readonly Color AmberL = Color.FromArgb(250, 238, 218), AmberD = Color.FromArgb(133, 79, 11);
        public static readonly Color GreenL = Color.FromArgb(234, 243, 222), GreenD = Color.FromArgb(59, 109, 17), Green = Color.FromArgb(99, 153, 34);
        public static readonly Color BlueL = Color.FromArgb(230, 241, 251), BlueD = Color.FromArgb(12, 68, 124), Blue = Color.FromArgb(24, 95, 165);
        public static readonly Font Base = new Font("Segoe UI", 10f);
        public static readonly Font Small = new Font("Segoe UI", 8.25f);
        public static readonly Font SmallBold = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        public static readonly Font Bold = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Title = new Font("Segoe UI", 12f, FontStyle.Bold);
        /** Iconos de Windows 10/11 (Segoe MDL2 Assets): no hace falta incluir imágenes. */
        public static readonly Font Icons = new Font("Segoe MDL2 Assets", 11f);
        public const string IcAttach = "", IcSend = "", IcTemplate = "", IcTransfer = "", IcCheck = "", IcMail = "",
            IcDelete = "", IcHistory = "", IcPanelOpen = "", IcPanelClose = "", IcGlobe = "", IcPhone = "",
            IcChat = "", IcClock = "", IcPerson = "", IcUndo = "";

        public const string IcEmoji = "";

        /** Escala para pantallas con zoom (125 %, 150 %…): el programa es DPI-aware y dibuja en píxeles reales. */
        public static readonly float K = DpiScale();
        static float DpiScale() { try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1f, g.DpiX / 96f); } catch { return 1f; } }
        public static int P(int px) { return (int)Math.Round(px * K); }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /** Color de fondo real de donde está el control (para pintar por fuera de las esquinas redondeadas). */
        public static Color BackOf(Control c)
        {
            for (c = c.Parent; c != null; c = c.Parent) if (c.BackColor.A == 255) return c.BackColor;
            return SystemColors.Control;
        }

        /** Etiqueta redondeada (tiempo de espera, contadores); devuelve su borde izquierdo. */
        public static int Pill(Graphics g, string text, Font f, int right, int top, int h, Color bg, Color fg)
        {
            var sz = TextRenderer.MeasureText(g, text, f, Size.Empty, TextFormatFlags.NoPadding);
            int w = sz.Width + P(12);
            var r = new Rectangle(right - w, top, w, h);
            using (var b = new SolidBrush(bg)) using (var path = Round(r, h / 2)) g.FillPath(b, path);
            TextRenderer.DrawText(g, text, f, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return r.Left;
        }

        /** Botón de icono plano (32 × 32) con texto de ayuda. */
        public static Btn IconButton(string glyph, string tip, ToolTip tips)
        {
            var b = new Btn { Text = glyph, Font = Icons, Size = new Size(P(32), P(32)), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Ink2, Cursor = Cursors.Hand, TabStop = false, UseMnemonic = false };
            b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = Paper;
            if (tips != null) tips.SetToolTip(b, tip);
            return b;
        }

        /** Tiempo de espera legible: «<1 min», «6 min», «1 h 5 min». */
        public static string Wait(long ms)
        {
            long m = ms / 60000;
            if (m < 1) return "<1 min";
            if (m < 60) return m + " min";
            return (m / 60) + " h" + (m % 60 > 0 ? " " + (m % 60) + " min" : "");
        }

        public static string S(J d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
        public static long L(J d, string k) { object v; if (d == null || !d.TryGetValue(k, out v) || v == null) return 0; try { return Convert.ToInt64(v); } catch { return 0; } }
        public static bool B(J d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v; }
        public static List<J> A(J d, string k)
        {
            object v; var r = new List<J>();
            if (d != null && d.TryGetValue(k, out v) && v is object[]) foreach (var o in (object[])v) { var x = o as J; if (x != null) r.Add(x); }
            if (d != null && d.TryGetValue(k, out v) && v is System.Collections.ArrayList) foreach (var o in (System.Collections.ArrayList)v) { var x = o as J; if (x != null) r.Add(x); }
            return r;
        }
        public static List<long> Ids(J d, string k)
        {
            object v; var r = new List<long>();
            if (d != null && d.TryGetValue(k, out v) && v is object[]) foreach (var o in (object[])v) r.Add(Convert.ToInt64(o));
            return r;
        }
        public static string Time(long ms)
        {
            if (ms <= 0) return "";
            var t = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
            return t.Date == DateTime.Today ? t.ToString("HH:mm") : t.ToString("dd/MM HH:mm");
        }
        public static void OpenUrl(string url)
        {
            Uri u;
            if (Uri.TryCreate(url, UriKind.Absolute, out u) && (u.Scheme == "https" || u.Scheme == "http"))
                try { Process.Start(u.AbsoluteUri); } catch { }
        }
        public static string Channel(string c) { return c == "whatsapp" ? "WhatsApp" : c == "facebook" ? "Facebook" : "Web"; }
        public static string LangName(string l)
        {
            var m = new Dictionary<string, string> { { "es", "español" }, { "en", "inglés" }, { "fr", "francés" }, { "de", "alemán" }, { "it", "italiano" }, { "pt", "portugués" }, { "nl", "neerlandés" }, { "pl", "polaco" }, { "ar", "árabe" }, { "zh", "chino" }, { "ru", "ruso" } };
            return m.ContainsKey(l) ? m[l] : l;
        }

        /** Icono del programa: rojo Bezares; en alerta (llamada entrante), ámbar para que destaque al parpadear. */
        public static Icon MakeIcon(bool alert)
        {
            return Icon.FromHandle(BrandIcon.Render(32, alert ? BrandIcon.Alert : BrandIcon.Red).GetHicon());
        }
    }

    /* =============================================================== botones y campos con borde redondeado */

    /// <summary>Botón plano con las esquinas redondeadas. Usa los colores de BackColor, ForeColor y FlatAppearance.</summary>
    class Btn : Button
    {
        public bool Circle;             // botón redondo (enviar)
        public Action<Graphics> Extra;  // dibujo añadido encima (contador de chats anteriores)
        bool over, down;

        public Btn()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.White;
            ForeColor = U.Ink;
            FlatAppearance.BorderColor = U.Line;
            UseMnemonic = false;
        }

        protected override void OnMouseEnter(EventArgs e) { over = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { over = false; down = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) down = true; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; base.OnMouseUp(e); Invalidate(); }

        static Color Darker(Color c, double k) { return Color.FromArgb((int)(c.R * k), (int)(c.G * k), (int)(c.B * k)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(U.BackOf(this));
            bool light = BackColor.GetBrightness() > 0.85f;
            Color hover = FlatAppearance.MouseOverBackColor.IsEmpty ? (light ? U.Paper : Darker(BackColor, 0.92)) : FlatAppearance.MouseOverBackColor;
            Color fill = !Enabled ? U.Line2 : down && over ? Darker(hover, 0.95) : over ? hover : BackColor;
            bool focus = Focused && ShowFocusCues;
            int line = FlatAppearance.BorderSize > 0 || focus ? 1 : 0;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = line > 0 ? PixelOffsetMode.Default : PixelOffsetMode.HighQuality;
            var r = new Rectangle(0, 0, Width - line, Height - line);
            using (var path = U.Round(r, Circle ? Height / 2 : U.P(8)))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (line > 0) using (var p = new Pen(focus ? U.Blue : Enabled ? FlatAppearance.BorderColor : U.Line, focus ? U.P(2) : 1) { Alignment = PenAlignment.Inset }) g.DrawPath(p, path);
            }
            g.PixelOffsetMode = PixelOffsetMode.Default;
            bool left = TextAlign == ContentAlignment.MiddleLeft, multi = Text.IndexOf('\n') >= 0;
            var t = new Rectangle(Padding.Left + (left ? U.P(6) : 0), 0, Width - Padding.Horizontal - (left ? U.P(8) : 0), Height);
            if (!left) t = new Rectangle(0, 0, Width, Height);
            var f = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (left ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            if (multi)
            {
                int h = TextRenderer.MeasureText(g, Text, Font, t.Size, f).Height;
                t = new Rectangle(t.X, (Height - h) / 2, t.Width, h);
            }
            else f |= TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, Text, Font, t, Enabled ? ForeColor : U.Muted, f);
            if (Extra != null) Extra(g);
        }
    }

    /// <summary>Marco redondeado con margen interior para un campo de texto, una lista o un desplegable.</summary>
    class Field : Panel
    {
        public readonly Control Inner;
        int padX, padY;
        bool grow, placing; // grow: la barra de desplazamiento solo aparece si el texto no cabe

        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        public Field(Control inner, int width, int height) : this(inner) { Size = new Size(width, height); }

        /** Campo de una línea. */
        public Field(Control inner, int width) : this(inner) { Size = new Size(width, U.P(34)); }

        public Field(Control inner)
        {
            Inner = inner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            padX = U.P(12); padY = U.P(8);
            inner.Dock = DockStyle.None;
            inner.Margin = new Padding(0);
            var t = inner as TextBox; var c = inner as ComboBox; var l = inner as ListBox;
            if (t != null)
            {
                t.BorderStyle = BorderStyle.None;
                grow = t.Multiline;
                if (grow) { t.ScrollBars = ScrollBars.None; t.TextChanged += (s, e) => Bars(); t.HandleCreated += (s, e) => Bars(); }
                Cursor = Cursors.IBeam;
            }
            else if (c != null) { c.FlatStyle = FlatStyle.Flat; c.BackColor = Color.White; padX = U.P(8); }
            else if (l != null) { l.BorderStyle = BorderStyle.None; padX = padY = U.P(6); }
            inner.Enter += (s, e) => Invalidate();
            inner.Leave += (s, e) => Invalidate();
            inner.EnabledChanged += (s, e) => Invalidate();
            Controls.Add(inner);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Inner.Focus(); }

        /** Líneas que ocupa el texto, contando las partidas por falta de ancho. */
        public int Lines { get { return Inner.IsHandleCreated ? Math.Max(1, (int)SendMessage(Inner.Handle, 0xBA, IntPtr.Zero, IntPtr.Zero)) : 1; } }

        bool barsPending;
        void Bars()
        {
            if (barsPending || !IsHandleCreated) return;
            barsPending = true;
            // Aplazado: cambiar la barra recrea el control y no debe hacerse mientras procesa una tecla.
            BeginInvoke((Action)(() =>
            {
                barsPending = false;
                var t = Inner as TextBox;
                if (t == null || t.IsDisposed || Height < U.P(20)) return;
                var want = Lines * t.Font.Height > t.Height + 2 ? ScrollBars.Vertical : ScrollBars.None;
                if (want == t.ScrollBars) return;
                t.ScrollBars = want;
                PerformLayout();
                t.ScrollToCaret();
            }));
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (placing || Inner == null) return;
            placing = true;
            try
            {
                var t = Inner as TextBox;
                int right = t != null && t.ScrollBars == ScrollBars.Vertical ? U.P(4) : padX; // la barra, junto al borde
                int w = Math.Max(10, Width - padX - right);
                if (Inner is ComboBox) Inner.SetBounds(padX, (Height - Inner.Height) / 2, w, Inner.Height);
                else if (t != null && !t.Multiline) Inner.SetBounds(padX, Math.Max(1, (Height - t.Height) / 2), w, t.Height);
                else Inner.SetBounds(padX, padY, w, Math.Max(10, Height - padY * 2));
            }
            finally { placing = false; }
            if (grow) Bars();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(U.BackOf(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool on = Inner.ContainsFocus;
            using (var path = U.Round(new Rectangle(0, 0, Width - 1, Height - 1), U.P(8)))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);
                using (var p = new Pen(on ? U.Blue : Color.FromArgb(180, 178, 169))) g.DrawPath(p, path);
            }
        }
    }

    /* =============================================================== emojis */

    /// <summary>
    /// Texto con emojis en color. Los controles de Windows Forms los dibujan en blanco y negro; aquí se usa DirectWrite y
    /// Direct2D (incluidos en Windows 8.1 o posterior). Si no están disponibles, se dibuja como siempre.
    /// </summary>
    static class Emoji
    {
        /** Los mismos grupos que en el chat de la web. */
        public static readonly string[][] Groups = new[]
        {
            "😀 😃 😄 😁 😆 😅 😂 🤣 😊 😇 🙂 🙃 😉 😌 😍 🥰 😘 😋 😜 🤪 🤔 🤗 🤭 🤫 😎 🤩 🥳 😏 😐 😶 🙄 😬 😴 😷 🤒 🤕 🙁 😕 😮 😲 😳 🥺 😢 😭 😱 😤 😡 🤯",
            "👍 👎 👌 ✌️ 🤞 🤝 👏 🙌 🙏 💪 👋 ✋ 👉 👈 👆 👇 ☝️ ✍️ 👀 🙋 🤷 🤦 👨‍💻 👩‍💻",
            "❤️ 🧡 💛 💚 💙 💜 🖤 💔 💯 🎉 🎊 🎁 🎂 🏆 🥇 ⭐ 🌟 ✨ 🔥 ☀️ 🌈 🌍 ☕ 🍀",
            "📦 🚚 ✈️ 🚢 🏭 🏢 🏠 🚗 🔧 🔨 ⚙️ 🔩 🧰 📏 💡 🔋 🔌 📞 📱 💻 📧 📎 📄 📋 📊 📅 ⏰ ⏳ 💶 💳 🛒 🔍 🔒 🔑 📍 📷",
            "✅ ❌ ⚠️ ❗ ❓ ℹ️ 🚫 ⛔ ➡️ ⬅️ ⬆️ ⬇️ 🔄 ➕ ➖ 🆗 🆕 🔴 🔵 ⚫"
        }.Select(s => s.Split(' ')).ToArray();

        public const int MaxRecent = 20;
        public static readonly List<string> Recent = new List<string>();

        public static void LoadRecent(string s)
        {
            Recent.Clear();
            foreach (var e in (s ?? "").Split(' ')) if (Recent.Count < MaxRecent && !Recent.Contains(e) && Groups.Any(g => g.Contains(e))) Recent.Add(e);
        }

        public static void Use(string e)
        {
            Recent.Remove(e);
            Recent.Insert(0, e);
            if (Recent.Count > MaxRecent) Recent.RemoveAt(MaxRecent);
        }

        /** ¿Hay algún emoji? (Los signos que Windows ya dibuja bien, como ⚠ o ✓, no cuentan.) */
        public static bool Has(string s)
        {
            foreach (char c in s ?? "") if ((c >= '\uD83C' && c <= '\uD83E') || c == '️' || "⭐✨☕⏰⏳⛔✅❌❗❓➕➖⚫✋".IndexOf(c) >= 0) return true;
            return false;
        }

        public class Item { public string Text = ""; public Font Font; public Rectangle Rect; public Color Color = Color.Black, Fill = Color.Empty; public bool Center; }

        /** Dibuja los elementos sobre un fondo liso. */
        public static void Draw(Graphics g, Rectangle area, Color back, IList<Item> items)
        {
            if (Ready && Paint(g, area, back, items)) return;
            using (var b = new SolidBrush(back)) g.FillRectangle(b, area);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var i in items)
            {
                if (!i.Fill.IsEmpty) using (var b = new SolidBrush(i.Fill)) using (var p = U.Round(i.Rect, U.P(6))) g.FillPath(b, p);
                var f = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (i.Center ? TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter : TextFormatFlags.WordBreak);
                TextRenderer.DrawText(g, i.Text, i.Font, i.Rect, i.Color, f);
            }
        }

        /** Tamaño del texto con ese ancho máximo (0 = sin límite), o vacío si hay que medir con Windows Forms. */
        public static Size Measure(string text, Font font, int maxWidth)
        {
            if (!Ready) return Size.Empty;
            IntPtr layout = IntPtr.Zero;
            try
            {
                layout = Layout(text, font, maxWidth > 0 ? maxWidth : 100000, 100000, false);
                Metrics m;
                Check(Fn<GetMetrics>(layout, 60)(layout, out m));
                return new Size((int)Math.Ceiling(m.WidthTrailing), (int)Math.Ceiling(m.Height));
            }
            catch { return Size.Empty; }
            finally { Free(layout); }
        }

        /* ---- Direct2D y DirectWrite, por posición en su tabla de funciones (no hacen falta bibliotecas) ---- */

        [StructLayout(LayoutKind.Sequential)] struct RtProps { public int Type, Format, Alpha; public float DpiX, DpiY; public int Usage, Level; }
        [StructLayout(LayoutKind.Sequential)] struct ColorF { public float R, G, B, A; }
        [StructLayout(LayoutKind.Sequential)] struct Pt { public float X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RoundRect { public float L, T, R, B, Rx, Ry; }
        [StructLayout(LayoutKind.Sequential)] struct Rc { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] struct Metrics { public float Left, Top, Width, WidthTrailing, Height, LayoutWidth, LayoutHeight; public uint Bidi, Lines; }

        [DllImport("d2d1.dll")] static extern int D2D1CreateFactory(int type, ref Guid iid, IntPtr options, out IntPtr factory);
        [DllImport("dwrite.dll")] static extern int DWriteCreateFactory(int type, ref Guid iid, out IntPtr factory);

        delegate int CreateDCTarget(IntPtr self, ref RtProps p, out IntPtr rt);
        delegate int CreateTextFormat(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string family, IntPtr coll, int weight, int style, int stretch, float size, [MarshalAs(UnmanagedType.LPWStr)] string locale, out IntPtr fmt);
        delegate int CreateTextLayout(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string text, uint len, IntPtr fmt, float w, float h, out IntPtr layout);
        delegate int SetInt(IntPtr self, int v);
        delegate int GetMetrics(IntPtr self, out Metrics m);
        delegate int BindDC(IntPtr self, IntPtr dc, ref Rc r);
        delegate int CreateBrush(IntPtr self, ref ColorF c, IntPtr props, out IntPtr brush);
        delegate void FillRound(IntPtr self, ref RoundRect r, IntPtr brush);
        delegate void DrawLayout(IntPtr self, Pt origin, IntPtr layout, IntPtr brush, int options);
        delegate void ClearTo(IntPtr self, ref ColorF c);
        delegate void Begin(IntPtr self);
        delegate int End(IntPtr self, IntPtr tag1, IntPtr tag2);

        static IntPtr d2d, dw, rt;
        static bool tried, ok;

        static T Fn<T>(IntPtr o, int slot) where T : class
        {
            return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(o), slot * IntPtr.Size), typeof(T));
        }
        static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
        static void Free(IntPtr p) { if (p != IntPtr.Zero) Marshal.Release(p); }
        static ColorF C(Color c) { return new ColorF { R = c.R / 255f, G = c.G / 255f, B = c.B / 255f, A = 1 }; }

        static bool Ready
        {
            get
            {
                if (tried) return ok;
                tried = true;
                try
                {
                    var f = new Guid("06152247-6f50-465a-9245-118bfd3b6007");
                    var w = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
                    Check(D2D1CreateFactory(0, ref f, IntPtr.Zero, out d2d));
                    Check(DWriteCreateFactory(0, ref w, out dw));
                    ok = Target();
                }
                catch { ok = false; }
                return ok;
            }
        }

        static bool Target()
        {
            // Por software (1): en las pantallas remotas y tarjetas antiguas funciona igual. 87 = BGRA, 3 = sin transparencia.
            var p = new RtProps { Type = 1, Format = 87, Alpha = 3, DpiX = 96, DpiY = 96 };
            Check(Fn<CreateDCTarget>(d2d, 16)(d2d, ref p, out rt));
            return true;
        }

        static IntPtr Layout(string text, Font font, float w, float h, bool center)
        {
            IntPtr fmt = IntPtr.Zero, layout;
            try
            {
                float px = font.SizeInPoints * U.K * 96f / 72f;
                Check(Fn<CreateTextFormat>(dw, 15)(dw, font.FontFamily.Name, IntPtr.Zero, font.Bold ? 700 : 400, 0, 5, px, "es-ES", out fmt));
                if (center)
                {
                    Check(Fn<SetInt>(fmt, 3)(fmt, 2));
                    Check(Fn<SetInt>(fmt, 4)(fmt, 2));
                    Check(Fn<SetInt>(fmt, 5)(fmt, 1));
                }
                Check(Fn<CreateTextLayout>(dw, 18)(dw, text, (uint)text.Length, fmt, w, h, out layout));
                return layout;
            }
            finally { Free(fmt); }
        }

        static bool Paint(Graphics g, Rectangle area, Color back, IList<Item> items)
        {
            IntPtr dc = IntPtr.Zero;
            bool drawing = false;
            try
            {
                dc = g.GetHdc();
                var r = new Rc { L = area.Left, T = area.Top, R = area.Right, B = area.Bottom };
                Check(Fn<BindDC>(rt, 57)(rt, dc, ref r));
                Fn<Begin>(rt, 48)(rt);
                drawing = true;
                var bg = C(back);
                Fn<ClearTo>(rt, 47)(rt, ref bg);
                foreach (var i in items)
                {
                    IntPtr brush = IntPtr.Zero, layout = IntPtr.Zero;
                    float x = i.Rect.X - area.X, y = i.Rect.Y - area.Y;
                    try
                    {
                        if (!i.Fill.IsEmpty)
                        {
                            var fc = C(i.Fill);
                            Check(Fn<CreateBrush>(rt, 8)(rt, ref fc, IntPtr.Zero, out brush));
                            var rr = new RoundRect { L = x, T = y, R = x + i.Rect.Width, B = y + i.Rect.Height, Rx = U.P(6), Ry = U.P(6) };
                            Fn<FillRound>(rt, 19)(rt, ref rr, brush);
                            Free(brush); brush = IntPtr.Zero;
                        }
                        if (i.Text == "") continue;
                        var c = C(i.Color);
                        Check(Fn<CreateBrush>(rt, 8)(rt, ref c, IntPtr.Zero, out brush));
                        layout = Layout(i.Text, i.Font, i.Rect.Width, i.Rect.Height, i.Center);
                        Fn<DrawLayout>(rt, 28)(rt, new Pt { X = x, Y = y }, layout, brush, 4); // 4 = fuentes en color
                    }
                    finally { Free(layout); Free(brush); }
                }
                drawing = false;
                Check(Fn<End>(rt, 49)(rt, IntPtr.Zero, IntPtr.Zero));
                return true;
            }
            catch
            {
                // Dispositivo perdido (p. ej. al cambiar de sesión): se crea otro para la próxima vez y ahora se dibuja sin color.
                try { if (drawing) Fn<End>(rt, 49)(rt, IntPtr.Zero, IntPtr.Zero); } catch { }
                IntPtr old = rt;
                try { ok = Target(); } catch { ok = false; rt = IntPtr.Zero; }
                Free(old);
                return false;
            }
            finally { if (dc != IntPtr.Zero) g.ReleaseHdc(dc); }
        }

        /* ---- selector ---- */

        /** Muestra el selector encima de un botón; lo elegido se inserta donde está el cursor del campo. */
        public static void Attach(Button button, TextBox box, Action saved)
        {
            ToolStripDropDown pop = null;
            int closed = 0;
            button.Click += (s, e) =>
            {
                if (pop != null && unchecked(Environment.TickCount - closed) < 250) return; // el clic que acaba de cerrarlo
                if (pop != null) pop.Dispose();
                var picker = new EmojiPicker();
                bool used = false;
                picker.Picked += em =>
                {
                    if (box.IsDisposed || box.TextLength - box.SelectionLength + em.Length > box.MaxLength) return;
                    box.SelectedText = em;
                    Use(em);
                    used = true;
                };
                pop = new ToolStripDropDown { Padding = new Padding(1), BackColor = Color.White, DropShadowEnabled = true };
                pop.Items.Add(new ToolStripControlHost(picker) { AutoSize = false, Size = picker.Size, Margin = Padding.Empty, Padding = Padding.Empty });
                pop.Closed += (s2, e2) =>
                {
                    closed = Environment.TickCount;
                    if (used && saved != null) saved();
                    if (!box.IsDisposed && box.CanFocus) box.Focus();
                };
                pop.Show(button, new Point(0, -pop.PreferredSize.Height - U.P(4)));
                picker.Focus();
            };
            button.Disposed += (s, e) => { if (pop != null) pop.Dispose(); };
        }
    }

    /// <summary>Cuadrícula de emojis con pestañas (recientes y grupos). Con el teclado: flechas, Intro y Tab para cambiar de grupo.</summary>
    class EmojiPicker : Control
    {
        const int Cols = 10, Rows = 5;
        static readonly Font Face = new Font("Segoe UI Emoji", 14f), TabFace = new Font("Segoe UI Emoji", 11f);
        static readonly string[] Tabs = { "🕒", "😀", "👍", "❤️", "📦", "✅" };
        static readonly string[] Names = { "Recientes", "Caras", "Manos", "Celebración", "Trabajo", "Símbolos" };
        readonly int cell = U.P(34), pad = U.P(6);
        readonly ToolTip tip = new ToolTip();
        int tab, hover = -1; // hover: 0… = emoji; -2 - n = pestaña n; -1 = nada
        IList<string> list;
        public event Action<string> Picked;

        public EmojiPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            BackColor = Color.White;
            AccessibleName = "Emojis";
            Size = new Size(Cols * cell + pad * 2, (Rows + 1) * cell + pad * 3);
            Open(Emoji.Recent.Count > 0 ? 0 : 1);
        }

        /** Los recientes se fijan al abrir la pestaña, para que no se muevan bajo el ratón. */
        public void Open(int t) { tab = t; list = t == 0 ? (IList<string>)Emoji.Recent.ToArray() : Emoji.Groups[t - 1]; hover = -1; Invalidate(); }

        Rectangle Cell(int i) { return new Rectangle(pad + i % Cols * cell, pad * 2 + cell + i / Cols * cell, cell, cell); }
        Rectangle TabRect(int i) { int w = (Width - pad * 2) / Tabs.Length; return new Rectangle(pad + i * w, pad, w, cell); }

        int Hit(Point p)
        {
            for (int i = 0; i < Tabs.Length; i++) if (TabRect(i).Contains(p)) return -2 - i;
            for (int i = 0; i < list.Count; i++) if (Cell(i).Contains(p)) return i;
            return -1;
        }

        void Hover(int h)
        {
            if (h == hover) return;
            hover = h;
            tip.SetToolTip(this, h <= -2 ? Names[-2 - h] : "");
            Cursor = h == -1 ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Hover(Hit(e.Location)); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover(-1); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int h = Hit(e.Location);
            if (h <= -2) Open(-2 - h); else if (h >= 0) Pick(h);
        }

        void Pick(int i) { if (Picked != null && i >= 0 && i < list.Count) Picked(list[i]); }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode) { case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: case Keys.Tab: case Keys.Enter: case Keys.Space: return true; }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int i = Math.Max(-1, hover);
            switch (e.KeyCode)
            {
                case Keys.Right: i++; break;
                case Keys.Left: i--; break;
                case Keys.Down: i = i < 0 ? 0 : i + Cols; break;
                case Keys.Up: i -= Cols; break;
                case Keys.Tab: Open((tab + (e.Shift ? Tabs.Length - 1 : 1)) % Tabs.Length); e.Handled = true; return;
                case Keys.Enter: case Keys.Space: Pick(i); e.Handled = true; return;
                default: return;
            }
            e.Handled = true;
            if (i >= 0 && i < list.Count) Hover(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var items = new List<Emoji.Item>();
            for (int i = 0; i < Tabs.Length; i++)
                items.Add(new Emoji.Item { Text = Tabs[i], Font = TabFace, Rect = TabRect(i), Center = true, Fill = i == tab ? U.BrandL : hover == -2 - i ? U.Line2 : Color.Empty });
            for (int i = 0; i < list.Count; i++)
                items.Add(new Emoji.Item { Text = list[i], Font = Face, Rect = Cell(i), Center = true, Fill = i == hover ? U.Line2 : Color.Empty });
            if (list.Count == 0)
                items.Add(new Emoji.Item { Text = "Aquí aparecerán los que uses", Font = U.Base, Rect = new Rectangle(0, pad * 2 + cell, Width, cell * 2), Center = true, Color = U.Muted });
            Emoji.Draw(e.Graphics, ClientRectangle, BackColor, items);
        }

        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }

    /// <summary>Etiqueta que dibuja los emojis en color; sin emojis es una etiqueta normal.</summary>
    class EmojiLabel : Label
    {
        Size measured; // vacío: se dibuja como una etiqueta normal
        string forText; int forWidth; Font forFont;

        bool Rich(int maxWidth)
        {
            if (!Emoji.Has(Text)) return false;
            if (forText != Text || forWidth != maxWidth || forFont != Font)
            {
                forText = Text; forWidth = maxWidth; forFont = Font;
                measured = Emoji.Measure(Text, Font, maxWidth > 0 ? maxWidth - U.P(6) : 0);
            }
            return !measured.IsEmpty;
        }

        public override Size GetPreferredSize(Size proposed)
        {
            return Rich(MaximumSize.Width) ? new Size(measured.Width + U.P(6), measured.Height + 1) : base.GetPreferredSize(proposed);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Rich(MaximumSize.Width)) { base.OnPaint(e); return; }
            var r = new Rectangle(U.P(3), 0, Math.Max(1, Width - U.P(6)) + 1, Height); // 1 px de más: que el redondeo no parta líneas
            Emoji.Draw(e.Graphics, ClientRectangle, BackColor.A == 255 ? BackColor : U.BackOf(this), new[] { new Emoji.Item { Text = Text, Font = Font, Rect = r, Color = ForeColor } });
        }
    }

    /* =============================================================== lista de chats con papelera */

    /// <summary>
    /// Lista de conversaciones con una pequeña papelera en cada fila. El clic en la papelera se intercepta
    /// antes de que la lista cambie de selección, así se puede borrar un chat sin abrirlo.
    /// </summary>
    class ConvListBox : ListBox
    {
        public Func<int, bool> CanTrash = i => false;
        public event Action<int> TrashClicked;
        public int HoverTrash = -1;

        public ConvListBox() { DoubleBuffered = true; }

        public Rectangle TrashRect(int index)
        {
            var b = GetItemRectangle(index);
            return new Rectangle(b.Right - U.P(30), b.Bottom - U.P(27), U.P(22), U.P(22));
        }

        bool HitTrash(Point p, out int index)
        {
            index = IndexFromPoint(p);
            return index >= 0 && index < Items.Count && CanTrash(index) && TrashRect(index).Contains(p);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x201, WM_LBUTTONDBLCLK = 0x203;
            if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK)
            {
                long lp = m.LParam.ToInt64();
                var p = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                int i;
                if (HitTrash(p, out i))
                {
                    var h = TrashClicked;
                    if (h != null) BeginInvoke(new Action(() => h(i)));
                    return; // no seleccionar (no abrir) el chat
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i;
            int nh = HitTrash(e.Location, out i) ? i : -1;
            if (nh != HoverTrash)
            {
                int old = HoverTrash;
                HoverTrash = nh;
                if (old >= 0 && old < Items.Count) Invalidate(GetItemRectangle(old));
                if (nh >= 0) Invalidate(GetItemRectangle(nh));
                Cursor = nh >= 0 ? Cursors.Hand : Cursors.Default;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (HoverTrash >= 0 && HoverTrash < Items.Count) Invalidate(GetItemRectangle(HoverTrash));
            HoverTrash = -1;
            Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        /** Dibuja una papelera pequeña (gris; roja al pasar el ratón). */
        public static void DrawTrash(Graphics g, Rectangle r, bool hover)
        {
            if (hover) using (var bg = new SolidBrush(Color.FromArgb(253, 236, 236))) g.FillEllipse(bg, r);
            float x = r.Left + (r.Width - 14) / 2f, y = r.Top + (r.Height - 16) / 2f;
            using (var pen = new Pen(hover ? Color.FromArgb(198, 40, 40) : Color.FromArgb(150, 156, 168), 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, x + 1, y + 3.5f, x + 13, y + 3.5f);              // tapa
                g.DrawLine(pen, x + 5, y + 1.5f, x + 9, y + 1.5f);               // asa
                g.DrawPolygon(pen, new[] { new PointF(x + 2.5f, y + 5.5f), new PointF(x + 11.5f, y + 5.5f), new PointF(x + 10.5f, y + 15), new PointF(x + 3.5f, y + 15) }); // cubo
                g.DrawLine(pen, x + 5.8f, y + 8, x + 6.1f, y + 12.5f);           // ranuras
                g.DrawLine(pen, x + 8.2f, y + 8, x + 7.9f, y + 12.5f);
            }
        }
    }

    /* =============================================================== banderas de país */

    /// <summary>
    /// Banderas pequeñas (40 px) de flagcdn.com, cacheadas en la carpeta "flags" junto al .exe.
    /// Solo se envía el código de país (p. ej. "es"); si no hay conexión se muestra el código en una etiqueta.
    /// </summary>
    static class Flags
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();
        static readonly HashSet<string> pending = new HashSet<string>(), failed = new HashSet<string>();
        public static event Action Loaded;
        static string Dir { get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "flags"); } }

        public static string Name(string cc)
        {
            try { return new System.Globalization.RegionInfo(cc).DisplayName; } catch { return cc; }
        }

        static Image FromBytes(byte[] b)
        {
            // Solo PNG (firma \x89PNG) y de tamaño razonable.
            if (b == null || b.Length < 8 || b.Length > 64 * 1024 || b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47) return null;
            using (var ms = new MemoryStream(b)) using (var img = Image.FromStream(ms)) return new Bitmap(img);
        }

        /** Devuelve la bandera si ya está disponible; si no, la descarga en segundo plano y avisa con Loaded. */
        public static Image Get(string cc)
        {
            cc = (cc ?? "").ToLowerInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(cc, "^[a-z]{2}$")) return null;
            Image img;
            if (cache.TryGetValue(cc, out img)) return img;
            if (failed.Contains(cc) || pending.Contains(cc)) return null;
            string file = Path.Combine(Dir, cc + ".png");
            try { if (File.Exists(file)) { img = FromBytes(File.ReadAllBytes(file)); if (img != null) { cache[cc] = img; return img; } } } catch { }
            pending.Add(cc);
            var ui = SynchronizationContext.Current;
            Task.Run(() =>
            {
                byte[] data = null;
                try
                {
                    var r = (HttpWebRequest)WebRequest.Create("https://flagcdn.com/w40/" + cc + ".png");
                    r.Timeout = 10000; r.UserAgent = "BezaresChat-Windows/1.0"; r.AllowAutoRedirect = false;
                    using (var res = (HttpWebResponse)r.GetResponse()) using (var s = res.GetResponseStream()) using (var ms = new MemoryStream())
                    {
                        var buf = new byte[8192]; int n;
                        while ((n = s.Read(buf, 0, buf.Length)) > 0 && ms.Length < 65536) ms.Write(buf, 0, n);
                        data = ms.ToArray();
                    }
                }
                catch { }
                Action done = () =>
                {
                    pending.Remove(cc);
                    Image got = null;
                    try { got = FromBytes(data); } catch { }
                    if (got == null) { failed.Add(cc); return; }
                    cache[cc] = got;
                    try { Directory.CreateDirectory(Dir); File.WriteAllBytes(file, data); } catch { }
                    var h = Loaded; if (h != null) h();
                };
                if (ui != null) ui.Post(_ => done(), null); else done();
            });
            return null;
        }

        /**
         * Avatar redondo: la bandera recortada en círculo. Sin país conocido (o mientras se descarga),
         * círculo gris con el icono del canal (web, WhatsApp, Messenger).
         */
        public static void DrawCircle(Graphics g, string cc, Rectangle r, string channel)
        {
            var oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var img = string.IsNullOrEmpty(cc) ? null : Get(cc);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(r);
                if (img != null)
                {
                    var oldClip = g.Clip;
                    g.SetClip(path, CombineMode.Intersect);
                    // Cubre el círculo: la bandera (4:3) se amplía hasta la altura y se centra.
                    int w = (int)Math.Ceiling(r.Height * (double)img.Width / img.Height);
                    var oldInterp = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, new Rectangle(r.Left - (w - r.Width) / 2, r.Top, w, r.Height));
                    g.InterpolationMode = oldInterp;
                    g.Clip = oldClip;
                }
                else
                {
                    using (var b = new SolidBrush(U.Line2)) g.FillPath(b, path);
                    string glyph = channel == "whatsapp" ? U.IcPhone : channel == "facebook" ? U.IcChat : U.IcGlobe;
                    using (var f = new Font("Segoe MDL2 Assets", Math.Max(7f, r.Height * 0.36f / U.K), GraphicsUnit.Point))
                        TextRenderer.DrawText(g, glyph, f, r, U.Ink2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
                using (var p = new Pen(Color.FromArgb(50, 0, 0, 0))) g.DrawEllipse(p, r);
            }
            g.SmoothingMode = oldMode;
        }

        /** Avatar redondo como imagen (cabecera y ficha del cliente). */
        public static Bitmap Circle(string cc, int size, string channel)
        {
            var bmp = new Bitmap(size + 2, size + 2);
            using (var g = Graphics.FromImage(bmp)) DrawCircle(g, cc, new Rectangle(1, 1, size - 1, size - 1), channel);
            return bmp;
        }

        /** Dibuja la bandera (o el código del país si aún no está) y devuelve el ancho ocupado. */
        public static int Draw(Graphics g, string cc, int x, int y, int h)
        {
            if (string.IsNullOrEmpty(cc)) return 0;
            int w = (int)Math.Round(h * 4 / 3.0);
            var img = Get(cc);
            if (img != null)
            {
                g.DrawImage(img, new Rectangle(x, y, w, h));
                using (var p = new Pen(Color.FromArgb(60, 0, 0, 0))) g.DrawRectangle(p, x, y, w - 1, h - 1);
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(232, 235, 240))) g.FillRectangle(b, x, y, w, h);
                TextRenderer.DrawText(g, cc.ToUpperInvariant(), U.Small, new Rectangle(x, y, w, h), Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            return w + 6;
        }
    }

    /* =============================================================== icono de marca (anagrama BZ) */

    /// <summary>
    /// Dibuja el icono de Bezares Chat (burbuja con el anagrama BZ recortado, fondo transparente)
    /// a partir de los mismos trazados que brand/bezares-chat-icon-*.svg. WinForms no lee SVG.
    /// </summary>
    static class BrandIcon
    {
        public static readonly Color Red = Color.FromArgb(0xF5, 0x33, 0x3F);
        public static readonly Color Alert = Color.FromArgb(0xFF, 0xA8, 0x00);

        static readonly string[] BzPaths = {
            "M31.99,97.77 L47.8,70.23 L15.95,70.17 L0,97.74 Z",
            "M170.13,97.87h16.96l-31.79,55.24h128.16l-16.13-27.56h-64.03l32.07-55.62l-65.11,0.12c0,0-4.21,10.27-7.68,13.86C165.93,87.51,170.13,97.87,170.13,97.87Z",
            "M98.83,42.27h2.81h25.73c5.28,0,14.3,4.39,14.3,13.96c0,7.3-6.61,13.96-15.04,13.96H82.36L98.83,42.27z M127.56,153.69c9.54,0,41.62-9.62,41.62-41.86c0-11.46-4.45-22.18-10.56-27.96c7.68-10.2,10.81-19.72,10.81-28.28c0-24.94-25.58-42.32-40.78-42.32H90.72c0,0-4.12-0.03-5.3,0.36c-2.08,0.69-3.27,2.4-4.49,4.56L5.92,147.62c-1.58,2.71-0.99,6.08,2.83,6.08H127.56z M126.75,125.64H50.42l16.47-27.92h2.81h59.12c3.64,0,12.96,3.45,12.96,14.54C141.79,119.05,135.81,125.64,126.75,125.64z"
        };

        /** Intérprete mínimo de trazados SVG (M L H V C S Z, absolutos y relativos). */
        static GraphicsPath Parse(string d)
        {
            var gp = new GraphicsPath(FillMode.Alternate); // par-impar: respeta los huecos de la B
            var tokens = System.Text.RegularExpressions.Regex.Matches(d, @"[MmLlHhVvCcSsZz]|-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?");
            int i = 0; char cmd = 'M';
            float x = 0, y = 0, sx = 0, sy = 0, lcx = 0, lcy = 0; bool lastCubic = false;
            Func<float> num = () => float.Parse(tokens[i++].Value, System.Globalization.CultureInfo.InvariantCulture);
            while (i < tokens.Count)
            {
                string t = tokens[i].Value;
                if (char.IsLetter(t[0])) { cmd = t[0]; i++; if (cmd == 'Z' || cmd == 'z') { gp.CloseFigure(); x = sx; y = sy; lastCubic = false; continue; } }
                bool rel = char.IsLower(cmd);
                switch (char.ToUpper(cmd))
                {
                    case 'M':
                        { float nx = num(), ny = num(); if (rel) { nx += x; ny += y; } gp.StartFigure(); x = sx = nx; y = sy = ny; cmd = rel ? 'l' : 'L'; lastCubic = false; break; }
                    case 'L':
                        { float nx = num(), ny = num(); if (rel) { nx += x; ny += y; } gp.AddLine(x, y, nx, ny); x = nx; y = ny; lastCubic = false; break; }
                    case 'H':
                        { float nx = num(); if (rel) nx += x; gp.AddLine(x, y, nx, y); x = nx; lastCubic = false; break; }
                    case 'V':
                        { float ny = num(); if (rel) ny += y; gp.AddLine(x, y, x, ny); y = ny; lastCubic = false; break; }
                    case 'C':
                        {
                            float x1 = num(), y1 = num(), x2 = num(), y2 = num(), nx = num(), ny = num();
                            if (rel) { x1 += x; y1 += y; x2 += x; y2 += y; nx += x; ny += y; }
                            gp.AddBezier(x, y, x1, y1, x2, y2, nx, ny); lcx = x2; lcy = y2; x = nx; y = ny; lastCubic = true; break;
                        }
                    case 'S':
                        {
                            float x2 = num(), y2 = num(), nx = num(), ny = num();
                            if (rel) { x2 += x; y2 += y; nx += x; ny += y; }
                            float x1 = lastCubic ? 2 * x - lcx : x, y1 = lastCubic ? 2 * y - lcy : y;
                            gp.AddBezier(x, y, x1, y1, x2, y2, nx, ny); lcx = x2; lcy = y2; x = nx; y = ny; lastCubic = true; break;
                        }
                    default: i++; break;
                }
            }
            return gp;
        }

        static GraphicsPath Bubble()
        {
            // Mismo contorno que el SVG (viewBox 256): rectángulo redondeado 16..240 x 36..204, radio 32, cola abajo a la izquierda.
            var p = new GraphicsPath();
            p.AddArc(16, 36, 64, 64, 180, 90);
            p.AddLine(48, 36, 208, 36);
            p.AddArc(176, 36, 64, 64, 270, 90);
            p.AddLine(240, 68, 240, 172);
            p.AddArc(176, 140, 64, 64, 0, 90);
            p.AddLine(208, 204, 122, 204);
            p.AddLine(122, 204, 76, 242);
            p.AddLine(76, 242, 76, 204);
            p.AddLine(76, 204, 48, 204);
            p.AddArc(16, 140, 64, 64, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Bitmap Render(int size, Color color)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float k = size / 256f;
                g.ScaleTransform(k, k);
                using (var b = new SolidBrush(color)) using (var bubble = Bubble()) g.FillPath(b, bubble);
                // Recorte del anagrama BZ: se "pinta" transparente sobre la burbuja.
                g.CompositingMode = CompositingMode.SourceCopy;
                g.TranslateTransform(33f, 64.05f);
                g.ScaleTransform(0.6703f, 0.6703f);
                using (var clear = new SolidBrush(Color.Transparent))
                    foreach (var d in BzPaths) using (var gp = Parse(d)) g.FillPath(clear, gp);
            }
            return bmp;
        }

        /** Solo el anagrama BZ, sin burbuja (fondo de la conversación). */
        public static Bitmap Mark(int width, Color color)
        {
            using (var all = new GraphicsPath(FillMode.Alternate))
            {
                foreach (var d in BzPaths) using (var gp = Parse(d)) all.AddPath(gp, false);
                var r = all.GetBounds();
                float k = width / r.Width;
                var bmp = new Bitmap(width, Math.Max(1, (int)Math.Ceiling(r.Height * k)), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.ScaleTransform(k, k);
                    g.TranslateTransform(-r.X, -r.Y);
                    using (var b = new SolidBrush(color)) foreach (var d in BzPaths) using (var gp = Parse(d)) g.FillPath(b, gp);
                }
                return bmp;
            }
        }

        /** Escribe un .ico con varios tamaños (PNG dentro del ICO, admitido desde Windows Vista). */
        public static void ExportIco(string path, Color color)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var pngs = new List<byte[]>();
            foreach (var s in sizes)
                using (var bmp = Render(s, color)) using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = File.Create(path)) using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int n = 0; n < sizes.Length; n++)
                {
                    byte dim = (byte)(sizes[n] >= 256 ? 0 : sizes[n]);
                    w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(pngs[n].Length); w.Write(offset);
                    offset += pngs[n].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }

    /* =============================================================== configuración portable */

    class Config
    {
        static string FilePath { get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "BezaresChat.ini"); } }
        public string Server = "https://chat.bezares.com", Email = "", Token = "";
        public bool Muted, Remember = true;
        public bool PanelCollapsed; // ficha del cliente plegada (la última elección del usuario)
        public string Emojis = ""; // últimos emojis usados

        public static Config Load()
        {
            var c = new Config();
            try
            {
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('='); if (i < 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    if (k == "server") c.Server = v;
                    else if (k == "email") c.Email = v;
                    else if (k == "muted") c.Muted = v == "1";
                    else if (k == "remember") c.Remember = v != "0";
                    else if (k == "panel") c.PanelCollapsed = v == "collapsed";
                    else if (k == "emojis") c.Emojis = v;
                    else if (k == "token") c.Token = Unprotect(v);
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                // Sin "Recordar sesión" el token solo vive en memoria: al cerrar el programa hay que volver a entrar.
                File.WriteAllLines(FilePath, new[] { "server=" + Server, "email=" + Email, "muted=" + (Muted ? "1" : "0"), "remember=" + (Remember ? "1" : "0"), "panel=" + (PanelCollapsed ? "collapsed" : "open"), "emojis=" + Emojis, "token=" + (Remember ? Protect(Token) : "") });
            }
            catch (Exception e) { MessageBox.Show("No se pudo guardar la configuración junto al programa: " + e.Message); }
        }

        /* ---- Iniciar con Windows (solo para el usuario actual, sin permisos de administrador) ---- */
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "BezaresChat";

        static string RunCommand { get { return "\"" + Application.ExecutablePath + "\" --minimized"; } }

        public static bool StartupEnabled
        {
            get
            {
                try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
                catch { return false; }
            }
        }

        public static void SetStartup(bool on)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, RunCommand);
                    else if (k.GetValue(RunName) != null) k.DeleteValue(RunName);
                }
            }
            catch (Exception e) { MessageBox.Show("No se pudo cambiar el inicio automático: " + e.Message, "Bezares Chat"); }
        }

        /** Programa portable: si se ha movido el .exe, actualiza la ruta guardada. */
        public static void RefreshStartupPath()
        {
            if (StartupEnabled) SetStartup(true);
        }

        // El token se cifra con DPAPI: solo el mismo usuario de Windows puede leerlo.
        static string Protect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), Encoding.UTF8.GetBytes("BezaresChat"), DataProtectionScope.CurrentUser));
        }
        static string Unprotect(string s)
        {
            try { return string.IsNullOrEmpty(s) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), Encoding.UTF8.GetBytes("BezaresChat"), DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }
    }

    /* =============================================================== API */

    class ApiException : Exception
    {
        public int Status;
        public ApiException(string m, int s) : base(m) { Status = s; }
    }

    class Api
    {
        public string Base = "";
        public string Token = "";
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

        public static string Normalize(string server)
        {
            server = (server ?? "").Trim().TrimEnd('/');
            if (server.Length > 0 && !server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                server = "https://" + server;
            Uri u;
            if (!Uri.TryCreate(server, UriKind.Absolute, out u)) throw new ApiException("Dirección del servidor no válida", 0);
            // Solo se permite http:// para pruebas en el propio equipo.
            if (u.Scheme != "https" && !u.IsLoopback) throw new ApiException("El servidor debe usar https://", 0);
            return server;
        }

        HttpWebRequest Req(string method, string path)
        {
            var r = (HttpWebRequest)WebRequest.Create(Base + "/api" + path);
            r.Method = method;
            r.Timeout = 30000;
            r.ReadWriteTimeout = 60000;
            r.UserAgent = "BezaresChat-Windows/1.0";
            r.Accept = "application/json";
            r.AllowAutoRedirect = false;
            if (!string.IsNullOrEmpty(Token)) r.Headers["X-BZC-Token"] = Token;
            return r;
        }

        J Read(HttpWebRequest r)
        {
            HttpWebResponse res;
            try { res = (HttpWebResponse)r.GetResponse(); }
            catch (WebException we)
            {
                res = we.Response as HttpWebResponse;
                if (res == null) throw new ApiException("Sin conexión con el servidor: " + we.Message, 0);
            }
            using (res)
            using (var sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                string body = sr.ReadToEnd();
                J d = null;
                try { d = Json.DeserializeObject(body) as J; } catch { }
                int code = (int)res.StatusCode;
                if (code < 200 || code >= 300 || d == null)
                    throw new ApiException(d != null && d.ContainsKey("message") ? U.S(d, "message") : "Respuesta no válida del servidor (HTTP " + code + ")", code);
                return d;
            }
        }

        public J Get(string path) { return Read(Req("GET", path)); }

        public J Delete(string path) { return Read(Req("DELETE", path)); }

        public J Post(string path, object body)
        {
            var r = Req("POST", path);
            byte[] b = Encoding.UTF8.GetBytes(Json.Serialize(body ?? new J()));
            r.ContentType = "application/json";
            r.ContentLength = b.Length;
            using (var s = r.GetRequestStream()) s.Write(b, 0, b.Length);
            return Read(r);
        }

        public J Upload(string path, string file, string text)
        {
            var r = Req("POST", path);
            string boundary = "----bzc" + Guid.NewGuid().ToString("N");
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            using (var ms = new MemoryStream())
            {
                Action<string> w = s => { var b = Encoding.UTF8.GetBytes(s); ms.Write(b, 0, b.Length); };
                if (!string.IsNullOrEmpty(text))
                    w("--" + boundary + "\r\nContent-Disposition: form-data; name=\"text\"\r\n\r\n" + text + "\r\n");
                string name = Path.GetFileName(file).Replace("\"", "");
                w("--" + boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"" + name + "\"\r\nContent-Type: application/octet-stream\r\n\r\n");
                var fb = File.ReadAllBytes(file); ms.Write(fb, 0, fb.Length);
                w("\r\n--" + boundary + "--\r\n");
                r.ContentLength = ms.Length;
                using (var s = r.GetRequestStream()) ms.WriteTo(s);
            }
            return Read(r);
        }
    }

    /* =============================================================== actualización automática */

    /// <summary>
    /// Actualizaciones publicadas en {servidor}/download. version.json lleva la versión, el tamaño, el SHA-256 y una firma
    /// RSA hecha con la clave privada de quien publica (publicar.ps1), que no está en el servidor: aunque alguien
    /// modificara los archivos del servidor, el programa no instalaría nada que no lleve esa firma.
    /// </summary>
    static class Updater
    {
        const string PublicKey = "<RSAKeyValue><Modulus>xI++ZLON++sdG7o+DFWr6U8tftq5CbOffHm7wG9fO9MZYiMjfLtN24KSbDj6OCIxOdJ9bDE+hg3AYfVglDq3xBp2GVpZyKdMvx/fVZcCk0xQxplw8ucyNJT68oAX0n+QJgccwmvgBGTymhoWSRQ+4M0PzOylJU+DwVdCIRHKZeaKrN/nHXkuiVuf6EmVSBG7bONqx1SntR6CwBHVzzPe9dhWbhZntT7j2VLSa8nBMJqzO7/CSoiidypnmcnQ38Mi/7eCukxmB5pgItDnwDnoaWDf0Yj7pt76MGIC6Dq7Rwl4wRKGk6ZXvpoqA9gJMQWM4patQjE5DJJ4+oe52hjDt+EvjhojcEoRAlhbGajW1p0P5RW9UVFQjWk/JHee3cAnRIDJujLtFy29Bm9RWtOf8ifPHLXlnZAUBTrLZNqa4IFqdF09nfxQDgnSN1IzlQLbe6H2YBUa9QnS5m3hmFHb3V9d0jxpCf/L7/g9IqZubG6s7VRNbtw5UXEOO2GzxsUl</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        const int MaxBytes = 30 * 1024 * 1024;

        public class Release { public Version Version; public string VersionText, Sha256, Signature; public long Size; }

        public static Version Current { get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; } }

        /** Texto firmado: la firma cubre la versión, el tamaño y el contenido. */
        public static string SignedText(string version, long size, string sha256) { return "BezaresChat|" + version + "|" + size + "|" + sha256; }

        /** Lee version.json y comprueba la firma. Devuelve null si no hay nada publicado o no es más nuevo que $than. */
        public static Release Check(string server, Version than, string key)
        {
            var d = new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(Fetch(server + "/download/version.json?t=" + DateTime.UtcNow.Ticks, 20000))) as J;
            var r = new Release { VersionText = U.S(d, "version"), Sha256 = U.S(d, "sha256").ToLowerInvariant(), Signature = U.S(d, "sig"), Size = U.L(d, "size") };
            if (!Version.TryParse(r.VersionText, out r.Version) || r.Version <= than) return null;
            if (r.Size <= 0 || r.Size > MaxBytes || !System.Text.RegularExpressions.Regex.IsMatch(r.Sha256, "^[0-9a-f]{64}$")) throw new InvalidDataException("version.json no válido");
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(key ?? PublicKey);
                if (!rsa.VerifyData(Encoding.UTF8.GetBytes(SignedText(r.VersionText, r.Size, r.Sha256)), "SHA256", Convert.FromBase64String(r.Signature)))
                    throw new InvalidDataException("La firma de la actualización no es válida");
            }
            return r;
        }

        /** Descarga el programa junto al actual (.new) y comprueba tamaño, SHA-256 y versión. */
        public static void Download(string server, Release r, string exe)
        {
            var bytes = Fetch(server + "/download/BezaresChat.exe?v=" + r.VersionText, (int)r.Size);
            if (bytes.Length != r.Size) throw new InvalidDataException("La descarga está incompleta");
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() != r.Sha256) throw new InvalidDataException("La descarga no coincide con la versión publicada");
            // Evita un bucle de actualizaciones si se publicara un programa con otra versión dentro.
            if (System.Reflection.Assembly.ReflectionOnlyLoad(bytes).GetName().Version != r.Version) throw new InvalidDataException("La versión del programa publicado no coincide");
            File.WriteAllBytes(exe + ".new", bytes);
        }

        /** Sustituye el programa por el descargado. Windows permite renombrar un .exe en uso, pero no borrarlo: queda como .old. */
        public static void Swap(string exe)
        {
            string old = exe + "." + DateTime.UtcNow.Ticks + ".old";
            File.Move(exe, old);
            try { File.Move(exe + ".new", exe); }
            catch { File.Move(old, exe); throw; }
        }

        /** Borra los restos de actualizaciones anteriores (al arrancar). */
        public static void Cleanup(string exe)
        {
            try
            {
                foreach (var f in Directory.GetFiles(Path.GetDirectoryName(exe), Path.GetFileName(exe) + ".*.old")) try { File.Delete(f); } catch { }
                if (File.Exists(exe + ".new")) File.Delete(exe + ".new");
            }
            catch { }
        }

        static byte[] Fetch(string url, int max)
        {
            var q = (HttpWebRequest)WebRequest.Create(url);
            q.Timeout = 30000; q.ReadWriteTimeout = 60000;
            q.UserAgent = "BezaresChat-Windows/" + Current;
            q.AllowAutoRedirect = false;
            q.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using (var res = (HttpWebResponse)q.GetResponse())
            using (var s = res.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                if (res.StatusCode != HttpStatusCode.OK) throw new WebException("HTTP " + (int)res.StatusCode);
                var buf = new byte[65536]; int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > max) throw new InvalidDataException("Respuesta demasiado grande");
                }
                return ms.ToArray();
            }
        }
    }

    /* =============================================================== sonidos */

    static class Sounds
    {
        static SoundPlayer ring, ping;
        static bool ringing;

        // Genera un WAV PCM en memoria.
        static MemoryStream Wav(double seconds, Func<double, double> f)
        {
            int rate = 22050, n = (int)(rate * seconds);
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int i = 0; i < n; i++) w.Write((short)(Math.Max(-1, Math.Min(1, f((double)i / rate))) * 30000));
            ms.Position = 0;
            return ms;
        }

        // Timbre de teléfono clásico: 400+450 Hz con trémolo, doble toque cada 3 s.
        static double RingSample(double t)
        {
            bool on = (t < 0.4) || (t >= 0.6 && t < 1.0);
            if (!on) return 0;
            double env = Math.Min(1, Math.Min(t % 0.6, 0.4 - (t % 0.6)) * 60);
            double trem = 0.75 + 0.25 * Math.Sin(2 * Math.PI * 20 * t);
            return 0.5 * (Math.Sin(2 * Math.PI * 400 * t) + Math.Sin(2 * Math.PI * 450 * t)) * trem * env;
        }

        static double PingSample(double t)
        {
            double f = t < 0.09 ? 880 : 1320;
            return 0.6 * Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t * 12);
        }

        public static void StartRing()
        {
            if (ringing) return;
            if (ring == null) { ring = new SoundPlayer(Wav(3.0, RingSample)); ring.Load(); }
            ring.PlayLooping();
            ringing = true;
        }

        public static void StopRing()
        {
            if (!ringing) return;
            ring.Stop();
            ringing = false;
        }

        public static bool IsRinging { get { return ringing; } }

        public static void Ping()
        {
            if (ringing) return;
            if (ping == null) { ping = new SoundPlayer(Wav(0.3, PingSample)); ping.Load(); }
            ping.Play();
        }
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        public static void Flash(Form f, bool on)
        {
            var fi = new FLASHWINFO { cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)), hwnd = f.Handle, dwFlags = on ? 3u | 4u : 0u, uCount = uint.MaxValue, dwTimeout = 0 };
            FlashWindowEx(ref fi);
        }
    }

    /* =============================================================== acceso */

    class LoginForm : Form
    {
        public TextBox TServer = new TextBox(), TEmail = new TextBox(), TPass = new TextBox { UseSystemPasswordChar = true };
        public Label LErr = new Label { ForeColor = Color.FromArgb(198, 40, 40), AutoSize = false, Height = 40 };
        public Button BOk = new Btn { Text = "Entrar", Height = 38, BackColor = U.Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        public CheckBox CShow = new CheckBox { Text = "Mostrar contraseña", AutoSize = true, ForeColor = Color.DimGray };
        public CheckBox CRemember = new CheckBox { Text = "Recordar sesión", AutoSize = true };
        public CheckBox CStartup = new CheckBox { Text = "Iniciar con Windows", AutoSize = true };
        public LinkLabel LForgot = new LinkLabel { Text = "¿Has olvidado tu contraseña?", AutoSize = true, LinkColor = U.Brand };

        public LoginForm(Config cfg)
        {
            Text = "Bezares Chat";
            Font = U.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = U.MakeIcon(false);
            // La ventana se ajusta a su contenido: así nada queda cortado con el escalado de pantalla de Windows.
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, Padding = new Padding(24, 16, 24, 16), WrapContents = false, Location = new Point(0, 0) };
            var brand = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
            brand.Controls.Add(new PictureBox { Image = BrandIcon.Render(44, BrandIcon.Red), Size = new Size(44, 44), Margin = new Padding(0, 0, 10, 0) });
            brand.Controls.Add(new Label { Text = "Bezares Chat", Font = U.Title, ForeColor = Color.FromArgb(27, 34, 48), AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            p.Controls.Add(brand);
            foreach (var x in new[] { Tuple.Create("Servidor", TServer), Tuple.Create("Correo", TEmail), Tuple.Create("Contraseña", TPass) })
            {
                p.Controls.Add(new Label { Text = x.Item1, AutoSize = true, ForeColor = Color.DimGray });
                p.Controls.Add(new Field(x.Item2, 330) { Margin = new Padding(3, 3, 3, 8) });
            }
            CShow.Margin = new Padding(0, 4, 0, 2);
            CShow.CheckedChanged += (s, e) => TPass.UseSystemPasswordChar = !CShow.Checked;
            p.Controls.Add(CShow);
            // Enlace de recuperación justo debajo de la contraseña.
            LForgot.Margin = new Padding(0, 2, 0, 10);
            LForgot.LinkClicked += (s, e) =>
            {
                try { U.OpenUrl(Api.Normalize(TServer.Text) + "/admin/forgot"); }
                catch (ApiException ex) { LErr.Text = ex.Message; }
            };
            p.Controls.Add(LForgot);
            CRemember.Checked = cfg.Remember;
            CStartup.Checked = Config.StartupEnabled;
            p.Controls.Add(CRemember);
            p.Controls.Add(CStartup);
            LErr.Width = 330;
            BOk.Width = 330;
            BOk.FlatAppearance.BorderSize = 0;
            p.Controls.Add(LErr);
            p.Controls.Add(BOk);
            Controls.Add(p);
            AcceptButton = BOk;
            TServer.Text = cfg.Server;
            TEmail.Text = cfg.Email;
            Shown += (s, e) => (TEmail.Text.Length > 0 ? TPass : TEmail).Focus();
        }
    }

    /* =============================================================== aviso de llamada */

    class RingForm : Form
    {
        // Aviso compacto (440 x 135): texto a la izquierda y botón ATENDER grande a la derecha.
        Label LHead = new Label { Text = "🔔  NUEVO CHAT ENTRANTE", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.White, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        public Label LWho = new Label { Font = new Font("Segoe UI", 12.5f, FontStyle.Bold), ForeColor = Color.White, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Top, Height = 28, UseMnemonic = false };
        public Label LMsg = new Label { Font = new Font("Segoe UI", 9.5f), ForeColor = Color.White, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.TopLeft, Dock = DockStyle.Fill, UseMnemonic = false };
        public Button BAnswer = new Btn { Text = "📞\nATENDER", Font = new Font("Segoe UI", 13f, FontStyle.Bold), BackColor = Color.FromArgb(26, 127, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 130, Dock = DockStyle.Right };
        public LinkLabel LLater = new LinkLabel { Text = "Posponer 2 minutos", Font = new Font("Segoe UI", 8.5f), LinkColor = Color.White, ActiveLinkColor = Color.White, AutoSize = false, TextAlign = ContentAlignment.BottomLeft, Dock = DockStyle.Bottom, Height = 18 };
        System.Windows.Forms.Timer blink = new System.Windows.Forms.Timer { Interval = 450 };
        bool phase;

        public RingForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(440, 135);
            BackColor = Color.FromArgb(220, 30, 40);
            Padding = new Padding(10);
            BAnswer.FlatAppearance.BorderSize = 0;
            var text = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 10, 0), BackColor = Color.Transparent };
            // Orden de anclaje: el último añadido se coloca primero (cabecera arriba del todo).
            text.Controls.Add(LMsg); text.Controls.Add(LLater); text.Controls.Add(LWho); text.Controls.Add(LHead);
            Controls.Add(text); Controls.Add(BAnswer);
            blink.Tick += (s, e) => { phase = !phase; BackColor = phase ? Color.FromArgb(255, 140, 0) : Color.FromArgb(220, 30, 40); BAnswer.Invalidate(); };
            VisibleChanged += (s, e) => { if (Visible) blink.Start(); else blink.Stop(); };
        }

        public void ShowCall(string who, string msg, int waiting)
        {
            LHead.Text = "🔔  NUEVO CHAT ENTRANTE" + (waiting > 1 ? "  ·  " + waiting + " esperando" : "");
            LWho.Text = who;
            LMsg.Text = msg.Length > 90 ? msg.Substring(0, 90) + "…" : msg;
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);
            if (!Visible) Show();
            TopMost = true;
            BringToFront();
        }

        protected override bool ShowWithoutActivation { get { return false; } }
    }

    /* =============================================================== solicitudes de alta (administradores) */

    /// <summary>Aviso de una cuenta creada con el enlace secreto: habilitar, ignorar o borrar. No quita el foco.</summary>
    class SignupForm : Form
    {
        Label lHead = new Label { Text = "Solicitud de acceso", Font = U.Bold, ForeColor = U.Ink, AutoSize = false, UseMnemonic = false };
        Label lWho = new Label { Font = U.Title, ForeColor = U.Ink, AutoSize = false, AutoEllipsis = true, UseMnemonic = false };
        Label lInfo = new Label { Font = U.Base, ForeColor = U.Ink2, AutoSize = false, AutoEllipsis = true, UseMnemonic = false };
        Btn bOk = new Btn { Text = "Habilitar", BackColor = U.GreenD, ForeColor = Color.White }, bIgnore = new Btn { Text = "Ignorar" }, bDel = new Btn { Text = "Borrar", ForeColor = U.BrandD };
        public Action<J, string> Decide; // solicitud, acción
        public J Item;

        public SignupForm()
        {
            Text = "Bezares Chat";
            Font = U.Base;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            Icon = U.MakeIcon(false);
            ClientSize = new Size(U.P(390), U.P(172));
            int x = U.P(18), w = ClientSize.Width - x * 2;
            lHead.SetBounds(x, U.P(12), w, U.P(22));
            lWho.SetBounds(x, U.P(36), w, U.P(28));
            lInfo.SetBounds(x, U.P(66), w, U.P(46));
            int bw = (w - U.P(16)) / 3, y = U.P(124);
            bOk.FlatAppearance.BorderColor = U.GreenD;
            bOk.SetBounds(x, y, bw, U.P(34)); bIgnore.SetBounds(x + bw + U.P(8), y, bw, U.P(34)); bDel.SetBounds(x + (bw + U.P(8)) * 2, y, bw, U.P(34));
            Controls.AddRange(new Control[] { lHead, lWho, lInfo, bOk, bIgnore, bDel });
            bOk.Click += (s, e) => Run("enable");
            bIgnore.Click += (s, e) => Run("ignore");
            bDel.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "¿Borrar la cuenta de " + U.S(Item, "email") + "?\nNo se puede deshacer.", "Solicitud de acceso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes) Run("delete");
            };
        }

        void Run(string action)
        {
            if (Item == null || Decide == null) return;
            bOk.Enabled = bIgnore.Enabled = bDel.Enabled = false;
            Decide(Item, action);
        }

        public void ShowItem(J item, int total)
        {
            Item = item;
            lHead.Text = "Solicitud de acceso" + (total > 1 ? "  ·  1 de " + total : "");
            lWho.Text = U.S(item, "name");
            lInfo.Text = U.S(item, "email") + Environment.NewLine + "Rol: " + U.S(item, "role_name") + "  ·  correo confirmado";
            bOk.Enabled = bIgnore.Enabled = bDel.Enabled = true;
            if (Visible) return;
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);
            Show();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } // cerrar = decidir más tarde
            base.OnFormClosing(e);
        }
    }

    /* =============================================================== transferir */

    /* =============================================================== enviar por correo */

    class EmailForm : Form
    {
        public const string DefaultIntro = "Hola {nombre},\r\n\r\nTe enviamos la transcripción de tu conversación con {empresa} del {fecha}.\r\nLos enlaces a archivos adjuntos caducan en 7 días.";
        public TextBox To = new TextBox { Width = 460 };
        public TextBox Intro = new TextBox { Width = 460, Height = 120, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };

        public EmailForm(string defaultTo, string customer, string only)
        {
            Text = "Enviar la conversación por correo";
            Font = U.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(500, 440);
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), WrapContents = false };
            p.Controls.Add(new Label { Text = "Se enviará la transcripción " + (only != "" ? "del chat de " + only : "completa de la conversación") + (customer != "" ? " con " + customer : "") + ", con enlaces a los adjuntos (válidos 7 días)." + (only != "" ? " Los demás chats de esta persona no se incluyen." : ""), AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.DimGray });
            p.Controls.Add(new Label { Text = "Enviar a (varios correos separados por comas):", AutoSize = true, Margin = new Padding(0, 12, 0, 2) });
            To.Text = defaultTo;
            p.Controls.Add(new Field(To, 460));
            p.Controls.Add(new Label { Text = "Introducción del correo:", AutoSize = true, Margin = new Padding(0, 12, 0, 2) });
            Intro.Text = DefaultIntro;
            p.Controls.Add(new Field(Intro, 460, 130));
            p.Controls.Add(new Label { Text = "Variables: {nombre} (cliente), {empresa}, {fecha} (inicio del chat), {agente} (tú). Cada destinatario recibe su propio correo.", AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.Gray, Font = U.Small });
            var ok = new Btn { Text = "✉ Enviar", DialogResult = DialogResult.OK, Width = 120, Height = 32, Margin = new Padding(0, 12, 0, 0) };
            p.Controls.Add(ok);
            Controls.Add(p);
            Shown += (s, e) => { To.Focus(); To.SelectAll(); };
        }

        /** Lista de correos escritos (separados por comas, punto y coma o espacios). */
        public List<string> Recipients()
        {
            return To.Text.Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x != "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /* =============================================================== lista de mensajes */

    /// <summary>
    /// Filas una debajo de otra con desplazamiento. (FlowLayoutPanel no siempre recalculaba la altura total y no dejaba
    /// llegar al último mensaje.) Si se estaba viendo el final, se sigue viendo al cambiar las filas o el tamaño.
    /// </summary>
    static class Watermark
    {
        public static readonly Color Back = Color.FromArgb(245, 244, 240);
        static Bitmap mark;

        /** Dibuja la BZ, muy clara, centrada en la zona visible. */
        public static void Draw(Graphics g, Size area)
        {
            int w = Math.Min(U.P(420), Math.Min(area.Width * 55 / 100, area.Height * 120 / 100)) / 8 * 8; // de 8 en 8: no se recalcula en cada píxel al redimensionar
            if (w < U.P(80)) return;
            if (mark == null || mark.Width != w)
            {
                if (mark != null) mark.Dispose();
                mark = BrandIcon.Mark(w, Color.FromArgb(235, 233, 226));
            }
            g.DrawImage(mark, new Rectangle((area.Width - mark.Width) / 2, (area.Height - mark.Height) / 2, mark.Width, mark.Height));
        }
    }

    /// <summary>Texto de «selecciona un chat» con la BZ de fondo.</summary>
    class WatermarkLabel : Label
    {
        public WatermarkLabel() { SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); Watermark.Draw(e.Graphics, ClientSize); }
    }

    /// <summary>Fila transparente de la conversación: deja ver el fondo alrededor de la burbuja.</summary>
    class Row : Panel
    {
        public Row()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }

        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.Style |= 0x02000000; return p; } } // WS_CLIPCHILDREN: repintar el fondo no toca la burbuja
    }

    /// <summary>Tres puntitos saltantes: alguien está escribiendo.</summary>
    class TypingDots : Control
    {
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 40 };
        readonly Stopwatch clock = Stopwatch.StartNew();

        public TypingDots()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(U.P(34), U.P(18));
            Margin = new Padding(0, 2, 0, 2);
            timer.Tick += (s, e) => Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); timer.Start(); }
        protected override void OnHandleDestroyed(EventArgs e) { timer.Stop(); base.OnHandleDestroyed(e); }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int d = U.P(6);
            long now = clock.ElapsedMilliseconds % 1200;
            for (int i = 0; i < 3; i++)
            {
                long t = now - i * 160;
                float up = t > 0 && t < 520 ? (float)Math.Sin(Math.PI * t / 520) : 0f; // cada punto salta un poco después que el anterior
                using (var b = new SolidBrush(up > 0.3f ? U.Ink2 : U.Muted)) e.Graphics.FillEllipse(b, U.P(1) + i * U.P(11), Height - d - U.P(3) - up * U.P(7), d, d);
            }
        }
    }

    class RowList : Panel
    {
        public Action<Control> LayoutRow; // ajusta una fila al ancho de la lista
        bool busy, again;
        int lastTotal, lastHeight, lastWidth;

        public RowList() { AutoScroll = true; DoubleBuffered = true; ResizeRedraw = true; }

        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.Style |= 0x02000000; return p; } } // WS_CLIPCHILDREN

        // La BZ no se desplaza con los mensajes: al moverse una fila hay que repintar su fondo (no su burbuja).
        protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); Watermark.Draw(e.Graphics, ClientSize); }
        protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); e.Control.LocationChanged += RowMoved; }
        protected override void OnControlRemoved(ControlEventArgs e) { e.Control.LocationChanged -= RowMoved; base.OnControlRemoved(e); }
        protected override void OnClientSizeChanged(EventArgs e) { base.OnClientSizeChanged(e); foreach (Control c in Controls) c.Invalidate(false); }
        void RowMoved(object s, EventArgs e) { ((Control)s).Invalidate(false); Invalidate(false); }

        bool AtEnd { get { return lastTotal <= lastHeight || -AutoScrollPosition.Y + lastHeight >= lastTotal - U.P(60); } }

        public void ScrollToEnd()
        {
            PerformLayout();
            AutoScrollPosition = new Point(0, lastTotal);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (busy) { again = true; return; }
            if (ClientSize.Height < 20 || Width < 100) return; // minimizada: se conserva la posición
            busy = true;
            try
            {
                bool end = AtEnd;
                for (int i = 0; i < 4; i++)
                {
                    // Primero el ancho de las filas: si no caben, saldría una barra horizontal.
                    if (Width != lastWidth && LayoutRow != null) { lastWidth = Width; foreach (Control c in Controls) LayoutRow(c); }
                    int total = Padding.Vertical;
                    foreach (Control c in Controls) total += c.Height;
                    if (AutoScrollMinSize.Height != total) AutoScrollMinSize = new Size(0, total);
                    Stack();
                    again = false; // lo que cambie a partir de aquí (barras, tamaño) obliga a repetir
                    base.OnLayout(e);
                    lastTotal = total; lastHeight = ClientSize.Height;
                    if (end) AutoScrollPosition = new Point(0, total);
                    Stack();
                    if (!again) break;
                }
            }
            finally { busy = false; }
        }

        void Stack()
        {
            int x = Padding.Left + AutoScrollPosition.X, y = Padding.Top + AutoScrollPosition.Y;
            foreach (Control c in Controls)
            {
                if (c.Left != x || c.Top != y) c.Location = new Point(x, y);
                y += c.Height;
            }
        }
    }

    /* =============================================================== editar un mensaje enviado */

    class EditMessageForm : Form
    {
        public TextBox Body = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, Font = new Font("Segoe UI", 10.5f), Dock = DockStyle.Fill, MaxLength = 4000 };

        public EditMessageForm(string text, bool resend, bool canBeEmpty)
        {
            Text = resend ? "Corregir y reenviar" : "Editar mensaje";
            Font = U.Base;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(U.P(460), U.P(250));
            MinimumSize = new Size(U.P(360), U.P(220));
            Padding = new Padding(U.P(16));
            var note = new Label
            {
                Dock = DockStyle.Top, Height = U.P(40), ForeColor = U.Muted, Font = U.Small, UseMnemonic = false,
                Text = resend ? "Este mensaje no llegó al cliente. Al guardar se intentará enviar de nuevo."
                              : "El cliente verá el texto nuevo en su ventana de chat, marcado como «editado». Puede haber leído ya el anterior."
            };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Height = U.P(46), Padding = new Padding(0, U.P(10), 0, 0) };
            var cancel = new Btn { Text = "Cancelar", DialogResult = DialogResult.Cancel, Size = new Size(U.P(100), U.P(32)) };
            var ok = new Btn { Text = resend ? "Reenviar" : "Guardar", DialogResult = DialogResult.OK, Size = new Size(U.P(110), U.P(32)) };
            var emoji = U.IconButton(U.IcEmoji, "Emojis", null);
            emoji.AccessibleName = "Emojis";
            Emoji.Attach(emoji, Body, null);
            bar.Controls.Add(ok); bar.Controls.Add(cancel); bar.Controls.Add(emoji);
            Body.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            Action check = () => ok.Enabled = (canBeEmpty || Body.Text.Trim() != "") && (resend || NewText != text.Trim());
            Body.TextChanged += (s, e) => check();
            check();
            // Igual que al escribir: Intro guarda y Mayús+Intro hace un salto de línea
            Body.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter || e.Shift) return;
                e.SuppressKeyPress = true;
                if (ok.Enabled) ok.PerformClick();
            };
            Controls.Add(new Field(Body) { Dock = DockStyle.Fill }); Controls.Add(bar); Controls.Add(note);
            CancelButton = cancel;
            Shown += (s, e) => { Body.Focus(); Body.SelectionStart = Body.TextLength; };
        }

        public string NewText { get { return Body.Text.Replace("\r\n", "\n").Trim(); } }
    }

    /* =============================================================== plantillas (recepción) */

    class TemplatesForm : Form
    {
        readonly Api api;
        public List<J> Items;
        ListBox list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        TextBox title = new TextBox { Width = 380 };
        TextBox body = new TextBox { Multiline = true, Width = 380, Height = 170, ScrollBars = ScrollBars.Vertical };
        long editingId;

        public TemplatesForm(Api api, List<J> items)
        {
            this.api = api;
            Items = items;
            Text = "Plantillas de respuesta";
            Font = U.Base;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ClientSize = new Size(680, 440);
            MinimumSize = new Size(640, 470);

            var left = new Panel { Dock = DockStyle.Left, Width = 230, Padding = new Padding(12, 12, 6, 12) };
            var bNew = new Btn { Text = "➕ Nueva plantilla", Dock = DockStyle.Bottom, Height = 32 };
            left.Controls.Add(new Field(list) { Dock = DockStyle.Fill }); left.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 8 }); left.Controls.Add(bNew);

            var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 12, 12, 12) };
            right.Controls.Add(new Label { Text = "Título (lo que verás en el menú)", AutoSize = true, ForeColor = Color.DimGray });
            right.Controls.Add(new Field(title, 400));
            right.Controls.Add(new Label { Text = "Texto de la respuesta", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 10, 3, 0) });
            right.Controls.Add(new Field(body, 400, 180));
            right.Controls.Add(new Label { Text = "Variables: {nombre} = nombre del cliente · {agente} = tu nombre", AutoSize = true, ForeColor = Color.Gray, Font = U.Small });
            var actions = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 10, 0, 0) };
            var bSave = new Btn { Text = "💾 Guardar", Width = 120, Height = 32, BackColor = U.Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            bSave.FlatAppearance.BorderSize = 0;
            var bDel = new Btn { Text = "✕ Eliminar", Width = 110, Height = 32 };
            actions.Controls.Add(bSave); actions.Controls.Add(bDel);
            right.Controls.Add(actions);

            Controls.Add(right); Controls.Add(left);
            list.DisplayMember = "Text";
            list.SelectedIndexChanged += (s, e) => Select();
            bNew.Click += (s, e) => { list.ClearSelected(); editingId = 0; title.Text = ""; body.Text = ""; title.Focus(); };
            bSave.Click += async (s, e) => await Save();
            bDel.Click += async (s, e) => await Delete();
            Fill(0);
        }

        class Item { public long Id; public string Text; public override string ToString() { return Text; } }

        void Fill(long selectId)
        {
            list.Items.Clear();
            foreach (var t in Items) list.Items.Add(new Item { Id = U.L(t, "id"), Text = U.S(t, "title") });
            for (int i = 0; i < list.Items.Count; i++) if (((Item)list.Items[i]).Id == selectId) list.SelectedIndex = i;
        }

        new void Select()
        {
            var it = list.SelectedItem as Item;
            if (it == null) return;
            var t = Items.FirstOrDefault(x => U.L(x, "id") == it.Id);
            if (t == null) return;
            editingId = it.Id; title.Text = U.S(t, "title"); body.Text = U.S(t, "body");
        }

        async Task Reload(long selectId)
        {
            var r = await Task.Run(() => api.Get("/templates"));
            Items = U.A(r, "templates");
            Fill(selectId);
        }

        async Task Save()
        {
            string t = title.Text.Trim(), b = body.Text.Trim();
            if (t == "" || b == "") { MessageBox.Show(this, "Escribe un título y un texto.", "Plantillas"); return; }
            try
            {
                long id = editingId;
                var r = await Task.Run(() => api.Post(id != 0 ? "/templates/" + id : "/templates", new J { { "title", t }, { "body", b } }));
                editingId = U.L(r["template"] as J, "id");
                await Reload(editingId);
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Plantillas"); }
        }

        async Task Delete()
        {
            if (editingId == 0) return;
            if (MessageBox.Show(this, "¿Eliminar la plantilla «" + title.Text + "»?", "Plantillas", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                long id = editingId;
                await Task.Run(() => api.Delete("/templates/" + id));
                editingId = 0; title.Text = ""; body.Text = "";
                await Reload(0);
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Plantillas"); }
        }
    }

    class TransferForm : Form
    {
        public ComboBox Agents = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
        public TextBox Note = new TextBox { Multiline = true, Width = 340, Height = 70 };

        public TransferForm(List<J> agents, long exclude) : this(agents, exclude, 0) { }

        /// <summary>Solo muestra a los compañeros conectados en este momento (programa abierto en el último minuto).</summary>
        public TransferForm(List<J> agents, long exclude, long myId)
        {
            Text = "Transferir conversación";
            Font = U.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 285);
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), WrapContents = false };
            p.Controls.Add(new Label { Text = "Transferir a:", AutoSize = true });
            foreach (var a in agents)
            {
                long aid = U.L(a, "id");
                if (U.L(a, "active") == 0 || aid == exclude || aid == myId || !U.B(a, "online")) continue;
                string role = U.S(a, "role_name") != "" ? U.S(a, "role_name") : (U.S(a, "role") == "sales" ? "Comercial" : U.S(a, "role") == "receptionist" ? "Recepción" : "Admin");
                Agents.Items.Add(new AgentItem { Id = aid, Text = "● " + U.S(a, "name") + " (" + role + ")" });
            }
            if (Agents.Items.Count > 0) Agents.SelectedIndex = 0;
            p.Controls.Add(new Field(Agents, 340));
            if (Agents.Items.Count == 0)
            {
                Agents.Enabled = false;
                p.Controls.Add(new Label { Text = "No hay ningún compañero conectado ahora mismo.", AutoSize = true, ForeColor = Color.FromArgb(198, 40, 40), Margin = new Padding(0, 4, 0, 0) });
            }
            p.Controls.Add(new Label { Text = "Nota para el compañero (opcional):", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            p.Controls.Add(new Field(Note, 340, 80));
            var ok = new Btn { Text = "Transferir", DialogResult = DialogResult.OK, Width = 120, Height = 32, BackColor = U.Blue, ForeColor = Color.White, Enabled = Agents.Items.Count > 0 };
            ok.FlatAppearance.BorderColor = U.Blue;
            var cancel = new Btn { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 100, Height = 32 };
            var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Size = new Size(346, 44), Margin = new Padding(0, 8, 0, 0) };
            bar.Controls.Add(ok); bar.Controls.Add(cancel);
            CancelButton = cancel;
            p.Controls.Add(bar);
            AcceptButton = ok;
            Controls.Add(p);
        }

        public class AgentItem { public long Id; public string Text; public override string ToString() { return Text; } }
    }

    /* =============================================================== ventana principal */

    class MainForm : Form
    {
        Config cfg = Config.Load();
        Api api = new Api();
        J me;

        // estado
        Dictionary<long, J> convs = new Dictionary<long, J>();
        Dictionary<long, List<J>> msgs = new Dictionary<long, List<J>>();
        Dictionary<long, int> unread = new Dictionary<long, int>();
        HashSet<long> ringing = new HashSet<long>(), seenIn = new HashSet<long>();
        List<J> agents = new List<J>();
        long since, activeId;
        int syncCount;
        bool syncing, firstSync = true, quitting;
        DateTime snoozeUntil = DateTime.MinValue;
        bool startHidden;
        ToolStripMenuItem startupItem = new ToolStripMenuItem("Iniciar con Windows");

        // controles
        // Lista de chats agrupada por estado (filas de cabecera + filas de chat, alturas variables)
        ConvListBox list = new ConvListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawVariable, BorderStyle = BorderStyle.None, IntegralHeight = false };
        HashSet<string> collapsedGroups = new HashSet<string> { "done" }; // «Resueltos y cerrados», plegado por defecto
        bool renderingList;
        TextBox search = new TextBox { Font = U.Base };
        long waitMinutes = 5, clockSkew; // umbral de espera (del servidor) y diferencia de reloj con el servidor

        // Cabecera del chat
        Panel head = new Panel { Dock = DockStyle.Top, BackColor = Color.White };
        PictureBox picHead = new PictureBox { SizeMode = PictureBoxSizeMode.CenterImage };
        Label lTitle = new Label { Font = U.Title, AutoSize = false, AutoEllipsis = true, ForeColor = U.Ink, UseMnemonic = false };
        Label lSub = new Label { Font = U.Small, ForeColor = U.Muted, AutoSize = false, AutoEllipsis = true, UseMnemonic = false };
        Label lWait = new Label { Font = U.SmallBold, AutoSize = true, Padding = new Padding(6, 2, 6, 2), Visible = false };
        Button bPanel;
        ToolTip tips = new ToolTip();
        RowList flow = new RowList { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 244, 240), Padding = new Padding(10) };
        Label lWarn = new Label { Dock = DockStyle.Bottom, Height = 0, BackColor = Color.FromArgb(255, 244, 229), ForeColor = Color.FromArgb(181, 71, 8), Padding = new Padding(8, 4, 8, 4), AutoSize = false };

        // Redacción
        Panel compose = new Panel { Dock = DockStyle.Bottom, BackColor = Color.White };
        TextBox input = new TextBox { Multiline = true, Font = new Font("Segoe UI", 10.5f) };
        Field inputField;
        Button bSend, bAttach, bTemplates, bEmoji;

        // Ficha del cliente (derecha): completa o plegada en una tira de iconos
        Panel cust = new Panel { Dock = DockStyle.Right, BackColor = Color.White };
        Panel custFull = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        Panel custStrip = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };
        Button bTransfer = new Btn { Text = "⇄   Transferir", FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White };
        Button bResolve = new Btn { Text = "✓   Resolver", FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White };
        Button bEmail = new Btn { Text = "✉   Enviar por correo", FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White };
        Button bDelete = new Btn { Text = "✕   Borrar", FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White, ForeColor = Color.FromArgb(163, 45, 45) };
        Button sTransfer, sResolve, sEmail, sHistory, sDelete; // mismos botones en la tira plegada
        Button bHeadTransfer; // atajo destacado en la cabecera del chat
        int stripHistCount; // contador que se dibuja en el botón de historial de la tira
        Label sHistoryCount = new Label { Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = Color.White, BackColor = U.BlueD, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        Dictionary<long, List<J>> histories = new Dictionary<long, List<J>>();
        HashSet<long> histLoading = new HashSet<long>();
        bool histOpen, panelForceOpen, renderingPanel;
        bool waitShown, templatesShown, headTransferShown; // .Visible devuelve false si el padre aún no se ve: la distribución usa estos
        string panelSig;

        ContextMenuStrip templatesMenu = new ContextMenuStrip();
        List<J> templates = new List<J>();
        Panel chatPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        Label empty = new WatermarkLabel { Text = "Selecciona un chat de la lista", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray, BackColor = Color.FromArgb(245, 244, 240) };
        // Barra superior: estado de conexión, buscador y conectar/desconectar
        Label status = new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        Panel statusBar = new Panel { Dock = DockStyle.Top, BackColor = Color.White };
        Button bOnline = new Btn { Text = "⏻ Desconectarme", Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        ToolStripMenuItem onlineItem = new ToolStripMenuItem("Desconectarme");
        bool offline; // desconectado: no recibe chats nuevos, avisos ni transferencias
        HashSet<long> rendered = new HashSet<long>();
        // «Está escribiendo»: chat -> quién (0 = el cliente; si no, un compañero). Lo envía el servidor en cada sincronización.
        List<J> typing = new List<J>();
        string typingSig = "";
        DateTime typedAt = DateTime.MinValue;
        long typedConv;
        volatile bool typingOff; // servidor sin esta función

        // Una persona = una conversación: chats con el mismo correo o teléfono, de cualquier canal.
        // activeId es el chat por el que se responde; el hilo muestra los mensajes de todos los chats de la persona.
        Dictionary<long, string> personOf = new Dictionary<long, string>();
        Dictionary<string, List<J>> people = new Dictionary<string, List<J>>();
        Dictionary<long, J> thread = new Dictionary<long, J>(); // chats de la persona abierta, también los antiguos que no están en la lista
        long threadId, lastRowConv; // chat cuyo hilo se está cargando; chat de la última fila dibujada
        Panel replyBar = new Panel { Dock = DockStyle.Bottom, Height = 0, BackColor = U.Paper };
        LinkLabel lReply = new LinkLabel { Font = U.Small, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, LinkColor = U.BlueD, ActiveLinkColor = U.Brand, LinkBehavior = LinkBehavior.HoverUnderline, UseMnemonic = false };
        LinkLabel lReplyNew = new LinkLabel { Font = U.SmallBold, AutoSize = true, LinkColor = U.AmberD, ActiveLinkColor = U.Brand, LinkBehavior = LinkBehavior.AlwaysUnderline, UseMnemonic = false };
        ContextMenuStrip replyMenu = new ContextMenuStrip();
        ContextMenuStrip msgMenu = new ContextMenuStrip(); // clic derecho en un mensaje
        long replyNewId;
        bool replyNewShown;

        NotifyIcon tray = new NotifyIcon();
        Icon icoNormal = U.MakeIcon(false), icoAlert = U.MakeIcon(true), icoOffline = Icon.FromHandle(BrandIcon.Render(32, Color.FromArgb(150, 156, 168)).GetHicon()); // gris = desconectado
        RingForm ringForm = new RingForm();
        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 2000 };
        System.Windows.Forms.Timer trayBlink = new System.Windows.Forms.Timer { Interval = 500 };

        // Actualización automática
        public bool JustUpdated;
        System.Windows.Forms.Timer updTimer = new System.Windows.Forms.Timer { Interval = 20000 };
        ToolStripMenuItem updItem = new ToolStripMenuItem("Buscar actualizaciones");
        Updater.Release updReady; // descargada y comprobada, a la espera de un buen momento
        DateTime updChecked = DateTime.MinValue;
        bool updBusy, updWarned;
        int updFails;

        // Cuentas creadas con el enlace secreto que esperan a un administrador
        SignupForm signupForm;
        List<J> signups = new List<J>();
        HashSet<long> signupsSeen = new HashSet<long>();
        bool signupBusy;

        public MainForm(bool minimized)
        {
            Text = "Bezares Chat";
            Font = U.Base;
            Icon = icoNormal;
            Size = new Size(U.P(1120), U.P(720));
            MinimumSize = new Size(U.P(780), U.P(480));
            StartPosition = FormStartPosition.CenterScreen;
            // Arranque con Windows: la ventana no se llega a mostrar (ver SetVisibleCore). Antes se arrancaba
            // minimizada y sin botón en la barra de tareas, y la ventana podía quedarse "colgada" fuera de la pantalla.
            startHidden = minimized;

            // Lista de chats agrupada por estado
            var left = new Panel { Dock = DockStyle.Left, Width = U.P(270), BackColor = Color.White };
            left.Controls.Add(list);
            left.Paint += (s, e) => { using (var p = new Pen(U.Line)) e.Graphics.DrawLine(p, left.Width - 1, 0, left.Width - 1, left.Height); };
            list.MeasureItem += (s, e) => { e.ItemHeight = e.Index >= 0 && e.Index < list.Items.Count && list.Items[e.Index] is HeaderItem ? U.P(28) : U.P(58); };
            list.DrawItem += DrawConv;
            // Papelera en cada fila (recepción y administración): borra sin abrir la conversación.
            list.CanTrash = i => i >= 0 && i < list.Items.Count && list.Items[i] is ConvItem && Can("delete");
            list.TrashClicked += i => { var ci = i >= 0 && i < list.Items.Count ? list.Items[i] as ConvItem : null; if (ci != null) DeleteChat(ci.Id); };
            list.SelectedIndexChanged += (s, e) =>
            {
                if (renderingList) return;
                var hi = list.SelectedItem as HeaderItem;
                if (hi != null) { if (!collapsedGroups.Remove(hi.Key)) collapsedGroups.Add(hi.Key); RenderList(); return; } // plegar/desplegar grupo
                var c = list.SelectedItem as ConvItem;
                if (c != null && !c.Ids.Contains(activeId)) OpenChat(c.Id);
            };
            // Clic en la persona que ya está abierta: si otro de sus chats está sonando, se atiende.
            list.MouseClick += (s, e) =>
            {
                int i = list.IndexFromPoint(e.Location);
                var c = i >= 0 && i < list.Items.Count ? list.Items[i] as ConvItem : null;
                if (c != null && e.Button == MouseButtons.Left && c.Ids.Contains(activeId) && c.Ids.Any(x => x != activeId && ringing.Contains(x))) OpenChat(c.Id);
            };

            // Cabecera del chat: bandera redonda, nombre, datos, tiempo de espera y botón de la ficha
            head.Height = U.P(60);
            bPanel = U.IconButton(U.IcPanelClose, "Plegar o desplegar la ficha del cliente", tips);
            bPanel.Click += (s, e) => TogglePanel();
            head.Controls.AddRange(new Control[] { picHead, lTitle, lSub, lWait, bPanel });
            head.Resize += (s, e) => LayoutHead();
            head.Paint += (s, e) => { using (var p = new Pen(U.Line)) e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1); };
            Flags.Loaded += () => { list.Invalidate(); panelSig = null; if (activeId != 0) RenderHead(); };

            // Redacción: adjuntar, plantillas, texto y enviar (botón redondo)
            compose.Height = U.P(62);
            bAttach = U.IconButton(U.IcAttach, "Adjuntar archivo (imagen, audio, vídeo o documento). También puedes arrastrarlo aquí.", tips);
            bTemplates = U.IconButton(U.IcTemplate, "Plantillas de respuesta", tips);
            bTemplates.Visible = false;
            bEmoji = U.IconButton(U.IcEmoji, "Emojis", tips);
            bEmoji.AccessibleName = "Emojis";
            Emoji.LoadRecent(cfg.Emojis);
            Emoji.Attach(bEmoji, input, () => { cfg.Emojis = string.Join(" ", Emoji.Recent.ToArray()); cfg.Save(); });
            bSend = new Btn { Text = U.IcSend, Font = U.Icons, Size = new Size(U.P(38), U.P(38)), FlatStyle = FlatStyle.Flat, BackColor = U.Brand, ForeColor = Color.White, Cursor = Cursors.Hand, TabStop = false };
            bSend.FlatAppearance.BorderSize = 0;
            bSend.FlatAppearance.MouseOverBackColor = U.BrandD;
            ((Btn)bSend).Circle = true;
            inputField = new Field(input);
            input.TextChanged += (s, e) => { LayoutCompose(); SendTyping(); };
            tips.SetToolTip(bSend, "Enviar (Intro). Mayús + Intro: nueva línea");
            compose.Controls.AddRange(new Control[] { bAttach, bTemplates, bEmoji, inputField, bSend });
            compose.Resize += (s, e) => LayoutCompose();
            compose.Paint += (s, e) => { using (var p = new Pen(U.Line)) e.Graphics.DrawLine(p, 0, 0, compose.Width, 0); };
            bSend.Click += (s, e) => SendText();
            bAttach.Click += (s, e) => SendFile();
            bTemplates.Click += async (s, e) => await ShowTemplatesMenu();
            input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; SendText(); } };

            // Ficha del cliente: acciones (también en la tira plegada)
            foreach (var b in new[] { bTransfer, bResolve, bEmail, bDelete })
            {
                b.Size = new Size(U.P(192), U.P(32));
                b.FlatAppearance.BorderColor = U.Line;
                b.FlatAppearance.MouseOverBackColor = U.Paper;
                b.Cursor = Cursors.Hand;
                b.Padding = new Padding(U.P(6), 0, 0, 0);
            }
            bResolve.FlatAppearance.BorderColor = Color.FromArgb(151, 196, 89);
            bResolve.ForeColor = U.GreenD;
            // Transferir destaca sobre las demás acciones: azul, en negrita y algo más alto (primero de la lista).
            bTransfer.BackColor = U.BlueL;
            bTransfer.ForeColor = U.BlueD;
            bTransfer.Font = U.Bold;
            bTransfer.FlatAppearance.BorderColor = U.Blue;
            bTransfer.FlatAppearance.MouseOverBackColor = Color.FromArgb(181, 212, 244);
            sTransfer = U.IconButton(U.IcTransfer, "Transferir a otro compañero", tips);
            sResolve = U.IconButton(U.IcCheck, "Marcar como resuelta", tips);
            sEmail = U.IconButton(U.IcMail, "Enviar la conversación por correo", tips);
            sHistory = U.IconButton(U.IcHistory, "Chats anteriores de este cliente", tips);
            // Contador de chats anteriores dibujado dentro del propio botón (esquina superior derecha): nunca se recorta.
            ((Btn)sHistory).Extra = g =>
            {
                if (stripHistCount <= 0) return;
                var r = new Rectangle(sHistory.Width - U.P(14), U.P(1), U.P(13), U.P(13));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(U.Blue)) g.FillEllipse(b, r);
                TextRenderer.DrawText(g, stripHistCount > 9 ? "9+" : stripHistCount.ToString(), sHistoryCount.Font, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            sDelete = U.IconButton(U.IcDelete, "Borrar definitivamente esta conversación", tips);
            sResolve.ForeColor = U.GreenD;
            sDelete.ForeColor = Color.FromArgb(163, 45, 45);
            sTransfer.BackColor = U.Blue; sTransfer.ForeColor = Color.White; // único icono relleno de la tira
            sTransfer.FlatAppearance.BorderColor = U.Blue;
            sTransfer.FlatAppearance.MouseOverBackColor = U.BlueD;
            // Atajo en la cabecera, junto al nombre: siempre a mano aunque la ficha esté plegada
            bHeadTransfer = new Btn { Text = "⇄  Transferir", Font = U.SmallBold, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat, BackColor = U.Blue, ForeColor = Color.White, Cursor = Cursors.Hand, TabStop = false, Padding = new Padding(U.P(6), U.P(2), U.P(6), U.P(2)), Visible = false };
            bHeadTransfer.FlatAppearance.BorderColor = U.Blue;
            bHeadTransfer.FlatAppearance.MouseOverBackColor = U.BlueD;
            tips.SetToolTip(bHeadTransfer, "Transferir la conversación a otro compañero conectado");
            bHeadTransfer.Click += (s, e) => Transfer();
            head.Controls.Add(bHeadTransfer);
            tips.SetToolTip(bTransfer, "Transferir la conversación a otro compañero conectado");
            tips.SetToolTip(bResolve, "Marcar la conversación como resuelta (deja de contar el tiempo de respuesta)");
            tips.SetToolTip(bEmail, "Enviar la conversación por correo electrónico");
            tips.SetToolTip(bDelete, "Borrar definitivamente esta conversación");
            bTransfer.Click += (s, e) => Transfer(); sTransfer.Click += (s, e) => Transfer();
            bResolve.Click += (s, e) => ToggleResolved(); sResolve.Click += (s, e) => ToggleResolved();
            bEmail.Click += (s, e) => EmailChat(); sEmail.Click += (s, e) => EmailChat();
            bDelete.Click += (s, e) => DeleteChat(activeId); sDelete.Click += (s, e) => DeleteChat(activeId);
            sHistory.Click += (s, e) => { histOpen = true; if (PanelCollapsedNow) TogglePanel(); else { panelSig = null; RenderPanel(); } };
            cust.Controls.Add(custFull); cust.Controls.Add(custStrip);
            cust.Paint += (s, e) => { using (var p = new Pen(U.Line)) e.Graphics.DrawLine(p, 0, 0, 0, cust.Height); };
            custFull.Padding = new Padding(1, 0, 0, 0); custStrip.Padding = new Padding(1, 0, 0, 0);
            custFull.Resize += (s, e) => { if (!renderingPanel) RenderPanel(); }; // la firma incluye el tamaño: solo se redibuja si cambia

            // Orden de acoplado: el último añadido se coloca primero (la ficha ocupa toda la altura a la derecha).
            replyBar.Controls.Add(lReply); replyBar.Controls.Add(lReplyNew);
            replyBar.Resize += (s, e) => LayoutReply();
            replyBar.Paint += (s, e) => { using (var p = new Pen(U.Line)) e.Graphics.DrawLine(p, 0, 0, replyBar.Width, 0); };
            lReply.LinkClicked += (s, e) => ShowReplyMenu();
            lReplyNew.LinkClicked += (s, e) => SwitchReply(replyNewId);
            chatPanel.Controls.Add(flow); chatPanel.Controls.Add(lWarn); chatPanel.Controls.Add(replyBar); chatPanel.Controls.Add(compose); chatPanel.Controls.Add(head); chatPanel.Controls.Add(cust);
            flow.LayoutRow = LayoutRow;
            Resize += (s, e) => { if (cust.Width != (PanelCollapsedNow ? U.P(44) : U.P(228))) ApplyPanelState(); };

            // Arrastrar y soltar archivos (documentos, fotos…) sobre la conversación para enviarlos.
            foreach (Control target in new Control[] { flow, input })
            {
                target.AllowDrop = true;
                target.DragEnter += (s, e) => { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && activeId != 0 ? DragDropEffects.Copy : DragDropEffects.None; };
                target.DragDrop += async (s, e) =>
                {
                    var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null) foreach (var f in files) await UploadPath(activeId, f);
                };
            }

            // Barra superior
            statusBar.Height = U.P(46);
            statusBar.Padding = new Padding(U.P(10), U.P(7), U.P(10), U.P(7));
            bOnline.Width = U.P(150);
            bOnline.FlatAppearance.BorderColor = U.Line;
            const string cue = "Buscar por nombre, correo o teléfono";
            // Ancho para que el texto de ayuda se lea entero; en ventanas estrechas cede sitio al estado de conexión.
            int searchWidth = Math.Max(U.P(320), TextRenderer.MeasureText(cue, U.Base).Width + U.P(44));
            var searchHost = new Panel { Dock = DockStyle.Right, Width = searchWidth, Padding = new Padding(0, 0, U.P(10), 0) };
            searchHost.Controls.Add(new Field(search) { Dock = DockStyle.Fill });
            Action fitSearch = () => searchHost.Width = Math.Max(U.P(180), Math.Min(searchWidth, statusBar.Width - statusBar.Padding.Horizontal - bOnline.Width - TextRenderer.MeasureText(status.Text, status.Font).Width - U.P(12)));
            statusBar.Resize += (s, e) => fitSearch();
            status.TextChanged += (s, e) => fitSearch();
            search.HandleCreated += (s, e) => SendMessage(search.Handle, 0x1501, (IntPtr)1, cue); // texto de ayuda
            search.TextChanged += (s, e) => RenderList();
            statusBar.Controls.Add(status); statusBar.Controls.Add(searchHost); statusBar.Controls.Add(bOnline);
            statusBar.Paint += (s, e) => { using (var p = new Pen(U.Line)) e.Graphics.DrawLine(p, 0, statusBar.Height - 1, statusBar.Width, statusBar.Height - 1); };
            tips.SetToolTip(bOnline, "Desconectado no recibes chats nuevos, avisos ni transferencias (los compañeros te ven desconectado)");
            bOnline.Click += (s, e) => ToggleOnline();
            Controls.Add(chatPanel); Controls.Add(empty); Controls.Add(left); Controls.Add(statusBar);
            ApplyPanelState();

            // Bandeja del sistema
            tray.Icon = icoNormal;
            tray.Text = "Bezares Chat";
            tray.Visible = true;
            tray.DoubleClick += (s, e) => ShowMain();
            tray.BalloonTipClicked += (s, e) => ShowMain();
            msgMenu.Opening += (s, e) => e.Cancel = !BuildMsgMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, (s, e) => ShowMain());
            var mute = new ToolStripMenuItem("Silenciar sonido") { CheckOnClick = true, Checked = cfg.Muted };
            mute.CheckedChanged += (s, e) => { cfg.Muted = mute.Checked; cfg.Save(); UpdateRinging(); };
            menu.Items.Add(mute);
            startupItem.Checked = Config.StartupEnabled;
            startupItem.Click += (s, e) => { Config.SetStartup(!startupItem.Checked); startupItem.Checked = Config.StartupEnabled; };
            menu.Items.Add(startupItem);
            onlineItem.Click += (s, e) => ToggleOnline();
            menu.Items.Add(onlineItem);
            menu.Items.Add("Cerrar sesión", null, (s, e) => Logout());
            menu.Items.Add("-");
            updItem.Click += async (s, e) => { if (updReady != null) RestartForUpdate(); else await CheckUpdate(true); };
            menu.Items.Add(updItem);
            menu.Items.Add(new ToolStripMenuItem("Versión " + Updater.Current) { Enabled = false });
            menu.Items.Add("-");
            menu.Items.Add("Salir", null, (s, e) => { quitting = true; Close(); });
            tray.ContextMenuStrip = menu;
            trayBlink.Tick += (s, e) => tray.Icon = tray.Icon == icoAlert ? icoNormal : icoAlert;

            ringForm.BAnswer.Click += (s, e) => AnswerRinging();
            ringForm.LLater.LinkClicked += (s, e) => { snoozeUntil = DateTime.Now.AddMinutes(2); UpdateRinging(); };

            updTimer.Tick += async (s, e) => await UpdateTick();
            updTimer.Start();
            timer.Tick += async (s, e) => await Sync();
            // Arranca en cuanto empieza el bucle de mensajes (no depende de Shown, que no llega si se inicia oculto).
            var boot = new System.Windows.Forms.Timer { Interval = 50 };
            boot.Tick += (s, e) => { boot.Stop(); boot.Dispose(); Start(); };
            boot.Start();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        /* ------------------------------------------------ distribución: cabecera, redacción y ficha */

        void LayoutHead()
        {
            int W = head.Width, pad = U.P(14);
            picHead.SetBounds(pad, (head.Height - U.P(40)) / 2, U.P(40), U.P(40));
            bPanel.Location = new Point(W - pad - bPanel.Width, (head.Height - bPanel.Height) / 2);
            int right = bPanel.Left - U.P(10);
            if (headTransferShown)
            {
                bHeadTransfer.Location = new Point(right - bHeadTransfer.Width, (head.Height - bHeadTransfer.Height) / 2);
                right = bHeadTransfer.Left - U.P(10);
            }
            if (waitShown) { lWait.Location = new Point(right - lWait.Width, (head.Height - lWait.Height) / 2); right = lWait.Left - U.P(10); }
            int x = picHead.Right + U.P(10);
            lTitle.SetBounds(x, U.P(9), Math.Max(50, right - x), U.P(24));
            lSub.SetBounds(x, U.P(33), Math.Max(50, right - x), U.P(18));
        }

        void LayoutCompose()
        {
            // El campo crece con el texto (hasta 4 líneas); los botones se quedan abajo.
            int fieldH = Math.Min(4, inputField.Lines) * input.Font.Height + U.P(20);
            if (compose.Height != fieldH + U.P(20) && compose.Width > 100) { compose.Height = fieldH + U.P(20); return; } // vuelve a entrar por Resize
            int one = input.Font.Height + U.P(20), bottom = compose.Height - U.P(10);
            int pad = U.P(10), top = bottom - (one + bAttach.Height) / 2, x = pad;
            bAttach.Location = new Point(x, top); x = bAttach.Right + U.P(6);
            if (templatesShown) { bTemplates.Location = new Point(x, top); x = bTemplates.Right + U.P(6); }
            bEmoji.Location = new Point(x, top); x = bEmoji.Right + U.P(6);
            bSend.Location = new Point(compose.Width - pad - bSend.Width, bottom - (one + bSend.Height) / 2);
            inputField.SetBounds(x + U.P(2), bottom - fieldH, Math.Max(80, bSend.Left - U.P(10) - x), fieldH);
        }

        /* ------------------------------------------------ una persona = una conversación */

        /** Agrupa los chats conocidos por persona: mismo correo o mismo teléfono (sin ninguno de los dos, cada chat va solo). */
        void BuildPeople()
        {
            personOf.Clear(); people.Clear();
            var mail = new Dictionary<string, string>(); // teléfono -> correo, cuando un mismo chat tiene los dos
            foreach (var c in convs.Values.Concat(thread.Values))
                if (MailKey(c) != "" && PhoneKey(c) != "") mail[PhoneKey(c)] = MailKey(c);
            foreach (var c in convs.Values)
            {
                string e = MailKey(c), p = PhoneKey(c), m;
                string key = e != "" ? "e:" + e : p != "" ? (mail.TryGetValue(p, out m) ? "e:" + m : "p:" + p) : "c:" + U.L(c, "id");
                personOf[U.L(c, "id")] = key;
                List<J> l;
                if (!people.TryGetValue(key, out l)) people[key] = l = new List<J>();
                l.Add(c);
            }
        }

        static string MailKey(J c) { return U.S(c, "email").Trim().ToLowerInvariant(); }
        static string PhoneKey(J c) { return new string(U.S(c, "phone").Where(char.IsDigit).ToArray()); }

        /** Chats de la lista que son de la misma persona que id (siempre lo incluye). */
        List<long> PersonIds(long id)
        {
            string key; List<J> l;
            var r = personOf.TryGetValue(id, out key) && people.TryGetValue(key, out l) ? l.Select(c => U.L(c, "id")).ToList() : new List<long>();
            if (!r.Contains(id)) r.Add(id);
            return r;
        }

        /** Todos los chats de la persona abierta, también los antiguos que ha traído el hilo. */
        HashSet<long> ActiveIds()
        {
            var r = new HashSet<long>();
            if (activeId == 0) return r;
            foreach (var id in PersonIds(activeId)) r.Add(id);
            foreach (var id in thread.Keys) r.Add(id);
            return r;
        }

        J ConvOf(long id) { J c; return convs.TryGetValue(id, out c) || thread.TryGetValue(id, out c) ? c : null; }

        /** Nombre, correo, teléfono o país de la persona: el del propio chat o, si le falta, el del más reciente que lo tenga. */
        string PersonField(J c, IEnumerable<long> ids, string field)
        {
            string v = U.S(c, field);
            if (v != "") return v;
            return ids.Select(ConvOf).Where(x => x != null && U.S(x, field) != "").OrderByDescending(x => U.L(x, "updated_at")).Select(x => U.S(x, field)).FirstOrDefault() ?? "";
        }

        string PersonName(J c, IEnumerable<long> ids)
        {
            string n = PersonField(c, ids, "name");
            return n != "" ? n : ConvName(c);
        }

        /** Chats sobre los que actúan Resolver y Transferir: el actual y los demás abiertos de la persona que sean míos o sin asignar. */
        List<long> ActionIds()
        {
            long myId = U.L(me, "id");
            var r = new List<long> { activeId };
            foreach (var id in ActiveIds())
            {
                var c = ConvOf(id);
                if (id == activeId || c == null || U.S(c, "status") != "open") continue;
                long asg = U.L(c, "assigned_to");
                if (asg == 0 || asg == myId) r.Add(id);
            }
            return r;
        }

        /** La conversación se considera resuelta si lo está el chat actual y no queda otro mío abierto. */
        bool ActiveResolved()
        {
            return ActionIds().Select(ConvOf).All(c => c != null && U.S(c, "status") == "resolved");
        }

        /** Chat que representa a la persona en la lista: el que suena, si no el más urgente y, a igualdad, el último en que escribió. */
        J Representative(List<J> l)
        {
            return l.OrderBy(c => ringing.Contains(U.L(c, "id")) ? 0 : 1)
                .ThenBy(c => Array.IndexOf(new[] { "transfer", "mine", "new", "others", "done" }, GroupOf(c)))
                .ThenByDescending(c => Math.Max(U.L(c, "last_customer_at"), U.L(c, "created_at"))).First();
        }

        /** Trae el hilo completo de la persona (todos sus chats y canales) y lo dibuja. */
        async Task LoadThread(long id)
        {
            if (threadId == id) return;
            threadId = id;
            try
            {
                List<J> cs = null, ms = null;
                try
                {
                    var r = await Task.Run(() => api.Get("/conversations/" + id + "/thread"));
                    cs = U.A(r, "conversations"); ms = U.A(r, "messages");
                }
                catch (ApiException ex) { if (ex.Status != 404) throw; }
                if (cs == null)
                {
                    // Servidor sin esta función: se piden uno a uno los chats conocidos de la persona.
                    cs = new List<J>(); ms = new List<J>();
                    foreach (var pid in PersonIds(id))
                    {
                        long one = pid;
                        var r = await Task.Run(() => api.Get("/conversations/" + one + "/messages"));
                        cs.Add(r["conversation"] as J); ms.AddRange(U.A(r, "messages"));
                    }
                }
                if (!ActiveIds().Contains(id)) return; // mientras tanto se abrió otra persona
                foreach (var c in cs)
                {
                    long cid = U.L(c, "id");
                    thread[cid] = c;
                    if (convs.ContainsKey(cid) || cid == id) convs[cid] = c;
                    msgs[cid] = ms.Where(m => U.L(m, "conversation_id") == cid).ToList();
                }
                BuildPeople();
                foreach (var pid in ActiveIds()) if (!msgs.ContainsKey(pid)) msgs[pid] = new List<J>(); // no pedirlo otra vez
                RenderChat(true);
            }
            finally { if (threadId == id) threadId = 0; }
        }

        /** Cambia el chat (canal) por el que se responde, sin salir de la conversación de la persona. */
        async void SwitchReply(long id)
        {
            if (id == 0 || id == activeId || !ActiveIds().Contains(id)) return;
            var c = ConvOf(id);
            if (c == null) return;
            activeId = id;
            if (!convs.ContainsKey(id)) { convs[id] = c; BuildPeople(); }
            panelSig = null;
            LoadHistory(id);
            RenderHead();
            RenderList();
            input.Focus();
            if (ringing.Contains(id)) await Accept(id);
        }

        void ShowReplyMenu()
        {
            replyMenu.Items.Clear();
            foreach (var c in ActiveIds().Select(ConvOf).Where(x => x != null).OrderBy(x => U.S(x, "status") == "open" ? 0 : 1).ThenByDescending(x => U.L(x, "last_customer_at")).Take(15))
            {
                long id = U.L(c, "id");
                string st = U.S(c, "status") == "resolved" ? " · resuelto" : U.S(c, "status") == "closed" ? " · cerrado" : "";
                string at = U.Time(U.L(c, "last_customer_at"));
                var it = new ToolStripMenuItem(ReplyName(c) + (at != "" ? " · último mensaje " + at : "") + st) { Checked = id == activeId };
                it.Click += (s, e) => SwitchReply(id);
                replyMenu.Items.Add(it);
            }
            replyMenu.Show(lReply, new Point(0, -replyMenu.PreferredSize.Height));
        }

        static string ReplyName(J c)
        {
            return U.Channel(U.S(c, "channel")) + (U.S(c, "channel") == "web" ? "" : U.S(c, "phone") != "" ? " " + U.S(c, "phone") : "");
        }

        /** Barra «Respondes por…»: solo si la persona tiene varios chats. En ámbar si ha escrito después por otro. */
        void RenderReplyBar(J c)
        {
            var ids = ActiveIds();
            bool show = ids.Count > 1;
            replyNewId = 0; replyNewShown = false;
            if (show)
            {
                // Nunca se cambia de canal solo: podría enviarse por otro canal un mensaje a medio escribir.
                var newer = ids.Where(x => x != activeId).Select(ConvOf)
                    .Where(x => x != null && (ringing.Contains(U.L(x, "id")) || WaitMs(x) > 0) && U.L(x, "last_customer_at") > U.L(c, "last_customer_at"))
                    .OrderByDescending(x => U.L(x, "last_customer_at")).FirstOrDefault();
                // En la web el correo lo escribe el propio visitante: nadie lo ha comprobado.
                lReply.Text = "Respondes por " + ReplyName(c) + (U.S(c, "channel") == "web" ? " (correo sin verificar)" : "") + "  ▾";
                tips.SetToolTip(lReply, "Elegir por qué canal responder." + (U.S(c, "channel") == "web" ? " El correo lo escribió el visitante: confirma que es esa persona antes de dar datos de sus otros chats." : ""));
                if (newer != null)
                {
                    replyNewId = U.L(newer, "id");
                    lReplyNew.Text = "Ha escrito por " + ReplyName(newer) + " a las " + U.Time(U.L(newer, "last_customer_at")) + " · responder ahí";
                    replyNewShown = true;
                }
                replyBar.BackColor = replyNewShown ? U.AmberL : U.Paper;
            }
            lReplyNew.Visible = replyNewShown;
            replyBar.Height = show ? U.P(26) : 0;
            LayoutReply();
        }

        void LayoutReply()
        {
            int pad = U.P(10), right = replyBar.Width - pad;
            if (replyNewShown) { lReplyNew.Location = new Point(Math.Max(pad, right - lReplyNew.Width), (replyBar.Height - lReplyNew.Height) / 2); right = lReplyNew.Left - pad; }
            lReply.SetBounds(pad, 1, Math.Max(0, right - pad), Math.Max(0, replyBar.Height - 1));
        }

        /** Ficha plegada si el usuario la plegó o si la ventana es estrecha (salvo que la abra a mano). */
        bool PanelCollapsedNow { get { return !panelForceOpen && (cfg.PanelCollapsed || ClientSize.Width < U.P(1000)); } }

        void TogglePanel()
        {
            if (PanelCollapsedNow) { cfg.PanelCollapsed = false; panelForceOpen = ClientSize.Width < U.P(1000); }
            else { cfg.PanelCollapsed = true; panelForceOpen = false; }
            cfg.Save();
            ApplyPanelState();
        }

        void ApplyPanelState()
        {
            bool col = PanelCollapsedNow;
            cust.Width = col ? U.P(44) : U.P(228);
            custFull.Visible = !col;
            custStrip.Visible = col;
            bPanel.Text = col ? U.IcPanelOpen : U.IcPanelClose;
            tips.SetToolTip(bPanel, col ? "Desplegar la ficha del cliente" : "Plegar la ficha del cliente");
            panelSig = null;
            RenderPanel();
        }

        /** Tiempo que lleva esperando el cliente (mensaje suyo sin respuesta), según el reloj del servidor; 0 si no espera. */
        long WaitMs(J c)
        {
            // El aviso de las 23 h (WhatsApp y Facebook) también para la espera.
            long lc = U.L(c, "last_customer_at"), la = Math.Max(U.L(c, "last_agent_at"), U.L(c, "window_notice_at"));
            if (U.S(c, "status") != "open" || lc <= 0 || lc <= la) return 0;
            return Math.Max(1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + clockSkew - lc);
        }

        void WaitColors(long ms, out Color bg, out Color fg)
        {
            long m = ms / 60000;
            if (m >= waitMinutes) { bg = U.BrandL; fg = U.BrandD; }
            else if (m >= Math.Max(1, waitMinutes / 2)) { bg = U.AmberL; fg = U.AmberD; }
            else { bg = U.Line2; fg = U.Ink2; }
        }

        static void DisposeChildren(Control parent, HashSet<Control> keep)
        {
            var all = parent.Controls.Cast<Control>().ToList();
            parent.Controls.Clear();
            foreach (var ctl in all) if (!keep.Contains(ctl)) ctl.Dispose();
        }

        /** Dibuja la ficha del cliente (completa o tira). Solo se reconstruye si ha cambiado algo de lo que muestra. */
        void RenderPanel()
        {
            if (renderingPanel) return; // sin reentradas (al aparecer la barra de desplazamiento cambia el tamaño)
            renderingPanel = true;
            try { RenderPanelCore(); }
            finally { renderingPanel = false; }
        }

        void RenderPanelCore()
        {
            J c;
            if (activeId == 0 || !convs.TryGetValue(activeId, out c)) return;
            var ids = ActiveIds();
            string pname = PersonName(c, ids), pmail = PersonField(c, ids, "email"), pcountry = PersonField(c, ids, "country");
            // Canales de la persona, sin repetir (el actual primero)
            var chans = ids.OrderBy(x => x == activeId ? 0 : 1).Select(ConvOf).Where(x => x != null).Select(x => U.S(x, "channel") + "|" + U.S(x, "phone")).Distinct().ToList();
            bool col = PanelCollapsedNow;
            List<J> hist; histories.TryGetValue(activeId, out hist);
            bool loading = histLoading.Contains(activeId);
            // Respuestas a las preguntas del robot (las del chat más reciente mandan).
            var answers = new List<string[]>();
            foreach (var ac in ids.Select(ConvOf).Where(x => x != null).OrderBy(x => U.L(x, "created_at")))
                foreach (var an in U.A(ac, "answers"))
                {
                    answers.RemoveAll(x => x[0] == U.S(an, "label"));
                    answers.Add(new[] { U.S(an, "label"), U.S(an, "value") });
                }
            string sig = string.Join("|", string.Join(";", answers.Select(x => x[0] + "=" + x[1])), activeId, col, custFull.Width, custFull.Height, ActiveResolved(), pname, pmail, string.Join(",", chans), pcountry,
                U.S(c, "lang"), U.L(c, "assigned_to"), hist == null ? -1 : hist.Count, loading, histOpen, permKey, bResolve.Enabled);
            if (sig == panelSig) return;
            panelSig = sig;
            bool resolved = ActiveResolved();
            string ch = U.S(c, "channel"), cc = pcountry;
            var keep = new HashSet<Control> { bTransfer, bResolve, bEmail, bDelete, sTransfer, sResolve, sEmail, sHistory, sDelete, sHistoryCount };
            bResolve.Text = resolved ? "↺   Reabrir" : "✓   Resolver";
            tips.SetToolTip(sResolve, resolved ? "Volver a abrir la conversación" : "Marcar como resuelta");
            sResolve.Text = resolved ? U.IcUndo : U.IcCheck;
            int histCount = hist == null ? 0 : hist.Count;

            if (col)
            {
                custStrip.SuspendLayout();
                DisposeChildren(custStrip, keep);
                var av = new PictureBox { Image = Flags.Circle(cc, U.P(28), ch), SizeMode = PictureBoxSizeMode.CenterImage, Bounds = new Rectangle(U.P(7), U.P(12), U.P(30), U.P(30)) };
                tips.SetToolTip(av, pname + (cc != "" ? " · " + Flags.Name(cc) : ""));
                custStrip.Controls.Add(av);
                int y = U.P(54);
                foreach (var b in new[] { sTransfer, sResolve, sEmail, sHistory, sDelete })
                {
                    bool vis = b == sTransfer ? Can("transfer") : b == sResolve ? Can("resolve") : b == sEmail ? Can("email") : b == sDelete ? Can("delete") : true;
                    if (!vis) continue;
                    b.Location = new Point(U.P(6), y);
                    custStrip.Controls.Add(b);
                    y += b.Height + U.P(6);
                }
                tips.SetToolTip(sHistory, hist == null ? "Chats anteriores de este cliente" : histCount == 0 ? "Sin chats anteriores" : histCount + (histCount == 1 ? " chat anterior" : " chats anteriores"));
                stripHistCount = histCount;
                sHistory.Invalidate();
                custStrip.ResumeLayout();
                return;
            }

            custFull.SuspendLayout();
            DisposeChildren(custFull, keep);
            // Ancho fijo que deja sitio a la barra de desplazamiento: así su aparición no cambia la distribución.
            int W = custFull.Width - SystemInformation.VerticalScrollBarWidth, x0 = U.P(14), w = W - U.P(22);
            int yy = U.P(18);
            var big = new PictureBox { Image = Flags.Circle(cc, U.P(56), ch), SizeMode = PictureBoxSizeMode.CenterImage, Bounds = new Rectangle((W - U.P(58)) / 2, yy, U.P(58), U.P(58)) };
            custFull.Controls.Add(big);
            yy = big.Bottom + U.P(6);
            custFull.Controls.Add(new Label { Text = pname, Font = U.Bold, ForeColor = U.Ink, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, UseMnemonic = false, Bounds = new Rectangle(x0, yy, w, U.P(22)) });
            yy += U.P(22);
            var place = new List<string>();
            if (cc != "") place.Add(Flags.Name(cc));
            string lang = U.S(c, "lang");
            if (lang != "") place.Add(U.LangName(lang));
            custFull.Controls.Add(new Label { Text = place.Count > 0 ? string.Join(" · ", place) : U.Channel(ch), Font = U.Small, ForeColor = U.Muted, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Bounds = new Rectangle(x0, yy, w, U.P(18)) });
            yy += U.P(28);
            custFull.Controls.Add(new Panel { BackColor = U.Line2, Bounds = new Rectangle(x0, yy, w, 1) });
            yy += U.P(10);

            Action<string, string, string> kv = (glyph, text, tip) =>
            {
                custFull.Controls.Add(new Label { Text = glyph, Font = U.Icons, ForeColor = U.Ink2, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(x0 - U.P(2), yy, U.P(22), U.P(22)) });
                var l = new Label { Text = text, Font = U.Small, ForeColor = U.Ink2, AutoEllipsis = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(x0 + U.P(24), yy, w - U.P(24), U.P(22)) };
                tips.SetToolTip(l, tip ?? text);
                custFull.Controls.Add(l);
                yy += U.P(24);
            };
            foreach (var chan in chans)
            {
                string kch = chan.Split('|')[0], phone = chan.Split('|')[1];
                kv(kch == "whatsapp" ? U.IcPhone : kch == "facebook" ? U.IcChat : U.IcGlobe, U.Channel(kch) + (phone != "" ? " · " + phone : ""), null);
            }
            if (pmail != "") kv(U.IcMail, pmail, null);
            foreach (var an in answers) kv(U.IcChat, an[0] + ": " + an[1], null);
            kv(U.IcClock, "Inicio " + U.Time(U.L(c, "created_at")), null);
            long asg = U.L(c, "assigned_to");
            kv(U.IcPerson, asg != 0 ? "Atiende: " + AgentName(asg) : "Sin asignar", null);
            string page = U.S(c, "page_url");
            if (ch == "web" && page != "") kv(U.IcGlobe, "Desde: " + page, page);

            // Chats anteriores del mismo cliente (mismo correo, teléfono o remitente)
            yy += U.P(4);
            if (hist == null || loading)
                custFull.Controls.Add(new Label { Text = "Buscando chats anteriores…", Font = U.Small, ForeColor = U.Muted, Bounds = new Rectangle(x0, yy, w, U.P(22)), TextAlign = ContentAlignment.MiddleLeft });
            else if (histCount == 0)
                custFull.Controls.Add(new Label { Text = "Sin chats anteriores", Font = U.Small, ForeColor = U.Muted, Bounds = new Rectangle(x0, yy, w, U.P(22)), TextAlign = ContentAlignment.MiddleLeft });
            else
            {
                var hb = new Btn { Text = (histOpen ? "▴  " : "▾  ") + histCount + (histCount == 1 ? " chat anterior" : " chats anteriores"), Font = U.Small, ForeColor = U.BlueD, FlatStyle = FlatStyle.Flat, BackColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand, Bounds = new Rectangle(x0, yy, w, U.P(28)) };
                hb.FlatAppearance.BorderColor = U.Line;
                hb.FlatAppearance.MouseOverBackColor = U.Paper;
                tips.SetToolTip(hb, "Conversaciones anteriores de este cliente (mismo correo, teléfono o remitente)");
                hb.Click += (s, e) => { histOpen = !histOpen; panelSig = null; RenderPanel(); };
                custFull.Controls.Add(hb);
                yy += U.P(32);
                if (histOpen)
                    foreach (var h in hist)
                    {
                        var item = h;
                        string st = U.S(item, "status") == "resolved" ? "resuelto" : U.S(item, "status") == "closed" ? "cerrado" : "abierto";
                        string when = DateTimeOffset.FromUnixTimeMilliseconds(U.L(item, "created_at")).LocalDateTime.ToString("dd/MM/yyyy");
                        string line2 = U.S(item, "subject") + (U.S(item, "agent") != "" ? " · " + U.S(item, "agent") : "") + " · " + st;
                        custFull.Controls.Add(new Panel { BackColor = Color.FromArgb(181, 212, 244), Bounds = new Rectangle(x0, yy + U.P(2), U.P(2), U.P(34)) });
                        var hl = new LinkLabel { Text = when + " · " + U.Channel(U.S(item, "channel")), Font = U.SmallBold, LinkColor = U.Ink, ActiveLinkColor = U.Brand, LinkBehavior = LinkBehavior.HoverUnderline, AutoSize = false, UseMnemonic = false, Bounds = new Rectangle(x0 + U.P(8), yy, w - U.P(8), U.P(18)) };
                        var hs = new Label { Text = line2, Font = U.Small, ForeColor = U.Muted, AutoEllipsis = true, UseMnemonic = false, Cursor = Cursors.Hand, Bounds = new Rectangle(x0 + U.P(8), yy + U.P(18), w - U.P(8), U.P(18)) };
                        tips.SetToolTip(hl, "Ir a este chat en la conversación"); tips.SetToolTip(hs, line2);
                        hl.LinkClicked += (s, e) => GoToChat(item);
                        hs.Click += (s, e) => GoToChat(item);
                        custFull.Controls.Add(hl); custFull.Controls.Add(hs);
                        yy += U.P(42);
                    }
            }

            // Acciones (según los permisos del rol), debajo del contenido o al pie si hay sitio
            var acts = new List<Button>();
            if (Can("transfer")) acts.Add(bTransfer);
            if (Can("resolve")) acts.Add(bResolve);
            if (Can("email")) acts.Add(bEmail);
            if (Can("delete")) acts.Add(bDelete);
            Func<Button, int> bh = b => b == bTransfer ? U.P(40) : U.P(32); // Transferir, más alto
            int need = acts.Sum(b => bh(b) + U.P(6));
            int ay = Math.Max(yy + U.P(16), custFull.Height - need - U.P(8));
            foreach (var b in acts) { b.Bounds = new Rectangle(x0, ay, w, bh(b)); custFull.Controls.Add(b); ay += bh(b) + U.P(6); }
            custFull.ResumeLayout();
        }

        async void LoadHistory(long id)
        {
            if (histLoading.Contains(id)) return;
            histLoading.Add(id);
            if (activeId == id) { panelSig = null; RenderPanel(); }
            List<J> list2;
            try { list2 = U.A(await Task.Run(() => api.Get("/conversations/" + id + "/history")), "history"); }
            catch { list2 = new List<J>(); } // servidor sin esta función o sin conexión: sin historial
            histories[id] = list2;
            histLoading.Remove(id);
            if (activeId == id) { panelSig = null; RenderPanel(); }
        }

        /** Va al comienzo de un chat anterior dentro del hilo; si no está en él (muy antiguo), lo abre en solo lectura. */
        void GoToChat(J h)
        {
            string name = "conv" + U.L(h, "id");
            var row = flow.Controls.Cast<Control>().FirstOrDefault(x => x.Name == name);
            if (row == null) { ShowHistoryChat(h); return; }
            var vs = flow.VerticalScroll;
            flow.AutoScrollPosition = new Point(0, vs.Value + row.Top);
        }

        /** Abre un chat anterior en solo lectura (no cambia el chat actual). */
        async void ShowHistoryChat(J h)
        {
            long hid = U.L(h, "id");
            try
            {
                var r = await Task.Run(() => api.Get("/conversations/" + hid + "/messages"));
                var c = r["conversation"] as J;
                var sb = new StringBuilder();
                foreach (var m in U.A(r, "messages"))
                {
                    string dir = U.S(m, "direction"), who = dir == "in" ? ConvName(c) : dir == "out" ? (AgentName(U.L(m, "agent_id")) != "" ? AgentName(U.L(m, "agent_id")) : "Agente") : dir == "bot" ? "Mensaje automático" : "Nota";
                    string body = U.S(m, "body"), tr = U.S(m, "translated");
                    if (U.L(m, "deleted") != 0) body = "(mensaje borrado)";
                    if (dir == "in" && tr != "") body = tr + "   [" + U.LangName(U.S(m, "lang")) + ": " + body + "]";
                    if (U.S(m, "media_url") != "") body += (body != "" ? "  " : "") + "[adjunto: " + (U.S(m, "media_name") != "" ? U.S(m, "media_name") : U.S(m, "media_type")) + "]";
                    sb.Append("[").Append(DateTimeOffset.FromUnixTimeMilliseconds(U.L(m, "created_at")).LocalDateTime.ToString("dd/MM/yyyy HH:mm")).Append("] ").Append(who).Append(": ").Append(body).Append(Environment.NewLine);
                }
                string when = DateTimeOffset.FromUnixTimeMilliseconds(U.L(c, "created_at")).LocalDateTime.ToString("dd/MM/yyyy");
                using (var f = new Form { Text = "Chat anterior · " + ConvName(c) + " · " + when, Size = new Size(U.P(680), U.P(540)), StartPosition = FormStartPosition.CenterParent, Font = U.Base, Icon = Icon, MinimizeBox = false, ShowInTaskbar = false })
                {
                    string st = U.S(c, "status") == "resolved" ? "resuelto" : U.S(c, "status") == "closed" ? "cerrado" : "abierto";
                    var info = new Label { Dock = DockStyle.Top, Height = U.P(30), Text = "  " + U.Channel(U.S(c, "channel")) + " · " + st + " · solo lectura", ForeColor = U.Muted, TextAlign = ContentAlignment.MiddleLeft, BackColor = U.Paper };
                    var tb = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = sb.Length > 0 ? sb.ToString() : "(sin mensajes)", BackColor = Color.White, BorderStyle = BorderStyle.None, Font = U.Base };
                    f.Controls.Add(tb); f.Controls.Add(info);
                    f.Shown += (s, e) => { tb.SelectionStart = 0; tb.SelectionLength = 0; };
                    f.ShowDialog(this);
                }
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
        }

        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

        protected override void SetVisibleCore(bool value)
        {
            // Inicio con Windows: crea la ventana pero la deja oculta; solo vive el icono de la bandeja.
            if (startHidden && value && !IsHandleCreated) { CreateHandle(); value = false; }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cerrar la ventana la minimiza a la bandeja para seguir recibiendo avisos.
            if (!quitting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                tray.ShowBalloonTip(3000, "Bezares Chat sigue activo", "Seguirás recibiendo los avisos. Para salir: clic derecho en este icono → Salir.", ToolTipIcon.Info);
                return;
            }
            Sounds.StopRing();
            tray.Visible = false;
            // Al salir del programa se aparece desconectado al instante (máx. 2 s de espera).
            if (me != null && !offline && !string.IsNullOrEmpty(api.Token))
                try { Task.Run(() => api.Post("/me/offline", null)).Wait(2000); } catch { }
            base.OnFormClosing(e);
            // Al salir se aprovecha para instalar la actualización pendiente (la próxima vez se abre la nueva).
            if (!e.Cancel && updReady != null) { try { Updater.Swap(Application.ExecutablePath); updReady = null; } catch (Exception ex) { Program.LogError(ex); } }
        }

        /* ------------------------------------------------ actualización automática */

        [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint cbSize, dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput p);

        static double IdleSeconds()
        {
            var i = new LastInput { cbSize = (uint)Marshal.SizeOf(typeof(LastInput)) };
            return GetLastInputInfo(ref i) ? unchecked((uint)Environment.TickCount - i.dwTime) / 1000.0 : 0;
        }

        /** ¿Se puede reiniciar sin molestar? Sin avisos, sin nada a medio escribir, sin ventanas abiertas y sin estar usando el programa. */
        bool GoodMomentToUpdate()
        {
            if (ringing.Count > 0 || input.Text.Trim() != "" || signupBusy || !bSend.Enabled || !bAttach.Enabled) return false;
            if (Application.OpenForms.Cast<Form>().Any(f => f != this && f != ringForm && f != signupForm && f.Visible)) return false;
            if (me != null && !cfg.Remember) return false; // al reiniciar habría que volver a escribir la contraseña: se instala al salir
            return Form.ActiveForm == null && (!Visible || WindowState == FormWindowState.Minimized || IdleSeconds() > 60) || IdleSeconds() > 180;
        }

        async Task UpdateTick()
        {
            if (JustUpdated)
            {
                JustUpdated = false;
                tray.ShowBalloonTip(4000, "Bezares Chat actualizado", "Ya tienes la versión " + Updater.Current + ".", ToolTipIcon.Info);
            }
            if (updReady != null) { if (GoodMomentToUpdate()) RestartForUpdate(); return; }
            if ((DateTime.UtcNow - updChecked).TotalMinutes >= 60) await CheckUpdate(false);
        }

        async Task CheckUpdate(bool manual)
        {
            if (updBusy || updReady != null) return;
            updBusy = true;
            updChecked = DateTime.UtcNow;
            string exe = Application.ExecutablePath, error = null;
            Updater.Release rel = null;
            try
            {
                string server = Api.Normalize(cfg.Server);
                rel = await Task.Run(() =>
                {
                    var r = Updater.Check(server, Updater.Current, null);
                    if (r != null) Updater.Download(server, r, exe);
                    return r;
                });
            }
            catch (Exception ex) { error = ex.Message; if (!(ex is WebException)) Program.LogError(ex); }
            finally { updBusy = false; }
            if (rel != null)
            {
                updReady = rel;
                updItem.Text = "Reiniciar para actualizar a la " + rel.VersionText;
                if (manual) RestartForUpdate();
                return;
            }
            if (manual) MessageBox.Show(error == null ? "Tienes la última versión (" + Updater.Current + ")." : "No se ha podido comprobar o descargar la actualización:\n" + error, "Bezares Chat");
            else if (error != null && !updWarned && !CanWrite(exe))
            {
                updWarned = true; // una vez por sesión: carpeta sin permiso de escritura
                tray.ShowBalloonTip(8000, "No se puede actualizar Bezares Chat", "La carpeta del programa no permite escribir. Muévelo a una carpeta tuya, por ejemplo en Documentos.", ToolTipIcon.Warning);
            }
        }

        static bool CanWrite(string exe)
        {
            try { string t = exe + ".tmp"; File.WriteAllText(t, ""); File.Delete(t); return true; }
            catch { return false; }
        }

        async void RestartForUpdate()
        {
            if (updReady == null || updBusy) return;
            string exe = Application.ExecutablePath;
            try { Updater.Swap(exe); }
            catch (Exception ex)
            {
                // El antivirus puede tener el archivo ocupado mientras lo analiza: se reintenta en la próxima vuelta.
                if (++updFails >= 30 || !File.Exists(exe + ".new")) { Program.LogError(ex); updReady = null; updFails = 0; updItem.Text = "Buscar actualizaciones"; }
                return;
            }
            updReady = null;
            updBusy = true;
            bool hidden = !Visible || WindowState == FormWindowState.Minimized, started = false;
            for (int i = 0; i < 25 && !started; i++)
            {
                try { Process.Start(new ProcessStartInfo(exe, "--updated" + (hidden ? " --minimized" : "")) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) }); started = true; }
                catch (System.ComponentModel.Win32Exception ex) { if (i == 24) Program.LogError(ex); }
                if (!started) await Task.Delay(2000);
            }
            if (!started)
            {
                // Se sigue con la versión abierta: la nueva se usará la próxima vez que se abra el programa.
                updItem.Text = "Actualizado: vuelve a abrir el programa"; updItem.Enabled = false;
                return;
            }
            quitting = true;
            Close();
        }

        /* ------------------------------------------------ solicitudes de alta (administradores) */

        void UpdateSignups(List<J> list)
        {
            if (signupBusy) return;
            signups = list;
            if (list.Count == 0) { if (signupForm != null) signupForm.Hide(); return; }
            bool fresh = list.Count(x => signupsSeen.Add(U.L(x, "id"))) > 0;
            if (signupForm == null) signupForm = new SignupForm { Decide = DecideSignup };
            if (fresh)
            {
                if (!cfg.Muted) Sounds.Ping();
                signupForm.ShowItem(list[0], list.Count);
            }
            else if (signupForm.Visible && !list.Any(x => U.L(x, "id") == U.L(signupForm.Item, "id"))) signupForm.ShowItem(list[0], list.Count); // la resolvió otro administrador
        }

        async void DecideSignup(J item, string action)
        {
            signupBusy = true;
            try
            {
                long id = U.L(item, "id");
                var r = await Task.Run(() => api.Post("/signups/" + id, new J { { "action", action } }));
                signups = U.A(r, "signups");
                if (action == "enable") tray.ShowBalloonTip(4000, "Cuenta habilitada", U.S(item, "name") + " ya puede entrar.", ToolTipIcon.Info);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(signupForm, ex.Message, "Solicitud de acceso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (ex.Status == 404) signups = signups.Where(x => U.L(x, "id") != U.L(item, "id")).ToList();
            }
            finally { signupBusy = false; }
            foreach (var s in signups) signupsSeen.Add(U.L(s, "id"));
            if (signups.Count > 0) signupForm.ShowItem(signups[0], signups.Count); else signupForm.Hide();
        }

        void ShowMain()
        {
            startHidden = false;
            if (!ShowInTaskbar) ShowInTaskbar = true;
            Show();
            // Se comprueba también el estado real de la ventana: WindowState puede no coincidir con él.
            if (WindowState == FormWindowState.Minimized || IsIconic(Handle))
            {
                ShowWindow(Handle, 9); // SW_RESTORE
                WindowState = FormWindowState.Normal;
            }
            Activate();
            SetForegroundWindow(Handle);
        }

        /* ------------------------------------------------ sesión */

        async void Start()
        {
            if (!string.IsNullOrEmpty(cfg.Token))
            {
                try { api.Base = Api.Normalize(cfg.Server); }
                catch { ShowLogin(null); return; }
                api.Token = cfg.Token;
                // Al iniciar Windows la red puede tardar en estar lista: sin conexión se reintenta en vez de pedir la contraseña.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        var r = await Task.Run(() => api.Get("/me"));
                        if (me != null || string.IsNullOrEmpty(api.Token)) return; // ya se entró o se cerró sesión mientras tanto
                        StartSession(r["agent"] as J);
                        return;
                    }
                    catch (ApiException ex)
                    {
                        if (ex.Status != 0 && ex.Status < 500) break; // sesión caducada o rechazada: pedir contraseña
                    }
                    catch (Exception ex) { Program.LogError(ex); break; }
                    if (me != null || string.IsNullOrEmpty(api.Token)) return;
                    status.Text = "Sin conexión con el servidor, reintentando…";
                    tray.Text = "Bezares Chat (sin conexión)";
                    await Task.Delay(Math.Min(60, 5 + attempt * 5) * 1000);
                    if (me != null || string.IsNullOrEmpty(api.Token)) return;
                }
            }
            ShowLogin(null);
        }

        void ShowLogin(string error)
        {
            timer.Stop();
            me = null;
            if (signupForm != null) signupForm.Hide();
            signupsSeen.Clear();
            Hide();
            startHidden = false;
            WindowState = FormWindowState.Normal;
            using (var lf = new LoginForm(cfg))
            {
                if (error != null) lf.LErr.Text = error;
                lf.BOk.Click += async (s, e) =>
                {
                    lf.LErr.Text = "";
                    lf.BOk.Enabled = false; lf.BOk.Text = "Conectando…";
                    try
                    {
                        string server = Api.Normalize(lf.TServer.Text);
                        string email = lf.TEmail.Text.Trim(), pass = lf.TPass.Text;
                        var a = new Api { Base = server };
                        var r = await Task.Run(() => a.Post("/auth/login", new J { { "email", email }, { "password", pass } }));
                        cfg.Server = server; cfg.Email = email; cfg.Token = U.S(r, "token");
                        cfg.Remember = lf.CRemember.Checked;
                        cfg.Save();
                        if (lf.CStartup.Checked != Config.StartupEnabled) { Config.SetStartup(lf.CStartup.Checked); startupItem.Checked = lf.CStartup.Checked; }
                        api.Base = server; api.Token = cfg.Token;
                        me = r["agent"] as J;
                        lf.DialogResult = DialogResult.OK;
                    }
                    catch (ApiException ex) { lf.LErr.Text = ex.Message; }
                    finally { lf.BOk.Enabled = true; lf.BOk.Text = "Entrar"; lf.TPass.Text = ""; }
                };
                if (lf.ShowDialog() != DialogResult.OK) { quitting = true; Close(); return; }
            }
            StartSession(me);
        }

        void StartSession(J agent)
        {
            me = agent;
            convs.Clear(); msgs.Clear(); unread.Clear(); ringing.Clear(); seenIn.Clear();
            thread.Clear(); histories.Clear(); BuildPeople();
            since = 0; firstSync = true; activeId = 0; syncCount = 0;
            offline = false; ApplyOnlineUi();
            Text = "Bezares Chat — " + U.S(me, "name");
            tray.Text = "Bezares Chat";
            ApplyPerms();
            templates.Clear();
            chatPanel.Visible = false; empty.Visible = true;
            if (startHidden)
            {
                // Arranque con Windows: se queda en la bandeja; el aviso de llamada aparecerá igualmente.
                startHidden = false;
                tray.ShowBalloonTip(3000, "Bezares Chat", "Conectado como " + U.S(me, "name") + ". Recibirás los avisos de chat aquí.", ToolTipIcon.Info);
            }
            else ShowMain();
            timer.Start();
            var _ = Sync();
        }

        async void Logout()
        {
            timer.Stop();
            Sounds.StopRing();
            ringForm.Hide();
            try { await Task.Run(() => api.Post("/auth/logout", null)); } catch { }
            cfg.Token = ""; cfg.Save(); api.Token = "";
            ShowLogin(null);
        }

        /* ------------------------------------------------ permisos del rol */

        /** ¿Tiene mi rol este permiso? (Si el servidor aún no envía permisos, se usa el comportamiento anterior por rol.) */
        bool Can(string perm)
        {
            if (me == null) return false;
            object v;
            if (me.TryGetValue("perms", out v) && v is object[])
                return ((object[])v).Any(x => Convert.ToString(x) == perm);
            string r = U.S(me, "role");
            if (perm == "transfer" || perm == "delete") return r != "sales";
            if (perm == "templates") return r == "receptionist";
            return true;
        }

        string permKey = null;

        /** Muestra u oculta los botones según los permisos (se vuelve a aplicar si el administrador los cambia). */
        void ApplyPerms()
        {
            // Transferir, resolver, correo y borrar están en la ficha del cliente (RenderPanel los coloca según permisos).
            templatesShown = Can("templates");
            bTemplates.Visible = templatesShown;
            LayoutCompose();
            headTransferShown = Can("transfer");
            bHeadTransfer.Visible = headTransferShown;
            LayoutHead();
            object v;
            permKey = me != null && me.TryGetValue("perms", out v) && v is object[] ? string.Join(",", ((object[])v).Select(Convert.ToString)) : "";
            panelSig = null;
            RenderPanel();
            list.Invalidate(); // papelera en la lista
        }

        /** En cada sincronización, toma mis permisos actuales de la lista de agentes. */
        void RefreshMyPerms()
        {
            if (me == null) return;
            var mine = agents.FirstOrDefault(a => U.L(a, "id") == U.L(me, "id"));
            object v;
            if (mine == null || !mine.TryGetValue("perms", out v) || !(v is object[])) return;
            string key = string.Join(",", ((object[])v).Select(Convert.ToString));
            if (key == permKey) return;
            me["perms"] = v;
            me["role"] = U.S(mine, "role");
            if (mine.ContainsKey("role_name")) me["role_name"] = mine["role_name"];
            ApplyPerms();
        }

        /* ------------------------------------------------ conectado / desconectado */

        async void ToggleOnline()
        {
            if (me == null) return;
            if (!offline)
            {
                // Desconectarse: deja de sincronizar (no suena, no recibe chats nuevos) y el servidor lo marca al instante.
                offline = true;
                timer.Stop();
                ringing.Clear();
                UpdateRinging();
                ApplyOnlineUi();
                try { await Task.Run(() => api.Post("/me/offline", null)); } catch { }
            }
            else
            {
                offline = false;
                ApplyOnlineUi();
                timer.Start();
                await Sync();
            }
        }

        void ApplyOnlineUi()
        {
            bOnline.Text = offline ? "▶ Conectarme" : "⏻ Desconectarme";
            bOnline.ForeColor = offline ? Color.FromArgb(26, 127, 55) : Color.Black;
            onlineItem.Text = offline ? "Conectarme" : "Desconectarme";
            tray.Icon = offline ? icoOffline : icoNormal;
            tray.Text = offline ? "Bezares Chat — desconectado" : "Bezares Chat";
            if (offline)
            {
                status.Text = "  ⚫ Desconectado: no recibes chats nuevos, avisos ni transferencias";
                status.ForeColor = Color.DimGray;
            }
        }

        /* ------------------------------------------------ sincronización */

        async Task Sync()
        {
            if (syncing || me == null || offline) return;
            syncing = true;
            try
            {
                syncCount++;
                bool full = since == 0 || syncCount % 30 == 0; // recarga completa cada ~60 s
                string path = "/sync?since=" + (full ? 0 : since);
                var d = await Task.Run(() => api.Get(path));
                status.Text = U.B(d, "business_open") ? "  ● Conectado · En horario laboral" : "  ● Conectado · Fuera de horario";
                status.ForeColor = Color.FromArgb(26, 127, 55);
                Apply(d, full && since != 0);
            }
            catch (ApiException ex)
            {
                if (ex.Status == 401) { cfg.Token = ""; cfg.Save(); ShowLogin("La sesión ha caducado. Vuelve a entrar."); return; }
                status.Text = "  ⚠ Sin conexión con el servidor, reintentando…";
                status.ForeColor = Color.FromArgb(198, 40, 40);
            }
            finally { syncing = false; }
        }

        void Apply(J d, bool full)
        {
            since = U.L(d, "now");
            if (since > 0) clockSkew = since - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (U.L(d, "wait_minutes") > 0) waitMinutes = U.L(d, "wait_minutes");
            agents = U.A(d, "agents");
            typing = U.A(d, "typing");
            UpdateSignups(U.A(d, "signups"));
            RefreshMyPerms();
            var incoming = U.A(d, "conversations");
            if (full)
            {
                var ids = new HashSet<long>(incoming.Select(c => U.L(c, "id")));
                foreach (var id in convs.Keys.ToList())
                {
                    if (ids.Contains(id)) continue;
                    // Un chat de hace más de 30 días no viene en la sincronización: si se está usando para responder, se conserva.
                    if (id == activeId && U.L(convs[id], "updated_at") < since - 29L * 86400000) continue;
                    convs.Remove(id); thread.Remove(id);
                }
                if (activeId != 0 && !convs.ContainsKey(activeId)) CloseChat();
            }
            bool activeChanged = false;
            var active = ActiveIds();
            foreach (var c in incoming)
            {
                long id = U.L(c, "id");
                J prev;
                if (active.Contains(id) && (!convs.TryGetValue(id, out prev) || U.L(prev, "updated_at") != U.L(c, "updated_at"))) activeChanged = true;
                convs[id] = c;
            }
            BuildPeople();
            active = ActiveIds(); // un chat puede pasar a ser de la persona abierta (acaba de dar su correo)

            var newIn = new List<J>();
            foreach (var m in U.A(d, "messages"))
            {
                long cid = U.L(m, "conversation_id"), mid = U.L(m, "id");
                List<J> l;
                if (msgs.TryGetValue(cid, out l))
                {
                    int at = l.FindIndex(x => U.L(x, "id") == mid);
                    if (at < 0) { l.Add(m); if (active.Contains(cid)) activeChanged = true; }
                    else if (U.L(l[at], "changed_at") < U.L(m, "changed_at")) { l[at] = m; ReplaceRow(m); } // editado o borrado
                }
                if (U.S(m, "direction") == "in" && !firstSync && seenIn.Add(mid)) newIn.Add(m);
            }
            bool focused = Visible && WindowState != FormWindowState.Minimized && Form.ActiveForm == this;
            foreach (var m in newIn)
            {
                long cid = U.L(m, "conversation_id");
                if (!active.Contains(cid) || !focused) unread[cid] = (unread.ContainsKey(cid) ? unread[cid] : 0) + 1;
            }
            if (newIn.Count > 0 && !(focused && newIn.All(m => active.Contains(U.L(m, "conversation_id")))))
            {
                if (!cfg.Muted) Sounds.Ping();
                if (!focused)
                {
                    var m = newIn[newIn.Count - 1];
                    J c; convs.TryGetValue(U.L(m, "conversation_id"), out c);
                    string body = U.S(m, "translated"); if (body == "") body = U.S(m, "body"); if (body == "") body = "📎 Adjunto";
                    tray.ShowBalloonTip(4000, c != null ? ConvName(c) : "Nuevo mensaje", body, ToolTipIcon.Info);
                    Native.Flash(this, true);
                }
            }

            var newRinging = new HashSet<long>(U.Ids(d, "ringing"));
            bool fresh = newRinging.Any(id => !ringing.Contains(id));
            ringing = newRinging;
            if (fresh && !firstSync) snoozeUntil = DateTime.MinValue; // un chat nuevo anula el "posponer"
            firstSync = false;
            UpdateRinging();
            RenderList();
            if (activeId != 0)
            {
                if (activeChanged) RenderChat(false); else { RenderHead(); UpdateTyping(); }
                // Chat nuevo de la persona abierta: se añade a su hilo (si falla, se reintenta en la próxima sincronización).
                if (active.Any(x => !msgs.ContainsKey(x))) { var _ = ReloadThread(activeId); }
            }
        }

        async Task ReloadThread(long id)
        {
            try { await LoadThread(id); } catch (ApiException) { }
        }

        void UpdateRinging()
        {
            bool on = ringing.Count > 0 && DateTime.Now >= snoozeUntil;
            if (on)
            {
                if (!cfg.Muted) Sounds.StartRing(); else Sounds.StopRing();
                long first = ringing.OrderBy(id => convs.ContainsKey(id) ? U.L(convs[id], "updated_at") : 0).First();
                J c; convs.TryGetValue(first, out c);
                string who = c != null ? ConvName(c) + "  ·  " + U.Channel(U.S(c, "channel")) : "Nuevo chat";
                ringForm.ShowCall(who, c != null ? U.S(c, "last_preview") : "", ringing.Count);
                if (!trayBlink.Enabled) trayBlink.Start();
                Native.Flash(this, true);
                tray.Text = "Bezares Chat — " + ringing.Count + " chat(s) esperando";
            }
            else
            {
                Sounds.StopRing();
                if (ringForm.Visible) ringForm.Hide();
                trayBlink.Stop();
                tray.Icon = offline ? icoOffline : icoNormal;
                tray.Text = offline ? "Bezares Chat — desconectado" : "Bezares Chat";
                if (ringing.Count == 0) Native.Flash(this, false);
            }
        }

        async void AnswerRinging()
        {
            if (ringing.Count == 0) return;
            long id = ringing.OrderBy(x => convs.ContainsKey(x) ? U.L(convs[x], "updated_at") : 0).First();
            ShowMain();
            await Accept(id);
            OpenChat(id);
        }

        async Task Accept(long id)
        {
            try
            {
                var r = await Task.Run(() => api.Post("/conversations/" + id + "/accept", null));
                convs[id] = r["conversation"] as J;
                ringing.Remove(id);
                UpdateRinging();
                RenderList();
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
        }

        /* ------------------------------------------------ lista */

        /** Fila de la lista: una persona. Id = chat que la representa (ver Representative); Ids = todos sus chats. */
        class ConvItem { public long Id; public string Group; public List<long> Ids = new List<long>(); }
        class HeaderItem { public string Key, Title; public int Count; public Color Bg, Fg; }

        /** Grupo de la bandeja: nuevos, transferidos a mí, en curso, de compañeros o resueltos/cerrados. */
        string GroupOf(J c)
        {
            long myId = U.L(me, "id"), asg = U.L(c, "assigned_to"), tb = U.L(c, "transferred_by");
            if (U.S(c, "status") != "open") return "done";
            if (asg == 0) return "new";
            if (asg == myId) return tb != 0 && tb != myId && U.L(c, "accepted_at") == 0 ? "transfer" : "mine";
            return "others";
        }

        static string ConvName(J c)
        {
            string n = U.S(c, "name");
            if (n == "") n = U.S(c, "phone");
            if (n == "") n = (U.S(c, "channel") == "web" ? "Visitante #" : "Cliente #") + U.L(c, "id");
            return n;
        }

        string AgentName(long id)
        {
            var a = agents.FirstOrDefault(x => U.L(x, "id") == id);
            return a != null ? U.S(a, "name") : "";
        }

        void RenderList()
        {
            if (me == null) return;
            string q = search.Text.Trim().ToLowerInvariant();
            Func<J, bool> match = c => q == "" || (ConvName(c) + " " + U.S(c, "email") + " " + U.S(c, "phone") + " " + U.S(c, "last_preview")).ToLowerInvariant().Contains(q);
            var groups = new[]
            {
                new HeaderItem { Key = "new", Title = "NUEVOS", Bg = U.Brand, Fg = Color.White },
                new HeaderItem { Key = "transfer", Title = "TRANSFERIDOS A MÍ", Bg = U.BlueL, Fg = U.BlueD },
                new HeaderItem { Key = "mine", Title = "EN CURSO", Bg = U.Line2, Fg = U.Ink2 },
                new HeaderItem { Key = "others", Title = "DE COMPAÑEROS", Bg = U.Line2, Fg = U.Ink2 },
                new HeaderItem { Key = "done", Title = "RESUELTOS Y CERRADOS", Bg = U.GreenL, Fg = U.GreenD },
            };
            // Una fila por persona (la búsqueda mira en todos sus chats)
            BuildPeople();
            var byGroup = people.Values.Where(l => l.Any(match)).Select(Representative).GroupBy(GroupOf).ToDictionary(g => g.Key, g => g.ToList());
            renderingList = true;
            list.BeginUpdate();
            int top = list.TopIndex;
            list.Items.Clear();
            foreach (var h in groups)
            {
                List<J> items;
                if (!byGroup.TryGetValue(h.Key, out items) || items.Count == 0) continue;
                // Los que esperan respuesta, primero el que más lleva esperando; el resto, el más reciente arriba.
                var ordered = h.Key == "new" || h.Key == "transfer"
                    ? items.OrderByDescending(c => ringing.Contains(U.L(c, "id")) ? 1 : 0).ThenBy(c => U.L(c, "last_customer_at") > 0 ? U.L(c, "last_customer_at") : U.L(c, "updated_at")).ToList()
                    : items.OrderByDescending(c => U.L(c, "updated_at")).ToList();
                h.Count = ordered.Count;
                list.Items.Add(h);
                if (collapsedGroups.Contains(h.Key) && q == "") continue; // grupo plegado (la búsqueda los despliega)
                foreach (var c in ordered) list.Items.Add(new ConvItem { Id = U.L(c, "id"), Group = h.Key, Ids = PersonIds(U.L(c, "id")) });
            }
            for (int i = 0; i < list.Items.Count; i++) { var ci = list.Items[i] as ConvItem; if (ci != null && ci.Ids.Contains(activeId)) list.SelectedIndex = i; }
            if (top < list.Items.Count) list.TopIndex = top;
            list.EndUpdate();
            renderingList = false;
            int total = unread.Values.Sum();
            Text = (ringing.Count > 0 ? "(" + ringing.Count + ") 📞 " : total > 0 ? "(" + total + ") " : "") + "Bezares Chat — " + U.S(me, "name");
        }

        void DrawConv(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            var g = e.Graphics;
            var bnd = e.Bounds;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Cabecera de grupo: ▾/▸ TÍTULO y contador (clic = plegar/desplegar)
            var hi = list.Items[e.Index] as HeaderItem;
            if (hi != null)
            {
                using (var b = new SolidBrush(U.Paper)) g.FillRectangle(b, bnd);
                using (var p = new Pen(U.Line2)) { g.DrawLine(p, bnd.Left, bnd.Top, bnd.Right, bnd.Top); g.DrawLine(p, bnd.Left, bnd.Bottom - 1, bnd.Right, bnd.Bottom - 1); }
                bool folded = collapsedGroups.Contains(hi.Key) && search.Text.Trim() == "";
                TextRenderer.DrawText(g, (folded ? "▸  " : "▾  ") + hi.Title, U.SmallBold, new Rectangle(bnd.Left + U.P(10), bnd.Top, bnd.Width - U.P(60), bnd.Height), U.Ink2, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                U.Pill(g, hi.Count.ToString(), U.SmallBold, bnd.Right - U.P(10), bnd.Top + (bnd.Height - U.P(17)) / 2, U.P(17), hi.Bg, hi.Fg);
                return;
            }

            var row = (ConvItem)list.Items[e.Index];
            long id = row.Id;
            J c; if (!convs.TryGetValue(id, out c)) return;
            var mine = row.Ids.Where(convs.ContainsKey).Select(k => convs[k]).ToList(); // todos los chats de la persona
            var latest = mine.OrderByDescending(k => U.L(k, "updated_at")).First();
            bool sel = row.Ids.Contains(activeId), ring = row.Ids.Any(ringing.Contains);
            string status = U.S(c, "status"), grp = row.Group;
            bool resolved = status == "resolved", closed = status == "closed";
            using (var b = new SolidBrush(ring ? Color.FromArgb(255, 240, 214) : sel ? U.BrandL : Color.White)) g.FillRectangle(b, bnd);
            using (var p = new Pen(U.Line2)) g.DrawLine(p, bnd.Left, bnd.Bottom - 1, bnd.Right, bnd.Bottom - 1);
            if (sel) using (var b = new SolidBrush(U.Brand)) g.FillRectangle(b, bnd.Left, bnd.Top, U.P(3), bnd.Height);

            // Avatar: bandera redonda (o icono del canal); marca verde = resuelta, gris = cerrada
            var av = new Rectangle(bnd.Left + U.P(12), bnd.Top + (bnd.Height - U.P(34)) / 2, U.P(34), U.P(34));
            Flags.DrawCircle(g, PersonField(c, row.Ids, "country"), av, U.S(c, "channel"));
            if (resolved || closed)
            {
                var m = new Rectangle(av.Right - U.P(12), av.Bottom - U.P(12), U.P(14), U.P(14));
                using (var b = new SolidBrush(resolved ? U.Green : Color.Silver)) g.FillEllipse(b, m);
                using (var p = new Pen(Color.White, 1.5f)) g.DrawEllipse(p, m);
                if (resolved) using (var p = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(p, new[] { new PointF(m.Left + m.Width * 0.27f, m.Top + m.Height * 0.52f), new PointF(m.Left + m.Width * 0.44f, m.Top + m.Height * 0.68f), new PointF(m.Left + m.Width * 0.74f, m.Top + m.Height * 0.34f) });
            }

            int x = av.Right + U.P(10), right = bnd.Right - U.P(10);
            // Arriba a la derecha: tiempo de espera (ámbar/rojo según el umbral) o la hora del último mensaje
            long wait = mine.Where(k => GroupOf(k) == grp).Select(k => WaitMs(k)).DefaultIfEmpty(0).Max();
            int nameRight;
            if (wait > 0 && grp != "done")
            {
                Color pb, pf; WaitColors(wait, out pb, out pf);
                nameRight = U.Pill(g, U.Wait(wait), U.SmallBold, right, bnd.Top + U.P(9), U.P(17), pb, pf) - U.P(6);
            }
            else
            {
                string t = U.Time(U.L(latest, "updated_at"));
                int tw = TextRenderer.MeasureText(g, t, U.Small).Width;
                TextRenderer.DrawText(g, t, U.Small, new Rectangle(right - tw, bnd.Top + U.P(9), tw, U.P(17)), U.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                nameRight = right - tw - U.P(6);
            }
            TextRenderer.DrawText(g, (ring ? "📞 " : "") + PersonName(c, row.Ids), U.Bold, new Rectangle(x, bnd.Top + U.P(8), nameRight - x, U.P(20)), closed ? U.Muted : U.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);

            // Segunda línea: último mensaje (con «De …» si me lo transfirieron, o quién lo atiende si es de un compañero)
            string pre = grp == "transfer" ? "De " + AgentName(U.L(c, "transferred_by")) + " · " : grp == "others" ? AgentName(U.L(c, "assigned_to")) + " · " : "";
            bool trash = list.CanTrash(e.Index);
            int n = row.Ids.Sum(k => unread.ContainsKey(k) ? unread[k] : 0);
            bool hasUnread = n > 0;
            int lineRight = right - (trash ? U.P(26) : 0) - (hasUnread ? U.P(28) : 0);
            TextRenderer.DrawText(g, pre + U.S(latest, "last_preview"), U.Small, new Rectangle(x, bnd.Top + U.P(31), lineRight - x, U.P(18)), U.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            if (hasUnread)
            {
                var r = new Rectangle(right - (trash ? U.P(26) : 0) - U.P(24), bnd.Top + U.P(31), U.P(22), U.P(18));
                using (var b = new SolidBrush(U.Brand)) using (var path = U.Round(r, r.Height / 2)) g.FillPath(b, path);
                TextRenderer.DrawText(g, n > 9 ? "9+" : n.ToString(), U.SmallBold, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            if (trash) ConvListBox.DrawTrash(g, list.TrashRect(e.Index), list.HoverTrash == e.Index); // papelera: borrar sin abrir
        }

        /* ------------------------------------------------ chat */

        void CloseChat()
        {
            activeId = 0;
            thread.Clear();
            chatPanel.Visible = false;
            empty.Visible = true;
        }

        async void OpenChat(long id)
        {
            if (!ActiveIds().Contains(id)) { histOpen = false; thread.Clear(); }
            activeId = id;
            foreach (var pid in ActiveIds()) unread.Remove(pid);
            panelSig = null;
            LoadHistory(id);
            chatPanel.Visible = true; empty.Visible = false;
            ClearRows();
            RenderHead();
            RenderList();
            foreach (var pid in PersonIds(id).Where(ringing.Contains).ToList()) await Accept(pid); // abrir un chat que suena = atenderlo
            try { await LoadThread(id); }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            input.Focus();
        }

        void RenderHead()
        {
            J c; if (!convs.TryGetValue(activeId, out c)) return;
            var ids = ActiveIds();
            lTitle.Text = PersonName(c, ids);
            // Bandera redonda del país de origen (o icono del canal) delante del nombre
            string cc = PersonField(c, ids, "country"), ch = U.S(c, "channel");
            string avKey = cc + "|" + ch + "|" + (Flags.Get(cc) != null);
            if ((string)picHead.Tag != avKey)
            {
                var old = picHead.Image; picHead.Image = Flags.Circle(cc, U.P(38), ch); if (old != null) old.Dispose();
                picHead.Tag = avKey;
                tips.SetToolTip(picHead, cc != "" ? Flags.Name(cc) : U.Channel(ch));
            }
            string lang = U.S(c, "lang");
            // Todos los canales de la persona, primero el chat por el que se responde
            var parts = new List<string> { string.Join(" + ", ids.OrderBy(x => x == activeId ? 0 : 1).Select(ConvOf).Where(x => x != null).Select(x => U.Channel(U.S(x, "channel"))).Distinct()) };
            if (lang != "" && lang != "es") parts.Add("escribe en " + U.LangName(lang));
            long asg = U.L(c, "assigned_to");
            parts.Add(asg != 0 ? "atiende " + AgentName(asg) : "sin asignar");
            lSub.Text = string.Join(" · ", parts);
            long wait = WaitMs(c);
            waitShown = wait > 0;
            lWait.Visible = waitShown;
            if (wait > 0)
            {
                Color wb, wf; WaitColors(wait, out wb, out wf);
                lWait.Text = "esperando " + U.Wait(wait);
                lWait.BackColor = wb; lWait.ForeColor = wf;
            }
            LayoutHead();
            RenderPanel();
            string warn = "";
            if (WindowClosed(c)) warn = "⚠ Han pasado más de 24 h desde el último mensaje del cliente: no se le puede escribir por este chat. Usa el envío por correo.";
            else if (lang != "" && lang != "es" && lang != "en") warn = "El cliente escribe en " + U.LangName(lang) + ": tus respuestas se traducirán automáticamente.";
            lWarn.Text = warn;
            lWarn.Height = warn == "" ? 0 : U.P(28);
            RenderReplyBar(c);
            bool isResolved = ActiveResolved();
            bResolve.ForeColor = isResolved ? U.Ink2 : U.GreenD;
            tips.SetToolTip(bResolve, isResolved ? "Volver a abrir la conversación" : "Marcar la conversación como resuelta (deja de contar el tiempo de respuesta)");
        }

        void ClearRows()
        {
            flow.SuspendLayout();
            while (flow.Controls.Count > 0) { var ctl = flow.Controls[0]; flow.Controls.RemoveAt(0); ctl.Dispose(); }
            flow.ResumeLayout();
            rendered.Clear();
            lastRowConv = 0;
            typingSig = "";
        }

        /* ------------------------------------------------ «está escribiendo» */

        void RemoveTyping()
        {
            typingSig = "";
            foreach (var r in flow.Controls.Cast<Control>().Where(x => x.Name == "typing").ToList()) { flow.Controls.Remove(r); r.Dispose(); }
        }

        /** Muestra u oculta los tres puntitos al final del hilo: a la izquierda el cliente, a la derecha los compañeros. */
        void UpdateTyping()
        {
            var ids = ActiveIds();
            long myId = U.L(me, "id");
            var who = typing.Where(x => ids.Contains(U.L(x, "id")) && U.L(x, "by") != myId).Select(x => U.L(x, "by")).Distinct().OrderBy(x => x).ToList();
            string sig = string.Join(",", who);
            if (sig == typingSig) return;
            flow.SuspendLayout();
            RemoveTyping();
            foreach (var by in who)
            {
                var row = new Row { Name = "typing", Width = RowWidth, Height = 30, Margin = new Padding(0), Tag = by == 0 ? "left" : "right" };
                var b = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(U.P(11), U.P(6), U.P(11), U.P(6)), BackColor = by == 0 ? Color.White : U.BrandL };
                if (by != 0) b.Controls.Add(new Label { Text = AgentName(by) + " está respondiendo", Font = U.SmallBold, ForeColor = U.BrandD, AutoSize = true, Margin = new Padding(0, 0, 0, 1), UseMnemonic = false });
                var dots = new TypingDots { AccessibleName = by == 0 ? "El cliente está escribiendo" : AgentName(by) + " está respondiendo" };
                tips.SetToolTip(dots, dots.AccessibleName);
                b.Controls.Add(dots);
                row.Controls.Add(b);
                b.SizeChanged += (s, e) => { LayoutRow(row); RoundBubble(b, U.P(10)); };
                flow.Controls.Add(row);
                LayoutRow(row);
                RoundBubble(b, U.P(10));
            }
            typingSig = sig;
            flow.ResumeLayout();
        }

        static void RoundBubble(Control b, int radius)
        {
            var old = b.Region;
            using (var path = U.Round(new Rectangle(0, 0, b.Width, b.Height), radius)) b.Region = new Region(path);
            if (old != null) old.Dispose();
        }

        /** Avisa al servidor de que estoy escribiendo en el chat abierto (como mucho cada 3 s; nunca se envía el texto). */
        void SendTyping()
        {
            long id = activeId;
            if (id == 0 || typingOff || offline || me == null) return;
            bool on = input.Text.Trim() != "";
            double ago = (DateTime.UtcNow - typedAt).TotalSeconds;
            if (on ? typedConv == id && ago < 3 : typedConv != id) return;
            typedAt = DateTime.UtcNow;
            typedConv = on ? id : 0;
            Task.Run(() =>
            {
                try { api.Post("/conversations/" + id + "/typing", new J { { "typing", on } }); }
                catch (ApiException ex) { if (ex.Message == "Ruta no encontrada") typingOff = true; }
                catch (Exception) { }
            });
        }

        void RenderChat(bool reset)
        {
            RenderHead();
            // Hilo de la persona: los mensajes de todos sus chats, por orden de fecha
            if (!msgs.ContainsKey(activeId)) return;
            var ids = ActiveIds();
            var l = ids.Where(msgs.ContainsKey).SelectMany(x => msgs[x]).OrderBy(m => U.L(m, "created_at")).ThenBy(m => U.L(m, "id")).ToList();
            if (reset) ClearRows();
            flow.SuspendLayout();
            RemoveTyping(); // va siempre al final: se vuelve a poner después de los mensajes
            foreach (var m in l)
            {
                long mid = U.L(m, "id");
                if (rendered.Contains(mid)) continue;
                rendered.Add(mid);
                // Separador al cambiar de chat: canal y fecha en que empezó
                long cid = U.L(m, "conversation_id");
                var mc = ConvOf(cid);
                if (ids.Count > 1 && cid != lastRowConv && mc != null)
                {
                    string when = DateTimeOffset.FromUnixTimeMilliseconds(U.L(mc, "created_at")).LocalDateTime.ToString("dd/MM/yyyy, HH:mm");
                    var sep = MakeRow(new J { { "direction", "sep" }, { "body", U.Channel(U.S(mc, "channel")) + "  ·  chat del " + when } });
                    sep.Name = "conv" + cid;
                    flow.Controls.Add(sep);
                }
                lastRowConv = cid;
                flow.Controls.Add(MakeRow(m));
            }
            RelayoutRows();
            UpdateTyping();
            flow.ResumeLayout();
            if (reset) flow.ScrollToEnd(); // al abrir un chat se ve el último mensaje
        }

        int RowWidth { get { return Math.Max(200, flow.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth); } }

        void RelayoutRows()
        {
            if (flow.Width < 100) return; // minimizada
            flow.SuspendLayout();
            foreach (Control row in flow.Controls) LayoutRow(row);
            flow.ResumeLayout();
        }

        static void LayoutRow(Control row)
        {
            var flowParent = row.Parent as RowList;
            if (flowParent == null || row.Controls.Count == 0) return;
            // Se reserva siempre el hueco de la barra: si dependiera de ClientSize, al aparecer la barra las filas se recolocarían en bucle.
            row.Width = Math.Max(200, flowParent.Width - flowParent.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            var b = row.Controls[0];
            string side = (string)row.Tag;
            b.MaximumSize = new Size((int)(row.Width * (side == "center" ? 0.9 : 0.68)), 0);
            b.Left = side == "right" ? row.Width - b.Width - 4 : side == "center" ? (row.Width - b.Width) / 2 : 4;
            b.Top = 3;
            row.Height = b.Height + 6;
        }

        Control MakeRow(J m)
        {
            string dir = U.S(m, "direction");
            var row = new Row { Width = RowWidth, Height = 30, Margin = new Padding(0), Tag = dir == "system" || dir == "sep" ? "center" : dir == "in" ? "left" : "right" };
            var b = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(9, 6, 9, 5) };
            int maxText = (int)(RowWidth * 0.62);
            Func<string, Font, Color, Label> lbl = (t, f, col) => new EmojiLabel { Text = t, Font = f, ForeColor = col, AutoSize = true, MaximumSize = new Size(maxText, 0), Margin = new Padding(0, 1, 0, 1), UseMnemonic = false };

            if (dir == "sep")
            {
                b.BackColor = U.BlueL;
                b.Padding = new Padding(12, 3, 12, 3);
                b.Controls.Add(lbl(U.S(m, "body"), U.SmallBold, U.BlueD));
            }
            else if (dir == "system")
            {
                b.BackColor = Color.FromArgb(234, 232, 225);
                b.Padding = new Padding(10, 3, 10, 3);
                b.Controls.Add(lbl(U.S(m, "body") + "  ·  " + U.Time(U.L(m, "created_at")), U.Small, U.Ink2));
            }
            else
            {
                // Burbujas: cliente en blanco, equipo en rojo claro, mensajes automáticos en gris
                b.BackColor = dir == "in" ? Color.White : dir == "bot" ? U.Line2 : U.BrandL;
                bool deleted = U.L(m, "deleted") != 0;
                if (deleted) b.BackColor = U.Line2;
                b.Tag = m; // lo usa el menú del clic derecho
                b.ContextMenuStrip = msgMenu;
                row.Name = "msg" + U.L(m, "id");
                if (dir == "out") b.Controls.Add(lbl(AgentName(U.L(m, "agent_id")), U.SmallBold, U.BrandD));
                if (dir == "bot") b.Controls.Add(lbl("Mensaje automático", U.Small, U.Muted));
                string url = U.S(m, "media_url"), type = U.S(m, "media_type");
                if (url != "")
                {
                    if (type == "image")
                    {
                        var pic = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(220, 160), Cursor = Cursors.Hand, Margin = new Padding(0, 2, 0, 2) };
                        pic.LoadCompleted += (s, e) =>
                        {
                            if (e.Error != null || pic.Image == null) return;
                            double k = Math.Min(1.0, Math.Min(260.0 / pic.Image.Width, 220.0 / pic.Image.Height));
                            pic.Size = new Size((int)(pic.Image.Width * k), (int)(pic.Image.Height * k));
                        };
                        pic.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) U.OpenUrl(url); };
                        try { pic.LoadAsync(url); } catch { }
                        b.Controls.Add(pic);
                    }
                    else
                    {
                        string label = type == "audio" ? "▶  Escuchar audio" : type == "video" ? "▶  Ver vídeo" : "📄  " + (U.S(m, "media_name") != "" ? U.S(m, "media_name") : "Abrir documento");
                        var link = new LinkLabel { Text = label, AutoSize = true, Font = U.Bold, Margin = new Padding(0, 3, 0, 3), UseMnemonic = false };
                        link.LinkClicked += (s, e) => { if (e.Button != MouseButtons.Right) U.OpenUrl(url); };
                        b.Controls.Add(link);
                    }
                }
                string body = U.S(m, "body"), tr = U.S(m, "translated"), lang = U.S(m, "lang");
                if (deleted) b.Controls.Add(lbl("Mensaje borrado", U.Base, U.Muted));
                else if (dir == "in" && tr != "")
                {
                    b.Controls.Add(lbl(tr, U.Base, Color.Black));
                    b.Controls.Add(lbl("Original (" + U.LangName(lang) + "): " + body, U.Small, Color.Gray));
                }
                else
                {
                    if (body != "") b.Controls.Add(lbl(body, U.Base, Color.Black));
                    if (dir == "out" && tr != "") b.Controls.Add(lbl("Enviado en " + U.LangName(lang) + ": " + tr, U.Small, Color.Gray));
                }
                if (U.S(m, "send_status") == "error") b.Controls.Add(lbl("⚠ No entregado: " + U.S(m, "send_error"), U.Small, Color.FromArgb(198, 40, 40)));

                // Pie: hora + botón para copiar el mensaje al portapapeles
                string copyText = CopyText(m);
                var foot = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 0) };
                foot.Controls.Add(new Label { Text = (U.L(m, "changed_at") != 0 && !deleted ? "editado · " : "") + U.Time(U.L(m, "created_at")), Font = U.Small, ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(0, 1, 8, 0) });
                if (copyText != "")
                {
                    var copy = new LinkLabel { Text = "Copiar", Font = U.Small, AutoSize = true, LinkColor = Color.FromArgb(90, 100, 120), ActiveLinkColor = U.Brand, LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 1, 0, 0), UseMnemonic = false };
                    tips.SetToolTip(copy, "Copiar este mensaje al portapapeles");
                    copy.LinkClicked += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Right) return; // abre el menú
                        try { Clipboard.SetText(copyText); copy.Text = "✓ Copiado"; }
                        catch { copy.Text = "No se pudo copiar"; }
                        var t = new System.Windows.Forms.Timer { Interval = 1500 };
                        t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); if (!copy.IsDisposed) copy.Text = "Copiar"; };
                        t.Start();
                    };
                    foot.Controls.Add(copy);
                }
                b.Controls.Add(foot);
            }
            row.Controls.Add(b);
            b.SizeChanged += (s, e) =>
            {
                LayoutRow(row);
                // Esquinas redondeadas (la esquina del lado del remitente, más cerrada)
                RoundBubble(b, dir == "system" || dir == "sep" ? b.Height / 2 : U.P(10));
            };
            return row;
        }

        /* ------------------------------------------------ clic derecho en un mensaje: copiar, editar, borrar */

        static string CopyText(J m)
        {
            string body = U.S(m, "body"), tr = U.S(m, "translated");
            if (U.S(m, "direction") == "in" && tr != "") return tr + Environment.NewLine + "(" + U.LangName(U.S(m, "lang")) + ") " + body;
            return body != "" ? body : U.S(m, "media_url");
        }

        /** Rellena el menú para el mensaje sobre el que se ha hecho clic. false = no hay nada que mostrar. */
        bool BuildMsgMenu()
        {
            J m = null;
            for (Control c = msgMenu.SourceControl; c != null && m == null; c = c.Parent) m = c.Tag as J;
            msgMenu.Items.Clear();
            if (m == null || U.L(m, "deleted") != 0) return false;
            string text = CopyText(m);
            if (text != "") msgMenu.Items.Add("Copiar", null, (s, e) => { try { Clipboard.SetText(text); } catch { } });
            if (U.S(m, "direction") == "out")
            {
                var conv = ConvOf(U.L(m, "conversation_id"));
                string channel = conv != null ? U.S(conv, "channel") : "";
                bool mine = U.L(m, "agent_id") == U.L(me, "id"), failed = U.S(m, "send_status") == "error";
                bool open = channel == "web" || failed; // en WhatsApp y Facebook, lo ya entregado no se puede cambiar
                var edit = new ToolStripMenuItem(channel != "web" && failed ? "Corregir y reenviar…" : "Editar…") { Enabled = open && mine };
                edit.Click += (s, e) => EditMessage(m, channel != "web");
                var del = new ToolStripMenuItem("Borrar") { Enabled = open && (mine || Can("delete")), ForeColor = Color.FromArgb(163, 45, 45) };
                del.Click += (s, e) => DeleteMessage(m, channel != "web");
                if (msgMenu.Items.Count > 0) msgMenu.Items.Add(new ToolStripSeparator());
                msgMenu.Items.Add(edit);
                msgMenu.Items.Add(del);
                string why = !open ? U.Channel(channel) + " no permite cambiar mensajes ya enviados" : !mine ? "Solo su autor puede editarlo" : "";
                if (why != "") msgMenu.Items.Add(new ToolStripMenuItem(why) { Enabled = false, Font = U.Small });
            }
            return msgMenu.Items.Count > 0;
        }

        async void EditMessage(J m, bool resend)
        {
            long mid = U.L(m, "id");
            string text;
            using (var f = new EditMessageForm(U.S(m, "body"), resend, U.S(m, "media_url") != ""))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                text = f.NewText;
            }
            try { AfterChange(await Task.Run(() => api.Post("/messages/" + mid, new J { { "text", text } }))); }
            catch (ApiException ex) { ChangeError(ex); }
        }

        async void DeleteMessage(J m, bool undelivered)
        {
            long mid = U.L(m, "id");
            string q = undelivered ? "¿Borrar este mensaje no entregado?" : "¿Borrar este mensaje?\n\nDesaparecerá de la ventana de chat del cliente, aunque puede haberlo leído ya.";
            if (U.S(m, "media_url") != "") q += "\nTambién se borrará el archivo adjunto.";
            if (MessageBox.Show(this, q + "\nNo se puede deshacer.", "Borrar mensaje", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try { AfterChange(await Task.Run(() => api.Delete("/messages/" + mid))); }
            catch (ApiException ex) { ChangeError(ex); }
        }

        void ChangeError(ApiException ex)
        {
            MessageBox.Show(this, ex.Message == "Ruta no encontrada" ? "El servidor todavía no permite editar ni borrar mensajes: hay que actualizarlo." : ex.Message, "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void AfterChange(J r)
        {
            var c = r["conversation"] as J;
            var m = r["message"] as J;
            long id = U.L(c, "id"), mid = U.L(m, "id");
            if (convs.ContainsKey(id)) convs[id] = c;
            if (thread.ContainsKey(id)) thread[id] = c;
            List<J> l;
            if (msgs.TryGetValue(id, out l))
            {
                int at = l.FindIndex(x => U.L(x, "id") == mid);
                if (at >= 0) l[at] = m;
            }
            ReplaceRow(m);
            BuildPeople();
            RenderList();
            if (U.S(m, "send_status") == "error") MessageBox.Show(this, "No se pudo entregar:\n" + U.S(m, "send_error"), "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /** Vuelve a dibujar un mensaje que ya está en pantalla, sin mover el resto del hilo. */
        void ReplaceRow(J m)
        {
            string key = "msg" + U.L(m, "id");
            var old = flow.Controls.Cast<Control>().FirstOrDefault(x => x.Name == key);
            if (old == null) return;
            int at = flow.Controls.GetChildIndex(old);
            flow.SuspendLayout();
            var row = MakeRow(m);
            flow.Controls.Remove(old);
            flow.Controls.Add(row);
            flow.Controls.SetChildIndex(row, at);
            LayoutRow(row);
            flow.ResumeLayout();
            old.Dispose();
        }

        /* ------------------------------------------------ envío */

        void AfterSend(J r)
        {
            var c = r["conversation"] as J;
            var m = r["message"] as J;
            long id = U.L(c, "id");
            convs[id] = c;
            List<J> l;
            if (msgs.TryGetValue(id, out l) && !l.Any(x => U.L(x, "id") == U.L(m, "id"))) l.Add(m);
            ringing.Remove(id);
            UpdateRinging();
            if (U.S(m, "send_status") == "error") MessageBox.Show(this, "El mensaje se guardó pero no se pudo entregar:\n" + U.S(m, "send_error"), "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            BuildPeople();
            if (ActiveIds().Contains(id)) { RenderChat(false); flow.ScrollToEnd(); }
            RenderList();
        }

        async void SendText()
        {
            string text = input.Text.Trim();
            long id = activeId;
            if (text == "" || id == 0 || !bSend.Enabled) return;
            if (WindowClosed(ConvOf(id))) { AskEmail(id, text); return; }
            bSend.Enabled = false;
            bool late = false;
            try
            {
                var r = await Task.Run(() => api.Post("/conversations/" + id + "/messages", new J { { "text", text } }));
                input.Clear();
                AfterSend(r);
            }
            catch (ApiException ex) { if (ex.Status == 409) late = true; else MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            finally { bSend.Enabled = true; input.Focus(); }
            if (late) AskEmail(id, text);
        }

        /** WhatsApp y Facebook: pasadas 24 h del último mensaje del cliente ya no se le puede escribir. */
        bool WindowClosed(J c)
        {
            if (c == null || (U.S(c, "channel") != "whatsapp" && U.S(c, "channel") != "facebook")) return false;
            return !U.B(c, "window_open") || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + clockSkew - U.L(c, "last_customer_at") >= 86400000;
        }

        /** Pide usar el correo en lugar del chat; lo escrito pasa a ser el texto del correo. */
        void AskEmail(long id, string text)
        {
            string msg = "Han pasado más de 24 horas desde el último mensaje del cliente y " + U.Channel(U.S(ConvOf(id), "channel")) + " no permite contestarle por el chat.\n\nUsa el envío por correo.";
            if (!Can("email")) { MessageBox.Show(this, msg + "\nTu rol no puede enviar correos desde el programa: pídeselo a un compañero.", "Contesta por correo", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (id != activeId) { MessageBox.Show(this, msg, "Contesta por correo", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            msg += text != "" ? "\n¿Abrirlo ahora con lo que has escrito?" : "\n¿Abrirlo ahora?";
            if (MessageBox.Show(this, msg, "Contesta por correo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) EmailChat(text);
        }

        async void SendFile()
        {
            long id = activeId;
            if (id == 0) return;
            const string media = "*.jpg;*.jpeg;*.png;*.gif;*.webp;*.mp4;*.3gp;*.mov;*.mp3;*.ogg;*.m4a;*.aac;*.wav;*.opus";
            const string docs = "*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.odt;*.ods;*.odp;*.rtf;*.txt;*.csv";
            using (var dlg = new OpenFileDialog
            {
                Title = "Enviar archivo",
                Filter = "Todos los admitidos|" + media + ";" + docs
                       + "|Documentos (PDF, Word, Excel, PowerPoint…)|" + docs
                       + "|Imágenes, vídeo y audio|" + media
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                await UploadPath(id, dlg.FileName);
            }
        }

        static readonly HashSet<string> AllowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".3gp", ".mov", ".mp3", ".ogg", ".m4a", ".aac", ".wav", ".opus",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".txt", ".csv" };

        async Task UploadPath(long id, string file)
        {
            if (id == 0 || !bAttach.Enabled) return;
            if (!AllowedExt.Contains(Path.GetExtension(file)))
            {
                MessageBox.Show(this, "Tipo de archivo no admitido.\nSe pueden enviar imágenes, audio, vídeo y documentos (PDF, Word, Excel, PowerPoint, OpenDocument, RTF, TXT, CSV).", "Bezares Chat");
                return;
            }
            var fi = new FileInfo(file);
            // El límite real lo decide el servidor (configurable); aquí solo se evita cargar archivos enormes.
            if (fi.Length > 100L * 1024 * 1024) { MessageBox.Show(this, "El archivo supera 100 MB.", "Bezares Chat"); return; }
            string text = input.Text.Trim();
            if (WindowClosed(ConvOf(id))) { AskEmail(id, text); return; }
            bool late = false;
            bAttach.Enabled = false; tips.SetToolTip(bAttach, "Enviando…");
            try
            {
                var r = await Task.Run(() => api.Upload("/conversations/" + id + "/messages", file, text));
                input.Clear();
                AfterSend(r);
            }
            catch (ApiException ex) { if (ex.Status == 409) late = true; else MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            catch (IOException ex) { MessageBox.Show(this, "No se pudo leer el archivo: " + ex.Message, "Bezares Chat"); }
            finally { bAttach.Enabled = true; tips.SetToolTip(bAttach, "Adjuntar archivo (imagen, audio, vídeo o documento). También puedes arrastrarlo aquí."); }
            if (late) AskEmail(id, text);
        }

        /* ------------------------------------------------ plantillas */

        async Task ShowTemplatesMenu()
        {
            if (templates.Count == 0)
            {
                try { templates = U.A(await Task.Run(() => api.Get("/templates")), "templates"); }
                catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Plantillas"); return; }
            }
            templatesMenu.Items.Clear();
            foreach (var t in templates)
            {
                var tpl = t;
                var item = new ToolStripMenuItem(U.S(tpl, "title")) { ToolTipText = U.S(tpl, "body").Length > 200 ? U.S(tpl, "body").Substring(0, 200) + "…" : U.S(tpl, "body") };
                item.Click += (s, e) => InsertTemplate(U.S(tpl, "body"));
                templatesMenu.Items.Add(item);
            }
            if (templates.Count == 0) templatesMenu.Items.Add(new ToolStripMenuItem("(sin plantillas todavía)") { Enabled = false });
            templatesMenu.Items.Add(new ToolStripSeparator());
            templatesMenu.Items.Add("⚙ Gestionar plantillas…", null, (s, e) => ManageTemplates());
            templatesMenu.Show(bTemplates, new Point(0, -templatesMenu.PreferredSize.Height));
        }

        void InsertTemplate(string text)
        {
            J c; convs.TryGetValue(activeId, out c);
            string name = c != null ? PersonField(c, ActiveIds(), "name") : "";
            text = text.Replace("{nombre}", name).Replace("{name}", name).Replace("{agente}", U.S(me, "name"));
            int pos = input.SelectionStart;
            input.SelectedText = text;
            input.SelectionStart = pos + text.Length;
            input.Focus();
        }

        void ManageTemplates()
        {
            using (var f = new TemplatesForm(api, templates))
            {
                f.ShowDialog(this);
                templates = f.Items;
            }
        }

        /* ------------------------------------------------ resolver */

        async void ToggleResolved()
        {
            long id = activeId;
            J c; if (id == 0 || !convs.TryGetValue(id, out c)) return;
            // Resolver afecta a todos los chats abiertos de la persona (míos o sin asignar); reabrir, solo al actual.
            bool resolve = !ActiveResolved();
            var todo = resolve ? ActionIds().Where(x => U.S(ConvOf(x), "status") != "resolved").ToList() : new List<long> { id };
            bResolve.Enabled = false; sResolve.Enabled = false;
            try
            {
                foreach (var tid in todo)
                {
                    long one = tid;
                    var r = await Task.Run(() => api.Post("/conversations/" + one + "/resolve", new J { { "resolved", resolve } }));
                    convs[one] = r["conversation"] as J;
                    ringing.Remove(one);
                }
                UpdateRinging();
                RenderList();
                if (activeId == id) RenderHead();
                var _ = Sync(); // trae la nota "marcó la conversación como resuelta"
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            finally { bResolve.Enabled = true; sResolve.Enabled = true; panelSig = null; RenderPanel(); }
        }

        /* ------------------------------------------------ borrar conversación */

        async void DeleteChat(long id)
        {
            J c; if (id == 0 || !convs.TryGetValue(id, out c)) return;
            // Se borra la conversación entera de la persona: antes se averigua cuántos chats son (también los antiguos).
            var all = new Dictionary<long, J>();
            foreach (var pid in PersonIds(id)) if (convs.ContainsKey(pid)) all[pid] = convs[pid];
            bool complete = true;
            bDelete.Enabled = false;
            try
            {
                var r = await Task.Run(() => api.Get("/conversations/" + id + "/thread"));
                foreach (var x in U.A(r, "conversations")) all[U.L(x, "id")] = x;
            }
            catch (ApiException ex) { complete = ex.Status == 404; if (!complete) MessageBox.Show(this, ex.Message, "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { bDelete.Enabled = true; }
            if (!complete) return;
            string who = PersonName(c, all.Keys);
            string detail = all.Count == 1 ? "" : "Son " + all.Count + " chats:\n" + string.Join("\n", all.Values.OrderByDescending(x => U.L(x, "created_at")).Take(8).Select(x =>
                "   •  " + U.Channel(U.S(x, "channel")) + ", " + DateTimeOffset.FromUnixTimeMilliseconds(U.L(x, "created_at")).LocalDateTime.ToString("dd/MM/yyyy")
                + (U.S(x, "status") == "open" ? " (abierto" + (U.L(x, "assigned_to") != 0 ? ", " + AgentName(U.L(x, "assigned_to")) : "") + ")" : ""))) + (all.Count > 8 ? "\n   …" : "") + "\n\n";
            var answer = MessageBox.Show(this,
                "¿Borrar definitivamente la conversación con " + who + "?\n\n" + detail +
                "Se borrarán todos sus mensajes y archivos adjuntos para todo el equipo. Esta acción no se puede deshacer.\n\n" +
                "Consejo: si quieres conservar una copia, envíala antes con «Enviar por correo».",
                "Borrar conversación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            bDelete.Enabled = false;
            try
            {
                foreach (var did in all.Keys.ToList())
                {
                    long one = did;
                    await Task.Run(() => api.Delete("/conversations/" + one));
                    convs.Remove(one); thread.Remove(one); msgs.Remove(one); unread.Remove(one); ringing.Remove(one);
                }
                tray.ShowBalloonTip(2500, "Conversación borrada", who, ToolTipIcon.Info);
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally
            {
                bDelete.Enabled = true;
                if (activeId != 0 && !convs.ContainsKey(activeId)) CloseChat();
                UpdateRinging();
                RenderList();
            }
        }

        /* ------------------------------------------------ enviar por correo */

        async void EmailChat(string draft = "")
        {
            long id = activeId;
            J c; if (id == 0 || !convs.TryGetValue(id, out c)) return;
            // Se envía solo el chat por el que se responde, no los demás de la persona (el correo puede no estar verificado).
            var eids = ActiveIds();
            string only = eids.Count > 1 ? U.Channel(U.S(c, "channel")) + " del " + DateTimeOffset.FromUnixTimeMilliseconds(U.L(c, "created_at")).LocalDateTime.ToString("dd/MM/yyyy") : "";
            using (var f = new EmailForm(PersonField(c, eids, "email"), PersonName(c, eids), only))
            {
                if (draft != "") f.Intro.Text = "Hola {nombre},\r\n\r\n" + draft.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n\r\nUn saludo,\r\n{agente} · {empresa}";
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var list = f.Recipients();
                var bad = list.Where(x => !System.Text.RegularExpressions.Regex.IsMatch(x, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")).ToList();
                if (list.Count == 0) { MessageBox.Show(this, "Escribe al menos un correo electrónico.", "Bezares Chat"); return; }
                if (bad.Count > 0) { MessageBox.Show(this, "Revisa estos correos:\n" + string.Join("\n", bad), "Bezares Chat"); return; }
                if (list.Count > 10) { MessageBox.Show(this, "Como máximo 10 destinatarios por envío.", "Bezares Chat"); return; }
                string to = string.Join(",", list);
                // Si no se cambió la introducción, el servidor usa la suya (mismo texto).
                string intro = f.Intro.Text.Trim() == EmailForm.DefaultIntro.Trim() ? "" : f.Intro.Text.Trim();
                bEmail.Enabled = false; sEmail.Enabled = false; bEmail.Text = "✉   Enviando…";
                try
                {
                    var r = await Task.Run(() => api.Post("/conversations/" + id + "/email", new J { { "to", to }, { "intro", intro } }));
                    object fo; var failedList = r.TryGetValue("failed", out fo) && fo is object[] ? ((object[])fo).Select(Convert.ToString).ToList() : new List<string>();
                    if (failedList.Count > 0)
                        MessageBox.Show(this, "Enviado, pero no se pudo entregar a:\n" + string.Join("\n", failedList), "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                        tray.ShowBalloonTip(3000, "Correo enviado", "Conversación enviada a " + string.Join(", ", list), ToolTipIcon.Info);
                    if (draft != "" && input.Text.Trim() == draft) input.Clear(); // ya ha salido por correo
                }
                catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { bEmail.Enabled = true; sEmail.Enabled = true; bEmail.Text = "✉   Enviar por correo"; }
            }
        }

        async void Transfer()
        {
            long id = activeId;
            J c; if (id == 0 || !convs.TryGetValue(id, out c)) return;
            var todo = Enumerable.Reverse(ActionIds()).ToList();
            using (var tf = new TransferForm(agents, U.L(c, "assigned_to"), U.L(me, "id")))
            {
                if (tf.ShowDialog(this) != DialogResult.OK || tf.Agents.SelectedItem == null) return;
                var to = (TransferForm.AgentItem)tf.Agents.SelectedItem;
                string note = tf.Note.Text;
                try
                {
                    // Se transfieren juntos todos los chats abiertos de la persona (míos o sin asignar); el actual, el último.
                    foreach (var tid in todo)
                    {
                        long one = tid;
                        var r = await Task.Run(() => api.Post("/conversations/" + one + "/transfer", new J { { "agent_id", to.Id }, { "note", note } }));
                        convs[one] = r["conversation"] as J;
                        ringing.Remove(one);
                    }
                    UpdateRinging();
                    tray.ShowBalloonTip(2500, "Transferido", "Conversación transferida a " + to.Text, ToolTipIcon.Info);
                    RenderList();
                    OpenChat(id);
                }
                catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            }
        }
    }
}
