Imports System.Data.Entity
Imports System.Data.Entity.Core
Imports System.Data.SqlClient
Imports System.Runtime.Serialization
Imports System.Threading
Imports log4net
Imports MetallicBlueDev.EntityGate.Configuration
Imports MetallicBlueDev.EntityGate.Extensions
Imports MetallicBlueDev.EntityGate.GateException
Imports MetallicBlueDev.EntityGate.Helpers
Imports MetallicBlueDev.EntityGate.InterfacedObject
Imports MetallicBlueDev.EntityGate.Provider
Imports MetallicBlueDev.EntityGate.Tracking

Namespace Gate








    <Serializable()>
    Public MustInherit Class EntityGateCore(Of TEntity As {Class, IEntityObjectIdentifier}, TContext As DbContext)
        Implements IEntityGate

        Protected Shared ReadOnly Logger As ILog = LogManager.GetLogger(Reflection.MethodBase.GetCurrentMethod().DeclaringType)

        Private mPrimaryKeys As IDictionary(Of String, Object) = Nothing
        Private mOriginalValues As KeyValuePair(Of String, Object)() = Nothing
        Private mAutoSaveOriginalValues As Boolean? = Nothing
        Private mCanAppendContext As Boolean = False

        Private mProvider As EntityGateProvider(Of TContext) = Nothing
        Private mCanceled As Boolean = False
        Private mDisposed As Boolean = False
        Private mNumberOfAttempts As Integer = 0
        Private mNumberOfRows As Integer = -1
        Private mSqlStatement As String = Nothing
        Private mCanUseLogging As Boolean = False

        <NonSerialized()>
        Private mEntity As TEntity = Nothing


        Public ReadOnly Property Configuration As ClientConfiguration Implements IEntityGate.Configuration


        Public ReadOnly Property NumberOfAttempts As Integer Implements IEntityGate.NumberOfAttempts
            Get
                Return mNumberOfAttempts
            End Get
        End Property


        Public ReadOnly Property NumberOfRows As Integer Implements IEntityGate.NumberOfRows
            Get
                Return mNumberOfRows
            End Get
        End Property


        Public Property SqlStatement As String Implements IEntityGate.SqlStatement
            Get
                Dim query As String = mSqlStatement

                If query Is Nothing Then
                    query = String.Empty
                End If

                Return query
            End Get
            Set(value As String)
                mSqlStatement = value
            End Set
        End Property


        Public Property AllowedSaving As Boolean Implements IEntityGate.AllowedSaving


        Public Property CanThrowException As Boolean = True Implements IEntityGate.CanThrowException


        Public Property CanUseLogging As Boolean Implements IEntityGate.CanUseLogging
            Get
                Return mCanUseLogging OrElse Logger.IsDebugEnabled
            End Get
            Set(value As Boolean)
                mCanUseLogging = value
            End Set
        End Property


        Public Property CanUseNotification As Boolean = True Implements IEntityGate.CanUseNotification







        Public Property Entity As TEntity
            Get
                Return GetOrCreateEntityObject()
            End Get
            Set(pValue As TEntity)
                SetEntityObject(pValue)
            End Set
        End Property






        Public ReadOnly Property HasEntityObject As Boolean Implements IEntityGate.HasEntityObject
            Get
                Return mEntity IsNot Nothing
            End Get
        End Property






        Public ReadOnly Property IsNewEntity As Boolean Implements IEntityGate.IsNewEntity
            Get
                Return Not mEntity.HasValidEntityKey()
            End Get
        End Property







        Public Property AutoSaveOriginalValues As Boolean
            Get
                Return mAutoSaveOriginalValues.HasValue AndAlso mAutoSaveOriginalValues.Value
            End Get
            Set(value As Boolean)
                If Not value _
                  AndAlso mAutoSaveOriginalValues Then
                    CleanOriginalValues()
                End If

                mAutoSaveOriginalValues = value
            End Set
        End Property





        Public Property IgnoreChildError As Boolean = False







        Public ReadOnly Property CurrentEntityObject As IEntityObjectIdentifier Implements IEntityGate.CurrentEntityObject
            Get
                Return Entity
            End Get
        End Property








        Friend Sub New(Optional pExternalEntity As TEntity = Nothing, Optional ByVal pConnectionName As String = Nothing)
            Configuration = New ClientConfiguration(Me)
            CanUseLogging = True

            If pConnectionName.IsNotNullOrEmpty() Then
                Configuration.ChangeConnectionString(pConnectionName)
            End If


            mCanAppendContext = True
            mEntity = pExternalEntity
        End Sub


        Public Sub CancelLastCommand() Implements IEntityGate.CancelLastCommand
            If Not mCanceled Then
                If CanUseLogging Then
                    Logger.Warn("Canceling last command...")
                End If

                mProvider.CancelLastCommand()
                mCanceled = True
            End If
        End Sub


        Public Sub Dispose() Implements IDisposable.Dispose
            If Not mDisposed Then
                DestroyProvider()
                mDisposed = True
            End If
        End Sub





        Public Sub NewEntity() Implements IEntityGate.NewEntity
            AllowedSaving = True
            ExecutionStart()

            Try
                MakeEntityObject()
            Catch ex As Exception
                LogFriendlyName("Failed to create", True)
                InternalError(ex, "Unable to create entity.", False)
            Finally
                ExecutionEnd()
            End Try
        End Sub












        Public Sub SetEntityObject(pEntity As IEntityObjectIdentifier) Implements IEntityGate.SetEntityObject
            ExecutionStart()

            FireSetEntityObject(pEntity)
        End Sub







        Public Function Load(Optional pKeyValue As Object = Nothing) As Boolean Implements IEntityGate.Load
            Dim loaded As Boolean = False
            AllowedSaving = False
            ExecutionStart()
            LogLoad(pKeyValue)

            Try
                loaded = LoadEntity(GetEntityObjectKey(pKeyValue))
            Catch ex As Exception
                LogFriendlyName("Failed to load", True)
                InternalError(ex, "Unable to load entity.", False)
            Finally
                ExecutionEnd()
            End Try

            Return loaded
        End Function









        Public Function List() As IEnumerable(Of TEntity)
            AllowedSaving = False
            ExecutionStart()

            LogFriendlyName("List", False)

            Dim rslt As IEnumerable(Of TEntity) = Nothing

            Try
                rslt = LoadEntitySet()
            Catch ex As Exception
                LogErrorModel("Failed to list")
                InternalError(ex, "Unable to list entities.", False)
            Finally
                ExecutionEnd()
            End Try

            Return rslt
        End Function








        Public Function ListEntities() As IEnumerable(Of IEntityObjectIdentifier) Implements IEntityGate.ListEntities
            Return List()
        End Function






        Public Function Save() As Boolean Implements IEntityGate.Save
            AllowedSaving = True
            ExecutionStart()
            LogSave()

            Try
                SaveEntity()
                ManageNotification()
            Catch ex As Exception
                LogErrorState()
                InternalError(ex, "Unable to save entity. " & GateHelper.GetContentInfo(mEntity), False)
            Finally
                ExecutionEnd()
            End Try

            Return (NumberOfRows > 0)
        End Function






        Public Function Delete() As Boolean Implements IEntityGate.Delete
            Delete(mEntity)

            Return Save()
        End Function







        Public Function Delete(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier Implements IEntityGate.Delete
            ExecutionStart()

            If Not HasEntityType() _
              AndAlso Not mProvider.HasCurrentEntityType() Then
                FireSetEntityObject(pEntity)
                pEntity = mEntity
            End If

            Return MarkAsDeleted(pEntity)
        End Function









        Public Function Apply(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier Implements IEntityGate.Apply
            ExecutionStart()

            If Not HasEntityType() _
              AndAlso Not mProvider.HasCurrentEntityType() Then
                FireSetEntityObject(pEntity)
            Else
                If pEntity.HasValidEntityKey() Then

                    pEntity = MarkAsModified(pEntity)
                Else

                    pEntity = MarkAsAdded(pEntity)
                End If
            End If

            Return pEntity
        End Function








        Public Function GetOriginalValues(Optional pAllProperties As Boolean = False) As KeyValuePair(Of String, Object)() Implements IEntityGate.GetOriginalValues
            Dim currentOriginalValues As KeyValuePair(Of String, Object)() = mOriginalValues

            If currentOriginalValues Is Nothing _
              AndAlso CanUseProvider() Then
                currentOriginalValues = If(IsNewEntity, New KeyValuePair(Of String, Object)() {}, mProvider.GetOriginalValues(mEntity, pAllProperties))

                If AutoSaveOriginalValues Then
                    mOriginalValues = currentOriginalValues
                End If
            End If

            Return currentOriginalValues
        End Function







        Public Function GetFieldValue(pFieldName As String) As Object Implements IEntityGate.GetFieldValue
            Dim value As Object = Nothing

            If HasEntityObject _
              AndAlso CanUseProvider() _
              AndAlso mProvider.HasCurrentEntityType() Then
                value = PocoHelper.GetFieldValue(mEntity, mProvider.GetCurrentEntityType(), pFieldName)
            End If

            Return value
        End Function






        Public Function GetTableName() As String Implements IEntityGate.GetTableName
            Dim name As String

            If CanUseProvider() _
              AndAlso mProvider.HasCurrentEntityType() Then
                name = mProvider.GetCurrentEntityType().Name
            ElseIf HasEntityObject Then
                name = mEntity.GetType().Name
            Else
                name = GetType(TEntity).Name
            End If

            Return name
        End Function







        Public Function GetFriendlyName() As String Implements IEntityGate.GetFriendlyName
            Dim fName As String

            If HasEntityObject Then
                fName = mEntity.GetEntityName()


                If fName Is Nothing Then
                    fName = GetPrimaryKeyFriendlyName()
                End If
            Else
                fName = "Virtual entity of " & GetTableName()
            End If

            Return fName
        End Function






        Public Function GetPrimaryKey() As KeyValuePair(Of String, Object) Implements IEntityGate.GetPrimaryKey
            Dim propKey As KeyValuePair(Of String, Object) = Nothing

            If HasEntityObject Then
                propKey = GateHelper.GetPrimaryKey(mEntity, If(CanUseProvider() AndAlso mProvider.HasCurrentEntityType(), mProvider.GetCurrentEntityType(), Nothing), GetPrimaryKeys())
            End If

            Return propKey
        End Function






        Public Function GetPrimaryKeys() As IEnumerable(Of KeyValuePair(Of String, Object))
            If mPrimaryKeys Is Nothing _
               AndAlso HasEntityObject Then
                mPrimaryKeys = GateHelper.GetPrimaryKeys(mEntity, If(CanUseProvider() AndAlso mProvider.HasCurrentEntityType(), mProvider.GetCurrentEntityType(), Nothing))

                If Not (mPrimaryKeys.Count > 0) Then
                    LogErrorModel("Entity key not found")
                End If
            End If

            Return mPrimaryKeys
        End Function





        Friend Function GetContext() As TContext
            ExecutionStart()


            mProvider.ChangeLazyLoading(False)


            mProvider.NoTracking()

            Return mProvider.Model
        End Function






        <OnSerializing()>
        Protected Sub OnSerializing(pContext As StreamingContext)
            If CanUseProvider() Then
                If HasEntityObject _
                  AndAlso Not mProvider.HasEntity(mEntity) Then
                    AppendEntity(mEntity)
                End If

                mProvider.ManagePocoEntitiesTracking()
                CheckAutoSaveOriginalValues()
            End If
        End Sub






        <OnDeserialized()>
        Protected Sub OnDeserialized(pContext As StreamingContext)
            If CanUseProvider() Then
                mEntity = mProvider.GetMainEntity(Of TEntity)()
            End If

            CreateProvider()
        End Sub





        Private Sub ExecutionStart()
            mNumberOfAttempts = 0
            mCanceled = False
            mNumberOfRows = -1

            CreateProvider()

            If Not mCanAppendContext Then
                mProvider.NoTracking()
            End If
        End Sub





        Private Sub ExecutionEnd()
            mProvider.EndAttempt()

            CleanOriginalValues()
        End Sub





        Private Sub PublishEvent()
            For Each tracking As EntityStateTracking In mProvider.GetChangedEntries()
                Dim gate As New EntityGate(Of IEntityObjectIdentifier)(tracking.EntityObject)
                gate.NoCache()


            Next
        End Sub




        Private Sub OnProviderAffected()
            Dim saveEntity As TEntity = mEntity


            mEntity = Nothing


            If PocoHelper.IsValidEntityType(saveEntity) Then

                AppendEntity(saveEntity)
            End If
        End Sub





        Private Sub SetNumberOfRows(pNumberOfRows As Integer)
            mNumberOfRows = pNumberOfRows
        End Sub





        Private Function CanUseProvider() As Boolean
            Return mProvider IsNot Nothing
        End Function




        Private Sub CreateProvider()
            mDisposed = False

            CheckProvider()
        End Sub




        Private Sub DestroyProvider()
            If Not mProvider Is Nothing Then
                mProvider.FreeMemory()
                mProvider = Nothing
            End If

            CleanPrimaryKeys()
        End Sub








        Private Function ExecutionAllowed(pEx As Exception, Optional ByVal pForRetry As Boolean = True) As Boolean
            If IsInvalidQuery(pEx) Then
                GateHelper.ExceptionMarker(pEx, Me)
                Throw pEx
            End If

            Dim isAllowed As Boolean = mNumberOfAttempts < Configuration.MaximumNumberOfAttempts


            If Not isAllowed _
              OrElse mCanceled Then
                ThrowStopExecution(pEx)
            Else
                If pForRetry Then
                    PreparingExecution()
                End If
            End If

            Return isAllowed
        End Function





        Private Function CanPublishEvent() As Boolean
            Return CanUseNotification _
                   AndAlso AllowedSaving _
                   AndAlso NumberOfRows > 0
        End Function




        Private Sub MakeProvider()
            mProvider = GetNewProvider()
            mProvider.Initialize()

            OnProviderAffected()
        End Sub






        Private Function GetNewProvider() As EntityGateProvider(Of TContext)
            Dim newProvider As New EntityGateProvider(Of TContext)(Me)

            If HasEntityType() Then
                newProvider.SetCurrentEntityType(GetType(TEntity))
            ElseIf HasEntityObject Then
                newProvider.SetCurrentEntityType(mEntity.GetType())
            End If

            Return newProvider
        End Function




        Private Sub CheckProvider()
            If Not CanUseProvider() Then
                MakeProvider()
            Else
                mProvider.Initialize()
            End If
        End Sub





        Private Sub ThrowStopExecution(pEx As Exception)
            If mCanceled Then

                If Not pEx Is Nothing Then

                    Throw pEx
                Else

                    Throw New TransactionCanceledException("Unhanded canceling operation.", pEx)
                End If
            Else

                GateHelper.ExceptionMarker(pEx, Me)


                Throw New TransactionCanceledException("Unable to execute " & Me.GetType.Name & " command after " & mNumberOfAttempts & " retry.", pEx)
            End If
        End Sub




        Private Sub PreparingExecution()
            If mNumberOfAttempts > 0 Then
                Thread.Sleep(Configuration.AttemptDelay)
            End If

            PreparingNextAttempt()


            mNumberOfAttempts += 1
        End Sub






        Private Function IsInvalidQuery(pEx As Exception) As Boolean
            Dim rslt As Boolean = False

            If pEx IsNot Nothing _
              AndAlso TypeOf pEx Is SqlException Then
                Select Case DirectCast(pEx, SqlException).Number
                    Case 102, 107, 170, 207, 208, 242, 547, 2705, 2812, 3621, 8152
                        rslt = True
                End Select
            End If

            Return rslt
        End Function





        Private Sub PreparingNextAttempt()
            If NumberOfAttempts > 0 _
              AndAlso CanUseLogging Then
                Logger.WarnFormat("New attempt ({1}/{2}: {3} seconds).", NumberOfAttempts, Configuration.MaximumNumberOfAttempts, Configuration.Timeout)
                Logger.InfoFormat("Query {0}", GetQueryInfo())
            End If

            mProvider.PreparingNextAttempt()
        End Sub





        Private Function GetQueryInfo() As String
            Return String.Format("query: {0}.", SqlStatement)
        End Function





        Private Sub ManageNotification()
            If CanPublishEvent() Then
                PublishEvent()
            End If
        End Sub




        Private Sub LogSave()
            If CanUseLogging Then
                Dim state As EntityState = GetEntityState()

                If state = EntityState.Deleted Then
                    Logger.InfoFormat("Deleting '{0}' '{1}'.", GetTableName(), GetFriendlyName())
                Else
                    Logger.InfoFormat("Saving '{0}' '{1}' in state '{2}'.", GetTableName(), GetFriendlyName(), state.ToString())
                End If
            End If
        End Sub





        Private Sub LogLoad(pKeyValue As Object)
            If CanUseLogging Then
                Logger.InfoFormat("Loading entity '{0}' ('{1}').", GetTableName(), If(pKeyValue, GetFriendlyName()))
            End If
        End Sub




        Private Sub LogErrorState()
            If CanUseLogging Then
                LogFriendlyName($"Error while saving state '{GetEntityState()}'", True)
            End If
        End Sub






        Private Sub LogFriendlyName(pMessage As String, pIsError As Boolean)
            If CanUseLogging Then
                If pIsError Then
                    Logger.FatalFormat("{0} entity '{1}' ('{2}').", pMessage, GetTableName(), GetFriendlyName())
                Else
                    Logger.InfoFormat("{0} entity '{1}' ('{2}').", pMessage, GetTableName(), GetFriendlyName())
                End If
            End If
        End Sub





        Private Sub LogErrorModel(pMessage As String)
            If CanUseLogging Then
                Logger.FatalFormat("{0} entity '{1}' ('{2}').", pMessage, GetTableName(), mProvider.GetModelName())
            End If
        End Sub





        Private Sub FireSetEntityObject(pEntity As IEntityObjectIdentifier)
            If pEntity Is Nothing _
               OrElse Not (TypeOf pEntity Is TEntity) Then
                Throw New EntityGateException("Can not handle this entity type.")
            End If

            AppendEntity(DirectCast(pEntity, TEntity))
        End Sub






        Private Function MarkAsDeleted(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier
            Try
                pEntity = mProvider.GetManagedOrPocoEntity(pEntity, Nothing)
                mProvider.ManageEntity(pEntity, Nothing, EntityState.Deleted)
            Catch ex As Exception
                InternalError(ex, GateHelper.GetContentInfo(pEntity), True)
            End Try

            Return pEntity
        End Function






        Private Function MarkAsAdded(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier
            Try
                pEntity = mProvider.GetManagedOrPocoEntity(pEntity, Nothing)
                mProvider.ManageEntity(pEntity, Nothing, EntityState.Added)
            Catch ex As Exception
                InternalError(ex, GateHelper.GetContentInfo(pEntity), True)
            End Try

            Return pEntity
        End Function






        Private Function MarkAsModified(pEntity As IEntityObjectIdentifier) As IEntityObjectIdentifier
            Try
                pEntity = mProvider.GetManagedOrPocoEntity(pEntity, Nothing)
                mProvider.ManageEntity(pEntity, Nothing, EntityState.Modified)
            Catch ex As Exception
                InternalError(ex, GateHelper.GetContentInfo(pEntity), True)
            End Try

            Return pEntity
        End Function






        Private Function GetOrCreateEntityObject() As TEntity
            If Not HasEntityObject Then
                ExecutionStart()
                MakeEntityObject()
            End If

            Return mEntity
        End Function




        Private Sub MakeEntityObject()
            Dim newEntity As TEntity = ReflectionHelper.MakeInstance(Of TEntity)(mProvider.GetCurrentEntityType())
            AffectEntity(newEntity)
        End Sub






        Private Function GetEntityObjectKey(pKeyValue As Object) As Object
            If pKeyValue Is Nothing Then
                If Not HasEntityObject Then
                    Throw New EntityGateException("Entity key not set.")
                End If

                pKeyValue = mEntity.Identifier
            End If

            Return pKeyValue
        End Function




        Private Sub NoCache()
            mCanAppendContext = False

            If CanUseProvider() Then
                mProvider.NoTracking()
            End If

            CleanOriginalValues()
        End Sub





        Private Function LoadEntity(pKeyValue As Object) As Boolean
            Dim loaded As Boolean = False

            While ExecutionAllowed(Nothing)
                Dim value As TEntity = mEntity

                Try
                    If mProvider.TryGetEntity(pKeyValue, value) Then
                        AppendEntity(value)
                        loaded = True
                        Exit While
                    Else
                        LogFriendlyName("Entity key not found for", False)
                        Exit While
                    End If
                Catch ex As Exception
                    ExecutionAllowed(ex, False)
                End Try
            End While

            Return loaded
        End Function






        Private Function LoadEntitySet() As IEnumerable(Of TEntity)
            Dim rslt As IEnumerable(Of TEntity) = Nothing

            While ExecutionAllowed(Nothing)
                Try
                    rslt = mProvider.MakeObjectSet(Of TEntity)()
                    Exit While
                Catch ex As Exception
                    ExecutionAllowed(ex, False)
                End Try
            End While

            Return rslt
        End Function





        Private Sub SaveEntity()
            While ExecutionAllowed(Nothing)
                Try
                    AppendEntity(mEntity)

                    If mProvider.HasChanges() Then
                        Dim oldState As EntityState = GetEntityState()
                        Dim numberOfRowsChanged As Integer = mProvider.SaveChanges()
                        RefreshAfterSave(numberOfRowsChanged, oldState)
                    End If
                    Exit While
                Catch ex As UpdateException
                    If TypeOf ex Is OptimisticConcurrencyException Then

                        mProvider.RefreshChanges(mEntity)
                    End If

                    ExecutionAllowed(ex, False)
                End Try
            End While
        End Sub






        Private Sub RefreshAfterSave(pNumberOfRowsChanged As Integer, pOldState As EntityState)
            Select Case pOldState
                Case EntityState.Added
                    RefreshPrimaryKey()

                Case EntityState.Deleted
                    If Not (pNumberOfRowsChanged > 0) Then
                        pNumberOfRowsChanged = 1
                    End If

            End Select

            SetNumberOfRows(pNumberOfRowsChanged)
        End Sub





        Private Function GetEntityState() As EntityState
            Return If(HasEntityObject AndAlso CanUseProvider(), mProvider.GetEntityState(mEntity), EntityState.Unchanged)
        End Function






        Private Function GetPrimaryKeyFriendlyName() As String
            Dim indexPair As KeyValuePair(Of String, Object) = GetPrimaryKey()
            Dim keyFriendlyName As Object

            If IsNewEntity Then
                keyFriendlyName = "NewKey"
            Else
                keyFriendlyName = indexPair.Value
            End If

            Return String.Format("{0}={1}", indexPair.Key, keyFriendlyName)
        End Function





        Private Sub CleanOriginalValues()
            If mOriginalValues IsNot Nothing Then
                mOriginalValues = Nothing
            End If
        End Sub




        Private Sub CleanPrimaryKeys()
            If mPrimaryKeys IsNot Nothing Then
                mPrimaryKeys.Clear()
                mPrimaryKeys = Nothing
            End If
        End Sub






        Private Shared Function HasEntityType() As Boolean
            Return PocoHelper.IsValidEntityType(GetType(TEntity))
        End Function





        Private Sub RefreshPrimaryKey()
            If HasEntityType() Then
                GateHelper.RefreshPrimaryKey(mPrimaryKeys, mEntity)
            Else

                CleanPrimaryKeys()
            End If
        End Sub





        Private Sub AppendEntity(pNextEntity As TEntity)
            If pNextEntity Is Nothing Then
                Throw New EntityGateException("Invalid entity instance.")
            End If

            If pNextEntity IsNot mEntity Then
                AffectEntity(pNextEntity)
            End If


            mProvider.ManageEntity(mEntity)
        End Sub






        Private Sub AffectEntity(pExternalEntity As TEntity)

            mProvider.SetCurrentEntityType(pExternalEntity.GetType())
            mEntity = DirectCast(mProvider.GetManagedOrPocoEntity(pExternalEntity, mProvider.GetCurrentEntityType()), TEntity)


            RefreshPrimaryKey()
        End Sub





        Private Sub CheckAutoSaveOriginalValues()
            If mCanAppendContext Then

                If Not mAutoSaveOriginalValues.HasValue _
                  AndAlso TypeOf mEntity Is IEntityObjectArchival Then
                    mAutoSaveOriginalValues = True
                End If


                If AutoSaveOriginalValues _
                  AndAlso mOriginalValues Is Nothing Then
                    mOriginalValues = GetOriginalValues()
                End If
            End If
        End Sub







        Private Sub InternalError(pLastError As Exception, pMessage As String, pIsChild As Boolean)
            Dim ignoreError As Boolean = pIsChild AndAlso IgnoreChildError

            If Not ignoreError Then
                GateHelper.ExceptionMarker(pLastError, Me)
            End If

            If Not ignoreError _
              AndAlso CanUseLogging Then
                If String.IsNullOrEmpty(pMessage) Then
                    pMessage = "Internal error."
                End If

                Logger.Error(pMessage, pLastError)
            ElseIf Logger.IsDebugEnabled Then
                Logger.Debug(pMessage, pLastError)
            End If

            If Not ignoreError _
              AndAlso CanThrowException _
              AndAlso Not pLastError Is Nothing Then
                Throw pLastError
            End If
        End Sub

    End Class

End Namespace
