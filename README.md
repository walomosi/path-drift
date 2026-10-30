# Path Drift

**Stop guessing if your accelerator IPs are still good. Watch the latency curve drift in real time.**

You picked the fastest IP once. Does it stay fast? Path Drift ping/HTTP-monitors your hosts IPs in the background and draws the drift curve — so you can see when an IP degrades, jitters, or times out, and swap to a better one.

No installer, no cloud, no telemetry. One WinForms exe, .NET Framework 4.8.

---

## Why

Ping once and you pick the fastest IP. But:

- a great IP at 9am can be terrible at 9pm
- ICMP-blocked IPs show as "timeout" even though HTTP works
- WARP/VPN adds a flat 50ms that hides the real path
- you have no idea which IP is actually the most stable over a day

Path Drift keeps watching, so you stop guessing.

## Features

| | |
|---|---|
| **Real-time drift curve** | Ping or HTTP-head your IPs on an interval and draw the latency curve — see drift, jitter, and dropouts at a glance. |
| **Read hosts button** | One click extracts all IPs from your system hosts file. |
| **HTTP mode** | For IPs that block ICMP, use HTTP HEAD to measure real response time. |
| **WARP detection** | Warns you if Cloudflare WARP is running — it flattens all curves by 20-100ms. |
| **Alarm threshold** | Set a ms threshold; when any IP exceeds it, the window title turns into an alarm. |
| **Export best IP** | One click copies the lowest-average-latency IP to clipboard. |
| **Export CSV** | Dump all samples for later analysis. |

## Quick start

```bash
# build (uses the in-box C# compiler)
build.cmd

# run
bin\PathDrift.exe
```

Paste your IPs (or click **Read hosts**), set the interval, click Start. Watch the curves. When one drifts, export the best IP and update your accelerator.

## Positioning

Path Drift is a **network path monitoring tool** for your own machine. It measures latency; it does not proxy traffic or modify your hosts.

Not for: attacking hosts or doing anything your ISP would not like.

## License

[MIT](LICENSE).

---

If this helps you keep your accelerator fast, a ⭐ is appreciated.
