Imports System.Text.Json.Serialization

Namespace StatLab.Models

    Public Class AnalysisRequest
        <JsonPropertyName("test")>
        Public Property Test As String = ""

        <JsonPropertyName("values")>
        Public Property Values As Double() = Array.Empty(Of Double)()

        <JsonPropertyName("groups")>
        Public Property Groups As List(Of Double()) = New List(Of Double())()

        <JsonPropertyName("median")>
        Public Property Median As Double = 0

        <JsonPropertyName("alpha")>
        Public Property Alpha As Double = 0.05

        <JsonPropertyName("alternative")>
        Public Property Alternative As String = "two-sided"

        Public Function Validate() As List(Of String)
            Dim errors As New List(Of String)()

            If String.IsNullOrWhiteSpace(Test) Then
                errors.Add("Test type is required.")
            End If

            If Alpha <= 0 OrElse Alpha >= 1 Then
                errors.Add("Alpha must be between 0 and 1 (exclusive).")
            End If

            Dim t = Test.Trim().ToLowerInvariant()
            If t = "sign" OrElse t = "signed-rank" OrElse t = "wilcoxon" OrElse t = "sign-rank" Then
                If Values Is Nothing OrElse Values.Length = 0 Then
                    errors.Add("Enter at least one observation.")
                End If
                If Values IsNot Nothing AndAlso Values.Any(Function(v) Double.IsNaN(v) OrElse Double.IsInfinity(v)) Then
                    errors.Add("Observations contain invalid numbers.")
                End If
            ElseIf t = "kruskal-wallis" OrElse t = "kruskal" OrElse t = "mann-whitney" OrElse t = "mannwhitney" OrElse t = "friedman" Then
                If Groups Is Nothing OrElse Groups.Count < 2 Then
                    errors.Add("At least two groups are required for this test.")
                End If
            End If

            Return errors
        End Function
    End Class

    Public Class DescriptiveStats
        Public Property Count As Integer
        Public Property Mean As Double
        Public Property Median As Double
        Public Property StdDev As Double
        Public Property Min As Double
        Public Property Max As Double
        Public Property Q1 As Double
        Public Property Q3 As Double
    End Class

    Public Class GroupSummary
        Public Property Group As Integer
        Public Property N As Integer
        Public Property RankSum As Double
        Public Property Mean As Double
        Public Property Median As Double
        Public Property StdDev As Double
        Public Property Min As Double
        Public Property Max As Double
    End Class

End Namespace
