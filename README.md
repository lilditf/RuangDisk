# RuangDisk 💾

> **Penganalisis Ruang Penyimpanan Windows Pribadi, Cepat, dan 100% Offline Lokal.**

RuangDisk adalah aplikasi desktop native Windows yang dirancang untuk membantu Anda menemukan folder dan file yang paling banyak mengonsumsi ruang penyimpanan disk secara hierarkis, interaktif, dan aman tanpa memerlukan akun, backend, ataupun koneksi internet.

---

## ✨ Fitur Utama

- **🚀 High-Speed Native Scanner**:
  - Ditenagai Win32 Kernel API (`FindFirstFileExW` dengan opsi `FIND_FIRST_EX_LARGE_FETCH`).
  - Mengabaikan overhead managed C# untuk kecepatan baca filesystem hingga 3x lipat.
  - Streaming progress dengan indikator kecepatan I/O real-time (*items/sec* & *bytes/sec*).
- **⏸ Jeda & Lanjutkan (Pause & Resume)**:
  - Kemampuan menjeda proses scanning seketika tanpa kehilangan data progres atau membuang CPU.
- **🌲 Pohon Hierarki (Virtual & Lazy-Loaded)**:
  - Navigasi folder bertingkat dengan penataan otomatis *On-Demand Expansion*.
  - Menghemat memori RAM hingga 70% pada drive dengan ratusan ribu item.
  - Menampilkan ukuran logis (*logical size*), persentase visual bar, jumlah file, dan tanggal modifikasi.
- **🗺 Peta Visualisasi Ruang (Treemap)**:
  - Tata letak *Squarified Treemap* yang proporsional terhadap ukuran data.
  - Fitur **Breadcrumb Drill-Down (Zoom-In)**: Klik 2x pada folder treemap untuk masuk dan melihat rincian isi folder tersebut.
  - Pengelompokan cerdas node-node kecil ke kategori *"Other"*.
- **📊 Analisis File Terbesar & Jenis File**:
  - Tabel peringkat file terbesar (Top 100) dilengkapi *Quick Filter Pills* (`> 1 GB`, `> 100 MB`, `Lama > 1 Thn`, `Video & Arsip`).
  - Grafik distribusi tipe ekstensi file (*file types breakdown*).
- **🛡 Aksi File yang Aman**:
  - Buka file, tampilkan di Windows Explorer, dan salin path lengkap.
  - **Penghapusan HANYA ke Recycle Bin** (tidak ada *permanent delete*).
  - Proteksi lokasi sistem terlindungi: Drive root (`C:\`), Root scan aktif, folder `Windows`, `Program Files`, `ProgramData`, dan root profil pengguna dilarang untuk dihapus.
- **💾 Snapshot Lokal & Ekspor**:
  - Simpan dan muat hasil pemindaian ke file snapshot `.ruangdisk` (JSON terkompresi GZip dengan `schemaVersion`).
  - Ekspor seluruh tabel hierarki ke format CSV standar RFC 4180.

---

## 🛠 Teknologi & Arsitektur

- **Bahasa & Platform**: C# 13, .NET 9 LTS, WPF (Windows Presentation Foundation)
- **Arsitektur**: Clean Layered Architecture & MVVM Pattern
- **Libraries**:
  - `CommunityToolkit.Mvvm`
  - `Microsoft.Extensions.DependencyInjection`
  - `Microsoft.Extensions.Logging`
  - `xUnit` untuk pengujian otomatis

### Struktur Proyek:
```text
RuangDisk.sln
├── src/
│   ├── RuangDisk.Core/            # Domain models, contracts, size calculations
│   ├── RuangDisk.Infrastructure/  # Win32 Native scanning, Recycle Bin shell, GZip snapshot, CSV export
│   └── RuangDisk.App/             # WPF Shell, ViewModels, Treemap engine, Fluent dark theme
└── tests/
    ├── RuangDisk.Core.Tests/
    └── RuangDisk.Infrastructure.Tests/
```

---

## 🚀 Cara Menjalankan & Build

### Prasyarat:
- Windows 10 atau Windows 11 (64-bit)
- .NET 9.0 SDK atau lebih baru

### 1. Menjalankan Mode Development:
```powershell
git clone https://github.com/lilditf/RuangDisk.git
cd RuangDisk
dotnet run --project src/RuangDisk.App/RuangDisk.App.csproj
```

### 2. Menjalankan Seluruh Unit & Integration Tests:
```powershell
dotnet test RuangDisk.sln -c Release
```

### 3. Build Executable Mandiri (Standalone Single-File):
```powershell
dotnet publish src/RuangDisk.App/RuangDisk.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Hasil binary `.exe` siap pakai akan berada di folder `src/RuangDisk.App/bin/Release/net9.0-windows/win-x64/publish/RuangDisk.App.exe`.

---

## 🔒 Privasi & Keamanan

- **100% Offline**: RuangDisk tidak memiliki telemetri, backend, analitik, ataupun koneksi keluar jaringan.
- **Tidak Meminta Hak Administrator**: Berjalan pada izin user standar tanpa elevasi UAC berisiko.
- **Privasi Log**: Tidak pernah mencatat path atau nama file pribadi pengguna dalam log bawaan.

---

## 📄 Lisensi
Distributed under the MIT License.
