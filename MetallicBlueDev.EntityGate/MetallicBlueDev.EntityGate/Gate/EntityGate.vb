Imports System.Data.Entity
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Gate








    <Serializable()>
    Public NotInheritable Class EntityGate(Of TEntity As {Class, IEntityObjectIdentifier})
        Inherits EntityGateCore(Of TEntity, DbContext)







        Public Sub New(Optional pExternalEntity As TEntity = Nothing, Optional ByVal pConnectionName As String = Nothing)
            MyBase.New(pExternalEntity, pConnectionName)
        End Sub

    End Class







    <Serializable()>
    Public NotInheritable Class EntityGate(Of TEntity As {Class, IEntityObjectIdentifier}, TContext As DbContext)
        Inherits EntityGateCore(Of TEntity, TContext)







        Public Sub New(Optional pExternalEntity As TEntity = Nothing, Optional ByVal pConnectionName As String = Nothing)
            MyBase.New(pExternalEntity, pConnectionName)
        End Sub

    End Class

End Namespace
