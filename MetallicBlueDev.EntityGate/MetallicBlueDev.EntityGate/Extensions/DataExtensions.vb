Imports System.Runtime.CompilerServices

Namespace Extensions

    Public Module DataExtensions










        <Extension()>
        Public Function GetDataSetRow(Of T As DataRow)(pData As DataTable, pIndex As Integer) As T
            Dim itemRow As T = Nothing

            If Not pData Is Nothing _
              AndAlso pData.Rows.Count > 0 _
              AndAlso pIndex >= 0 _
              AndAlso pIndex < pData.Rows.Count Then
                itemRow = DirectCast(pData.Rows(pIndex), T)
            End If

            Return itemRow
        End Function








        <Extension()>
        Public Function CopyToDataRow(Of T As DataRow)(pSource As DataRow) As T
            Dim newRow As T = Nothing

            If Not pSource Is Nothing _
              AndAlso Not pSource.Table Is Nothing Then
                If Not pSource.Table.DataSet Is Nothing Then

                    Dim dataSet As DataSet = pSource.Table.DataSet.Copy()
                    newRow = DirectCast(dataSet.Tables(pSource.Table.TableName).NewRow(), T)
                Else

                    newRow = DirectCast(pSource.Table.Copy().NewRow(), T)
                End If

                newRow.MergeAllValue(pSource)
            End If

            Return newRow
        End Function







        <Extension()>
        Public Sub MergeAllValue(pTarget As DataRow, pOther As DataRow)
            If Not pTarget Is Nothing _
              AndAlso Not pOther Is Nothing Then
                If Not pTarget.Table Is Nothing _
                  AndAlso Not pOther.Table Is Nothing Then

                    For Each currentColumn As DataColumn In pTarget.Table.Columns

                        If pOther.Table.Columns.Contains(currentColumn.ColumnName) _
                          AndAlso Not pOther(currentColumn.ColumnName) Is Nothing _
                          AndAlso Not pOther(currentColumn.ColumnName).Equals(DBNull.Value) Then

                            pTarget(currentColumn) = pOther(currentColumn.ColumnName)
                        End If
                    Next
                End If
            End If
        End Sub

    End Module

End Namespace
