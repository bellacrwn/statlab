# StatLab 2.0 — Nonparametric Test Laboratory

> A production-grade, polished statistics workbench for nonparametric inference — now with 5 tests, CSV drag-drop, visualizations, effect sizes, and a hardened VB.NET backend.

![Version](https://img.shields.io/badge/version-2.0.0-accent)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![VB.NET](https://img.shields.io/badge/language-VB.NET-blue)
![License](https://img.shields.io/badge/license-MIT-green)

---

## ✨ What's New in 2.0

**From 3 → 5 tests:**
- **Sign Test** — exact binomial, one-sample median
- **Wilcoxon Signed-Rank** — tie-corrected, continuity correction, effect r
- **Kruskal-Wallis H** — χ² approx, tie correction, η² effect
- **Mann-Whitney U** (NEW) — two independent groups, rank-biserial r
- **Friedman Test** (NEW) — k related / repeated measures, Kendall's W

**Frontend overhaul:**
- Dark / light theme, fully responsive, accessible
- Drag & drop CSV/TXT, Excel paste, live validation
- Canvas charts: histogram, sparkline, rank bars
- Export JSON / CSV, copy, share API, history (localStorage)
- Descriptive stats (mean, median, SD, Q1/Q3, min/max)
- Assumptions, formulas, decision guidance

**Backend hardening:**
- Modular architecture: `Program.vb` (server) + `Statistics.vb` (engine) + `Models.vb` (DTOs)
- Security headers, CORS, request ID logging, graceful shutdown
- Configurable port via `PORT` env
- Validation, exact & approximated p-values, effect sizes
- Endpoints: `/api/analyze`, `/api/health`, `/api/tests`, `/api/version`
- Docker & docker-compose ready

---

## 🧪 Statistical Details

### Sign Test
Exact two-sided binomial: `p = 2 * Σ_{i=0}^{k} C(n,i) 0.5^n` where `k = min(positive, negative)`.  
Handles ties by exclusion, reports positive/negative/ties, effect = |pos-neg|/n.

### Wilcoxon Signed-Rank
- Ranks absolute differences with tie averaging
- Tie correction: `σ² = n(n+1)(2n+1)/24 - Σ t(t+1)(2t+1)/48`
- Continuity correction, normal approx (exact table recommended for n≤25)
- Effect `r = |z| / √n` (0.1 small, 0.3 medium, 0.5 large)

### Kruskal-Wallis H
`H = 12/(N(N+1)) Σ R_i²/n_i - 3(N+1)` with tie correction `C = 1 - Σ(t³-t)/(N³-N)`  
`H/C ~ χ²(k-1)`. Effect `η² = (H - k + 1)/(N - k)` (0.01 small, 0.06 medium, 0.14 large)

### Mann-Whitney U
`U = n1*n2 + n1(n1+1)/2 - R1`, normal approx with tie correction, effect `r` and rank-biserial `r_rb = 1 - 2U/(n1 n2)`

### Friedman
`χ²_F = 12/(nk(k+1)) Σ R_j² - 3n(k+1)`, Kendall's W = χ²_F / (n(k-1))

> **Note:** For very small samples or production research, cross-check with R / SPSS / SciPy. This app is student-friendly but now includes tie corrections and effect sizes.

---

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK (or .NET 10 if you update TargetFramework)
- Any modern browser

Check:
```bash
dotnet --version
```

### Run (Development)
```bash
# from repo root
dotnet run
# Server at http://localhost:5000
```

Custom port:
```bash
PORT=8080 dotnet run
```

### Docker
```bash
docker build -t statlab:2.0 .
docker run -p 5000:5000 statlab:2.0

# or
docker-compose up --build
```

### Open
Navigate to `http://localhost:5000` — backend serves frontend at same origin.  
No need to open `wwwroot/index.html` via file://, but it now falls back to localhost API if you do.

---

## 🔌 API

### POST /api/analyze
```json
{
  "test": "sign | signed-rank | kruskal-wallis | mann-whitney | friedman",
  "values": [45, 51, 62],
  "groups": [[12,15,13], [19,17,20]],
  "median": 50,
  "alpha": 0.05,
  "alternative": "two-sided | greater | less"
}
```

Response includes:
- `pValue`, `statistic`, `decision`, `effectSize`
- `descriptive`, `groupSummaries`, `conclusion`, `method`, `assumptions`

### GET /api/health
Returns status, version, uptime, available tests.

### GET /api/tests
Returns metadata for all tests (description, formula, assumptions).

### GET /api/version
Simple version endpoint.

---

## 📁 Project Structure
```
StatLab/
├── Program.vb               # HttpListener server, routing, security headers
├── Statistics.vb            # All tests + helpers (exact binomial, normal, χ²)
├── Models.vb                # DTOs + validation
├── StatisticalTests.vbproj  # .NET 8 project
├── wwwroot/
│   ├── index.html           # Modern UI, 5 tabs, drop zone, charts
│   ├── style.css            # Design system, dark/light, responsive
│   └── script.js            # State, CSV parse, charts, history, export
├── Dockerfile
├── docker-compose.yml
└── README.md
```

Legacy folder `Desktop/StatLab_VBNet_Project_NET10_FIXED/` preserved for reference.

---

## 🎨 Frontend Features

- **Data Input:** Paste, CSV drag-drop, file upload, Excel-friendly parsing
- **Validation:** Live counts, min/max, warnings for small n
- **Charts:** Histogram + H₀ line, rank bars, sparkline in hero
- **Results:** Stat grid, decision badge, effect interpretation, descriptive table, group summary
- **Export:** Copy JSON, export CSV, Web Share API
- **History:** Last 20 analyses in localStorage, click to reload
- **Theme:** Dark/light toggle, persisted
- **A11y:** Keyboard nav, focus states, ARIA labels

---

## 🔒 Security & Production

- `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`
- CORS `*` for dev, tighten for prod
- Request ID logging, timing
- Input validation (NaN, Inf, alpha bounds, group sizes)
- Graceful Ctrl+C shutdown

For production, consider:
- Reverse proxy (nginx) with HTTPS
- Rate limiting
- Moving to ASP.NET Core minimal API (Kestrel) for better performance

---

## 🧑‍💻 Development Notes

- Original project targeted `net10.0` (future). This 2.0 targets `net8.0` LTS for compatibility; change to `net10.0` in csproj if SDK available.
- No external NuGet dependencies — pure BCL.
- Frontend has zero npm dependencies — vanilla JS + Canvas.

---

## 📚 References

- Hollander, Wolfe, Chicken — Nonparametric Statistical Methods
- Conover — Practical Nonparametric Statistics
- Numerical Recipes — Gamma functions for χ² survival

---

## 📄 License

MIT — feel free to use for coursework, teaching, or as a starter for a fuller stats platform.

---

**Built with VB.NET + love for clean stats UX.**
