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
  All four specs are done, plus a combined fifth:
  `IndicadorTendenciaHourly`; `IndicadorCvdDivergencia` (one DLL holding the Institutional CVD
  Trigger & Absorption Engine plus the companion "Velas CVD (panel)"); `IndicadorFootprintPredictor`
  (Footprint Predictor Pro); `IndicadorLvn` (Institutional 90-Day Deep LVN Profile Engine); and
  `IndicadorCombinado` (all four modules in one indicator, with one draggable dashboard).
- **Architecture.** The logic lives in engines in `Compartido/`: `MotorTendencia`, `MotorCvd`
  (+ `CalculadoraCvd`), `MotorFootprint` and `MotorLvn`. `Directory.Build.props` compiles
  `Compartido/*.cs` into every project. Each indicator is a thin shell: settings forward to its
  engine, it does the dragging, and it implements `IGrafico` **explicitly** to give engines candles,
  tick size, time zone and chart coordinates. The combined indicator is the same shell with four
  engines. Fix logic in the engine and every DLL gets it. Engines are created lazily (`Motor =>
  _motor ??= ...`) in case ATAS reads settings during the base constructor. There is no abstract
  class between `Indicator` and ours: the official repo has none, so ATAS's handling of one is
  unverified. Public setting enums live in `Compartido/Enumeraciones.cs`.
