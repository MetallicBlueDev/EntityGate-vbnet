Imports System.Data.Entity
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Tracking

    <Serializable()>
    Friend Class EntityStateTracking

        Friend Property State As EntityState

        Friend ReadOnly Property EntityObject As IEntityObjectIdentifier

        Friend ReadOnly Property IsMainEntity As Boolean

        Friend Sub New(pEntityObject As IEntityObjectIdentifier, pState As EntityState, pIsMainEntity As Boolean)
            IsMainEntity = pIsMainEntity
            EntityObject = pEntityObject
            State = pState
        End Sub

    End Class

End Namespace
