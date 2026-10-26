# CLAUDE.md — ATAS indicators (`atas-indicador-cvd-divergencia`)

## Working style (all sessions)

Confirm the task before building. **Don't assume a direction, write a pile of
code, and burn tokens producing the wrong thing.** When anything is ambiguous or
a detail is unstated, ask one quick clarifying question and get clear alignment
first — then do the work.

- If the task is genuinely unambiguous, just do it — no needless confirmation
  friction.
- If there's a real fork or a missing detail, check in briefly before spending
  effort.
- Verify facts instead of guessing (e.g., check an address/mailbox/branch
  exists rather than assuming its spelling).
- **Under-promise, over-deliver (owner directive, 2026-08-12).** Saying "I'll
  do it" and then not doing it is disobedience, not an accident of
  architecture. Never claim a future behavior unless the mechanism that
  guarantees it is verifiably in place (armed wakeup, cron entry, committed
  hook); otherwise state plainly what is NOT guaranteed. Deliver more than was
  promised, never less.

**Harness-backed persistence rule (owner directive, 2026-08-12).** A promise
to "keep working" is not a schedule: when a turn ends, the session stops
existing until something re-invokes it, and intentions held in context do not
survive that. Therefore: whenever the owner asks for ongoing, overnight,
multi-hour, or run-to-completion work, arm a HARNESS-BACKED loop in that same
turn — /loop (ScheduleWakeup or CronCreate) — and end EVERY subsequent turn by
re-arming the next wakeup until the work is genuinely done. Never accept such
a task with only a verbal commitment; if the loop is not armed, say so instead
of promising. (Background: the 2026-08-11 overnight classifier project died by
sleep despite full advance permissions — the mechanism was missing, not the
authorization.)

## This project

- Custom indicators for the ATAS platform: C#, `net10.0-windows`, Visual Studio 2026.
  Four indicators planned; done: `IndicadorTendenciaHourly`.
- Layout: one branch (`main`), one folder per indicator, all built by `Indicadores.slnx`.
  Shared settings and the ATAS references live in `Directory.Build.props`, so a new
  indicator only needs a minimal `.csproj` plus a line in the `.slnx`.
- Everything user-facing is in **Spanish**: indicator names, settings, panel text,
  `README.md`, `docs/GUIA.md`.
- The code is mirrored to `uniontrading777-sketch/atas-indicador-cvd-divergencia`, where it is
  built on Windows. Updates reach it by pull request (tbuitendyk as collaborator). The
  transfer/build/install steps are in `docs/GUIA.md`; keep it current when they change.

## Verifying ATAS code (the platform DLLs are not available here)

The ATAS DLLs only come from a Windows ATAS install (no NuGet package), so nothing in a
cloud session builds against the real API. Verify in three layers:

1. **API shapes**: check every ATAS call against the official sources,
   `git clone --depth 1 https://github.com/AtasPlatform/Indicators` (`Technical/` holds ~285
   built-in indicators). Copy call shapes from there, never from memory or an AI-written spec.
2. **Compile**: install the .NET 10 SDK (`dotnet-install.sh --channel 10.0`), build stand-in
   DLLs (`ATAS.Indicators`, `ATAS.DataFeedsCore`, `OFT.Rendering`, `Utils.Common`) that declare
   only members verified in step 1, then
   `dotnet build Indicadores.slnx -p:ATAS_BASE=<stub dir> -p:EnableWindowsTargeting=true`.
3. **Logic**: compile the indicator `.cs` into a `net10.0` console app with stand-ins that feed
   candles in and record what `OnRender` draws (stub `System.Windows.Media.Color` so it runs on
   Linux), and assert the panel output.

Say plainly that the first build against the real DLLs happens on Windows.

### ATAS API facts (verified against official sources, 2026-10-26)

- `RenderContext.DrawString` with a `RenderStringFormat` takes a `Rectangle`, not x/y.
  `DrawRectangle` takes a `RenderPen`, not a color. `FillRectangle(Color, Rectangle)` and
  `FillPolygon(Color, Point[])` take `System.Drawing` types.
- `CrossColor` is **not** an ATAS type: it is an alias in the official repo's own
  `GlobalUsings.cs` (`System.Windows.Media.Color` in classic ATAS). Use the WPF color type for
  color settings and convert it for drawing.
- Candle `Time` is **UTC**: add `InstrumentInfo.TimeZoneOffset` for chart time.
- Panel-only indicators must hide the default series:
  `DataSeries[0].IsHidden = true;` and `((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;`.
- `OnCalculate` is called repeatedly for the live bar. `OnCalculate` and `OnRender` run on
  different threads.
- Built DLLs go in `Documents\ATAS\Indicators`. ATAS then shows a blinking reload button on
  its bottom panel.
