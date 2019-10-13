Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions

Namespace Extensions

    Public Module StringExtensions








        <Extension()>
        Public Function IsNotNullOrEmpty(pSource As String, Optional ByVal pMinimumLength As Integer = 0) As Boolean
            Return Not String.IsNullOrEmpty(pSource) AndAlso pSource.Trim().Length > pMinimumLength
        End Function








        <Extension()>
        Public Function EqualsIgnoreCase(pValue As String, pOther As String) As Boolean
            Dim rslt As Boolean = False

            If Not pValue Is Nothing Then
                If Not pOther Is Nothing Then
                    rslt = pValue.Trim().ToUpper().Equals(pOther.Trim().ToUpper())
                End If
            End If

            Return rslt
        End Function








        <Extension()>
        Public Function IsMatch(pSource As String, pPattern As String) As Boolean
            Dim rslt As Boolean = False

            If pSource.IsNotNullOrEmpty() _
              AndAlso pPattern.IsNotNullOrEmpty() Then
                rslt = Regex.IsMatch(pSource, pPattern, RegexOptions.CultureInvariant Or RegexOptions.IgnoreCase Or RegexOptions.IgnorePatternWhitespace)
            End If

            Return rslt
        End Function

    End Module

End Namespace
