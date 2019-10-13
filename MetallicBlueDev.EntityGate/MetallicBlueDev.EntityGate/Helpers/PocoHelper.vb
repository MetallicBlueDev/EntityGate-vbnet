Imports System.Data.Entity.Core.Objects
Imports System.Reflection
Imports MetallicBlueDev.EntityGate.Extensions
Imports MetallicBlueDev.EntityGate.InterfacedObject

Namespace Helpers





    Friend Class PocoHelper

        Private Shared ReadOnly CollectionRemoveMethodName As String
        Private Shared ReadOnly CollectionAddMethodName As String
        Private Shared ReadOnly CollectionCountPropertyName As String

        Shared Sub New()
            CollectionRemoveMethodName = ReflectionHelper.GetPropertyName(Of HashSet(Of Object))(Function(pO) pO.Remove(Nothing))
            CollectionAddMethodName = ReflectionHelper.GetPropertyName(Of HashSet(Of Object))(Function(pO) pO.Add(Nothing))
            CollectionCountPropertyName = ReflectionHelper.GetPropertyName(Of HashSet(Of Object))(Function(pO) pO.Count)
        End Sub






        Friend Shared Function IsValidEntityType(pEntity As Object) As Boolean
            Return pEntity IsNot Nothing _
                          AndAlso IsValidEntityType(pEntity.GetType())
        End Function






        Friend Shared Function IsValidEntityType(pEntityType As Type) As Boolean
            Return pEntityType IsNot Nothing _
                    AndAlso Not pEntityType.IsInterface _
                    AndAlso Not pEntityType.IsAbstract
        End Function






        Friend Shared Function IsValidEntityKeyValue(pEntityKeyValue As Object) As Boolean
            Dim valid As Boolean = pEntityKeyValue IsNot Nothing AndAlso Not pEntityKeyValue.Equals(Nothing)

            If valid _
                    AndAlso pEntityKeyValue.GetType().IsPrimitive Then
                valid = Convert.ToInt64(pEntityKeyValue) > 0
            End If

            Return valid
        End Function






        Friend Shared Sub SetEmptyEntityCollectionAsNull(Of T As Class)(pSource As T)
            Dim properties As PropertyInfo() = pSource.GetType().GetProperties()

            For Each collectionField As PropertyInfo In ReflectionHelper.GetEntityCollectionProperties(properties)
                Dim currentCollection As Object = collectionField.GetValue(pSource, Nothing)

                If currentCollection IsNot Nothing Then
                    Dim propertyInfo As PropertyInfo = collectionField.PropertyType.GetProperty(CollectionCountPropertyName)
                    Dim value As Object = propertyInfo.GetValue(currentCollection)

                    If TypeOf value Is Integer _
                                AndAlso DirectCast(value, Integer) <= 0 Then
                        collectionField.SetValue(pSource, Nothing)
                    End If
                End If
            Next
        End Sub












        Friend Shared Function GetPocoEntity(Of T As Class)(pSource As T, Optional pContextEntityType As Type = Nothing, Optional pCircularReferences As ArrayList = Nothing, Optional pWithDataRelation As Boolean = False) As T
            Dim result As T
            Dim entityType As Type = pSource.GetType()

            If pContextEntityType Is Nothing Then
                pContextEntityType = ObjectContext.GetObjectType(entityType)
            End If

            If pContextEntityType Is entityType Then

                result = pSource
            Else
                result = ReflectionHelper.CloneEntity(pSource, pContextEntityType, pWithDataRelation)
            End If

            If pCircularReferences Is Nothing Then
                pCircularReferences = New ArrayList()
            End If

            CheckPocoEntityInChildren(result, pContextEntityType, pCircularReferences)

            Return result
        End Function








        Friend Shared Function GetFieldValue(pSource As IEntityObjectIdentifier, pEntityType As Type, pFieldName As String) As Object
            Dim value As Object = Nothing

            If pSource IsNot Nothing _
                     AndAlso pFieldName.IsNotNullOrEmpty() _
                     AndAlso IsValidEntityType(pEntityType) Then
                Dim fieldInfo As PropertyInfo = pEntityType.GetProperty(pFieldName, BindingFlags.Public Or BindingFlags.Instance)

                If Not fieldInfo Is Nothing Then
                    value = fieldInfo.GetValue(pSource, Nothing)
                End If
            End If

            Return value
        End Function








        Private Shared Sub CheckPocoEntityInChildren(Of T As Class)(pSource As T, pContextEntityType As Type, pCircularReferences As ArrayList)
            If Not pCircularReferences.Contains(pSource) Then
                pCircularReferences.Add(pSource)

                Dim properties As PropertyInfo() = pContextEntityType.GetProperties()

                CheckPocoEntityInEntityFields(pSource, properties, pCircularReferences)
                CheckPocoEntityInEntityCollections(pSource, properties, pCircularReferences)
            End If
        End Sub








        Private Shared Sub CheckPocoEntityInEntityFields(Of T As Class)(pSource As T, pProperties As PropertyInfo(), pCircularReferences As ArrayList)
            For Each entityField As PropertyInfo In ReflectionHelper.GetEntityClassProperties(pProperties)
                Dim currentEntityObject As Object = entityField.GetValue(pSource, Nothing)

                If currentEntityObject IsNot Nothing Then
                    Dim newEntityObject As Object = GetPocoEntity(currentEntityObject, Nothing, pCircularReferences, pWithDataRelation:=True)

                    If currentEntityObject IsNot newEntityObject Then
                        entityField.SetValue(pSource, newEntityObject, Nothing)
                    End If
                End If
            Next
        End Sub








        Private Shared Sub CheckPocoEntityInEntityCollections(Of T As Class)(pSource As T, pProperties As PropertyInfo(), pCircularReferences As ArrayList)
            For Each collectionField As PropertyInfo In ReflectionHelper.GetEntityCollectionProperties(pProperties)
                Dim currentCollection As Object = collectionField.GetValue(pSource, Nothing)

                If currentCollection IsNot Nothing Then
                    Dim removeList As ArrayList = Nothing
                    Dim addList As ArrayList = Nothing

                    For Each currentEntityObject As Object In DirectCast(currentCollection, IEnumerable)
                        Dim newEntityObject As Object = GetPocoEntity(currentEntityObject, Nothing, pCircularReferences, pWithDataRelation:=True)

                        If currentEntityObject IsNot newEntityObject Then
                            If removeList Is Nothing Then
                                removeList = New ArrayList()
                            End If

                            If addList Is Nothing Then
                                addList = New ArrayList()
                            End If

                            removeList.Add(currentEntityObject)
                            addList.Add(newEntityObject)
                        End If
                    Next

                    If removeList IsNot Nothing Then
                        Dim removeMethod As MethodInfo = collectionField.PropertyType.GetMethod(CollectionRemoveMethodName)
                        Dim addMethod As MethodInfo = collectionField.PropertyType.GetMethod(CollectionAddMethodName)

                        For i As Integer = 0 To removeList.Count - 1
                            removeMethod.Invoke(currentCollection, New Object() {removeList(i)})
                            addMethod.Invoke(currentCollection, New Object() {addList(i)})
                        Next
                    End If
                End If
            Next
        End Sub

    End Class

End Namespace
