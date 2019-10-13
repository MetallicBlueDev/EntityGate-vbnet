Imports MetallicBlueDev.EntityGate.Configuration
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Gate






    Public Interface IEntityGate
        Inherits IDisposable







        Property CanThrowException As Boolean








        Property CanUseLogging As Boolean







        Property CanUseNotification As Boolean







        ReadOnly Property Configuration As ClientConfiguration







        ReadOnly Property NumberOfAttempts As Integer








        ReadOnly Property NumberOfRows As Integer







        Property SqlStatement As String







        Property AllowedSaving As Boolean






        ReadOnly Property CurrentEntityObject As IEntityObjectIdentifier






        ReadOnly Property HasEntityObject As Boolean





        ReadOnly Property IsNewEntity As Boolean





        Sub CancelLastCommand()




        Sub NewEntity()





        Function GetTableName() As String







        Function GetFriendlyName() As String





        Function GetPrimaryKey() As KeyValuePair(Of String, Object)






        Function Load(Optional pKeyValue As Object = Nothing) As Boolean





        Function Save() As Boolean





        Function Delete() As Boolean






        Function Delete(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier









        Function Apply(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier







        Function GetOriginalValues(Optional pAllProperties As Boolean = False) As KeyValuePair(Of String, Object)()






        Function GetFieldValue(pFieldName As String) As Object





        Function ListEntities() As IEnumerable(Of IEntityObjectIdentifier)











        Sub SetEntityObject(pEntity As IEntityObjectIdentifier)

    End Interface

End Namespace
