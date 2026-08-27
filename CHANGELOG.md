# Changelog

## 2.0.0 - 2026-08-27

### Major Overhaul
- **Backend:**
  - Split monolith Program.vb into Program.vb + Statistics.vb + Models.vb
  - Added Mann-Whitney U and Friedman tests (now 5 tests total)
  - Added effect sizes: r, η², Kendall's W, rank-biserial
  - Added descriptive statistics (mean, median, SD, Q1/Q3)
  - Improved tie corrections, continuity correction, exact binomial
  - Added alternative hypotheses (greater/less/two-sided)
  - Added security headers, request ID logging, graceful shutdown
  - Configurable PORT env, /api/tests and /api/version endpoints
  - Validation and better error messages

- **Frontend:**
  - Complete UI redesign with design system, dark/light themes
  - Responsive, accessible, modern typography (Geist + Instrument Serif)
  - Drag & drop CSV/TXT, file upload, Excel paste
  - Live validation, value counts, warnings for small n
  - Canvas charts: histogram, rank bars, sparkline
  - Export JSON/CSV, copy, share, history (localStorage)
  - Assumptions, formulas, method explanations
  - Info section with guidance on test selection

- **DevOps:**
  - Dockerfile, docker-compose.yml
  - .gitignore, LICENSE MIT
  - GitHub Actions CI
  - run.sh and improved Run-StatLab.bat
  - Targets .NET 8 LTS (was .NET 10 future)

## 1.0.0 - 2026-08-26
- Initial release with Sign, Wilcoxon Signed-Rank, Kruskal-Wallis
- Basic HttpListener server + vanilla HTML/CSS/JS
