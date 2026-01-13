# ⚡ ARES Network Sentinel

> **“Monitor the grid. Control the flow. Defend the system.”**

ARES Network Sentinel is a **high-fidelity Windows network monitoring dashboard** inspired by **TRON / Cyberpunk aesthetics**.  
Built in **C# (WPF) on .NET 8**, ARES transforms standard Windows networking utilities into a **visual command center** for real-time situational awareness and active defense.

<img width="1046" height="660" alt="ARES Win v1 Screenshot" src="https://github.com/user-attachments/assets/b2fbe4bc-ebe3-42f1-ad12-5556453cce84" />

This is not packet sniffing.  
This is **visibility + control**, executed cleanly on native Windows.

---

[![Download Latest](https://img.shields.io/badge/DOWNLOAD-ARES_v1.2-00eaff?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/amilapcsgit/AresWin/releases/latest/download/AresWin-Setup.zip)

> **A sci-fi themed real-time network monitoring dashboard built with .NET 8 and WPF.**

## 🚀 Quick Deployment
This application is **portable** and does not require installation.

1. **[Click here to download the latest version](https://github.com/amilapcsgit/AresWin/releases/latest/download/AresWin-Setup.zip)**.
2. **Extract** the ZIP file to any folder.
3. Run **`AresWin.exe`**.
   * *Note: Run as Administrator to enable Firewall Blocking and Process Termination features.*

---

## 🟦 What is ARES?

ARES wraps **native Windows networking utilities** (`netstat`, `netsh`) and enriches their output with **process intelligence, geolocation, and heuristic risk analysis**, presented through a GPU-accelerated WPF interface.

Think of it as:
- A **live network map**
- A **process-aware connection inspector**
- A **manual response console** for suspicious activity

All without kernel drivers, services, or invasive hooks.

---

## 🔍 Core Features

### 🔁 Real-Time Network Monitoring
- Parses live `netstat` output
- Displays **active TCP connections**
- Maps connections to **local Process IDs (PID)**
- Continuous refresh without blocking the UI

### 🌍 Geo-Intelligence Layer
- Enriches remote IPs using external API
- Displays:
  - Country
  - City
  - ISP / ASN (when available)
- Designed for **quick human triage**, not forensic overload

### ⚠️ Threat Heuristics
- Flags **high-risk or suspicious ports** (example):
  - `4444` (common backdoor)
  - `3389` (RDP exposure)
- Simple, explainable logic  
  *(No black-box “AI says bad” nonsense)*

### 🛡 Active Defense Controls
From the UI you can:
- **Terminate the owning process**
- **Block the remote IP** via Windows Firewall  
  (`netsh advfirewall` rules)

> ⚠️ **Admin rights required** for process termination and firewall rule creation.

### 🎨 TRON / Cyberpunk Visual Engine
- Custom WPF styles and neon color palette
- “Matrix-style” digital rain animation
- Hardware-accelerated rendering
- Designed to stay responsive under load

---

## 🧱 Tech Stack

| Component | Description |
|---------|-------------|
| UI | Windows Presentation Foundation (WPF) |
| Runtime | .NET 8.0 |
| Networking | `netstat`, `netsh` (native Windows) |
| Data | `System.Text.Json` |
| API | `HttpClient` |
| Platform | Windows 10 / 11 |

---

## ⚙️ Build & Run

### ✅ Requirements
- Windows 10 / 11
- **.NET 8 SDK**
- **Visual Studio 2022** (recommended) with:
  - **.NET desktop development** workload
  - Optional: Git for Windows (for cloning)

> For full functionality (Terminate + Firewall block), launch the app **as Administrator**.

---

### 🟦 Build using Visual Studio 2022

1. **Clone** the repository:
   ```bash
   git clone https://github.com/amilapcsgit/AresWin.git
   ```
2. Open **Visual Studio 2022**
3. Go to: **File → Open → Project/Solution**
4. Select the solution file (`.sln`) in the repo root (or open the project if no `.sln` is present).
5. Confirm:
   - **Configuration**: `Debug` (or `Release`)
   - **Platform**: `Any CPU` (or the project’s default)
6. Build:
   - **Build → Build Solution** (or press `Ctrl+Shift+B`)
7. Run:
   - Press `F5` (Start Debugging)  
   - Or `Ctrl+F5` (Start Without Debugging)

**Run as Administrator (recommended):**
- Visual Studio: **Project → Properties → Debug**  
  Enable **Run as administrator** (if available), or:
- Right-click the built `.exe` → **Run as administrator**

---

### 🟦 Build using .NET CLI

```bash
git clone https://github.com/amilapcsgit/AresWin.git
cd AresWin
dotnet build -c Release
dotnet run
```

---

## ⚠️ Notes / Limitations

- This tool observes **connection-level data** (what Windows reports), not raw packets.
- Geo-IP enrichment depends on the configured API and its availability.
- Blocking uses Windows Firewall rules (`netsh advfirewall`) and requires elevation.

---

## 📜 License (Custom / Non-Commercial)

**Free to use** for personal or internal/business use, under the conditions below.

### ✅ You MAY:
- Use, modify, and run this software for free.
- Use it internally within your organization.

### ❌ You may NOT:
- Sell this software or any modified version of it.
- Redistribute this software (original or modified) in source or binary form.

### 🏷 Attribution Required:
Any use of this software **must credit** the author prominently (in documentation, about page, or repository README):

**Author: L.J. Amila Prasad Perera**

### 📌 Summary
If you want redistribution rights or commercial licensing, contact the author.

---

## 🟦 Final Note

ARES is a **visual interface to the Windows network stack**, built for people who prefer **understanding systems over trusting abstractions**.

Welcome to the Grid.
