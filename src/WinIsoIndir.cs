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
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Windows ISO İndir")]
[assembly: AssemblyProduct("Windows ISO İndir")]
[assembly: AssemblyCompany("Önsel Özden")]
[assembly: AssemblyCopyright("Hazırlayan: Önsel Özden")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class Program
{
    public const string Ad = "Windows ISO İndir";
    public const string Surum = "1.1";

    [STAThread]
    static void Main()
    {
        if (!YoneticiMi())
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" };
                Process.Start(psi);
            }
            catch
            {
                MessageBox.Show("Program yönetici izniyle çalışmalı.", Ad, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288; }
        catch { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new AnaForm());
    }

    static bool YoneticiMi()
    {
        try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
        catch { return false; }
    }
}

class Tema
{
    public Color Zemin, Kart, Yazi, Soluk, Vurgu, Kenar, Basari, Hata;
    public bool Koyu;
    public static readonly Tema Acik = new Tema { Zemin = Color.FromArgb(245, 247, 250), Kart = Color.White, Yazi = Color.FromArgb(20, 24, 31), Soluk = Color.FromArgb(110, 118, 130), Vurgu = Color.FromArgb(10, 92, 255), Kenar = Color.FromArgb(220, 224, 230), Basari = Color.FromArgb(0, 140, 70), Hata = Color.FromArgb(200, 40, 40), Koyu = false };
    public static readonly Tema KoyuT = new Tema { Zemin = Color.FromArgb(20, 24, 31), Kart = Color.FromArgb(32, 37, 46), Yazi = Color.FromArgb(235, 238, 243), Soluk = Color.FromArgb(150, 158, 170), Vurgu = Color.FromArgb(40, 115, 255), Kenar = Color.FromArgb(55, 62, 74), Basari = Color.FromArgb(60, 200, 120), Hata = Color.FromArgb(255, 90, 90), Koyu = true };
}

static class Logo
{
    static Image img;
    public static Image Al()
    {
        if (img == null)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo96.png");
            if (s != null) img = Image.FromStream(s);
        }
        return img;
    }
    public static Icon Ikon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { return null; }
    }
}

static class Ayar
{
    const string Yol = @"Software\WindowsISOIndir";
    public static string Oku(string ad, string vars)
    {
        try { using (var k = Registry.CurrentUser.OpenSubKey(Yol)) { if (k != null) { var v = k.GetValue(ad) as string; if (v != null) return v; } } } catch { }
        return vars;
    }
    public static void Yaz(string ad, string deger)
    {
        try { using (var k = Registry.CurrentUser.CreateSubKey(Yol)) k.SetValue(ad, deger); } catch { }
    }
}

// ---------- Ürünler ----------

class SurumSecenek
{
    public string Ad;
    public string[] Onekler; // UUP sürüm kimliği önekleri (ör. "professional", "serverstandard")
    public override string ToString() { return Ad; }
}

class Urun
{
    public string Ad;
    public string[] Aramalar;
    public Func<string, bool> BaslikUygun;
    public string[] Mimariler;
    public SurumSecenek[] Surumler;
    public bool Sunucu;
    public override string ToString() { return Ad; }

    static SurumSecenek S(string ad, params string[] onek) { return new SurumSecenek { Ad = ad, Onekler = onek }; }

    static bool Icerir(string t, string s) { return t.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0; }

    public static readonly Urun[] Hepsi =
    {
        new Urun
        {
            Ad = "Windows 11 (en güncel sürüm)",
            Aramalar = new[] { "Windows 11, version" },
            BaslikUygun = t => t.StartsWith("Windows 11, version", StringComparison.OrdinalIgnoreCase),
            Mimariler = new[] { "x64 (amd64)|amd64", "ARM64|arm64" },
            Surumler = new[] { S("Pro", "professional"), S("Home", "core"), S("Pro + Home", "professional", "core") }
        },
        new Urun
        {
            Ad = "Windows 10 22H2",
            Aramalar = new[] { "Windows 10, version 22H2" },
            BaslikUygun = t => t.StartsWith("Windows 10, version 22H2", StringComparison.OrdinalIgnoreCase),
            Mimariler = new[] { "x64 (amd64)|amd64", "x86 (32 bit)|x86" },
            Surumler = new[] { S("Pro", "professional"), S("Home", "core"), S("Pro + Home", "professional", "core") }
        },
        new Urun
        {
            Ad = "Windows Server (en güncel sürüm)",
            Aramalar = new[] { "Windows Server", "server operating system" },
            BaslikUygun = t => Icerir(t, "server") && !Icerir(t, "Insider") && !Icerir(t, "Preview") && !Icerir(t, "Azure")
                               && !Icerir(t, "Cumulative") && !Icerir(t, "Update for") && !Icerir(t, "vNext"),
            Mimariler = new[] { "x64 (amd64)|amd64" },
            Surumler = new[] { S("Standard", "serverstandard"), S("Datacenter", "serverdatacenter"), S("Standard + Datacenter", "serverstandard", "serverdatacenter") },
            Sunucu = true
        }
    };
}

