Imports System.Net
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.IO
Imports System.Threading
Imports System.Linq

Module Program

    Private ReadOnly JsonOptions As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .WriteIndented = False
    }

    Sub Main()
        Dim listener As New HttpListener()
        listener.Prefixes.Add("http://localhost:5000/")
        listener.Start()

        Console.WriteLine("==============================================")
        Console.WriteLine(" Statistical Tests Web Application")
        Console.WriteLine(" Server: http://localhost:5000/")
        Console.WriteLine(" Press Ctrl+C to stop the server.")
        Console.WriteLine("==============================================")

        While listener.IsListening
            Try
                Dim context As HttpListenerContext = listener.GetContext()
                ThreadPool.QueueUserWorkItem(Sub(state) HandleRequest(DirectCast(state, HttpListenerContext)), context)
            Catch ex As HttpListenerException
                Exit While
            Catch ex As Exception
                Console.WriteLine("Server error: " & ex.Message)
            End Try
        End While

        listener.Close()
    End Sub

    Private Sub HandleRequest(context As HttpListenerContext)
        Try
            Dim request = context.Request
            Dim response = context.Response

            response.Headers.Add("Access-Control-Allow-Origin", "*")
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type")
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS")

            If request.HttpMethod = "OPTIONS" Then
                response.StatusCode = 204
                response.Close()
                Return
            End If

            If request.HttpMethod = "POST" AndAlso request.Url.AbsolutePath.Equals("/api/analyze", StringComparison.OrdinalIgnoreCase) Then
                HandleAnalyze(request, response)
                Return
            End If

            If request.HttpMethod = "GET" AndAlso request.Url.AbsolutePath.Equals("/api/health", StringComparison.OrdinalIgnoreCase) Then
                WriteJson(response, New With {.status = "ok", .message = "VB.NET backend is running."}, 200)
                Return
            End If

            ServeStaticFile(request, response)

        Catch ex As Exception
            Try
                WriteJson(context.Response, New With {.error = ex.Message}, 500)
            Catch
            End Try
        End Try
    End Sub

    Private Sub HandleAnalyze(request As HttpListenerRequest, response As HttpListenerResponse)
        Dim body As String
        Using reader As New StreamReader(request.InputStream, request.ContentEncoding)
            body = reader.ReadToEnd()
        End Using

        Dim input As AnalysisRequest = JsonSerializer.Deserialize(Of AnalysisRequest)(body, JsonOptions)

        If input Is Nothing OrElse String.IsNullOrWhiteSpace(input.Test) Then
            WriteJson(response, New With {.error = "A statistical test must be selected."}, 400)
            Return
        End If

        Dim result As Object

        Select Case input.Test.Trim().ToLowerInvariant()
            Case "sign"
                result = Statistics.SignTest(input.Values, input.Median, input.Alpha)

            Case "signed-rank", "wilcoxon", "sign-rank"
                result = Statistics.WilcoxonSignedRank(input.Values, input.Median, input.Alpha)

            Case "kruskal-wallis", "kruskal"
                result = Statistics.KruskalWallis(input.Groups, input.Alpha)

            Case Else
                WriteJson(response, New With {.error = "Unknown statistical test."}, 400)
                Return
        End Select

        WriteJson(response, result, 200)
    End Sub

    Private Sub ServeStaticFile(request As HttpListenerRequest, response As HttpListenerResponse)
        Dim relativePath = request.Url.AbsolutePath.TrimStart("/"c)

        If String.IsNullOrWhiteSpace(relativePath) Then
            relativePath = "index.html"
        End If

        relativePath = relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())

        ' When running with `dotnet run`, the working directory is the project folder.
        ' When running a built/published executable, wwwroot is copied beside the executable.
        Dim outputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
        Dim projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"))
        Dim basePath = If(Directory.Exists(outputRoot), outputRoot, projectRoot)
        Dim fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath))

        Dim basePrefix = If(basePath.EndsWith(Path.DirectorySeparatorChar), basePath, basePath & Path.DirectorySeparatorChar)
        If Not (fullPath.Equals(basePath, StringComparison.OrdinalIgnoreCase) OrElse fullPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)) OrElse Not File.Exists(fullPath) Then
            WriteText(response, "<h1>404 - Page not found</h1>", "text/html; charset=utf-8", 404)
            Return
        End If

        Dim bytes = File.ReadAllBytes(fullPath)
        response.StatusCode = 200
        response.ContentType = GetContentType(fullPath)
        response.ContentLength64 = bytes.Length
        response.OutputStream.Write(bytes, 0, bytes.Length)
        response.OutputStream.Close()
    End Sub

    Private Function GetContentType(path As String) As String
        Select Case IO.Path.GetExtension(path).ToLowerInvariant()
            Case ".html" : Return "text/html; charset=utf-8"
            Case ".css" : Return "text/css; charset=utf-8"
            Case ".js" : Return "application/javascript; charset=utf-8"
            Case ".json" : Return "application/json; charset=utf-8"
            Case ".svg" : Return "image/svg+xml"
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

