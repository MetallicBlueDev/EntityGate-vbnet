Imports System.Data.SqlClient
Imports System.Reflection
Imports System.Text
Imports log4net
Imports MetallicBlueDev.EntityGate.Extensions
Imports MetallicBlueDev.EntityGate.Gate

Namespace Helpers




    Friend Class GateHelper

        Private Shared ReadOnly Logger As ILog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType)






        Friend Shared Sub ExceptionMarker(pEx As Exception, gate As IEntityGate)
            Dim sqlEx As SqlException = GetSqlException(pEx)
            Dim lastQuery As String

            If gate.SqlStatement.IsNotNullOrEmpty() Then
                lastQuery = gate.SqlStatement
            Else
                lastQuery = "sorry, unknown query"
            End If

            If Not sqlEx Is Nothing Then
                For Each currentError As SqlError In sqlEx.Errors
                    Logger.FatalFormat(
                      "Sql message: {0}." &
                        Environment.NewLine & "LineNumber: {1}" &
                        Environment.NewLine & "Source: {2}" &
                        Environment.NewLine & "Procedure: {3}" &
                        Environment.NewLine & "Sql server error class: {4}" &
                        Environment.NewLine & "Sql server error number: {5}" &
                        Environment.NewLine & "Query: {6}",
                      currentError.Message,
                      currentError.LineNumber,
                      currentError.Source,
                      currentError.Procedure,
                      currentError.Class,
                      currentError.Number,
                      lastQuery
                      )
                Next
            Else
                Logger.FatalFormat("Failed to execute last token: {0}.", lastQuery)
            End If
        End Sub








        Friend Shared Function GetPrimaryKey(pEntity As Object, pEntityType As Type, Optional ByVal pCacheKeys As IEnumerable(Of KeyValuePair(Of String, Object)) = Nothing) As KeyValuePair(Of String, Object)
            Dim propKey As KeyValuePair(Of String, Object) = Nothing

            If pCacheKeys Is Nothing Then
                pCacheKeys = GetPrimaryKeys(pEntity, pEntityType)
            End If

            Dim propKeyEnum As IEnumerator = pCacheKeys.GetEnumerator()

            While propKeyEnum.MoveNext
                propKey = DirectCast(propKeyEnum.Current, KeyValuePair(Of String, Object))
                Exit While
            End While

            If propKey.Key Is Nothing Then
                Logger.ErrorFormat("No key found on the object '{0}'.", pEntity)
            End If

            Return propKey
        End Function







        Friend Shared Function GetPrimaryKeys(pEntity As Object, pEntityType As Type) As IDictionary(Of String, Object)
            Dim primaryKeys As New Dictionary(Of String, Object)

            If Not pEntity Is Nothing Then
                Dim currentEntityType As Type = If(pEntityType, pEntity.GetType())

                BuiltPrimaryKeysByConvention(pEntity, primaryKeys, currentEntityType)

                If Not (primaryKeys.Count > 0) Then
                    If Logger.IsDebugEnabled Then
                        Logger.DebugFormat("Violation of naming conventions of primary keys for '{0}'.", pEntity)
                    End If

                    BuiltPrimaryKeysBySearch(pEntity, primaryKeys, currentEntityType)
                End If
            End If

            Return primaryKeys
        End Function






        Friend Shared Sub RefreshPrimaryKey(pCacheKeys As IDictionary(Of String, Object), pEntity As Object)
            If Not pCacheKeys Is Nothing _
               AndAlso Not pEntity Is Nothing Then
                Dim currentEntityType As Type = pEntity.GetType()

                For index As Integer = pCacheKeys.Count - 1 To 0 Step -1
                    Dim indexPair As KeyValuePair(Of String, Object) = pCacheKeys.ElementAt(index)
                    AppendPrimaryKey(pCacheKeys, pEntity, currentEntityType.GetProperty(indexPair.Key), True)
                Next
            End If
        End Sub








        Friend Shared Sub AppendPrimaryKey(pPrimaryKeys As IDictionary(Of String, Object), pEntity As Object, pPropertyKey As PropertyInfo, pDoUpdate As Boolean)
            If pPrimaryKeys IsNot Nothing _
              AndAlso pEntity IsNot Nothing _
              AndAlso pPropertyKey IsNot Nothing Then
                Dim keyName As String = pPropertyKey.Name
                Dim doAdd As Boolean = Not pPrimaryKeys.ContainsKey(keyName)

                If doAdd _
                   OrElse pDoUpdate Then
                    Dim keyValue As Object = pPropertyKey.GetValue(pEntity, Nothing)

                    If keyValue IsNot Nothing Then
                        If doAdd Then
                            pPrimaryKeys.Add(keyName, keyValue)
                        ElseIf pDoUpdate Then
                            pPrimaryKeys(keyName) = keyValue
                        End If
                    End If
                End If
            End If
        End Sub








        Friend Shared Function GetSqlException(pEx As Exception) As SqlException
            Dim sqlEx As SqlException = Nothing

            If Not pEx Is Nothing Then
                If Not (TypeOf pEx Is SqlException) Then

                    While Not pEx.InnerException Is Nothing
                        If TypeOf pEx Is SqlException Then
                            Exit While
                        End If

                        pEx = pEx.InnerException
                    End While
                End If

                If TypeOf pEx Is SqlException Then
                    sqlEx = DirectCast(pEx, SqlException)
                End If
            End If

            Return sqlEx
        End Function






        Friend Shared Function GetContentInfo(pSource As Object) As String
            Dim content As New StringBuilder()

            If pSource IsNot Nothing Then
                content.Append(pSource)
                content.Append(" (")

                Try
                    BuiltContentInfo(pSource, content)
                Catch ex As Exception
                    Logger.Error("Failed to get all contents informations in entity.", ex)
                End Try

                content.Append(")")
            Else
                content.Append("Entity is Nothing")
            End If

            Return content.ToString()
        End Function








        Private Shared Sub BuiltPrimaryKeysByConvention(pSource As Object, pPrimaryKeys As Dictionary(Of String, Object), pEntityType As Type)
            For Each propInfo As PropertyInfo In pEntityType.GetProperties() _
              .Where(Function(pProperty) pProperty.CanRead AndAlso pProperty.CanWrite AndAlso pProperty.Name IsNot Nothing AndAlso pProperty.Name.EndsWith("_ID", StringComparison.Ordinal))
                AppendPrimaryKey(pPrimaryKeys, pSource, propInfo, False)
            Next
        End Sub








        Private Shared Sub BuiltPrimaryKeysBySearch(pEntity As Object, pPrimaryKeys As Dictionary(Of String, Object), pEntityType As Type)
            For Each propInfo As PropertyInfo In pEntityType.GetProperties() _
              .Where(Function(pProperty) pProperty.CanRead AndAlso pProperty.CanWrite AndAlso pProperty.Name IsNot Nothing AndAlso pProperty.Name.ToUpper().EndsWith("ID", StringComparison.Ordinal))
                AppendPrimaryKey(pPrimaryKeys, pEntity, propInfo, False)
            Next
        End Sub






        Private Shared Sub BuiltContentInfo(pSource As Object, pContent As StringBuilder)
            For Each info As PropertyInfo In ReflectionHelper.GetReadWriteProperties(pSource.GetType(), False)
                Dim valueObject As Object = info.GetValue(pSource, Nothing)

                If valueObject Is Nothing Then
                    Continue For
                End If

                pContent.Append(info.Name)
                pContent.Append("=")
                pContent.Append(valueObject)
                pContent.Append(", ")
            Next
        End Sub

    End Class

End Namespace