class BuildBilgi
{
    public string Uuid, Baslik, Build, Mimari;
}

static class Uup
{
    const string Api = "https://api.uupdump.net/";
    const string Site = "https://uupdump.net/";
    const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WindowsISOIndir/" + Program.Surum;

    static WebClient Istemci()
    {
        var wc = new WebClient();
        wc.Encoding = Encoding.UTF8;
        wc.Headers[HttpRequestHeader.UserAgent] = UA;
        return wc;
    }

    static Dictionary<string, object> Json(string url)
    {
        string txt;
        using (var wc = Istemci()) txt = wc.DownloadString(url);
        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var kok = ser.DeserializeObject(txt) as Dictionary<string, object>;
        object r;
        if (kok == null || !kok.TryGetValue("response", out r) || !(r is Dictionary<string, object>)) throw new Exception("UUP dump yanıtı okunamadı.");
        var resp = (Dictionary<string, object>)r;
        if (resp.ContainsKey("error")) throw new Exception("UUP dump hatası: " + resp["error"]);
        return resp;
    }

    static IEnumerable<object> Ogeler(Dictionary<string, object> d, string k)
    {
        object o;
        if (!d.TryGetValue(k, out o) || o == null) return new object[0];
        if (o is Dictionary<string, object>) return ((Dictionary<string, object>)o).Values;
        if (o is IEnumerable && !(o is string)) return ((IEnumerable)o).Cast<object>();
        return new object[0];
    }

    static string D(Dictionary<string, object> d, string k)
    {
        object v;
        return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
    }

    static Version Ver(string b)
    {
        Version v;
        return Version.TryParse(b, out v) ? v : new Version(0, 0);
    }

    public static BuildBilgi EnGuncel(Urun u, string mimari)
    {
        BuildBilgi enIyi = null;
        foreach (var arama in u.Aramalar)
        {
            var resp = Json(Api + "listid.php?sortByDate=1&search=" + Uri.EscapeDataString(arama));
            foreach (var o in Ogeler(resp, "builds"))
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                var b = new BuildBilgi { Uuid = D(d, "uuid"), Baslik = D(d, "title"), Build = D(d, "build"), Mimari = D(d, "arch") };
                if (b.Uuid.Length == 0 || !u.BaslikUygun(b.Baslik)) continue;
                if (!string.Equals(b.Mimari, mimari, StringComparison.OrdinalIgnoreCase)) continue;
                if (enIyi == null || Ver(b.Build) > Ver(enIyi.Build)) enIyi = b;
            }
        }
        if (enIyi == null) throw new Exception("Uygun build bulunamadı (" + u.Ad + ", " + mimari + ").");
        return enIyi;
    }

    public static List<string> Diller(string id)
    {
        return Ogeler(Json(Api + "listlangs.php?id=" + id), "langList").Select(x => x.ToString().ToLowerInvariant()).ToList();
    }

    public static List<string> Surumler(string id, string dil)
    {
        return Ogeler(Json(Api + "listeditions.php?id=" + id + "&lang=" + dil), "editionList").Select(x => x.ToString().ToLowerInvariant()).ToList();
    }

    public static byte[] Paket(string id, string dil, string surum, bool guncelleme, bool temizlik)
    {
        var form = new NameValueCollection();
        form["autodl"] = "2";
        if (guncelleme) form["updates"] = "1";
        if (temizlik) form["cleanup"] = "1";
        string url = Site + "get.php?id=" + id + "&pack=" + dil + "&edition=" + Uri.EscapeDataString(surum);
        byte[] veri;
        using (var wc = Istemci()) veri = wc.UploadValues(url, "POST", form);
        if (veri.Length < 4 || veri[0] != 'P' || veri[1] != 'K')
        {
            string t = Encoding.UTF8.GetString(veri);
            t = Regex.Replace(t, "<[^>]+>", " ");
            t = Regex.Replace(t, "\\s+", " ").Trim();
            if (t.Length > 300) t = t.Substring(0, 300);
            throw new Exception("İndirme paketi alınamadı: " + t);
        }
        return veri;
    }
}

