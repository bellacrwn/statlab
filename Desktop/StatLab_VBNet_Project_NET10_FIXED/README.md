# Statistical Tests Web App

A responsive Statistics project for:

1. Sign Test
2. Wilcoxon Signed-Rank Test
3. Kruskal-Wallis H Test

## Technology

- Frontend: HTML, CSS, JavaScript
- Backend: VB.NET
- Runtime: .NET 10
- Connection: JavaScript `fetch()` calls the VB.NET REST-style API

## Requirements

Install the .NET 10 SDK and VS Code.

The project targets `net10.0`.

Check installation:

```bash
dotnet --version
```

## Run

Open this folder in VS Code, then open the terminal:

```bash
dotnet run
```

The server will start at:

`http://localhost:5000`

Open that address in your browser. Do not use the `file://` URL if you can avoid it; the backend serves the frontend at the same address.

If you accidentally open `wwwroot/index.html` directly, the JavaScript now falls back to `http://localhost:5000` for API calls.

## How the connection works

The browser sends JSON data to:

`POST /api/analyze`

The VB.NET backend calculates the requested test and returns JSON.

The JavaScript frontend displays the returned result.

## Important statistical note

The Wilcoxon Signed-Rank implementation uses a normal approximation with continuity correction for its p-value. The Sign Test uses an exact two-sided binomial p-value. Kruskal-Wallis uses a chi-square approximation with `k - 1` degrees of freedom.

This makes the application suitable for a student demonstration/project. For very small samples or production statistical work, compare results with a trusted statistics package.
