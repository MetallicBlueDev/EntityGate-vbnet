Imports System.Data.Entity
Imports MetallicBlueDev.EntityGate.Helpers
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Gate







    <Serializable()>
    Public NotInheritable Class EntityGateAgent
        Inherits EntityGateCore(Of IEntityObjectIdentifier, DbContext)







        Public Sub New(pExternalEntityType As Type, Optional ByVal pConnectionName As String = Nothing)
            MyBase.New(ReflectionHelper.MakeInstance(Of IEntityObjectIdentifier)(pExternalEntityType), pConnectionName)
        End Sub

    End Class

End Namespace
