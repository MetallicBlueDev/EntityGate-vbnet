Imports System.Data.Entity
Imports MetallicBlueDev.EntityGate.Gate
Imports MetallicBlueDev.EntityGate.GateException
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Helpers

    Public Class EntityHelper

        Private Sub New()

        End Sub







        Public Shared Function GetEntityGate(Of T As DbContext)(ByRef pComponent As IEntityObjectIdentifier) As EntityGateContext(Of T)
            Dim gate As New EntityGateContext(Of T)(pComponent)

            If Not gate.IsNewEntity Then
                gate.Load()
            End If

            pComponent = gate.Entity

            Return gate
        End Function








        Public Shared Function GetEntityGate(Of T As DbContext)(pEntityType As Type, pEntityIdentifier As Object) As EntityGateContext(Of T)
            Dim speedEntityInstance As IEntityObjectIdentifier = ReflectionHelper.MakeInstance(Of IEntityObjectIdentifier)(pEntityType)
            Dim gate As New EntityGateContext(Of T)(speedEntityInstance)

            If Not gate.Load(pEntityIdentifier) Then
                Throw New EntityGateException("Unable to load " & gate.GetFriendlyName() & " with identifier '" & If(Not pEntityIdentifier Is Nothing, pEntityIdentifier.ToString(), "null") & "'.")
            End If

            Return gate
        End Function







        Public Shared Function CheckEntityType(Of TEntity As {Class, IEntityObjectIdentifier})(pEntity As IEntityObjectIdentifier) As TEntity
            If pEntity Is Nothing _
              OrElse GetType(TEntity) IsNot pEntity.GetType() Then
                Throw New EntityGateException("Invalid entity type.")
            End If

            Return DirectCast(pEntity, TEntity)
        End Function







        Public Shared Function Reload(Of TEntity As {Class, IEntityObjectIdentifier})(pEntity As TEntity) As TEntity
            Dim gate As New EntityGate(Of TEntity)(pEntity)

            If Not gate.Load() Then
                Throw New EntityGateException("Fail to reload Entity " & gate.GetFriendlyName())
            End If

            Return gate.Entity
        End Function








        Public Shared Function LoadAllEntities(Of TEntity As {Class, IEntityObjectIdentifier})() As List(Of TEntity)
            Return New EntityGate(Of TEntity)().List().ToList()
        End Function

    End Class

End Namespace
