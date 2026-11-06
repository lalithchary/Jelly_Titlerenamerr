<div align="center">

<img src="Logo/jtitle.png" alt="Jelly_Titlerenamerr Logo" width="160" style="border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.5);" />

# Jelly_Titlerenamerr

**The Smart Jellyfin Title & Metadata Cleaner**

[![GitHub Pages](https://img.shields.io/badge/Website-GitHub%20Pages-00a4dc?style=flat&logo=github)](https://lalithchary.github.io/Jelly_Titlerenamerr/)
[![Jellyfin](https://img.shields.io/badge/Jellyfin-12.0%2B%20%7C%2012.40-00a4dc?style=flat&logo=jellyfin)](https://jellyfin.org/)
[![Author](https://img.shields.io/badge/Author-lalithchary-4caf50?style=flat)](https://github.com/lalithchary)
[![Release](https://img.shields.io/badge/Release-v1.3.1.0-blue?style=flat)](https://github.com/lalithchary/Jelly_Titlerenamerr/releases)

---

</div>

**Jelly_Titlerenamerr** is a Jellyfin server plugin created by **lalithchary** that automatically cleans movie and TV episode titles by stripping piracy website domain prefixes (e.g. `www.5MovieRulz.company`, `1TamilMV.cz`, `TamilBlasters.vip`, `CineVood`), scene tags, audio specs, and rip clutter.

Crucially, **Jelly_Titlerenamerr** extracts the release year from the filename and sets it directly into Jellyfin's **`ProductionYear` metadata field** while leaving the movie title completely clean (e.g. `"Lalithchary"` instead of `"Lalithchary (2026)"`).

---

## 🌐 Official Website & Repository

- **Website / Documentation:** [https://lalithchary.github.io/Jelly_Titlerenamerr/](https://lalithchary.github.io/Jelly_Titlerenamerr/)
- **GitHub Repository:** [https://github.com/lalithchary/Jelly_Titlerenamerr.git](https://github.com/lalithchary/Jelly_Titlerenamerr.git)
- **Plugin Repository Manifest URL:** `https://lalithchary.github.io/Jelly_Titlerenamerr/manifest.json`

---

## 🚀 Installation

### Method 1: Jellyfin Plugin Repository (Recommended)

1. Open your Jellyfin Web Interface and navigate to:
   **Dashboard → Plugins → Repositories** tab.
2. Click the **+** (Add) button.
3. Fill in:
   - **Repository Name:** `Jelly_Titlerenamerr`
   - **Repository URL:**
     ```
     https://lalithchary.github.io/Jelly_Titlerenamerr/manifest.json
     ```
4. Click **Save**.
5. Switch to the **Catalog** tab.
6. Look for **Jelly_Titlerenamerr** under the *Metadata* category.
7. Click **Install** and restart your Jellyfin server.

---

### Method 2: Manual Installation (.dll)

1. Download [`Jelly_Titlerenamerr.dll`](https://github.com/lalithchary/Jelly_Titlerenamerr/releases/download/v1.3.1.0/Jelly_Titlerenamerr.dll) (or download the release zip).
2. Place the file inside your Jellyfin plugins directory:
   - **Windows:**
     ```
     %ProgramData%\Jellyfin\Server\plugins\Jelly_Titlerenamerr\Jelly_Titlerenamerr.dll
     ```
   - **Linux / Docker:**
     ```
     /config/plugins/Jelly_Titlerenamerr/Jelly_Titlerenamerr.dll
     ```
3. Restart your Jellyfin server.
4. Navigate to **Dashboard → Plugins → Jelly_Titlerenamerr** to configure settings.

---

## ✨ Features

- **🌐 Complete Piracy Domain Stripper:**
  - Automatically identifies and removes website domains with any modern TLD (`.company`, `.cz`, `.lat`, `.to`, `.vip`, `.top`, `.online`, etc.).
  - Strips prefixes such as `www.5MovieRulz.company - ` or `[1TamilMV.cz] `.
- **📅 Year in Metadata, Clean Title:**
  - Releases like `www.5MovieRulz.company - Lalithchary (2026)...` get parsed into:
    - **Title:** `Lalithchary`
    - **Metadata Year (`ProductionYear`):** `2026`
- **🧪 Interactive 3-Field Live Preview GUI:**
  - Field 1: **Current Metadata (Raw Filename / Title)**
  - Field 2: **What is Required from Current Data (Extracted Metadata / Year)**
  - Field 3: **Output Title (Cleaned Title for Jellyfin - Read-Only)**
- **📂 Library Selection:**
  - Select specifically which libraries to clean (e.g. Telugu Movies, Movies), or clean all libraries.
- **⚡ Triggers & Automation:**
  - Intercept newly added files upon download or library scan.
  - Scheduled Task for 1-click bulk library cleanup.
- **🧹 Technical Specs Stripper:**
  - Removes audio specs `(DD+5.1 - 192kbps & AAC)`, file sizes `[2.1GB]`, multi-language tags `[Hindi + Telugu]`, codecs `HEVC`, `x264`, `x265`, and resolutions `1080p`, `4K`, `HDRip`.
- **✏️ Custom Keywords & Regex:**
  - Add your own custom leak sites and regex patterns directly through the web UI.

---

## 🛠️ Building from Source

```bash
git clone https://github.com/lalithchary/Jelly_Titlerenamerr.git
cd Jelly_Titlerenamerr
dotnet build -c Release
dotnet test
```

Compiled binaries will be created in `Jelly_Titlerenamerr/bin/Release/net10.0/`.

---

## 👤 Author

Developed by **[lalithchary](https://github.com/lalithchary)**.

---

## 📄 License

This project is licensed under the MIT License.
