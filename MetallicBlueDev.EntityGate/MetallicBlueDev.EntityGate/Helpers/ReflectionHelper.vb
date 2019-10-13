Imports System.Linq.Expressions
Imports System.Reflection
Imports MetallicBlueDev.EntityGate.GateException

Namespace Helpers




    Friend Class ReflectionHelper










        Friend Shared Function MakeInstance(Of T)(pType As Type, ByVal ParamArray pArgs() As Object) As T
            Dim rslt As T = Nothing

            If Not pType Is Nothing Then

                Dim rsltObject As Object

                Try
                    rsltObject = Activator.CreateInstance(pType, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic, Nothing, pArgs, Nothing)

                    If Not rsltObject Is Nothing Then

                        rslt = DirectCast(rsltObject, T)
                    End If
                Catch ex As Exception
                    Throw New ReflectionException("", ex)
                End Try
            End If

            If rslt Is Nothing Then
                Throw New ReflectionException("Unable to create object '" & GetType(T).Name & "' with type '" & pType?.Name & "'.")
            End If

            Return rslt
        End Function








        Friend Shared Function GetPropertyName(Of T)(pExp As Expression(Of Func(Of T, Object))) As String
            Dim propertyName As String = Nothing

            If pExp.Body.NodeType = ExpressionType.MemberAccess Then
                propertyName = DirectCast(pExp.Body, MemberExpression).Member.Name
            ElseIf pExp.Body.NodeType = ExpressionType.Call Then
                propertyName = DirectCast(pExp.Body, MethodCallExpression).Method.Name
            ElseIf pExp.Body.NodeType = ExpressionType.Convert Then
                Dim expression As Expression = DirectCast(pExp.Body, UnaryExpression).Operand

                If TypeOf expression Is MemberExpression Then
                    propertyName = DirectCast(expression, MemberExpression).Member.Name
                ElseIf TypeOf expression Is MethodCallExpression Then
                    propertyName = DirectCast(expression, MethodCallExpression).Method.Name
                End If
            End If

            If propertyName Is Nothing Then
                Throw New ReflectionException("Unable to get property name (" & pExp.ToString() & ").")
            End If

            Return propertyName
        End Function









        Friend Shared Function CloneEntity(Of T As Class)(pSource As T, pEntityType As Type, pWithDataRelation As Boolean) As T
            Dim result As T = Nothing

            If pSource IsNot Nothing _
                     AndAlso pEntityType IsNot Nothing Then
                result = ReflectionHelper.MakeInstance(Of T)(pEntityType)

                For Each info As PropertyInfo In GetReadWriteProperties(pEntityType, pWithDataRelation)
                    Dim valueObject As Object = info.GetValue(pSource, Nothing)
                    Dim currentValueObject As Object = info.GetValue(result, Nothing)

                    If Not valueObject Is Nothing _
                                 AndAlso Not valueObject.Equals(currentValueObject) Then
                        info.SetValue(result, valueObject, Nothing)
                    End If
                Next
            End If

            Return result
        End Function








        Friend Shared Function GetReadWriteProperties(pEntityType As Type, pWithDataRelation As Boolean) As IEnumerable(Of PropertyInfo)
            Return pEntityType.GetProperties() _
                    .Where(Function(pInfo) pInfo.CanRead _
                                           AndAlso pInfo.CanWrite _
                                           AndAlso (Not pInfo.PropertyType.IsClass OrElse pInfo.PropertyType Is GetType(String)) _
                                           AndAlso (pWithDataRelation OrElse Not GetType(IEnumerable).IsAssignableFrom(pInfo.PropertyType)))
        End Function






        Friend Shared Function GetEntityClassProperties(pProperties As PropertyInfo()) As IEnumerable(Of PropertyInfo)
            Return pProperties _
                    .Where(Function(pInfo) pInfo.CanRead _
                                           AndAlso pInfo.CanWrite _
                                           AndAlso (pInfo.PropertyType.IsClass AndAlso Not pInfo.PropertyType Is GetType(String)))
        End Function






        Friend Shared Function GetEntityCollectionProperties(pProperties As PropertyInfo()) As IEnumerable(Of PropertyInfo)
            Return pProperties _
                    .Where(Function(pInfo) pInfo.CanRead _
                                           AndAlso pInfo.CanWrite _
                                           AndAlso Not pInfo.PropertyType.IsClass _
                                           AndAlso GetType(IEnumerable).IsAssignableFrom(pInfo.PropertyType))
        End Function

    End Class

End Namespace