- All engines use one calculation pattern: when bar N starts, process bars before N once, as
  closed bars, re-reading their final values; the live bar only feeds display values. (The
  footprint spec's `if (bar >= CurrentBar - 1) return;` never evaluated anything in real time.)
  Heavy work waits for the last bar (`bar >= CurrentBar - 1`), so it runs once at the end of the
  history load and once per new bar. `MotorLvn` keeps per-day profiles and counters, and recomputes
  in about 1 ms per bar on 60 days of 1-minute data.
- On-chart panels stack down the left edge at X 25: Tendencia Y 60, CVD HUD Y 230, Footprint Y 410,
  LVN HUD Y 490. The combined dashboard sits at (25, 60) and stacks its sections with 4 px gaps.
- Every panel and the dashboard can be dragged by their title bar through `Compartido/ArrastrePanel.cs`
  (`_arrastre.Ubicar(...)` when drawing, plus the same four mouse overrides in each shell). Floating
  a panel **outside** the chart is not possible through the indicator API. A custom WPF window
  would make ATAS X reject the DLL, so the owner declined it (2026-10-26).
- Owner preferences so far: signals are evaluated on **closed candles** (no repainting). Where a
  spec's prose and its code disagree, ask; the specs are AI-written and their code has never compiled
  as-is.
- Layout: one branch (`main`), one folder per indicator, all built by `Indicadores.slnx`.
  Shared settings and the ATAS references live in `Directory.Build.props`, so a new
  indicator only needs a minimal `.csproj` plus a line in the `.slnx`.
- Everything user-facing is in **Spanish**: indicator names, settings, panel text,
  `README.md`, `docs/GUIA.md`.
- The code is mirrored to `uniontrading777-sketch/atas-indicador-cvd-divergencia`, where it is
  built on Windows. tbuitendyk is a collaborator there (2026-10-26). A Claude Code session cannot
  attach both repos: they share a name, and each is checked out at `<cwd>/<name>`. So a session on
  this repo pushes only here; the mirror is updated with `git pull upstream main` +
  `git push origin main` (`docs/GUIA.md` §5). The transfer/build/install steps are in
  `docs/GUIA.md`; keep it current when they change.

## Verifying ATAS code (the platform DLLs are not available here)

The ATAS DLLs only come from a Windows ATAS install (no NuGet package), so nothing in a
cloud session builds against the real API. Verify in three layers:

1. **API shapes**: check every ATAS call against the official sources,
   `git clone --depth 1 https://github.com/AtasPlatform/Indicators` (`Technical/` holds ~285
   built-in indicators). Copy call shapes from there, never from memory or an AI-written spec.
   `Technical/PublicAPI.Shipped.txt` lists exact signatures, including the platform types they use.
2. **Compile + behaviour, one command**: install the .NET 10 SDK (`dotnet-install.sh --channel 10.0`),
   then run `bash pruebas/verificar.sh`. It builds stand-in ATAS DLLs from
   `pruebas/compilacion/StubsCompilacion.cs`, which declare only members verified in step 1. It
   compiles `Indicadores.slnx` against them (must stay at 0 warnings and 0 errors). Then it runs
   `pruebas/Pruebas.csproj`: the real indicator sources against the behaviour stand-ins in
   `pruebas/StubsAtas.cs`. These feed candles, sessions and per-price Bid/Ask, and record text,
   polygons and lines. 125 checks at last count. When a new ATAS member is used, add it to
   **both** stand-in files, with the shape copied from the official sources, and add tests.
3. `pruebas/` has its own empty `Directory.Build.props/.targets` to stop the root settings
   leaking in. It is not part of `Indicadores.slnx`, so it doesn't affect the Windows build.

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
- One classic build (`net10.0-windows`, `UseWPF`) also runs on ATAS X, **provided the only WPF
  types used are `System.Windows.Media.Color`/`Colors`** (ATAS X converts those). Any other type
  from `WindowsBase`/`PresentationCore`/`PresentationFramework`, a custom WPF editor, or a
  `System.Xaml` reference makes ATAS X skip the whole DLL ("Rejected assembly" in its log).
  Check a build by listing its TypeRefs with `System.Reflection.Metadata`. Checked 2026-10-26:
  all five DLLs use only `System.Windows.Media.Color`.
- Lines: `context.DrawLine(RenderPen, x1, y1, x2, y2)`; a pen with width is `new RenderPen(color, width)`.
  `CurrentBar` is the number of bars (the live bar is `CurrentBar - 1`).
- `OnCalculate` is called repeatedly for the live bar. `OnCalculate` and `OnRender` run on
  different threads. Never accumulate with `+=` per call. Recompute each bar from the previous
  bar's value, and re-read a bar's final values when the next bar starts.
- Session CVD, as ATAS's own `CumulativeDelta.cs` does it: previous bar's CVD + `candle.Delta`,
  reset on `IsNewSession(bar)`. CVD-candle wicks: `+ candle.MaxDelta` / `+ candle.MinDelta`.
- Visible range: `FirstVisibleBarNumber` / `LastVisibleBarNumber` (`...Index` does not exist).
  `ChartInfo.GetXByBar(bar)` is the candle's **left** edge; center with
  `+ (int)((ChartInfo.PriceChartContainer.BarsWidth - w) / 2)`. The candle step is
  `BarSpacing + BarsWidth`; always cast, since their type is unconfirmed. Price to pixel:
  `ChartInfo.GetYByPrice(price, false)`.
- Per-price volume: `candle.GetPriceVolumeInfo(price)` stepping by `InstrumentInfo.TickSize`;
  it returns null for a price with no trades. It exposes `.Volume`, `.Bid` and `.Ask`. The POC is
  `candle.MaxVolumePriceInfo.Price` (null-check it). `ChartInfo.PriceStep` does not exist.
- Mouse: `public override bool ProcessMouseDown/ProcessMouseMove/ProcessMouseUp(OFT.Rendering.Control.RenderControlMouseEventArgs e)`.
  Return `true` when handled, otherwise `base.…(e)`. `public override OFT.Rendering.StdCursor GetCursor(e)`
  (`StdCursor.Hand`). `e.Location`/`e.X`/`e.Y` are in the same coordinates as drawing. The official
  code never reads a mouse button, so neither do we. Call `RedrawChart()` after moving something.
  The price-chart area is `ChartInfo.PriceChartContainer.Region`.
- Dot/marker series: `new ValueDataSeries("Id", "Name") { VisualType = VisualMode.Dots,
  ShowZeroValue = false, Width = n, Color = <WPF color> }`. Without `ShowZeroValue = false`, every
  bar gets a dot at price 0. When recalculating, write 0 to bars that have no signal, so no stale
  dots survive a settings change.
- Candle panel: `CandleDataSeries` + `DataSeries.Add(series)` + `Panel = IndicatorDataProvider.NewPanel`,
  `series[bar] = new Candle { ... }`. `UpCandleColor`/`DownCandleColor` take the WPF color type.
  An indicator draws in one panel only, so price-chart drawing and a separate panel need two
  indicators (they can share one DLL).
- Built DLLs go in `%APPDATA%\ATAS\Indicators` (classic) or `%APPDATA%\ATAS X\Indicators`
  (ATAS X), **not** `Documents\ATAS\Indicators`. Older pages and community repos say Documents,
  but a DLL there is silently ignored: this cost a failed first install. Easiest route: the
  **Add custom indicator** button in the indicators window copies and loads the DLL. After a
  manual copy, press the reload button in the status bar. Custom indicators are listed in the
  **Custom** group. Source: docs.atas.net "Development of a user indicator".