End Module

Public Class AnalysisRequest
    Public Property Test As String = ""
    Public Property Values As Double() = Array.Empty(Of Double)()
    Public Property Groups As List(Of Double()) = New List(Of Double())()
    Public Property Median As Double = 0
    Public Property Alpha As Double = 0.05
End Class

Public Module Statistics

    Public Function SignTest(values As Double(), median As Double, alpha As Double) As Object
        If values Is Nothing OrElse values.Length = 0 Then
            Throw New ArgumentException("Enter at least one observation.")
        End If

        Dim positive As Integer = 0
        Dim negative As Integer = 0
        Dim ties As Integer = 0

        For Each x In values
            If x > median Then
                positive += 1
            ElseIf x < median Then
                negative += 1
            Else
                ties += 1
            End If
        Next

        Dim n = positive + negative
        If n = 0 Then
            Throw New ArgumentException("All observations are equal to the hypothesized median.")
        End If

        Dim k = Math.Min(positive, negative)
        Dim pValue = 2.0 * BinomialCdf(k, n, 0.5)
        If pValue > 1.0 Then pValue = 1.0

        Dim decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀")

        Return New With {
            .test = "Sign Test",
            .n = n,
            .positive = positive,
            .negative = negative,
            .ties = ties,
            .statistic = k,
            .pValue = pValue,
            .alpha = alpha,
            .decision = decision,
            .conclusion = If(pValue < alpha,
                "There is statistically significant evidence that the population median differs from the hypothesized median.",
                "There is not enough statistical evidence to conclude that the population median differs from the hypothesized median.")
        }
    End Function

    Public Function WilcoxonSignedRank(values As Double(), median As Double, alpha As Double) As Object
        If values Is Nothing OrElse values.Length = 0 Then
            Throw New ArgumentException("Enter at least one observation.")
        End If

        Dim diffs As New List(Of Double)()

        For Each x In values
            Dim d = x - median
            If Math.Abs(d) > 0.0000000001 Then
                diffs.Add(d)
            End If
        Next

        If diffs.Count = 0 Then
            Throw New ArgumentException("All differences are zero. The signed-rank statistic cannot be calculated.")
        End If

        Dim absRanks = RankWithTies(diffs.Select(Function(d) Math.Abs(d)).ToList())

        Dim wPlus As Double = 0
        Dim wMinus As Double = 0

        For i As Integer = 0 To diffs.Count - 1
            If diffs(i) > 0 Then
                wPlus += absRanks(i)
            Else
                wMinus += absRanks(i)
            End If
        Next

        Dim w = Math.Min(wPlus, wMinus)
        Dim n = diffs.Count
        Dim mean = n * (n + 1) / 4.0

        Dim tieCorrection As Double = 0
        Dim groups = absRanks.GroupBy(Function(r) r).ToList()
        For Each g In groups
            Dim t = g.Count()
            If t > 1 Then
                tieCorrection += t * (t + 1) * (2 * t + 1) / 48.0
            End If
        Next

        Dim variance = n * (n + 1) * (2 * n + 1) / 24.0 - tieCorrection
        If variance <= 0 Then variance = 0.0000001

        Dim continuity = If(w > mean, -0.5, 0.5)
        Dim z = (w - mean + continuity) / Math.Sqrt(variance)
        Dim pValue = 2.0 * NormalCdf(-Math.Abs(z))
        Dim decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀")

        Return New With {
            .test = "Wilcoxon Signed-Rank Test",
            .n = n,
            .zeroDifferences = values.Length - n,
            .wPlus = wPlus,
            .wMinus = wMinus,
            .statistic = w,
            .z = z,
            .pValue = pValue,
            .alpha = alpha,
            .decision = decision,
            .conclusion = If(pValue < alpha,
                "There is statistically significant evidence of a difference between the sample median and the hypothesized median.",
                "There is not enough statistical evidence to conclude that the sample differs from the hypothesized median.")
        }
    End Function

    Public Function KruskalWallis(groups As List(Of Double()), alpha As Double) As Object
        If groups Is Nothing OrElse groups.Count < 2 Then
            Throw New ArgumentException("Enter at least two groups.")
        End If

        Dim observations As New List(Of Observation)()
        For groupIndex As Integer = 0 To groups.Count - 1
            If groups(groupIndex) Is Nothing OrElse groups(groupIndex).Length = 0 Then
                Throw New ArgumentException("Every group must contain at least one observation.")
            End If

            For Each x In groups(groupIndex)
                observations.Add(New Observation With {.Value = x, .Group = groupIndex})
            Next
        Next

        Dim ranks = RankObservations(observations)
        Dim n = observations.Count
        Dim k = groups.Count

        Dim sumTerm As Double = 0
        Dim rankIndex As Integer = 0

        For groupIndex As Integer = 0 To k - 1
            Dim count = groups(groupIndex).Length
            Dim currentGroupIndex As Integer = groupIndex
            Dim rankSum As Double = ranks.Where(Function(r) r.Group = currentGroupIndex).Sum(Function(r) r.Rank)
            sumTerm += (rankSum * rankSum) / count
        Next

        Dim h = (12.0 / (n * (n + 1))) * sumTerm - 3.0 * (n + 1)

        Dim tieCorrection = CalculateKruskalTieCorrection(observations)
        If tieCorrection > 0 AndAlso tieCorrection < 1 Then
            h = h / tieCorrection
        End If

        Dim df = k - 1
        Dim pValue = ChiSquareSurvival(h, df)
        Dim decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀")

        Dim groupSummaries As New List(Of Object)()
        For groupIndex As Integer = 0 To k - 1
            Dim currentGroupIndex As Integer = groupIndex
            Dim rankSum = ranks.Where(Function(r) r.Group = currentGroupIndex).Sum(Function(r) r.Rank)
            groupSummaries.Add(New With {
                .group = groupIndex + 1,
                .n = groups(groupIndex).Length,
                .rankSum = rankSum,
                .mean = groups(groupIndex).Average()
            })
        Next

        Return New With {
            .test = "Kruskal-Wallis H Test",
            .groups = k,
            .n = n,
            .statistic = h,
            .degreesOfFreedom = df,
            .pValue = pValue,
            .alpha = alpha,
            .decision = decision,
            .groupSummaries = groupSummaries,
            .conclusion = If(pValue < alpha,
                "There is statistically significant evidence that at least one group differs from the others.",
                "There is not enough statistical evidence to conclude that the groups differ significantly.")
        }
    End Function

    Private Class Observation
        Public Property Value As Double
        Public Property Group As Integer
        Public Property Rank As Double
    End Class

    Private Function RankObservations(items As List(Of Observation)) As List(Of Observation)
        Dim sorted = items.OrderBy(Function(o) o.Value).ToList()
        Dim i As Integer = 0

        While i < sorted.Count
            Dim j = i
            While j + 1 < sorted.Count AndAlso Math.Abs(sorted(j + 1).Value - sorted(i).Value) < 0.0000000001
                j += 1
            End While

            Dim rank = (i + 1 + j + 1) / 2.0
            For p As Integer = i To j
                sorted(p).Rank = rank
            Next
            i = j + 1
        End While

        Return sorted
    End Function

    Private Function RankWithTies(values As List(Of Double)) As List(Of Double)
        Dim indexed = values.Select(Function(v, indexValue) New With {.Value = v, .Index = indexValue}).OrderBy(Function(x) x.Value).ToList()
        Dim result(values.Count - 1) As Double
        Dim i As Integer = 0

        While i < indexed.Count
            Dim j = i
            While j + 1 < indexed.Count AndAlso Math.Abs(indexed(j + 1).Value - indexed(i).Value) < 0.0000000001
                j += 1
            End While

            Dim rank = (i + 1 + j + 1) / 2.0
            For p As Integer = i To j
                result(indexed(p).Index) = rank
            Next

            i = j + 1
        End While

        Return result.ToList()
    End Function

    Private Function CalculateKruskalTieCorrection(observations As List(Of Observation)) As Double
        Dim n = observations.Count
        Dim tieSum As Double = 0

        For Each g In observations.GroupBy(Function(o) o.Value)
            Dim t = g.Count()
            If t > 1 Then
                tieSum += t * t * t - t
            End If
        Next

        Return 1.0 - tieSum / (n * n * n - n)
    End Function

    Private Function BinomialCdf(k As Integer, n As Integer, p As Double) As Double
        If k < 0 Then Return 0
        If k >= n Then Return 1

        Dim sum As Double = 0
        For i As Integer = 0 To k
            sum += BinomialProbability(n, i, p)
        Next
        Return Math.Min(1, Math.Max(0, sum))
    End Function

    Private Function BinomialProbability(n As Integer, k As Integer, p As Double) As Double
        If k < 0 OrElse k > n Then Return 0
        If p = 0 Then Return If(k = 0, 1, 0)
        If p = 1 Then Return If(k = n, 1, 0)

        Dim logProb = LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1) +
                      k * Math.Log(p) + (n - k) * Math.Log(1 - p)

        Return Math.Exp(logProb)
    End Function

    Private Function NormalCdf(x As Double) As Double
        Return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)))
    End Function

    Private Function Erf(x As Double) As Double
        Dim sign = If(x < 0, -1.0, 1.0)
        x = Math.Abs(x)

        Dim a1 = 0.254829592
        Dim a2 = -0.284496736
        Dim a3 = 1.421413741
        Dim a4 = -1.453152027
        Dim a5 = 1.061405429
        Dim p = 0.3275911

        Dim t = 1.0 / (1.0 + p * x)
        Dim y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x)

        Return sign * y
    End Function

    Private Function ChiSquareSurvival(x As Double, df As Integer) As Double
        If x <= 0 Then Return 1
        Return RegularizedGammaUpper(df / 2.0, x / 2.0)
    End Function

    Private Function RegularizedGammaUpper(a As Double, x As Double) As Double
        If x < 0 OrElse a <= 0 Then Return Double.NaN
        If x = 0 Then Return 1

        If x < a + 1 Then
            Return 1 - RegularizedGammaLowerSeries(a, x)
        Else
            Return GammaContinuedFraction(a, x)
        End If
    End Function

    Private Function RegularizedGammaLowerSeries(a As Double, x As Double) As Double
        Dim sum = 1.0 / a
        Dim term = sum
        Dim ap = a

        For n As Integer = 1 To 1000
            ap += 1
            term *= x / ap
            sum += term
            If Math.Abs(term) < Math.Abs(sum) * 1.0E-14 Then Exit For
        Next

        Return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a))
    End Function

    Private Function GammaContinuedFraction(a As Double, x As Double) As Double
        Dim b = x + 1 - a
        Dim c = 1.0 / 1.0E-300
        Dim d = 1.0 / b
        Dim h = d

        For i As Integer = 1 To 1000
            Dim an = -i * (i - a)
            b += 2
            d = an * d + b
            If Math.Abs(d) < 1.0E-300 Then d = 1.0E-300
            c = b + an / c
            If Math.Abs(c) < 1.0E-300 Then c = 1.0E-300
            d = 1.0 / d
            Dim delta = d * c
            h *= delta

            If Math.Abs(delta - 1) < 1.0E-14 Then Exit For
        Next

        Return Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h
    End Function

    Private Function LogGamma(z As Double) As Double
        Dim coefficients() As Double = {
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716E-6,
            1.5056327351493116E-7
        }

        If z < 0.5 Then
            Return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * z)) - LogGamma(1 - z)
        End If

        z -= 1
        Dim x = 0.99999999999980993
        For i As Integer = 0 To coefficients.Length - 1
            x += coefficients(i) / (z + i + 1)
        Next

        Dim t = z + coefficients.Length - 0.5
        Return 0.5 * Math.Log(2 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x)
    End Function

End Module
