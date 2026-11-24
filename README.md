# Windows ISO İndir

Güncel **Türkçe Windows 10 / 11 / Server** ISO dosyasını, [UUP dump](https://uupdump.net) üzerinden doğrudan Microsoft sunucularından indirip oluşturan küçük bir Windows programı.

Sistemi, mimariyi ve sürümü seç, **ISO Oluştur**'a bas. Program seçilen sürümün en güncel build'ini kendisi bulur, indirir ve ISO'ya dönüştürür.

## Seçenekler

- Windows 11 26H2 / 25H2 / 24H2 – x64 / ARM64 – Pro, Home, Pro + Home
- Windows 10 22H2 – x64 / x86 – Pro, Home, Pro + Home
- Windows Server 2025 / 2022 – x64 – Standard, Datacenter, Standard + Datacenter (Masaüstü ve Core)
- Son güncellemeleri ISO'ya ekleme, bileşen temizliği

## Özellikler

- Tek dosya, kurulum gerektirmez
- Beyaz / siyah tema
- Yönetici izniyle çalışır (UAC)
- En az 20 GB boş alan önerilir (geçici dosyalar `C:\UUPTemp`)

## İndirme

[Releases](../../releases) sayfasından `Windows-ISO-Indir.exe`'yi indir.

Program imzasız olduğu için Windows "Bilinmeyen yayımcı" uyarısı verebilir: **Ek bilgi → Yine de çalıştır**.

## Derleme

Windows (.NET Framework csc):

```
rc assets\app.rc
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /codepage:65001 /win32res:assets\app.res /resource:assets\logo96.png,logo96.png /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Windows-ISO-Indir.exe src\WinIsoIndir.cs
```

Linux (Mono + mingw windres):

```
cd assets && x86_64-w64-mingw32-windres --preprocessor=cat -c 1254 -i app.rc -O res -o app.res && cd ..
mcs -target:winexe -codepage:utf8 -win32res:assets/app.res -resource:assets/logo96.png,logo96.png -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -out:Windows-ISO-Indir.exe src/WinIsoIndir.cs
```

## Hazırlayan

Önsel Özden
