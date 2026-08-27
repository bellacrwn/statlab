Imports System.Net
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.IO
Imports System.Threading
Imports StatLab.Core
Imports StatLab.Models

Module Program

    Private ReadOnly JsonOptions As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .WriteIndented = False,
        .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    }

    Private ReadOnly Logger As New ConsoleLogger()

    Sub Main()
        Dim port = Environment.GetEnvironmentVariable("PORT")
        If String.IsNullOrWhiteSpace(port) Then port = Environment.GetEnvironmentVariable("STATLAB_PORT")
        If String.IsNullOrWhiteSpace(port) Then port = "5000"

        Dim prefix = $"http://localhost:{port}/"
        Dim listener As New HttpListener()
        Try
            listener.Prefixes.Add(prefix)
            listener.Start()
        Catch ex As Exception
            Logger.Error($"Failed to start on {prefix}: {ex.Message}")
            Logger.Info("Trying fallback http://localhost:5000/")
            listener = New HttpListener()
            listener.Prefixes.Add("http://localhost:5000/")
            listener.Start()
            prefix = "http://localhost:5000/"
        End Try

        Console.WriteLine()
        Console.WriteLine("  ███████╗████████╗ █████╗ ████████╗██╗      █████╗ ██████╗ ")
        Console.WriteLine("  ██╔════╝╚══██╔══╝██╔══██╗╚══██╔══╝██║     ██╔══██╗██╔══██╗")
        Console.WriteLine("  ███████╗   ██║   ███████║   ██║   ██║     ███████║██████╔╝")
        Console.WriteLine("  ╚════██║   ██║   ██╔══██║   ██║   ██║     ██╔══██║██╔══██╗")
        Console.WriteLine("  ███████║   ██║   ██║  ██║   ██║   ███████╗██║  ██║██████╔╝")
        Console.WriteLine("  ╚══════╝   ╚═╝   ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═╝╚═════╝ ")
        Console.WriteLine()
        Console.WriteLine($"  Version 2.0.0 — Nonparametric Test Laboratory")
        Console.WriteLine($"  Server running at {prefix}")
        Console.WriteLine($"  API: {prefix}api/analyze")
        Console.WriteLine($"  Health: {prefix}api/health")
        Console.WriteLine($"  Press Ctrl+C to stop")
        Console.WriteLine("  ------------------------------------------------")

        Dim cts As New CancellationTokenSource()
        AddHandler Console.CancelKeyPress,
            Sub(sender, e)
                e.Cancel = True
                cts.Cancel()
                Logger.Info("Shutting down...")
                Try
                    listener.Stop()
                Catch
                End Try
            End Sub

        While listener.IsListening AndAlso Not cts.IsCancellationRequested
            Try
                Dim context As HttpListenerContext = listener.GetContext()
                ThreadPool.QueueUserWorkItem(Sub(state) HandleRequest(DirectCast(state, HttpListenerContext)), context)
            Catch ex As HttpListenerException
                Exit While
            Catch ex As Exception
                Logger.Error("Server loop error: " & ex.Message)
            End Try
        End While

        listener.Close()
        Logger.Info("Server stopped.")
    End Sub

    Private Sub HandleRequest(context As HttpListenerContext)
        Dim requestId = Guid.NewGuid().ToString("N").Substring(0, 8)
        Dim sw = System.Diagnostics.Stopwatch.StartNew()
        Try
            Dim request = context.Request
            Dim response = context.Response

            ' Security & CORS headers
            response.Headers.Add("Access-Control-Allow-Origin", "*")
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization")
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
            response.Headers.Add("X-Content-Type-Options", "nosniff")
            response.Headers.Add("X-Frame-Options", "DENY")
            response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin")
            response.Headers.Add("X-Request-Id", requestId)

            If request.HttpMethod = "OPTIONS" Then
                response.StatusCode = 204
                response.Close()
                Return
            End If

            Dim path = request.Url.AbsolutePath.ToLowerInvariant()

            Select Case path
                Case "/api/analyze"
                    If request.HttpMethod <> "POST" Then
                        WriteJson(response, New With {.error = "Method not allowed. Use POST."}, 405)
                        Return
                    End If
                    HandleAnalyze(request, response)

                Case "/api/health"
                    HandleHealth(response)

                Case "/api/tests"
                    HandleTestsList(response)

                Case "/api/version"
                    WriteJson(response, New With {.name = "StatLab", .version = "2.0.0", .tests = New String() {"sign", "signed-rank", "kruskal-wallis", "mann-whitney", "friedman"}}, 200)

                Case Else
                    ServeStaticFile(request, response)
            End Select

        Catch ex As Exception
            Logger.Error($"[{requestId}] Unhandled error: {ex.Message}")
            Try
                WriteJson(context.Response, New With {.error = ex.Message, .requestId = requestId}, 500)
            Catch
            End Try
        Finally
            sw.Stop()
            If context.Request IsNot Nothing Then
                Logger.Info($"[{requestId}] {context.Request.HttpMethod} {context.Request.Url.AbsolutePath} -> {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms")
            End If
        End Try
    End Sub

    Private Sub HandleHealth(response As HttpListenerResponse)
        Dim health = New With {
            .status = "ok",
            .message = "VB.NET StatLab backend is running.",
            .timestamp = DateTime.UtcNow.ToString("o"),
            .version = "2.0.0",
            .uptime = Environment.TickCount64,
            .tests = New Object() {
                New With {.id = "sign", .name = "Sign Test", .type = "one-sample"},
                New With {.id = "signed-rank", .name = "Wilcoxon Signed-Rank", .type = "one-sample/paired"},
                New With {.id = "kruskal-wallis", .name = "Kruskal-Wallis H", .type = "k-independent"},
                New With {.id = "mann-whitney", .name = "Mann-Whitney U", .type = "2-independent"},
                New With {.id = "friedman", .name = "Friedman Test", .type = "k-related"}
            }
        }
        WriteJson(response, health, 200)
    End Sub

    Private Sub HandleTestsList(response As HttpListenerResponse)
        Dim tests = New Object() {
            New With {
                .id = "sign",
                .name = "Sign Test",
                .description = "Tests whether population median differs from hypothesized value using only direction of differences.",
                .assumptions = New String() {"Independent observations", "Ordinal or continuous", "Continuous distribution"},
                .formula = "p = 2 * Σ_{i=0}^{k} C(n,i) 0.5^n",
                .useCase = "One-sample median test when symmetry cannot be assumed"
            },
            New With {
                .id = "signed-rank",
                .name = "Wilcoxon Signed-Rank Test",
                .description = "Uses ranks of absolute differences; more powerful than sign test when symmetry holds.",
                .assumptions = New String() {"Symmetric differences", "Independent", "Ordinal"},
                .formula = "W = min(W+, W-), z = (W - μ + 0.5)/σ",
                .useCase = "Paired samples or one-sample median with symmetric distribution"
            },
            New With {
                .id = "kruskal-wallis",
                .name = "Kruskal-Wallis H Test",
                .description = "Nonparametric ANOVA for 3+ independent groups using ranks.",
                .assumptions = New String() {"Independent groups", "Ordinal/continuous", "Similar shapes"},
                .formula = "H = 12/(N(N+1)) Σ R_i²/n_i - 3(N+1)",
                .useCase = "Compare 3+ independent groups"
            },
            New With {
                .id = "mann-whitney",
                .name = "Mann-Whitney U Test",
                .description = "Compare two independent groups; equivalent to Wilcoxon rank-sum.",
                .assumptions = New String() {"Independent groups", "Ordinal", "Independence within groups"},
                .formula = "U = n1*n2 + n1(n1+1)/2 - R1",
                .useCase = "Two independent groups alternative to t-test"
            },
            New With {
                .id = "friedman",
                .name = "Friedman Test",
                .description = "Nonparametric repeated measures ANOVA for k related groups.",
                .assumptions = New String() {"Related samples", "Blocks independent", "Ordinal"},
                .formula = "χ²_F = 12/(nk(k+1)) Σ R_j² - 3n(k+1)",
                .useCase = "Repeated measures / randomized block design"
            }
        }
        WriteJson(response, New With {.tests = tests}, 200)
    End Sub

    Private Sub HandleAnalyze(request As HttpListenerRequest, response As HttpListenerResponse)
        Dim body As String
        Using reader As New StreamReader(request.InputStream, request.ContentEncoding)
            body = reader.ReadToEnd()
        End Using

        If String.IsNullOrWhiteSpace(body) Then
            WriteJson(response, New With {.error = "Request body is empty."}, 400)
            Return
        End If

        Dim input As AnalysisRequest
        Try
            input = JsonSerializer.Deserialize(Of AnalysisRequest)(body, JsonOptions)
        Catch ex As JsonException
            WriteJson(response, New With {.error = "Invalid JSON: " & ex.Message}, 400)
            Return
        End Try

        If input Is Nothing Then
            WriteJson(response, New With {.error = "Invalid request."}, 400)
            Return
        End If

        Dim validation = input.Validate()
        If validation.Count > 0 Then
            WriteJson(response, New With {.error = String.Join(" ", validation)}, 400)
            Return
        End If

        Dim result As Object
        Try
            Select Case input.Test.Trim().ToLowerInvariant()
                Case "sign"
                    result = Statistics.SignTest(input.Values, input.Median, input.Alpha, input.Alternative)

                Case "signed-rank", "wilcoxon", "sign-rank", "wilcoxon-signed-rank"
                    result = Statistics.WilcoxonSignedRank(input.Values, input.Median, input.Alpha, input.Alternative)

                Case "kruskal-wallis", "kruskal"
                    result = Statistics.KruskalWallis(input.Groups, input.Alpha)

                Case "mann-whitney", "mannwhitney", "u-test", "wilcoxon-rank-sum", "rank-sum"
                    result = Statistics.MannWhitneyUTest(input.Groups, input.Alpha, input.Alternative)

                Case "friedman"
                    result = Statistics.FriedmanTest(input.Groups, input.Alpha)

                Case Else
                    WriteJson(response, New With {.error = $"Unknown test '{input.Test}'. Available: sign, signed-rank, kruskal-wallis, mann-whitney, friedman"}, 400)
                    Return
            End Select
        Catch ex As ArgumentException
            WriteJson(response, New With {.error = ex.Message}, 400)
            Return
        Catch ex As Exception
            Logger.Error($"Analysis failed: {ex.Message}")
            WriteJson(response, New With {.error = "Analysis failed: " & ex.Message}, 500)
            Return
        End Try

        WriteJson(response, result, 200)
    End Sub

    Private Sub ServeStaticFile(request As HttpListenerRequest, response As HttpListenerResponse)
        Dim relativePath = request.Url.AbsolutePath.TrimStart("/"c)

        If String.IsNullOrWhiteSpace(relativePath) Then
            relativePath = "index.html"
        End If

        ' Prevent query string injection
        Dim qIdx = relativePath.IndexOf("?"c)
        If qIdx >= 0 Then relativePath = relativePath.Substring(0, qIdx)

        relativePath = relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())

        Dim outputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
        Dim projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"))
        Dim basePath As String = Nothing

        If Directory.Exists(outputRoot) AndAlso File.Exists(Path.Combine(outputRoot, "index.html")) Then
            basePath = outputRoot
        ElseIf Directory.Exists(projectRoot) Then
            basePath = projectRoot
        Else
            ' Fallback: try parent directory
            Dim alt = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "wwwroot"))
            If Directory.Exists(alt) Then basePath = alt
        End If

        If basePath Is Nothing Then
            WriteText(response, "<h1>404 - wwwroot not found</h1><p>Ensure wwwroot folder exists.</p>", "text/html; charset=utf-8", 404)
            Return
        End If

        Dim fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath))
        Dim basePrefix = If(basePath.EndsWith(Path.DirectorySeparatorChar), basePath, basePath & Path.DirectorySeparatorChar)

        If Not (fullPath.Equals(basePath, StringComparison.OrdinalIgnoreCase) OrElse fullPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)) Then
            WriteJson(response, New With {.error = "Forbidden"}, 403)
            Return
        End If

        ' Directory traversal: serve index.html for SPA fallback if not found but path has no extension?
        If Not File.Exists(fullPath) Then
            ' Try index.html for SPA routing
            If Not Path.HasExtension(fullPath) Then
                fullPath = Path.Combine(basePath, "index.html")
            End If
            If Not File.Exists(fullPath) Then
                WriteText(response, "<h1>404 - Page not found</h1>", "text/html; charset=utf-8", 404)
                Return
            End If
        End If

        Try
            Dim bytes = File.ReadAllBytes(fullPath)
            response.StatusCode = 200
            response.ContentType = GetContentType(fullPath)
            response.Headers.Add("Cache-Control", If(fullPath.EndsWith(".html"), "no-cache", "public, max-age=3600"))
            response.ContentLength64 = bytes.Length
            response.OutputStream.Write(bytes, 0, bytes.Length)
            response.OutputStream.Close()
        Catch ex As Exception
            WriteText(response, $"Error serving file: {ex.Message}", "text/plain", 500)
        End Try
    End Sub

    Private Function GetContentType(path As String) As String
        Select Case IO.Path.GetExtension(path).ToLowerInvariant()
            Case ".html" : Return "text/html; charset=utf-8"
            Case ".css" : Return "text/css; charset=utf-8"
            Case ".js" : Return "application/javascript; charset=utf-8"
            Case ".json" : Return "application/json; charset=utf-8"
            Case ".svg" : Return "image/svg+xml"
            Case ".png" : Return "image/png"
            Case ".jpg", ".jpeg" : Return "image/jpeg"
            Case ".ico" : Return "image/x-icon"
            Case ".woff" : Return "font/woff"
            Case ".woff2" : Return "font/woff2"
            Case Else : Return "application/octet-stream"
        End Select
    End Function

    Private Sub WriteJson(response As HttpListenerResponse, value As Object, statusCode As Integer)
        Dim json = JsonSerializer.Serialize(value, JsonOptions)
        Dim bytes = Encoding.UTF8.GetBytes(json)
        response.StatusCode = statusCode
        response.ContentType = "application/json; charset=utf-8"
        response.ContentLength64 = bytes.Length
        response.OutputStream.Write(bytes, 0, bytes.Length)
        response.OutputStream.Close()
    End Sub

    Private Sub WriteText(response As HttpListenerResponse, text As String, contentType As String, statusCode As Integer)
        Dim bytes = Encoding.UTF8.GetBytes(text)
        response.StatusCode = statusCode
        response.ContentType = contentType
        response.ContentLength64 = bytes.Length
        response.OutputStream.Write(bytes, 0, bytes.Length)
        response.OutputStream.Close()
    End Sub

    Private Class ConsoleLogger
        Public Sub Info(msg As String)
            Console.ForegroundColor = ConsoleColor.DarkGray
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}")
            Console.ResetColor()
        End Sub
        Public Sub [Error](msg As String)
            Console.ForegroundColor = ConsoleColor.Red
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {msg}")
            Console.ResetColor()
        End Sub
    End Class

End Module
