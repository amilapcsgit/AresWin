# ⚡ ARES Network Sentinel

> **“Monitor the grid. Control the flow. Defend the system.”**

ARES Network Sentinel is a **high-fidelity Windows network monitoring dashboard** inspired by **TRON / Cyberpunk aesthetics**.  
Built in **C# (WPF) on .NET 8**, ARES transforms standard Windows networking utilities into a **visual command center** for real-time situational awareness and active defense.

This is not packet sniffing.  
This is **visibility + control**, executed cleanly on native Windows.

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
