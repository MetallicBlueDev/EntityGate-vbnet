Imports System.Data.Entity
Imports System.Reflection
Imports log4net
Imports MetallicBlueDev.EntityGate.GateException
Imports MetallicBlueDev.EntityGate.Helpers
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Tracking

    <Serializable()>
    Friend Class EntityGateTracking

        Private Shared ReadOnly Logger As ILog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType)

        Private ReadOnly mEntities As New List(Of EntityStateTracking)()

        Friend Sub UnloadEmptyEntityCollection()
            For Each entityObject As IEntityObjectIdentifier In mEntities.Select(Function(pTracking) pTracking.EntityObject)
                PocoHelper.SetEmptyEntityCollectionAsNull(entityObject)
            Next
        End Sub

        Friend Function GetMainEntity() As IEntityObjectIdentifier
            Dim mainEntity As IEntityObjectIdentifier() = GetMainEntities().ToArray()

            If mainEntity.Length <> 1 Then
                Throw New EntityGateException("Invalid main entity.")
            End If

            Return mainEntity.First()
        End Function

        Friend Sub MarkEntity(pEntity As Object, pState As EntityState, pIsMainEntity As Boolean)
            If Not TypeOf pEntity Is IEntityObjectIdentifier Then
                Throw New EntityGateException(pEntity.ToString() & " must implement the interface IEntityObjectIdentifier.")
            End If

            Mark(DirectCast(pEntity, IEntityObjectIdentifier), pState, pIsMainEntity)
        End Sub

        Friend Sub CleanTracking()
            mEntities.Clear()
        End Sub

        Friend Function HasEntities() As Boolean
            Return mEntities.Count > 0
        End Function

        Friend Function GetEntities() As IList(Of EntityStateTracking)
            Return mEntities
        End Function

        Private Function GetMainEntities() As IEnumerable(Of IEntityObjectIdentifier)
            Return mEntities _
              .Where(Function(pTracking) pTracking.IsMainEntity) _
              .Select(Function(pTracking) pTracking.EntityObject)
        End Function

        Private Sub Mark(pEntity As IEntityObjectIdentifier, pState As EntityState, pIsMainEntity As Boolean)
            Select Case pState
                Case EntityState.Deleted, EntityState.Added, EntityState.Modified, EntityState.Unchanged
                    SetState(pEntity, pState, pIsMainEntity)

                Case Else
                    Throw New EntityGateException("State '" & pState & "' is not valid entity state for tracking (" & pEntity.ToString() & ").")

            End Select
        End Sub

        Private Sub SetState(pEntity As IEntityObjectIdentifier, pState As EntityState, pIsMainEntity As Boolean)
            If Logger.IsDebugEnabled Then
                Logger.DebugFormat("Tracking '{0}' with state '{1}' (main entity={2}).", pEntity, pState, pIsMainEntity)
            End If

            Dim trackingIndex As Integer = mEntities.FindIndex(Function(pTracking) pTracking.EntityObject.Equals(pEntity))

            If trackingIndex >= 0 Then
                mEntities(trackingIndex).State = pState
            Else
                mEntities.Add(New EntityStateTracking(pEntity, pState, pIsMainEntity))
            End If
        End Sub

    End Class

End Namespace
