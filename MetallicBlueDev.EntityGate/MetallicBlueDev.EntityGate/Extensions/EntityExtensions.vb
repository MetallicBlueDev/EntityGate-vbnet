Imports System.Data.Entity.Core.Objects
Imports System.Data.Entity.Infrastructure
Imports System.Runtime.CompilerServices
Imports MetallicBlueDev.EntityGate.GateException
Imports MetallicBlueDev.EntityGate.Helpers
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Extensions





    Public Module EntityExtensions






        <Extension()>
        Public Function GetObjectContext(pContext As IObjectContextAdapter) As ObjectContext
            If pContext Is Nothing _
               OrElse pContext.ObjectContext Is Nothing Then
                Throw New EntityGateException("Context is not available.")
            End If

            Return pContext.ObjectContext
        End Function






        <Extension()>
        Public Function HasValidEntityKey(pEntity As IEntityObjectIdentifier) As Boolean
            Return pEntity IsNot Nothing AndAlso PocoHelper.IsValidEntityKeyValue(pEntity.Identifier)
        End Function








        <Extension()>
        Public Function CloneEntity(Of T As {Class, IEntityObjectIdentifier})(pSource As T, Optional ByVal pWithDataRelation As Boolean = False) As T
            Dim result As T = Nothing

            If pSource IsNot Nothing Then
                Dim contextEntityType As Type = ObjectContext.GetObjectType(pSource.GetType())
                result = ReflectionHelper.CloneEntity(pSource, contextEntityType, pWithDataRelation)
            End If

            Return result
        End Function







        <Extension()>
        Public Function IsEntityNameable(pEntityObject As IEntityObjectIdentifier) As Boolean
            Return TypeOf pEntityObject Is IEntityObjectNameable
        End Function







        <Extension()>
        Public Function IsEntityReferential(pEntityObject As IEntityObjectIdentifier) As Boolean
            Return TypeOf pEntityObject Is IEntityObjectReferential
        End Function







        <Extension()>
        Public Function HasEntityRecognizableCode(pEntityObject As IEntityObjectIdentifier) As Boolean
            Return TypeOf pEntityObject Is IEntityObjectRecognizableCode
        End Function







        <Extension()>
        Public Function HasEntitySingleValue(pEntityObject As IEntityObjectIdentifier) As Boolean
            Return TypeOf pEntityObject Is IEntityObjectSingleValue
        End Function







        <Extension()>
        Public Function IsEntityDisable(pEntityObject As IEntityObjectIdentifier) As Boolean
            Return TypeOf pEntityObject Is IEntityObjectDisable
        End Function







        <Extension()>
        Public Function GetEntityName(pEntityObject As IEntityObjectIdentifier) As String
            Dim name As String = Nothing

            If pEntityObject.IsEntityNameable() Then
                name = DirectCast(pEntityObject, IEntityObjectNameable).Name
            End If

            Return name
        End Function







        <Extension()>
        Public Function GetEntityCodeName(pEntityObject As IEntityObjectIdentifier) As String
            Dim name As String = Nothing

            If pEntityObject.HasEntityRecognizableCode() Then
                name = DirectCast(pEntityObject, IEntityObjectRecognizableCode).CodeName
            End If

            Return name
        End Function







        <Extension()>
        Public Function GetEntitySingleValue(pEntityObject As IEntityObjectIdentifier) As String
            Dim name As String = Nothing

            If pEntityObject.HasEntitySingleValue() Then
                name = DirectCast(pEntityObject, IEntityObjectSingleValue).SingleValue
            End If

            Return name
        End Function

    End Module

End Namespace
