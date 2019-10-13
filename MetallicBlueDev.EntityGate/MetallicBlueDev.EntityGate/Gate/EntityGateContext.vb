Imports System.Data.Entity
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Gate








    <Serializable()>
    Public NotInheritable Class EntityGateContext(Of TContext As DbContext)
        Inherits EntityGateCore(Of IEntityObjectIdentifier, TContext)







        Public Sub New(Optional pExternalEntity As IEntityObjectIdentifier = Nothing, Optional ByVal pConnectionName As String = Nothing)
            MyBase.New(pExternalEntity, pConnectionName)
        End Sub







        Public Shared Function MakeModel(Optional ByVal pConnectionName As String = Nothing) As TContext
            Dim gate As New EntityGate(Of IEntityObjectIdentifier, TContext)(Nothing, pConnectionName)
            Return gate.GetContext()
        End Function

    End Class

End Namespace
