// Windows ISO İndir - Önsel
// UUP dump (uupdump.net) API'si ile güncel Türkçe Windows 10 / 11 ISO oluşturur.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WinIsoIndir
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo(Application.ExecutablePath);
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch
                {
                    MessageBox.Show("Program yönetici olarak çalıştırılmalı.", "Önsel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        static bool IsAdmin()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    class BuildInfo
    {
        public string Uuid;
        public string Title;
        public string Build;
        public string Arch;
    }

    class Uup
    {
        const string Api = "https://api.uupdump.net/";
        const string Site = "https://uupdump.net/";
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WindowsISOIndir/1.0";

        static WebClient NewClient()
        {
            var wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            wc.Headers[HttpRequestHeader.UserAgent] = UA;
            return wc;
        }

        static Dictionary<string, object> GetJson(string url)
        {
            string txt;
            using (var wc = NewClient()) txt = wc.DownloadString(url);
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            var root = ser.DeserializeObject(txt) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("response")) throw new Exception("API yanıtı okunamadı.");
            var resp = root["response"] as Dictionary<string, object>;
            if (resp == null) throw new Exception("API yanıtı okunamadı.");
            if (resp.ContainsKey("error")) throw new Exception("UUP dump hatası: " + resp["error"]);
            return resp;
        }

        static IEnumerable<object> Items(object o)
        {
            if (o is Dictionary<string, object>) return ((Dictionary<string, object>)o).Values;
            if (o is IEnumerable && !(o is string)) return ((IEnumerable)o).Cast<object>();
            return new object[0];
        }

        static string S(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        static Version ParseBuild(string b)
        {
            Version v;
            return Version.TryParse(b, out v) ? v : new Version(0, 0);
        }

        // Verilen başlık önekine ve mimariye uyan en yüksek build'i bulur.
        public static BuildInfo FindLatest(string prefix, string arch)
        {
            var resp = GetJson(Api + "listid.php?sortByDate=1&search=" + Uri.EscapeDataString(prefix));
            BuildInfo best = null;
            foreach (var o in Items(resp.ContainsKey("builds") ? resp["builds"] : null))
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                var b = new BuildInfo { Uuid = S(d, "uuid"), Title = S(d, "title"), Build = S(d, "build"), Arch = S(d, "arch") };
                if (!b.Title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(b.Arch, arch, StringComparison.OrdinalIgnoreCase)) continue;
                if (b.Title.IndexOf("Insider", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (b.Uuid.Length == 0) continue;
                if (best == null || ParseBuild(b.Build) > ParseBuild(best.Build)) best = b;
            }
            if (best == null) throw new Exception("Uygun build bulunamadı (" + prefix + ", " + arch + ").");
            return best;
        }

        public static List<string> Languages(string id)
        {
            var resp = GetJson(Api + "listlangs.php?id=" + id);
            return Items(resp.ContainsKey("langList") ? resp["langList"] : null).Select(x => x.ToString().ToLowerInvariant()).ToList();
        }

        public static List<string> Editions(string id, string lang)
        {
            var resp = GetJson(Api + "listeditions.php?id=" + id + "&lang=" + lang);
            return Items(resp.ContainsKey("editionList") ? resp["editionList"] : null).Select(x => x.ToString().ToLowerInvariant()).ToList();
        }

        // uupdump.net'in "indir ve ISO'ya dönüştür" paketini (zip) indirir.
        public static byte[] GetPackage(string id, string lang, string edition, bool updates, bool cleanup)
        {
            var form = new NameValueCollection();
            form["autodl"] = "2";
            if (updates) form["updates"] = "1";
            if (cleanup) form["cleanup"] = "1";
            string url = Site + "get.php?id=" + id + "&pack=" + lang + "&edition=" + Uri.EscapeDataString(edition);
            byte[] data;
            using (var wc = NewClient()) data = wc.UploadValues(url, "POST", form);
            if (data.Length < 4 || data[0] != 'P' || data[1] != 'K')
            {
                string t = Encoding.UTF8.GetString(data);
                t = Regex.Replace(t, "<[^>]+>", " ");
                t = Regex.Replace(t, "\\s+", " ").Trim();
                if (t.Length > 300) t = t.Substring(0, 300);
                throw new Exception("Paket alınamadı: " + t);
            }
            return data;
        }
    }

    class MainForm : Form
    {
        readonly Color Accent = Color.FromArgb(0, 103, 192);
        readonly Color Bg = Color.FromArgb(245, 247, 250);

        ComboBox cmbWin, cmbArch, cmbEd;
        TextBox txtOut, txtLog;
        CheckBox chkUpd, chkClean;
        Button btnStart, btnBrowse;
        ProgressBar bar;
        Label lblStatus;
        bool busy;

        public MainForm()
        {
            Text = "Önsel";
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Bg;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 560);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Accent };
            header.Controls.Add(new Label { Text = "Önsel", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 16f), AutoSize = true, Location = new Point(18, 8) });
            header.Controls.Add(new Label { Text = "Windows ISO İndir  •  Güncel Türkçe Windows 10 / 11", ForeColor = Color.FromArgb(220, 235, 255), AutoSize = true, Location = new Point(20, 42) });
            Controls.Add(header);

            int y = 88;
            AddLabel("Windows:", y); cmbWin = AddCombo(y, new[] { "Windows 11 (en güncel sürüm)", "Windows 10 22H2" });
            y += 36;
            AddLabel("Mimari:", y); cmbArch = AddCombo(y, new[] { "x64 (amd64)", "ARM64" });
            y += 36;
            AddLabel("Sürüm:", y); cmbEd = AddCombo(y, new[] { "Pro", "Home", "Pro + Home" });
            y += 36;
            AddLabel("Kayıt yeri:", y);
            txtOut = new TextBox { Location = new Point(120, y - 2), Width = 330, Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            btnBrowse = new Button { Text = "Seç...", Location = new Point(458, y - 4), Width = 80, Height = 28, FlatStyle = FlatStyle.System };
            btnBrowse.Click += delegate
            {
                using (var f = new FolderBrowserDialog { SelectedPath = txtOut.Text })
                    if (f.ShowDialog(this) == DialogResult.OK) txtOut.Text = f.SelectedPath;
            };
            Controls.Add(txtOut); Controls.Add(btnBrowse);
            y += 36;
            chkUpd = new CheckBox { Text = "Son güncellemeleri ISO'ya ekle", Checked = true, AutoSize = true, Location = new Point(120, y) };
            chkClean = new CheckBox { Text = "Bileşen temizliği yap (daha küçük ISO, daha uzun sürer)", Checked = false, AutoSize = true, Location = new Point(120, y + 24) };
            Controls.Add(chkUpd); Controls.Add(chkClean);
            y += 58;

            btnStart = new Button { Text = "ISO Oluştur", Location = new Point(120, y), Width = 160, Height = 34, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 10f) };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += delegate { Start(); };
            Controls.Add(btnStart);
            y += 46;

            lblStatus = new Label { Text = "Hazır.", Location = new Point(20, y), AutoSize = false, Width = 520, Height = 20 };
            Controls.Add(lblStatus);
            y += 22;
            bar = new ProgressBar { Location = new Point(20, y), Width = 520, Height = 14, Style = ProgressBarStyle.Continuous };
            Controls.Add(bar);
            y += 22;
            txtLog = new TextBox { Location = new Point(20, y), Width = 520, Height = 560 - y - 36, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Font = new Font("Consolas", 8.5f) };
            Controls.Add(txtLog);

            var footer = new Label { Text = "Hazırlayan: Önsel   •   Kaynak: uupdump.net (Microsoft sunucuları)", ForeColor = Color.Gray, AutoSize = true, Location = new Point(20, 536) };
            Controls.Add(footer);

            cmbWin.SelectedIndexChanged += delegate
            {
                bool w10 = cmbWin.SelectedIndex == 1;
                cmbArch.Items.Clear();
                cmbArch.Items.Add("x64 (amd64)");
                if (w10) cmbArch.Items.Add("x86 (32 bit)"); else cmbArch.Items.Add("ARM64");
                cmbArch.SelectedIndex = 0;
            };
            FormClosing += (s, e) =>
            {
                if (busy && MessageBox.Show("İşlem sürüyor. Kapatmak istiyor musunuz?", "Önsel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        void AddLabel(string t, int y)
        {
            Controls.Add(new Label { Text = t, Location = new Point(20, y), AutoSize = true });
        }

        ComboBox AddCombo(int y, string[] items)
        {
            var c = new ComboBox { Location = new Point(120, y - 3), Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SelectedIndex = 0;
            Controls.Add(c);
            return c;
        }

        void Log(string s)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), s); return; }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
        }

        void Status(string s, int pct)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, int>(Status), s, pct); return; }
            lblStatus.Text = s;
            if (pct < 0) { bar.Style = ProgressBarStyle.Marquee; }
            else { bar.Style = ProgressBarStyle.Continuous; bar.Value = Math.Max(0, Math.Min(100, pct)); }
        }

        void SetBusy(bool b)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(SetBusy), b); return; }
            busy = b;
            foreach (Control c in new Control[] { cmbWin, cmbArch, cmbEd, txtOut, btnBrowse, chkUpd, chkClean, btnStart }) c.Enabled = !b;
        }

        void Start()
        {
            string outDir = txtOut.Text.Trim();
            if (!Directory.Exists(outDir)) { MessageBox.Show("Kayıt klasörü bulunamadı.", "Önsel"); return; }

            string sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
            long free = new DriveInfo(sysDrive).AvailableFreeSpace;
            if (free < 20L * 1024 * 1024 * 1024 &&
                MessageBox.Show(sysDrive + " sürücüsünde " + (free / 1073741824) + " GB boş alan var. En az 20 GB önerilir. Devam edilsin mi?", "Önsel", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            bool w10 = cmbWin.SelectedIndex == 1;
            string prefix = w10 ? "Windows 10, version 22H2" : "Windows 11, version";
            string arch = cmbArch.SelectedIndex == 0 ? "amd64" : (w10 ? "x86" : "arm64");
            string edition = cmbEd.SelectedIndex == 0 ? "professional" : cmbEd.SelectedIndex == 1 ? "core" : "professional;core";
            bool upd = chkUpd.Checked, clean = chkClean.Checked;

            txtLog.Clear();
            SetBusy(true);
            var th = new Thread(() => Run(prefix, arch, edition, upd, clean, outDir, sysDrive));
            th.IsBackground = true;
            th.Start();
        }

        void Run(string prefix, string arch, string edition, bool upd, bool clean, string outDir, string sysDrive)
        {
            string work = null;
            try
            {
                const string lang = "tr-tr";
                Status("Güncel build aranıyor...", -1);
                var b = Uup.FindLatest(prefix, arch);
                Log("Bulunan: " + b.Title);

                Status("Dil ve sürüm kontrol ediliyor...", -1);
                if (!Uup.Languages(b.Uuid).Contains(lang)) throw new Exception("Bu build için Türkçe bulunamadı.");
                var eds = Uup.Editions(b.Uuid, lang);
                foreach (var e in edition.Split(';'))
                    if (!eds.Contains(e)) throw new Exception("Sürüm bulunamadı: " + e);
                Log("Türkçe ve seçilen sürüm mevcut.");

                Status("İndirme paketi hazırlanıyor...", -1);
                var zip = Uup.GetPackage(b.Uuid, lang, edition, upd, clean);

                // Dönüştürücü kısa ve boşluksuz yol ister.
                work = Path.Combine(sysDrive, "UUPTemp\\" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(work);
                string zipPath = Path.Combine(work, "paket.zip");
                File.WriteAllBytes(zipPath, zip);
                ZipFile.ExtractToDirectory(zipPath, work);
                File.Delete(zipPath);
                Log("Paket açıldı: " + work);

                // İşlem sonunda tuş beklemesin.
                foreach (var ini in Directory.GetFiles(work, "ConvertConfig.ini", SearchOption.AllDirectories))
                {
                    string t = File.ReadAllText(ini);
                    t = Regex.Replace(t, @"(?mi)^(\s*AutoExit\s*=\s*)\d+", "${1}1");
                    File.WriteAllText(ini, t);
                }

                string cmd = Directory.GetFiles(work, "uup_download_windows.cmd", SearchOption.AllDirectories).FirstOrDefault();
                if (cmd == null) throw new Exception("uup_download_windows.cmd paket içinde bulunamadı.");

                Status("İndiriliyor ve ISO oluşturuluyor... (konsol penceresini kapatmayın)", -1);
                Log("İndirme ve dönüştürme başladı. Bu işlem 20-60 dk sürebilir.");
                var psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + cmd + "\"\"");
                psi.WorkingDirectory = Path.GetDirectoryName(cmd);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = false;
                using (var p = Process.Start(psi)) p.WaitForExit();

                var iso = Directory.GetFiles(work, "*.iso", SearchOption.AllDirectories)
                    .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
                if (iso == null) throw new Exception("ISO oluşturulamadı. Klasördeki kayıtları kontrol edin: " + work);

                Status("ISO taşınıyor...", -1);
                string dest = Path.Combine(outDir, Path.GetFileName(iso));
                if (File.Exists(dest)) dest = Path.Combine(outDir, Path.GetFileNameWithoutExtension(iso) + "_" + DateTime.Now.ToString("HHmmss") + ".iso");
                File.Move(iso, dest);
                Log("ISO: " + dest);

                Status("Geçici dosyalar siliniyor...", -1);
                try { Directory.Delete(work, true); } catch { }
                try { var parent = Path.GetDirectoryName(work); if (Directory.GetFileSystemEntries(parent).Length == 0) Directory.Delete(parent); } catch { }

                Status("Tamamlandı.", 100);
                BeginInvoke(new Action(() =>
                {
                    if (MessageBox.Show(this, "ISO hazır:\n" + dest + "\n\nKlasör açılsın mı?", "Önsel", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        Process.Start("explorer.exe", "/select,\"" + dest + "\"");
                }));
            }
            catch (Exception ex)
            {
                string msg = ex is WebException ? "Bağlantı hatası: " + ex.Message : ex.Message;
                Log("HATA: " + msg);
                Status("Hata oluştu.", 0);
                BeginInvoke(new Action(() => MessageBox.Show(this, msg, "Önsel", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}
