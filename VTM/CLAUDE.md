# VTM (MOONSUNG-DUALV2)

## Version Log
| Version | Date | Summary of Changes |
|---------|------|-------------------|
| 2.8 | 2026-07-24 | LED half-circle/position-drift fix, crash logger + UI hang watchdog, python guard, vision step Copy/Paste |
| 2.7 | - | (previous baseline) |

## Current Problems
### 🟡 [P1] Vision page hang without model (Event 1002)
- **Status:** Investigating (instrumented, waiting for reproduction)
- **Related Files:** `VisionPage.xaml.cs`, `CameraControl.xaml.cs`, `LCD.cs`, `HangDiag.cs`
- **Description:** On PC `DESKTOP-475TTFB`, entering Vision page without a model loaded froze the UI (Application Hang, Event ID 1002, no stack).
- **Root Cause (if known):** Suspected LCD Tesseract OCR on UI thread every 500ms, or DirectShow camera property sets blocking; timer Dispatcher.Invoke pile-up.
- **What We've Tried:**
  1. Added re-entrancy guards to both vision timers → possibly the fix, pending test
  2. Added UI hang watchdog (`HangDiag`) that writes last checkpoint to `CrashLogs\CrashLog_*.txt` after 8s freeze
  3. Added `videoCapture` null/IsOpened guard in `SetParammeter(CameraSetting)`
- **Next Steps:** Reproduce on the test PC with the new build; read `[HANG]` entry in CrashLogs to identify the frozen checkpoint.
- **Added:** 2026-07-24

## Gotchas
- PropertyChanged.Fody: an interrupted/parallel build can leave an un-woven `HVT.VTM.Program.dll` in obj → runtime crash "Could not load assembly 'PropertyChanged'". Fix: Rebuild Solution.
- Audio AI (`AudioTester`) requires Python 3.13 at `%LocalAppData%\Programs\Python\Python313`; if absent, feature is silently disabled (no dialog) and `Predict` returns false.
- Crash logs: `CrashLogs\CrashLog_yyyyMMdd.txt` next to `VTM.exe` (unhandled exceptions + UI hang watchdog entries).
- Vision step Copy/Paste copies LED/FND-segment/LCD-ROI only; GLED is not stored per step.
