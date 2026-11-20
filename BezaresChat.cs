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
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

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
            bool created;
            using (var mutex = new Mutex(true, "BezaresChat_SingleInstance", out created))
            {
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
                Application.Run(new MainForm(minimized));
            }
        }
    }

    /* =============================================================== utilidades */

    static class U
    {
        public static readonly Color Brand = Color.FromArgb(0, 87, 168);
        public static readonly Font Base = new Font("Segoe UI", 10f);
        public static readonly Font Small = new Font("Segoe UI", 8.25f);
        public static readonly Font Bold = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Title = new Font("Segoe UI", 13f, FontStyle.Bold);

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

        public static Icon MakeIcon(bool alert)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(alert ? Color.FromArgb(220, 30, 40) : Brand), 1, 1, 30, 30);
                g.FillEllipse(Brushes.White, 7, 8, 18, 13);
                g.FillPolygon(Brushes.White, new[] { new Point(10, 19), new Point(8, 26), new Point(15, 20) });
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    /* =============================================================== configuración portable */

    class Config
    {
        static string FilePath { get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "BezaresChat.ini"); } }
        public string Server = "https://chat.bezares.com", Email = "", Token = "";
        public bool Muted, Remember = true;

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
                File.WriteAllLines(FilePath, new[] { "server=" + Server, "email=" + Email, "muted=" + (Muted ? "1" : "0"), "remember=" + (Remember ? "1" : "0"), "token=" + (Remember ? Protect(Token) : "") });
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
        public Button BOk = new Button { Text = "Entrar", Height = 38, BackColor = U.Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
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
            ClientSize = new Size(380, 440);
            Icon = U.MakeIcon(false);
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(24, 16, 24, 16), WrapContents = false };
            p.Controls.Add(new Label { Text = "● Bezares Chat", Font = U.Title, ForeColor = U.Brand, AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
            foreach (var x in new[] { Tuple.Create("Servidor", TServer), Tuple.Create("Correo", TEmail), Tuple.Create("Contraseña", TPass) })
            {
                p.Controls.Add(new Label { Text = x.Item1, AutoSize = true, ForeColor = Color.DimGray });
                x.Item2.Width = 330;
                p.Controls.Add(x.Item2);
            }
            CShow.Margin = new Padding(0, 4, 0, 6);
            CShow.CheckedChanged += (s, e) => TPass.UseSystemPasswordChar = !CShow.Checked;
            p.Controls.Add(CShow);
            CRemember.Checked = cfg.Remember;
            CStartup.Checked = Config.StartupEnabled;
            p.Controls.Add(CRemember);
            p.Controls.Add(CStartup);
            LErr.Width = 330;
            BOk.Width = 330;
            BOk.FlatAppearance.BorderSize = 0;
            p.Controls.Add(LErr);
            p.Controls.Add(BOk);
            LForgot.Margin = new Padding(0, 10, 0, 0);
            // Abre la página de recuperación del servidor indicado en el navegador.
            LForgot.LinkClicked += (s, e) =>
            {
                try { U.OpenUrl(Api.Normalize(TServer.Text) + "/admin/forgot"); }
                catch (ApiException ex) { LErr.Text = ex.Message; }
            };
            p.Controls.Add(LForgot);
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
        public Label LWho = new Label { Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.White, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 44 };
        public Label LMsg = new Label { Font = new Font("Segoe UI", 11f), ForeColor = Color.White, AutoSize = false, TextAlign = ContentAlignment.TopCenter, Dock = DockStyle.Top, Height = 60 };
        public Button BAnswer = new Button { Text = "📞  ATENDER", Font = new Font("Segoe UI", 14f, FontStyle.Bold), BackColor = Color.FromArgb(26, 127, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Height = 56, Dock = DockStyle.Bottom };
        public LinkLabel LLater = new LinkLabel { Text = "Posponer 2 minutos", Font = new Font("Segoe UI", 10f), LinkColor = Color.White, ActiveLinkColor = Color.White, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Bottom, Height = 32 };
        System.Windows.Forms.Timer blink = new System.Windows.Forms.Timer { Interval = 450 };
        bool phase;

        public RingForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(440, 270);
            BackColor = Color.FromArgb(220, 30, 40);
            Padding = new Padding(14);
            var head = new Label { Text = "🔔  NUEVO CHAT ENTRANTE", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Top, Height = 32, TextAlign = ContentAlignment.MiddleCenter };
            BAnswer.FlatAppearance.BorderSize = 0;
            Controls.Add(LMsg); Controls.Add(LWho); Controls.Add(head);
            Controls.Add(LLater); Controls.Add(BAnswer);
            blink.Tick += (s, e) => { phase = !phase; BackColor = phase ? Color.FromArgb(255, 140, 0) : Color.FromArgb(220, 30, 40); };
            VisibleChanged += (s, e) => { if (Visible) blink.Start(); else blink.Stop(); };
        }

        public void ShowCall(string who, string msg, int waiting)
        {
            LWho.Text = who;
            LMsg.Text = (msg.Length > 140 ? msg.Substring(0, 140) + "…" : msg) + (waiting > 1 ? "\n(" + waiting + " chats esperando)" : "");
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);
            if (!Visible) Show();
            TopMost = true;
            BringToFront();
        }

        protected override bool ShowWithoutActivation { get { return false; } }
    }

    /* =============================================================== transferir */

    class TransferForm : Form
    {
        public ComboBox Agents = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
        public TextBox Note = new TextBox { Multiline = true, Width = 340, Height = 70 };

        public TransferForm(List<J> agents, long exclude)
        {
            Text = "Transferir conversación";
            Font = U.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 250);
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), WrapContents = false };
            p.Controls.Add(new Label { Text = "Transferir a:", AutoSize = true });
            foreach (var a in agents)
            {
                if (U.L(a, "active") == 0 || U.L(a, "id") == exclude) continue;
                string role = U.S(a, "role") == "sales" ? "Comercial" : U.S(a, "role") == "receptionist" ? "Recepción" : "Admin";
                Agents.Items.Add(new AgentItem { Id = U.L(a, "id"), Text = U.S(a, "name") + " (" + role + ")" + (U.B(a, "online") ? "  ● conectado" : "") });
            }
            if (Agents.Items.Count > 0) Agents.SelectedIndex = 0;
            p.Controls.Add(Agents);
            p.Controls.Add(new Label { Text = "Nota para el compañero (opcional):", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            p.Controls.Add(Note);
            var ok = new Button { Text = "Transferir", DialogResult = DialogResult.OK, Width = 120, Height = 32 };
            p.Controls.Add(ok);
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
        ListBox list = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 58, BorderStyle = BorderStyle.None, IntegralHeight = false };
        Label lTitle = new Label { Font = U.Title, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        Label lSub = new Label { Font = U.Small, ForeColor = Color.DimGray, AutoSize = false, Dock = DockStyle.Bottom, Height = 18 };
        Button bTransfer = new Button { Text = "↪ Transferir", Width = 120, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat };
        FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(244, 246, 249), Padding = new Padding(8) };
        Label lWarn = new Label { Dock = DockStyle.Bottom, Height = 0, BackColor = Color.FromArgb(255, 244, 229), ForeColor = Color.FromArgb(181, 71, 8), Padding = new Padding(8, 4, 8, 4), AutoSize = false };
        TextBox input = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11f), ScrollBars = ScrollBars.Vertical };
        Button bSend = new Button { Text = "Enviar", Dock = DockStyle.Right, Width = 100, BackColor = U.Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        Button bAttach = new Button { Text = "📎 Adjuntar archivo", Dock = DockStyle.Left, Width = 190, FlatStyle = FlatStyle.Flat };
        Panel chatPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        Label empty = new Label { Text = "Selecciona un chat de la lista", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray };
        Label status = new Label { Dock = DockStyle.Bottom, Height = 24, ForeColor = Color.DimGray, Padding = new Padding(8, 4, 0, 0) };
        HashSet<long> rendered = new HashSet<long>();

        NotifyIcon tray = new NotifyIcon();
        Icon icoNormal = U.MakeIcon(false), icoAlert = U.MakeIcon(true);
        RingForm ringForm = new RingForm();
        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 2000 };
        System.Windows.Forms.Timer trayBlink = new System.Windows.Forms.Timer { Interval = 500 };

        public MainForm(bool minimized)
        {
            Text = "Bezares Chat";
            Font = U.Base;
            Icon = icoNormal;
            Size = new Size(1000, 700);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen;
            startHidden = minimized;
            if (minimized) { WindowState = FormWindowState.Minimized; ShowInTaskbar = false; }

            // Lista de chats
            var left = new Panel { Dock = DockStyle.Left, Width = 300, BackColor = Color.White };
            var listHead = new Label { Text = "  Chats", Font = U.Bold, Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleLeft, BackColor = U.Brand, ForeColor = Color.White };
            left.Controls.Add(list); left.Controls.Add(listHead);
            list.DrawItem += DrawConv;
            list.SelectedIndexChanged += (s, e) => { var c = list.SelectedItem as ConvItem; if (c != null && c.Id != activeId) OpenChat(c.Id); };

            // Cabecera del chat
            var head = new Panel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 6, 12, 6), BackColor = Color.White };
            head.Controls.Add(lTitle); head.Controls.Add(bTransfer); head.Controls.Add(lSub);
            bTransfer.Click += (s, e) => Transfer();

            // Redacción
            var compose = new Panel { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(8), BackColor = Color.White };
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 34 };
            bSend.FlatAppearance.BorderSize = 0;
            buttons.Controls.Add(bAttach); buttons.Controls.Add(bSend);
            compose.Controls.Add(input); compose.Controls.Add(buttons);
            bSend.Click += (s, e) => SendText();
            bAttach.Click += (s, e) => SendFile();
            input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; SendText(); } };

            chatPanel.Controls.Add(flow); chatPanel.Controls.Add(lWarn); chatPanel.Controls.Add(compose); chatPanel.Controls.Add(head);
            flow.Resize += (s, e) => RelayoutRows();

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

            Controls.Add(chatPanel); Controls.Add(empty); Controls.Add(left); Controls.Add(status);

            // Bandeja del sistema
            tray.Icon = icoNormal;
            tray.Text = "Bezares Chat";
            tray.Visible = true;
            tray.DoubleClick += (s, e) => ShowMain();
            tray.BalloonTipClicked += (s, e) => ShowMain();
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, (s, e) => ShowMain());
            var mute = new ToolStripMenuItem("Silenciar sonido") { CheckOnClick = true, Checked = cfg.Muted };
            mute.CheckedChanged += (s, e) => { cfg.Muted = mute.Checked; cfg.Save(); UpdateRinging(); };
            menu.Items.Add(mute);
            startupItem.Checked = Config.StartupEnabled;
            startupItem.Click += (s, e) => { Config.SetStartup(!startupItem.Checked); startupItem.Checked = Config.StartupEnabled; };
            menu.Items.Add(startupItem);
            menu.Items.Add("Cerrar sesión", null, (s, e) => Logout());
            menu.Items.Add("-");
            menu.Items.Add("Salir", null, (s, e) => { quitting = true; Close(); });
            tray.ContextMenuStrip = menu;
            trayBlink.Tick += (s, e) => tray.Icon = tray.Icon == icoAlert ? icoNormal : icoAlert;

            ringForm.BAnswer.Click += (s, e) => AnswerRinging();
            ringForm.LLater.LinkClicked += (s, e) => { snoozeUntil = DateTime.Now.AddMinutes(2); UpdateRinging(); };

            timer.Tick += async (s, e) => await Sync();
            Shown += (s, e) => Start();
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
            base.OnFormClosing(e);
        }

        void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        /* ------------------------------------------------ sesión */

        async void Start()
        {
            if (!string.IsNullOrEmpty(cfg.Token))
            {
                try
                {
                    api.Base = Api.Normalize(cfg.Server);
                    api.Token = cfg.Token;
                    var r = await Task.Run(() => api.Get("/me"));
                    StartSession(r["agent"] as J);
                    return;
                }
                catch { }
            }
            ShowLogin(null);
        }

        void ShowLogin(string error)
        {
            timer.Stop();
            Hide();
            startHidden = false;
            ShowInTaskbar = true;
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
            since = 0; firstSync = true; activeId = 0; syncCount = 0;
            Text = "Bezares Chat — " + U.S(me, "name");
            bTransfer.Visible = U.S(me, "role") != "sales"; // solo recepción/admin transfiere
            chatPanel.Visible = false; empty.Visible = true;
            if (startHidden)
            {
                // Arranque con Windows: se queda en la bandeja; el aviso de llamada aparecerá igualmente.
                startHidden = false;
                Hide();
                ShowInTaskbar = true;
                WindowState = FormWindowState.Normal;
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

        /* ------------------------------------------------ sincronización */

        async Task Sync()
        {
            if (syncing || me == null) return;
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
            agents = U.A(d, "agents");
            var incoming = U.A(d, "conversations");
            if (full)
            {
                var ids = new HashSet<long>(incoming.Select(c => U.L(c, "id")));
                foreach (var id in convs.Keys.ToList()) if (!ids.Contains(id)) convs.Remove(id);
                if (activeId != 0 && !convs.ContainsKey(activeId)) CloseChat();
            }
            bool activeChanged = false;
            foreach (var c in incoming)
            {
                long id = U.L(c, "id");
                J prev;
                if (id == activeId && (!convs.TryGetValue(id, out prev) || U.L(prev, "updated_at") != U.L(c, "updated_at"))) activeChanged = true;
                convs[id] = c;
            }

            var newIn = new List<J>();
            foreach (var m in U.A(d, "messages"))
            {
                long cid = U.L(m, "conversation_id"), mid = U.L(m, "id");
                List<J> l;
                if (msgs.TryGetValue(cid, out l) && !l.Any(x => U.L(x, "id") == mid)) { l.Add(m); if (cid == activeId) activeChanged = true; }
                if (U.S(m, "direction") == "in" && !firstSync && seenIn.Add(mid)) newIn.Add(m);
            }
            bool focused = Visible && WindowState != FormWindowState.Minimized && Form.ActiveForm == this;
            foreach (var m in newIn)
            {
                long cid = U.L(m, "conversation_id");
                if (cid != activeId || !focused) unread[cid] = (unread.ContainsKey(cid) ? unread[cid] : 0) + 1;
            }
            if (newIn.Count > 0 && !(focused && newIn.All(m => U.L(m, "conversation_id") == activeId)))
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
            if (activeId != 0) { if (activeChanged) RenderChat(false); else RenderHead(); }
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
                tray.Icon = icoNormal;
                tray.Text = "Bezares Chat";
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

        class ConvItem { public long Id; }

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
            var ordered = convs.Values
                .OrderByDescending(c => ringing.Contains(U.L(c, "id")) ? 1 : 0)
                .ThenByDescending(c => U.S(c, "status") == "closed" ? 0 : 1)
                .ThenByDescending(c => U.L(c, "updated_at")).ToList();
            list.BeginUpdate();
            int top = list.TopIndex;
            list.Items.Clear();
            foreach (var c in ordered) list.Items.Add(new ConvItem { Id = U.L(c, "id") });
            for (int i = 0; i < list.Items.Count; i++) if (((ConvItem)list.Items[i]).Id == activeId) list.SelectedIndex = i;
            if (top < list.Items.Count) list.TopIndex = top;
            list.EndUpdate();
            int total = unread.Values.Sum();
            Text = (ringing.Count > 0 ? "(" + ringing.Count + ") 📞 " : total > 0 ? "(" + total + ") " : "") + "Bezares Chat — " + U.S(me, "name");
        }

        void DrawConv(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            long id = ((ConvItem)list.Items[e.Index]).Id;
            J c; if (!convs.TryGetValue(id, out c)) return;
            bool sel = id == activeId, ring = ringing.Contains(id);
            var bg = ring ? Color.FromArgb(255, 226, 184) : sel ? Color.FromArgb(232, 240, 251) : Color.White;
            var g = e.Graphics;
            g.FillRectangle(new SolidBrush(bg), e.Bounds);
            g.DrawLine(new Pen(Color.FromArgb(238, 240, 244)), e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var ch = U.S(c, "channel");
            var dot = ch == "whatsapp" ? Color.FromArgb(37, 211, 102) : ch == "facebook" ? Color.FromArgb(24, 119, 242) : U.Brand;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(new SolidBrush(dot), e.Bounds.Left + 10, e.Bounds.Top + 12, 10, 10);
            int x = e.Bounds.Left + 28, w = e.Bounds.Width - 28 - 60;
            bool closed = U.S(c, "status") == "closed";
            TextRenderer.DrawText(g, (ring ? "📞 " : "") + ConvName(c), U.Bold, new Rectangle(x, e.Bounds.Top + 6, w, 20), closed ? Color.Gray : Color.Black, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, U.Time(U.L(c, "updated_at")), U.Small, new Rectangle(e.Bounds.Right - 64, e.Bounds.Top + 8, 58, 16), Color.Gray, TextFormatFlags.Right);
            TextRenderer.DrawText(g, U.S(c, "last_preview"), U.Small, new Rectangle(x, e.Bounds.Top + 26, w, 16), Color.DimGray, TextFormatFlags.EndEllipsis);
            long asg = U.L(c, "assigned_to");
            TextRenderer.DrawText(g, U.Channel(ch) + " · " + (asg != 0 ? AgentName(asg) : "Sin asignar") + (closed ? " · cerrado" : ""), U.Small, new Rectangle(x, e.Bounds.Top + 40, w, 16), Color.Gray, TextFormatFlags.EndEllipsis);
            int n; if (unread.TryGetValue(id, out n) && n > 0)
            {
                var r = new Rectangle(e.Bounds.Right - 34, e.Bounds.Top + 30, 24, 18);
                g.FillEllipse(new SolidBrush(Color.FromArgb(220, 30, 40)), r);
                TextRenderer.DrawText(g, n > 9 ? "9+" : n.ToString(), U.Small, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        /* ------------------------------------------------ chat */

        void CloseChat()
        {
            activeId = 0;
            chatPanel.Visible = false;
            empty.Visible = true;
        }

        async void OpenChat(long id)
        {
            activeId = id;
            unread.Remove(id);
            chatPanel.Visible = true; empty.Visible = false;
            ClearRows();
            RenderHead();
            RenderList();
            if (ringing.Contains(id)) await Accept(id); // abrir un chat que suena = atenderlo
            try
            {
                var r = await Task.Run(() => api.Get("/conversations/" + id + "/messages"));
                if (activeId != id) return;
                convs[id] = r["conversation"] as J;
                msgs[id] = U.A(r, "messages");
                RenderChat(true);
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            input.Focus();
        }

        void RenderHead()
        {
            J c; if (!convs.TryGetValue(activeId, out c)) return;
            lTitle.Text = ConvName(c);
            var parts = new List<string> { U.Channel(U.S(c, "channel")) };
            if (U.S(c, "email") != "") parts.Add(U.S(c, "email"));
            if (U.S(c, "phone") != "") parts.Add(U.S(c, "phone"));
            string lang = U.S(c, "lang");
            if (lang != "") parts.Add("Idioma: " + U.LangName(lang));
            long asg = U.L(c, "assigned_to");
            parts.Add(asg != 0 ? "Atiende: " + AgentName(asg) : "Sin asignar");
            lSub.Text = string.Join("  ·  ", parts);
            string warn = "";
            if (!U.B(c, "window_open")) warn = "⚠ Han pasado más de 24 h desde el último mensaje del cliente: WhatsApp/Facebook no permiten escribirle hasta que vuelva a escribir.";
            else if (lang != "" && lang != "es" && lang != "en") warn = "🌐 El cliente escribe en " + U.LangName(lang) + ": tus respuestas se traducirán automáticamente.";
            lWarn.Text = warn;
            lWarn.Height = warn == "" ? 0 : 28;
        }

        void ClearRows()
        {
            flow.SuspendLayout();
            while (flow.Controls.Count > 0) { var ctl = flow.Controls[0]; flow.Controls.RemoveAt(0); ctl.Dispose(); }
            flow.ResumeLayout();
            rendered.Clear();
        }

        void RenderChat(bool reset)
        {
            RenderHead();
            List<J> l; if (!msgs.TryGetValue(activeId, out l)) return;
            if (reset) ClearRows();
            var vs = flow.VerticalScroll;
            bool atBottom = reset || vs.Value + flow.ClientSize.Height >= vs.Maximum - 60;
            flow.SuspendLayout();
            Control last = null;
            foreach (var m in l)
            {
                long mid = U.L(m, "id");
                if (rendered.Contains(mid)) continue;
                rendered.Add(mid);
                last = MakeRow(m);
                flow.Controls.Add(last);
            }
            flow.ResumeLayout();
            RelayoutRows();
            if (last != null && atBottom) flow.ScrollControlIntoView(last);
        }

        int RowWidth { get { return Math.Max(200, flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth); } }

        void RelayoutRows()
        {
            foreach (Control row in flow.Controls) LayoutRow(row);
        }

        static void LayoutRow(Control row)
        {
            var flowParent = row.Parent as FlowLayoutPanel;
            if (flowParent == null || row.Controls.Count == 0) return;
            row.Width = Math.Max(200, flowParent.ClientSize.Width - flowParent.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
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
            var row = new Panel { Width = RowWidth, Height = 30, Margin = new Padding(0), Tag = dir == "system" ? "center" : dir == "in" ? "left" : "right" };
            var b = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(9, 6, 9, 5) };
            int maxText = (int)(RowWidth * 0.62);
            Func<string, Font, Color, Label> lbl = (t, f, col) => new Label { Text = t, Font = f, ForeColor = col, AutoSize = true, MaximumSize = new Size(maxText, 0), Margin = new Padding(0, 1, 0, 1), UseMnemonic = false };

            if (dir == "system")
            {
                b.BackColor = Color.FromArgb(232, 235, 240);
                b.Controls.Add(lbl(U.S(m, "body") + "  ·  " + U.Time(U.L(m, "created_at")), U.Small, Color.DimGray));
            }
            else
            {
                b.BackColor = dir == "in" ? Color.White : dir == "bot" ? Color.FromArgb(238, 241, 245) : Color.FromArgb(220, 235, 251);
                if (dir == "out") b.Controls.Add(lbl(AgentName(U.L(m, "agent_id")), U.Small, Color.Gray));
                if (dir == "bot") b.Controls.Add(lbl("🤖 Mensaje automático", U.Small, Color.Gray));
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
                        pic.Click += (s, e) => U.OpenUrl(url);
                        try { pic.LoadAsync(url); } catch { }
                        b.Controls.Add(pic);
                    }
                    else
                    {
                        string label = type == "audio" ? "▶  Escuchar audio" : type == "video" ? "▶  Ver vídeo" : "📄  " + (U.S(m, "media_name") != "" ? U.S(m, "media_name") : "Abrir documento");
                        var link = new LinkLabel { Text = label, AutoSize = true, Font = U.Bold, Margin = new Padding(0, 3, 0, 3), UseMnemonic = false };
                        link.LinkClicked += (s, e) => U.OpenUrl(url);
                        b.Controls.Add(link);
                    }
                }
                string body = U.S(m, "body"), tr = U.S(m, "translated"), lang = U.S(m, "lang");
                if (dir == "in" && tr != "")
                {
                    b.Controls.Add(lbl(tr, U.Base, Color.Black));
                    b.Controls.Add(lbl("🌐 Original (" + U.LangName(lang) + "): " + body, U.Small, Color.Gray));
                }
                else
                {
                    if (body != "") b.Controls.Add(lbl(body, U.Base, Color.Black));
                    if (dir == "out" && tr != "") b.Controls.Add(lbl("🌐 Enviado en " + U.LangName(lang) + ": " + tr, U.Small, Color.Gray));
                }
                if (U.S(m, "send_status") == "error") b.Controls.Add(lbl("⚠ No entregado: " + U.S(m, "send_error"), U.Small, Color.FromArgb(198, 40, 40)));
                b.Controls.Add(lbl(U.Time(U.L(m, "created_at")), U.Small, Color.Gray));
            }
            row.Controls.Add(b);
            b.SizeChanged += (s, e) => LayoutRow(row);
            return row;
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
            if (id == activeId) RenderChat(false);
            RenderList();
        }

        async void SendText()
        {
            string text = input.Text.Trim();
            long id = activeId;
            if (text == "" || id == 0 || !bSend.Enabled) return;
            bSend.Enabled = false;
            try
            {
                var r = await Task.Run(() => api.Post("/conversations/" + id + "/messages", new J { { "text", text } }));
                input.Clear();
                AfterSend(r);
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            finally { bSend.Enabled = true; input.Focus(); }
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
            bAttach.Enabled = false; bAttach.Text = "Enviando…";
            try
            {
                var r = await Task.Run(() => api.Upload("/conversations/" + id + "/messages", file, text));
                input.Clear();
                AfterSend(r);
            }
            catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            catch (IOException ex) { MessageBox.Show(this, "No se pudo leer el archivo: " + ex.Message, "Bezares Chat"); }
            finally { bAttach.Enabled = true; bAttach.Text = "📎 Adjuntar archivo"; }
        }

        async void Transfer()
        {
            long id = activeId;
            J c; if (id == 0 || !convs.TryGetValue(id, out c)) return;
            using (var tf = new TransferForm(agents, U.L(c, "assigned_to")))
            {
                if (tf.ShowDialog(this) != DialogResult.OK || tf.Agents.SelectedItem == null) return;
                var to = (TransferForm.AgentItem)tf.Agents.SelectedItem;
                string note = tf.Note.Text;
                try
                {
                    var r = await Task.Run(() => api.Post("/conversations/" + id + "/transfer", new J { { "agent_id", to.Id }, { "note", note } }));
                    convs[id] = r["conversation"] as J;
                    tray.ShowBalloonTip(2500, "Transferido", "Conversación transferida a " + to.Text, ToolTipIcon.Info);
                    RenderList();
                    OpenChat(id);
                }
                catch (ApiException ex) { MessageBox.Show(this, ex.Message, "Bezares Chat"); }
            }
        }
    }
}
