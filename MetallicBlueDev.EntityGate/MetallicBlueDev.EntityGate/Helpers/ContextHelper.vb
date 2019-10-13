Imports System.Data.Entity
Imports System.Data.Entity.Core.Metadata.Edm
Imports System.Data.Entity.Core.Objects
Imports System.Text
Imports MetallicBlueDev.EntityGate.Extensions

Namespace Helpers

    Friend Class ContextHelper





        Friend Shared Function GetEntityContainer(pModel As DbContext) As EntityContainer
            Dim sourceContext As ObjectContext = pModel.GetObjectContext()
            Return sourceContext.MetadataWorkspace.GetEntityContainer(sourceContext.DefaultContainerName, DataSpace.CSpace)
        End Function








        Friend Shared Function IsValidContext(Of TContext As DbContext)(pEntityType As Type, pContextType As Type) As Boolean
            Return GetType(TContext).IsAssignableFrom(pContextType) _
              AndAlso pEntityType.Namespace.Equals(pContextType.Namespace)
        End Function








        Friend Shared Function IsContextRelativeToEntityType(Of TContext As DbContext)(pContext As TContext, pEntityType As Type) As Boolean
            Dim isValidContext As Boolean = False

            If pContext IsNot Nothing Then

                Dim entitySetsContext As EntityContainer = GetEntityContainer(pContext)

                If entitySetsContext IsNot Nothing _
                  AndAlso entitySetsContext.BaseEntitySets IsNot Nothing Then

                    If entitySetsContext.BaseEntitySets.Any(Function(pEntitySetBase) MatchEntityType(pEntitySetBase, pEntityType)) Then
                        isValidContext = True
                    End If
                End If
            End If

            Return isValidContext
        End Function







        Friend Shared Function MatchEntityType(pEntitySetBase As EntitySetBase, pEntityType As Type) As Boolean
            Return pEntitySetBase.ElementType IsNot Nothing _
              AndAlso pEntitySetBase.ElementType.Name.EqualsIgnoreCase(pEntityType.Name)
        End Function







        Friend Shared Function GetMetadata(pContextTypeName As String, pResourceNames As IEnumerable(Of String)) As String
            Dim metadata As New StringBuilder()

            For Each resourceName As String In pResourceNames.Where(Function(pName) pName.Length > 3 AndAlso pName.Contains(pContextTypeName))
                Select Case resourceName.Substring(resourceName.Length - 4)
                    Case "csdl", "ssdl", ".msl"
                        If metadata.Length > 0 Then
                            metadata.Append("|")
                        End If

                        metadata.Append("res://*/")
                        metadata.Append(resourceName)
                End Select
            Next

            Return metadata.ToString()
        End Function

    End Class

End Namespace