// ---------- Arayüz ----------

class AnaForm : Form
{
    Tema tema;
    MenuStrip menu;
    ToolStripMenuItem miAcik, miKoyu;
    Label lblBaslik, lblAlt, lblIslem;
    Panel pnlKart;
    ComboBox cmbUrun, cmbMimari, cmbSurum;
    TextBox txtKlasor, txtKayit;
    CheckBox chkGuncel, chkTemiz;
    Button btnBaslat, btnKlasor;
    ProgressBar pb;
    bool calisiyor;

    public AnaForm()
    {
        Text = Program.Ad + " - Önsel";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        var ik = Logo.Ikon(); if (ik != null) Icon = ik;

        menu = new MenuStrip();
        var mTema = new ToolStripMenuItem("Tema");
        miAcik = new ToolStripMenuItem("Beyaz (açık)", null, delegate { TemaUygula(Tema.Acik, true); });
        miKoyu = new ToolStripMenuItem("Siyah (koyu)", null, delegate { TemaUygula(Tema.KoyuT, true); });
        mTema.DropDownItems.Add(miAcik);
        mTema.DropDownItems.Add(miKoyu);
        var mHak = new ToolStripMenuItem("Hakkında", null, delegate { using (var h = new HakkindaForm(tema)) h.ShowDialog(this); });
        menu.Items.Add(mTema);
        menu.Items.Add(mHak);
        MainMenuStrip = menu;

        int y0 = 34;
        var pbLogo = new PictureBox { Image = Logo.Al(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(20, y0 + 6), Size = new Size(48, 48) };
        Controls.Add(pbLogo);
        lblBaslik = new Label { Text = Program.Ad, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Location = new Point(78, y0 + 6), AutoSize = true };
        lblAlt = new Label { Text = "Güncel Türkçe Windows 10 / 11 / Server ISO", Font = new Font("Segoe UI", 9f), Location = new Point(80, y0 + 36), AutoSize = true };
        Controls.Add(lblBaslik); Controls.Add(lblAlt);
        var lblAd = new Label { Text = "Önsel", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(410, y0 + 36), Size = new Size(110, 20), TextAlign = ContentAlignment.MiddleRight, Tag = "soluk" };
        Controls.Add(lblAd);

        pnlKart = new Panel { Location = new Point(20, y0 + 70), Size = new Size(500, 230) };
        int ry = 14;
        cmbUrun = Satir("Windows", ry); ry += 38;
        cmbMimari = Satir("Mimari", ry); ry += 38;
        cmbSurum = Satir("Sürüm", ry); ry += 38;
        pnlKart.Controls.Add(new Label { Text = "Kayıt yeri", Location = new Point(14, ry + 3), Size = new Size(90, 24) });
        txtKlasor = new TextBox { Location = new Point(110, ry), Width = 280, Text = Ayar.Oku("Klasor", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)), BorderStyle = BorderStyle.FixedSingle };
        btnKlasor = new Button { Text = "Seç...", Location = new Point(398, ry - 2), Size = new Size(88, 30), FlatStyle = FlatStyle.Flat };
        btnKlasor.Click += delegate
        {
            using (var f = new FolderBrowserDialog { SelectedPath = txtKlasor.Text })
                if (f.ShowDialog(this) == DialogResult.OK) txtKlasor.Text = f.SelectedPath;
        };
        pnlKart.Controls.Add(txtKlasor); pnlKart.Controls.Add(btnKlasor);
        ry += 40;
        chkGuncel = new CheckBox { Text = "Son güncellemeleri ISO'ya ekle", Checked = true, Location = new Point(110, ry), Size = new Size(380, 26) };
        chkTemiz = new CheckBox { Text = "Bileşen temizliği (daha küçük ISO, daha uzun sürer)", Checked = false, Location = new Point(110, ry + 28), Size = new Size(380, 26) };
        pnlKart.Controls.Add(chkGuncel); pnlKart.Controls.Add(chkTemiz);
        Controls.Add(pnlKart);

        foreach (var u in Urun.Hepsi) cmbUrun.Items.Add(u);
        cmbUrun.SelectedIndexChanged += delegate { UrunDegisti(); };
        cmbUrun.SelectedIndex = 0;

        int y = pnlKart.Bottom + 14;
        lblIslem = new Label { Text = "Hazır", Location = new Point(20, y), Size = new Size(500, 22) };
        pb = new ProgressBar { Location = new Point(20, y + 26), Size = new Size(500, 18) };
        txtKayit = new TextBox { Location = new Point(20, y + 54), Size = new Size(500, 120), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 8.5f) };
        Controls.AddRange(new Control[] { lblIslem, pb, txtKayit });

