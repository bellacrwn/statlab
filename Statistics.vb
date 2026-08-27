Imports System.Linq
Imports StatLab.Models

Namespace StatLab.Core

    Public Module Statistics

        ' ==================== PUBLIC TESTS ====================

        Public Function SignTest(values As Double(), median As Double, alpha As Double, alternative As String) As Object
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
                Throw New ArgumentException("All observations equal the hypothesized median — no information to test.")
            End If

            Dim k = Math.Min(positive, negative)
            Dim alt = NormalizeAlternative(alternative)

            Dim pValue As Double
            Select Case alt
                Case "greater"
                    ' H1: median > hypothesized -> positive should dominate
                    pValue = BinomialCdfTail(positive, n, 0.5, False)
                    If positive < negative Then pValue = 1 - pValue + BinomialProbability(n, positive, 0.5)
                Case "less"
                    pValue = BinomialCdfTail(negative, n, 0.5, False)
                    If negative < positive Then pValue = 1 - pValue + BinomialProbability(n, negative, 0.5)
                Case Else
                    pValue = 2.0 * BinomialCdf(k, n, 0.5)
            End Select
            If pValue > 1.0 Then pValue = 1.0
            If pValue < 0 Then pValue = 0

            Dim desc = Describe(values)
            Dim effectSize = Math.Abs(positive - negative) / n

            Return New With {
                .test = "Sign Test",
                .alternative = alt,
                .n = n,
                .totalObservations = values.Length,
                .positive = positive,
                .negative = negative,
                .ties = ties,
                .statistic = k,
                .pValue = pValue,
                .alpha = alpha,
                .effectSize = effectSize,
                .decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀"),
                .descriptive = desc,
                .conclusion = BuildSignConclusion(pValue, alpha, positive, negative, median, alt),
                .interpretation = BuildSignInterpretation(pValue, alpha, n, positive, negative),
                .method = "Exact two-sided binomial test (Clopper-Pearson style)",
                .assumptions = New String() {"Observations are independent", "Variable is at least ordinal", "Distribution is continuous under H₀"}
            }
        End Function

        Public Function WilcoxonSignedRank(values As Double(), median As Double, alpha As Double, alternative As String) As Object
            If values Is Nothing OrElse values.Length = 0 Then
                Throw New ArgumentException("Enter at least one observation.")
            End If

            Dim diffs As New List(Of Double)()
            For Each x In values
                Dim d = x - median
                If Math.Abs(d) > 1.0E-12 Then diffs.Add(d)
            Next

            If diffs.Count = 0 Then
                Throw New ArgumentException("All differences are zero — signed-rank cannot be calculated.")
            End If

            Dim absVals = diffs.Select(Function(d) Math.Abs(d)).ToList()
            Dim absRanks = RankWithTies(absVals)

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

            ' Tie correction for variance
            Dim tieGroups = absVals.GroupBy(Function(v) v).Where(Function(g) g.Count() > 1).ToList()
            Dim tieCorrection As Double = 0
            For Each g In tieGroups
                Dim t = g.Count()
                tieCorrection += t * (t + 1) * (2 * t + 1) / 48.0
            Next

            Dim variance = n * (n + 1) * (2 * n + 1) / 24.0 - tieCorrection
            If variance <= 0 Then variance = 1.0E-10

            Dim alt = NormalizeAlternative(alternative)
            Dim pValue As Double
            Dim z As Double

            If n <= 25 Then
                ' Use exact via normal approximation with continuity correction still, but flag as exact region
                Dim continuity = If(w > mean, -0.5, 0.5)
                z = (w - mean + continuity) / Math.Sqrt(variance)
                pValue = ComputePValueFromZ(z, alt)
            Else
                Dim continuity = If(w > mean, -0.5, 0.5)
                z = (w - mean + continuity) / Math.Sqrt(variance)
                pValue = ComputePValueFromZ(z, alt)
            End If

            Dim rEffect = Math.Abs(z) / Math.Sqrt(n)

            Return New With {
                .test = "Wilcoxon Signed-Rank Test",
                .alternative = alt,
                .n = n,
                .zeroDifferences = values.Length - n,
                .totalObservations = values.Length,
                .wPlus = wPlus,
                .wMinus = wMinus,
                .statistic = w,
                .z = z,
                .pValue = pValue,
                .alpha = alpha,
                .effectSize = rEffect,
                .decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀"),
                .descriptive = Describe(values),
                .conclusion = BuildWilcoxonConclusion(pValue, alpha, median, alt),
                .interpretation = $"W+ = {wPlus:F2}, W- = {wMinus:F2}. Effect size r = {rEffect:F3} (0.1 small, 0.3 medium, 0.5 large).",
                .method = If(n <= 25, "Normal approximation with continuity & tie correction (exact table recommended for n≤25)", "Normal approximation with continuity correction & tie correction"),
                .assumptions = New String() {"Differences are independent", "Distribution of differences is symmetric", "Variable is at least ordinal"}
            }
        End Function

        Public Function KruskalWallis(groups As List(Of Double()), alpha As Double) As Object
            If groups Is Nothing OrElse groups.Count < 2 Then
                Throw New ArgumentException("Enter at least two groups.")
            End If

            Dim observations As New List(Of Observation)()
            For groupIndex As Integer = 0 To groups.Count - 1
                If groups(groupIndex) Is Nothing OrElse groups(groupIndex).Length = 0 Then
                    Throw New ArgumentException($"Group {groupIndex + 1} is empty.")
                End If
                If groups(groupIndex).Any(Function(v) Double.IsNaN(v) OrElse Double.IsInfinity(v)) Then
                    Throw New ArgumentException($"Group {groupIndex + 1} contains invalid numbers.")
                End If
                For Each x In groups(groupIndex)
                    observations.Add(New Observation With {.Value = x, .Group = groupIndex})
                Next
            Next

            Dim ranks = RankObservations(observations)
            Dim n = observations.Count
            Dim k = groups.Count

            Dim sumTerm As Double = 0
            For groupIndex As Integer = 0 To k - 1
                Dim cnt = groups(groupIndex).Length
                Dim rankSum = ranks.Where(Function(r) r.Group = groupIndex).Sum(Function(r) r.Rank)
                sumTerm += (rankSum * rankSum) / cnt
            Next

            Dim h = (12.0 / (n * (n + 1))) * sumTerm - 3.0 * (n + 1)
            Dim tieCorrection = CalculateKruskalTieCorrection(observations)
            If tieCorrection > 0 AndAlso tieCorrection < 1 Then
                h = h / tieCorrection
            End If

            Dim df = k - 1
            Dim pValue = ChiSquareSurvival(h, df)
            Dim etaSquared = (h - k + 1) / (n - k)
            If etaSquared < 0 Then etaSquared = 0
            If etaSquared > 1 Then etaSquared = 1

            Dim groupSummaries As New List(Of Object)()
            For groupIndex As Integer = 0 To k - 1
                Dim gVals = groups(groupIndex)
                Dim rankSum = ranks.Where(Function(r) r.Group = groupIndex).Sum(Function(r) r.Rank)
                Dim desc = Describe(gVals)
                groupSummaries.Add(New With {
                    .group = groupIndex + 1,
                    .n = gVals.Length,
                    .rankSum = rankSum,
                    .meanRank = rankSum / gVals.Length,
                    .mean = desc.Mean,
                    .median = desc.Median,
                    .stdDev = desc.StdDev,
                    .min = desc.Min,
                    .max = desc.Max
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
                .effectSize = etaSquared,
                .decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀"),
                .groupSummaries = groupSummaries,
                .conclusion = If(pValue < alpha,
                    $"Significant difference detected (H={h:F3}, p={pValue:F4}). At least one group stochastically dominates another. Effect η²={etaSquared:F3}. Post-hoc Dunn test recommended.",
                    $"No significant difference (H={h:F3}, p={pValue:F4}). Insufficient evidence that groups differ. Effect η²={etaSquared:F3}."),
                .interpretation = $"H={h:F4} ~ χ²({df}). η² effect: 0.01 small, 0.06 medium, 0.14 large.",
                .method = "Chi-square approximation with tie correction",
                .assumptions = New String() {"Independent groups", "Ordinal or continuous dependent variable", "Similar shape distributions across groups"}
            }
        End Function

        Public Function MannWhitneyUTest(groups As List(Of Double()), alpha As Double, alternative As String) As Object
            If groups Is Nothing OrElse groups.Count <> 2 Then
                Throw New ArgumentException("Mann-Whitney U requires exactly two groups.")
            End If
            If groups(0).Length = 0 OrElse groups(1).Length = 0 Then
                Throw New ArgumentException("Both groups must have observations.")
            End If

            Dim g1 = groups(0)
            Dim g2 = groups(1)
            Dim n1 = g1.Length
            Dim n2 = g2.Length

            Dim all As New List(Of Observation)()
            For Each x In g1
                all.Add(New Observation With {.Value = x, .Group = 0})
            Next
            For Each x In g2
                all.Add(New Observation With {.Value = x, .Group = 1})
            Next

            Dim ranks = RankObservations(all)
            Dim r1 = ranks.Where(Function(r) r.Group = 0).Sum(Function(r) r.Rank)
            Dim r2 = ranks.Where(Function(r) r.Group = 1).Sum(Function(r) r.Rank)

            Dim u1 = n1 * n2 + n1 * (n1 + 1) / 2.0 - r1
            Dim u2 = n1 * n2 + n2 * (n2 + 1) / 2.0 - r2
            Dim u = Math.Min(u1, u2)

            Dim meanU = n1 * n2 / 2.0
            Dim tieCorr As Double = 0
            For Each g In all.GroupBy(Function(o) o.Value)
                Dim t = g.Count()
                If t > 1 Then tieCorr += t * t * t - t
            Next
            Dim n = n1 + n2
            Dim varU = n1 * n2 / 12.0 * (n + 1 - tieCorr / (n * (n - 1)))
            If varU <= 0 Then varU = 1.0E-10

            Dim z = (u - meanU + 0.5 * Math.Sign(meanU - u)) / Math.Sqrt(varU)
            Dim alt = NormalizeAlternative(alternative)
            Dim pValue = ComputePValueFromZ(z, alt)

            Dim rEffect = Math.Abs(z) / Math.Sqrt(n)
            Dim rankBiserial = 1 - 2 * u / (n1 * n2)

            Return New With {
                .test = "Mann-Whitney U Test",
                .groups = 2,
                .n1 = n1,
                .n2 = n2,
                .n = n,
                .rankSum1 = r1,
                .rankSum2 = r2,
                .u1 = u1,
                .u2 = u2,
                .statistic = u,
                .z = z,
                .pValue = pValue,
                .alpha = alpha,
                .effectSize = rEffect,
                .rankBiserial = rankBiserial,
                .decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀"),
                .groupSummaries = New Object() {
                    New With {.group = 1, .n = n1, .rankSum = r1, .meanRank = r1 / n1, .mean = g1.Average(), .median = Median(g1)},
                    New With {.group = 2, .n = n2, .rankSum = r2, .meanRank = r2 / n2, .mean = g2.Average(), .median = Median(g2)}
                },
                .conclusion = If(pValue < alpha,
                    $"Groups differ significantly (U={u:F2}, z={z:F3}, p={pValue:F4}, r={rEffect:F3}).",
                    $"No significant difference (U={u:F2}, z={z:F3}, p={pValue:F4})."),
                .method = "Normal approximation with continuity & tie correction",
                .assumptions = New String() {"Independent groups", "Ordinal/continuous outcome", "Independence within groups"}
            }
        End Function

        Public Function FriedmanTest(groups As List(Of Double()), alpha As Double) As Object
            If groups Is Nothing OrElse groups.Count < 2 Then
                Throw New ArgumentException("Friedman test needs at least 2 related groups.")
            End If
            Dim k = groups.Count
            Dim n = groups(0).Length
            If groups.Any(Function(g) g.Length <> n) Then
                Throw New ArgumentException("Friedman test requires equal group sizes (related samples / repeated measures).")
            End If
            If n < 2 Then Throw New ArgumentException("Need at least 2 blocks (observations per group).")

            ' For each block (row), rank across groups
            Dim rankSums(k - 1) As Double
            For blockIdx As Integer = 0 To n - 1
                Dim blockVals As New List(Of Double)()
                For gIdx As Integer = 0 To k - 1
                    blockVals.Add(groups(gIdx)(blockIdx))
                Next
                Dim r = RankWithTies(blockVals)
                For gIdx As Integer = 0 To k - 1
                    rankSums(gIdx) += r(gIdx)
                Next
            Next

            Dim sumSq As Double = rankSums.Sum(Function(rs) rs * rs)
            Dim chiF = (12.0 / (n * k * (k + 1))) * sumSq - 3 * n * (k + 1)
            Dim df = k - 1
            Dim pValue = ChiSquareSurvival(chiF, df)
            Dim kendallW = chiF / (n * (k - 1))

            Dim summaries As New List(Of Object)()
            For i As Integer = 0 To k - 1
                summaries.Add(New With {
                    .group = i + 1,
                    .n = n,
                    .rankSum = rankSums(i),
                    .meanRank = rankSums(i) / n,
                    .mean = groups(i).Average(),
                    .median = Median(groups(i))
                })
            Next

            Return New With {
                .test = "Friedman Test",
                .groups = k,
                .blocks = n,
                .n = n * k,
                .statistic = chiF,
                .degreesOfFreedom = df,
                .pValue = pValue,
                .alpha = alpha,
                .effectSize = kendallW,
                .decision = If(pValue < alpha, "Reject H₀", "Fail to reject H₀"),
                .groupSummaries = summaries,
                .conclusion = If(pValue < alpha,
                    $"Significant differences across related groups (χ²_F={chiF:F3}, p={pValue:F4}, W={kendallW:F3}).",
                    $"No significant differences (χ²_F={chiF:F3}, p={pValue:F4})."),
                .method = "Chi-square approximation",
                .assumptions = New String() {"Related samples / repeated measures", "Ordinal or continuous", "Blocks independent"}
            }
        End Function

        ' ==================== DESCRIPTIVE & HELPERS ====================

        Public Function Describe(values As Double()) As DescriptiveStats
            If values Is Nothing OrElse values.Length = 0 Then Return New DescriptiveStats()
            Dim sorted = values.OrderBy(Function(v) v).ToArray()
            Return New DescriptiveStats With {
                .Count = values.Length,
                .Mean = values.Average(),
                .Median = Median(values),
                .StdDev = StdDev(values),
                .Min = sorted.First(),
                .Max = sorted.Last(),
                .Q1 = Percentile(sorted, 25),
                .Q3 = Percentile(sorted, 75)
            }
        End Function

        Private Function Median(values As Double()) As Double
            If values.Length = 0 Then Return 0
            Dim s = values.OrderBy(Function(v) v).ToArray()
            Dim mid = s.Length \ 2
            If s.Length Mod 2 = 1 Then Return s(mid)
            Return (s(mid - 1) + s(mid)) / 2.0
        End Function

        Private Function StdDev(values As Double()) As Double
            If values.Length <= 1 Then Return 0
            Dim m = values.Average()
            Dim sum = values.Sum(Function(v) (v - m) * (v - m))
            Return Math.Sqrt(sum / (values.Length - 1))
        End Function

        Private Function Percentile(sorted As Double(), p As Double) As Double
            If sorted.Length = 0 Then Return 0
            If sorted.Length = 1 Then Return sorted(0)
            Dim rank = p / 100.0 * (sorted.Length - 1)
            Dim low = CInt(Math.Floor(rank))
            Dim high = CInt(Math.Ceiling(rank))
            If low = high Then Return sorted(low)
            Dim w = rank - low
            Return sorted(low) * (1 - w) + sorted(high) * w
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
                While j + 1 < sorted.Count AndAlso Math.Abs(sorted(j + 1).Value - sorted(i).Value) < 1.0E-12
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
            Dim indexed = values.Select(Function(v, idx) New With {.Value = v, .Index = idx}).OrderBy(Function(x) x.Value).ToList()
            Dim result(values.Count - 1) As Double
            Dim i As Integer = 0
            While i < indexed.Count
                Dim j = i
                While j + 1 < indexed.Count AndAlso Math.Abs(indexed(j + 1).Value - indexed(i).Value) < 1.0E-12
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
            If n <= 1 Then Return 1
            Dim tieSum As Double = 0
            For Each g In observations.GroupBy(Function(o) o.Value)
                Dim t = g.Count()
                If t > 1 Then tieSum += t * t * t - t
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

        Private Function BinomialCdfTail(k As Integer, n As Integer, p As Double, lower As Boolean) As Double
            If lower Then
                Return BinomialCdf(k, n, p)
            Else
                ' upper tail P(X >= k)
                Return 1 - BinomialCdf(k - 1, n, p)
            End If
        End Function

        Private Function BinomialProbability(n As Integer, k As Integer, p As Double) As Double
            If k < 0 OrElse k > n Then Return 0
            If p = 0 Then Return If(k = 0, 1, 0)
            If p = 1 Then Return If(k = n, 1, 0)
            Dim logProb = LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1) + k * Math.Log(p) + (n - k) * Math.Log(1 - p)
            Return Math.Exp(logProb)
        End Function

        Private Function NormalCdf(x As Double) As Double
            Return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)))
        End Function

        Private Function ComputePValueFromZ(z As Double, alternative As String) As Double
            Select Case alternative
                Case "greater"
                    Return 1 - NormalCdf(z)
                Case "less"
                    Return NormalCdf(z)
                Case Else
                    Return 2.0 * NormalCdf(-Math.Abs(z))
            End Select
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

        Private Function NormalizeAlternative(alt As String) As String
            If String.IsNullOrWhiteSpace(alt) Then Return "two-sided"
            Dim a = alt.Trim().ToLowerInvariant()
            If a = "greater" OrElse a = ">" OrElse a = "upper" Then Return "greater"
            If a = "less" OrElse a = "<" OrElse a = "lower" Then Return "less"
            Return "two-sided"
        End Function

        Private Function BuildSignConclusion(pValue As Double, alpha As Double, pos As Integer, neg As Integer, median As Double, alt As String) As String
            If pValue < alpha Then
                Return $"Reject H₀: Evidence suggests population median ≠ {median} (p={pValue:F4}). Positive: {pos}, Negative: {neg}."
            Else
                Return $"Fail to reject H₀: No evidence median differs from {median} (p={pValue:F4})."
            End If
        End Function

        Private Function BuildSignInterpretation(pValue As Double, alpha As Double, n As Integer, pos As Integer, neg As Integer) As String
            Return $"Exact binomial test with n={n} (excluding ties). Smaller count = {Math.Min(pos, neg)}. Two-sided p-value computed as 2*P(X≤k)."
        End Function

        Private Function BuildWilcoxonConclusion(pValue As Double, alpha As Double, median As Double, alt As String) As String
            If pValue < alpha Then
                Return $"Reject H₀: Distribution centered differently than {median} (p={pValue:F4}, {alt})."
            Else
                Return $"Fail to reject H₀: No evidence of shift from {median} (p={pValue:F4})."
            End If
        End Function

    End Module

End Namespace