        btnBaslat = new Button { Text = "ISO Oluştur", Location = new Point(360, txtKayit.Bottom + 14), Size = new Size(160, 38), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Color.White };
        btnBaslat.FlatAppearance.BorderSize = 0;
        btnBaslat.Click += delegate { Baslat(); };
        var lblKaynak = new Label { Text = "Kaynak: uupdump.net (Microsoft sunucuları)", Font = new Font("Segoe UI", 8.5f), Location = new Point(20, txtKayit.Bottom + 24), AutoSize = true, Tag = "soluk" };
        Controls.Add(btnBaslat); Controls.Add(lblKaynak);
        Controls.Add(menu);

        ClientSize = new Size(540, btnBaslat.Bottom + 20);
        TemaUygula(Ayar.Oku("Tema", "acik") == "koyu" ? Tema.KoyuT : Tema.Acik, false);
    }

    ComboBox Satir(string etiket, int y)
    {
        pnlKart.Controls.Add(new Label { Text = etiket, Location = new Point(14, y + 3), Size = new Size(90, 24) });
        var c = new ComboBox { Location = new Point(110, y), Width = 376, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        pnlKart.Controls.Add(c);
        return c;
    }

    void UrunDegisti()
    {
        var u = (Urun)cmbUrun.SelectedItem;
        cmbMimari.Items.Clear();
        foreach (var m in u.Mimariler) cmbMimari.Items.Add(m.Split('|')[0]);
        cmbMimari.SelectedIndex = 0;
        cmbSurum.Items.Clear();
        foreach (var s in u.Surumler) cmbSurum.Items.Add(s);
        cmbSurum.SelectedIndex = 0;
    }

    void TemaUygula(Tema t, bool kaydet)
    {
        tema = t;
        BackColor = t.Zemin;
        menu.BackColor = t.Kart; menu.ForeColor = t.Yazi;
        menu.Renderer = new ToolStripProfessionalRenderer(new MenuRenk(t));
        foreach (ToolStripMenuItem m in menu.Items)
        {
            m.ForeColor = t.Yazi;
            foreach (ToolStripItem a in m.DropDownItems) { a.BackColor = t.Kart; a.ForeColor = t.Yazi; }
        }
        miAcik.Checked = !t.Koyu; miKoyu.Checked = t.Koyu;
        lblBaslik.ForeColor = t.Yazi; lblAlt.ForeColor = t.Soluk; lblIslem.ForeColor = t.Yazi;
        foreach (Control c in Controls) if ("soluk".Equals(c.Tag)) c.ForeColor = t.Soluk;
        pnlKart.BackColor = t.Kart;
        foreach (Control c in pnlKart.Controls)
        {
            c.ForeColor = t.Yazi;
            c.BackColor = (c is TextBox || c is ComboBox) ? t.Zemin : t.Kart;
        }
        btnKlasor.FlatAppearance.BorderColor = t.Kenar;
        txtKayit.BackColor = t.Kart; txtKayit.ForeColor = t.Yazi;
        btnBaslat.BackColor = t.Vurgu;
        if (kaydet) Ayar.Yaz("Tema", t.Koyu ? "koyu" : "acik");
    }

    void UI(Action a) { if (IsHandleCreated) BeginInvoke(a); }

    void Kayit(string s)
    {
        UI(delegate { txtKayit.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine); });
    }

    void Durum(string s, string tur)
    {
        UI(delegate
        {
            lblIslem.Text = s;
            lblIslem.ForeColor = tur == "ok" ? tema.Basari : tur == "hata" ? tema.Hata : tema.Yazi;
            if (tur == "aktif") { pb.Style = ProgressBarStyle.Marquee; pb.MarqueeAnimationSpeed = 30; }
            else { pb.Style = ProgressBarStyle.Continuous; pb.Value = tur == "ok" ? 100 : 0; }
        });
    }

    void Kilit(bool k)
    {
        UI(delegate
        {
            calisiyor = k;
            foreach (Control c in new Control[] { cmbUrun, cmbMimari, cmbSurum, txtKlasor, btnKlasor, chkGuncel, chkTemiz, btnBaslat }) c.Enabled = !k;
        });
    }

    void Baslat()
    {
        string klasor = txtKlasor.Text.Trim();
        if (!Directory.Exists(klasor)) { MessageBox.Show(this, "Kayıt klasörü bulunamadı.", Text); return; }
        Ayar.Yaz("Klasor", klasor);

        string surucu = Path.GetPathRoot(Environment.SystemDirectory);
        long bos = new DriveInfo(surucu).AvailableFreeSpace;
        if (bos < 20L * 1073741824 &&
            MessageBox.Show(this, surucu + " sürücüsünde " + (bos / 1073741824) + " GB boş alan var. En az 20 GB önerilir. Devam edilsin mi?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var u = (Urun)cmbUrun.SelectedItem;
        string mimari = u.Mimariler[cmbMimari.SelectedIndex].Split('|')[1];
        var s = (SurumSecenek)cmbSurum.SelectedItem;
        bool gnc = chkGuncel.Checked, tmz = chkTemiz.Checked;

        txtKayit.Clear();
        Kilit(true);
        var th = new Thread(() => Calis(u, mimari, s, gnc, tmz, klasor, surucu)) { IsBackground = true };
        th.Start();
    }

    bool Sor(string mesaj)
    {
        bool sonuc = false;
        Invoke((Action)delegate { sonuc = MessageBox.Show(this, mesaj, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; });
        return sonuc;
    }

    void Calis(Urun u, string mimari, SurumSecenek s, bool gnc, bool tmz, string klasor, string surucu)
    {
        string calisma = null;
        try
        {
            Durum("Güncel build aranıyor...", "aktif");
            var b = Uup.EnGuncel(u, mimari);
            Kayit("Bulunan: " + b.Baslik);

            Durum("Dil ve sürüm kontrol ediliyor...", "aktif");
            string dil = "tr-tr";
            var diller = Uup.Diller(b.Uuid);
            if (!diller.Contains(dil))
            {
                if (u.Sunucu && diller.Contains("en-us") && Sor("Bu sunucu sürümünde Türkçe yok. İngilizce (en-us) indirilsin mi?"))
                    dil = "en-us";
                else
                    throw new Exception("Bu build için Türkçe bulunamadı.");
            }
            var mevcut = Uup.Surumler(b.Uuid, dil);
            var secilen = new List<string>();
            foreach (var on in s.Onekler)
            {
                var es = u.Sunucu ? mevcut.Where(e => e.StartsWith(on)).ToList() : mevcut.Where(e => e == on).ToList();
                if (es.Count == 0) throw new Exception("Sürüm bulunamadı: " + on + "  (mevcut: " + string.Join(", ", mevcut) + ")");
                secilen.AddRange(es);
            }
            string surum = string.Join(";", secilen.Distinct());
            Kayit("Dil: " + dil + "   Sürüm: " + surum);

            Durum("İndirme paketi hazırlanıyor...", "aktif");
            var zip = Uup.Paket(b.Uuid, dil, surum, gnc, tmz);

            // Dönüştürücü kısa ve boşluksuz yol ister.
            calisma = Path.Combine(surucu, "UUPTemp\\" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(calisma);
            string zipYol = Path.Combine(calisma, "paket.zip");
            File.WriteAllBytes(zipYol, zip);
            ZipFile.ExtractToDirectory(zipYol, calisma);
            File.Delete(zipYol);
            Kayit("Paket açıldı: " + calisma);

            foreach (var ini in Directory.GetFiles(calisma, "ConvertConfig.ini", SearchOption.AllDirectories))
            {
                string t = File.ReadAllText(ini);
                t = Regex.Replace(t, @"(?mi)^(\s*AutoExit\s*=\s*)\d+", "${1}1");
                File.WriteAllText(ini, t);
            }

            string cmd = Directory.GetFiles(calisma, "uup_download_windows.cmd", SearchOption.AllDirectories).FirstOrDefault();
            if (cmd == null) throw new Exception("uup_download_windows.cmd paket içinde bulunamadı.");

            Durum("İndiriliyor ve ISO oluşturuluyor... (konsol penceresini kapatmayın)", "aktif");
            Kayit("İndirme ve dönüştürme başladı. 20-60 dk sürebilir.");
            var psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + cmd + "\"\"")
            {
                WorkingDirectory = Path.GetDirectoryName(cmd),
                UseShellExecute = false,
                CreateNoWindow = false
            };
            using (var p = Process.Start(psi)) p.WaitForExit();

            var iso = Directory.GetFiles(calisma, "*.iso", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            if (iso == null) throw new Exception("ISO oluşturulamadı. Kayıtlar: " + calisma);

            Durum("ISO taşınıyor...", "aktif");
            string hedef = Path.Combine(klasor, Path.GetFileName(iso));
            if (File.Exists(hedef)) hedef = Path.Combine(klasor, Path.GetFileNameWithoutExtension(iso) + "_" + DateTime.Now.ToString("HHmmss") + ".iso");
            File.Move(iso, hedef);
            Kayit("ISO: " + hedef);

            Durum("Geçici dosyalar siliniyor...", "aktif");
            try { Directory.Delete(calisma, true); } catch { }
            try { var ust = Path.GetDirectoryName(calisma); if (Directory.GetFileSystemEntries(ust).Length == 0) Directory.Delete(ust); } catch { }

            Durum("Tamamlandı.", "ok");
            UI(delegate
            {
                if (MessageBox.Show(this, "ISO hazır:\n" + hedef + "\n\nKlasör açılsın mı?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start("explorer.exe", "/select,\"" + hedef + "\"");
            });
        }
        catch (Exception ex)
        {
            string m = ex is WebException ? "Bağlantı hatası: " + ex.Message : ex.Message;
            Kayit("HATA: " + m);
            Durum("Hata oluştu.", "hata");
            UI(delegate { MessageBox.Show(this, m, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); });
        }
        finally
        {
            Kilit(false);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (calisiyor && MessageBox.Show(this, "İşlem sürüyor. Yine de kapatılsın mı?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            e.Cancel = true;
        base.OnFormClosing(e);
    }
}

class MenuRenk : ProfessionalColorTable
{
    Tema t;
    public MenuRenk(Tema t) { this.t = t; UseSystemColors = false; }
    public override Color MenuStripGradientBegin { get { return t.Kart; } }
    public override Color MenuStripGradientEnd { get { return t.Kart; } }
    public override Color ToolStripDropDownBackground { get { return t.Kart; } }
    public override Color ImageMarginGradientBegin { get { return t.Kart; } }
    public override Color ImageMarginGradientMiddle { get { return t.Kart; } }
    public override Color ImageMarginGradientEnd { get { return t.Kart; } }
    public override Color MenuItemSelected { get { return t.Kenar; } }
    public override Color MenuItemSelectedGradientBegin { get { return t.Kenar; } }
    public override Color MenuItemSelectedGradientEnd { get { return t.Kenar; } }
    public override Color MenuItemPressedGradientBegin { get { return t.Kenar; } }
    public override Color MenuItemPressedGradientEnd { get { return t.Kenar; } }
    public override Color MenuItemBorder { get { return t.Kenar; } }
    public override Color MenuBorder { get { return t.Kenar; } }
    public override Color CheckBackground { get { return t.Kenar; } }
    public override Color CheckSelectedBackground { get { return t.Kenar; } }
}

class HakkindaForm : Form
{
    public HakkindaForm(Tema t)
    {
        Text = "Hakkında";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(340, 280);
        Font = new Font("Segoe UI", 10f);
        BackColor = t.Zemin;
        var ik = Logo.Ikon(); if (ik != null) Icon = ik;

        var pb = new PictureBox { Image = Logo.Al(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(80, 80), Location = new Point(130, 20) };
        var ad = new Label { Text = Program.Ad, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = t.Yazi, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 108), Size = new Size(340, 28) };
        var sur = new Label { Text = "Sürüm " + Program.Surum, ForeColor = t.Soluk, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 136), Size = new Size(340, 22) };
        var acik = new Label { Text = "Kaynak: uupdump.net (Microsoft sunucuları)", Font = new Font("Segoe UI", 9f), ForeColor = t.Soluk, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 158), Size = new Size(340, 20) };
        var haz = new Label { Text = "Hazırlayan: Önsel Özden", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = t.Yazi, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 188), Size = new Size(340, 24) };
        var tamam = new Button { Text = "Tamam", DialogResult = DialogResult.OK, Size = new Size(100, 32), Location = new Point(120, 230), FlatStyle = FlatStyle.Flat, BackColor = t.Vurgu, ForeColor = Color.White };
        tamam.FlatAppearance.BorderSize = 0;
        AcceptButton = tamam; CancelButton = tamam;
        Controls.AddRange(new Control[] { pb, ad, sur, acik, haz, tamam });
    }
}
