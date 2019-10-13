Imports System.Data.Common
Imports System.Data.Entity
Imports System.Data.Entity.Core
Imports System.Data.Entity.Core.EntityClient
Imports System.Data.Entity.Core.Objects
Imports System.Data.Entity.Infrastructure
Imports System.Data.SqlClient
Imports System.Reflection
Imports log4net
Imports MetallicBlueDev.EntityGate.Extensions
Imports MetallicBlueDev.EntityGate.Gate
Imports MetallicBlueDev.EntityGate.GateException
Imports MetallicBlueDev.EntityGate.Helpers
Imports MetallicBlueDev.EntityGate.InterfacedObject
Imports MetallicBlueDev.EntityGate.Tracking

Namespace Provider

    <Serializable()>
    Friend NotInheritable Class EntityGateProvider(Of TContext As DbContext)
        Implements IDisposable

        Protected Shared ReadOnly Logger As ILog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType)

        Private ReadOnly mGate As IEntityGate
        Private ReadOnly mTracking As EntityGateTracking

        <NonSerialized()>
        Private mConnection As IDbConnection = Nothing

        Private mDisposed As Boolean = False
        Private mCurrentEntityType As Type = Nothing
        Private mLazyLoading As Boolean = False

        <NonSerialized()>
        Private mModel As TContext = Nothing

        <NonSerialized()>
        Private mToken As EntityGateToken = Nothing

        Friend ReadOnly Property Model As TContext
            Get
                Return mModel
            End Get
        End Property

        Friend Sub New(gate As IEntityGate)
            mGate = gate
            mTracking = New EntityGateTracking()


            mLazyLoading = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If Not mDisposed Then
                FreeMemory()
                mDisposed = True
            End If
        End Sub





        Friend Sub Initialize()
            If mModel Is Nothing _
              OrElse mGate.Configuration.Updated Then
                RemoveHandlers()


                mModel = MakeModel()
                mToken = MakeToken()

                InitializeModel()

                AddHandlers()
            End If
        End Sub





        Friend Sub SetConnection(pConnection As IDbConnection)
            mConnection = pConnection

            CheckConnectionConfiguration()
        End Sub




        Friend Sub CheckConnectionConfiguration()
            If mGate.Configuration.Updated Then
                mGate.Configuration.Update(mConnection)
            End If
        End Sub




        Friend Sub FreeMemory()
            RemoveHandlers()

            If Not mModel Is Nothing Then
                mModel.Dispose()

                mToken = Nothing
                mModel = Nothing
            End If

            Close()
            mConnection = Nothing
        End Sub





        Friend Sub EndAttempt()
            mTracking.CleanTracking()
        End Sub




        Friend Sub NoTracking()
            mTracking.CleanTracking()
            mToken.IsTracked = False
            mToken.IsManaged = False
        End Sub






        Friend Function GetEntityState(pEntity As IEntityObjectIdentifier) As EntityState
            Return GetEntityEntry(pEntity).State
        End Function






        Friend Sub SetCurrentEntityType(pEntityType As Type)
            If PocoHelper.IsValidEntityType(pEntityType) _
              AndAlso mCurrentEntityType IsNot pEntityType Then
                mCurrentEntityType = ObjectContext.GetObjectType(pEntityType)

                If Logger.IsDebugEnabled Then
                    Logger.DebugFormat("Current entity type is '{0}'.", mCurrentEntityType)
                End If

                If Not mCurrentEntityType.IsSerializable Then
                    Throw New EntityGateException($"Entity type '{mCurrentEntityType.Name}' is not serializable.")
                End If
            End If
        End Sub






        Friend Function GetCurrentEntityType() As Type
            If mCurrentEntityType Is Nothing Then

                Throw New EntityGateException("Current entity type is undefined.")
            End If

            Return mCurrentEntityType
        End Function




        Friend Sub ManagePocoEntitiesTracking()
            Dim isProxyCreationEnabled As Boolean = mModel.Configuration.ProxyCreationEnabled
            mModel.Configuration.ProxyCreationEnabled = False

            Try
                FireManagePocoEntitiesTracking()
            Catch ex As Exception
                Logger.Fatal("Fail to track POCO entities.", ex)
                Throw
            Finally
                mModel.Configuration.ProxyCreationEnabled = isProxyCreationEnabled
            End Try
        End Sub





        Friend Function GetMainEntity(Of TEntity As {Class, IEntityObjectIdentifier})() As TEntity
            Dim mainEntity As IEntityObjectIdentifier = mTracking.GetMainEntity()
            Return DirectCast(mainEntity, TEntity)
        End Function






        Friend Function HasCurrentEntityType() As Boolean
            Return Not mCurrentEntityType Is Nothing
        End Function






        Friend Function GetModelName() As String
            Return mModel.GetObjectContext().DefaultContainerName
        End Function






        Friend Sub ChangeLazyLoading(pEnabled As Boolean)
            mLazyLoading = pEnabled

            If Logger.IsDebugEnabled Then
                Logger.DebugFormat("Lazy loading is '{0}'.", pEnabled)
            End If

            If pEnabled AndAlso
               Not mModel.Configuration.ProxyCreationEnabled Then
                Throw New EntityGateException("Proxy creation must be enabled for use lazy loading.")
            End If

            mModel.Configuration.LazyLoadingEnabled = pEnabled
        End Sub







        Friend Function MakeObjectSet(Of TEntity As {Class, IEntityObjectIdentifier})() As DbSet(Of TEntity)
            Dim currentDbSet As DbSet(Of TEntity)

            If PocoHelper.IsValidEntityType(GetType(TEntity)) Then
                currentDbSet = mModel.Set(Of TEntity)()
            Else
                currentDbSet = mModel.Set(GetCurrentEntityType()).Cast(Of TEntity)()
            End If

            Return currentDbSet
        End Function







        Friend Function HasEntity(pEntity As IEntityObjectIdentifier) As Boolean
            Return TryGetEntityStateEntry(pEntity, Nothing)
        End Function






        Friend Function HasChanges() As Boolean
            Return mModel.ChangeTracker.HasChanges()
        End Function





        Friend Function GetChangedEntries() As IEnumerable(Of EntityStateTracking)
            Return mTracking.GetEntities() _
              .Where(Function(pEntity) pEntity.State <> EntityState.Unchanged AndAlso pEntity.State <> EntityState.Detached)
        End Function








        Friend Function TryGetEntity(Of TEntity As {Class, IEntityObjectIdentifier})(pKeyValue As Object, ByRef pEntity As TEntity) As Boolean
            If PocoHelper.IsValidEntityType(GetType(TEntity)) Then
                pEntity = mModel.Set(Of TEntity)().Find(pKeyValue)
            Else
                pEntity = DirectCast(mModel.Set(GetCurrentEntityType()).Find(pKeyValue), TEntity)
            End If

            Return pEntity IsNot Nothing
        End Function







        Friend Function GetManagedOrPocoEntity(pExternalEntity As IEntityObjectIdentifier, pContextEntityType As Type) As IEntityObjectIdentifier
            If Not HasEntity(pExternalEntity) Then

                Dim entityTracked As IEntityObjectIdentifier = GetEntityTracked(pExternalEntity, True)

                If entityTracked IsNot Nothing Then

                    pExternalEntity = entityTracked
                Else

                    pExternalEntity = PocoHelper.GetPocoEntity(pExternalEntity, pContextEntityType, pWithDataRelation:=True)
                End If
            End If

            Return pExternalEntity
        End Function





        Friend Sub ManageEntity(pEntity As IEntityObjectIdentifier)
            Dim currentState As EntityState = GetEntityState(pEntity)
            Dim targetState As EntityState = GetEntityStateTargeted(pEntity, currentState)
            ManageEntity(pEntity, currentState, targetState)
        End Sub






        Friend Sub ManageEntity(pEntity As IEntityObjectIdentifier, pCurrentState As EntityState?, pTargetState As EntityState)
            If Logger.IsDebugEnabled Then
                Logger.DebugFormat("Apply entity '{0}' to state '{1}'.", pEntity, pTargetState)
            End If

            If pTargetState = EntityState.Detached Then
                Throw New EntityGateException($"Unexpected entity state '{pTargetState}' for '{pEntity}'")
            End If


            If Not pCurrentState.HasValue _
              AndAlso pTargetState <> EntityState.Added Then
                pCurrentState = GetEntityState(pEntity)
            End If


            If pCurrentState.HasValue _
              AndAlso pCurrentState = EntityState.Detached Then
                Dim entityTracked As IEntityObjectIdentifier = GetEntityTracked(pEntity, pTargetState <> EntityState.Deleted)
                pEntity = If(entityTracked, pEntity)
            End If

            GetEntityEntry(pEntity).State = pTargetState
        End Sub





        Friend Function SaveChanges() As Integer
            DetectChanges()
            DetectLocalMode()

            If Not mTracking.HasEntities() Then
                Throw New EntityGateException("There is no tracked entity.")
            End If

            If Logger.IsDebugEnabled Then
                Logger.DebugFormat("Save changes.")
            End If

            Return mModel.SaveChanges()
        End Function






        Friend Sub RefreshChanges(pEntity As IEntityObjectIdentifier)
            If pEntity Is Nothing Then
                Throw New EntityGateException("Invalid entity for refresh changes.")
            End If

            mModel.GetObjectContext().Refresh(RefreshMode.ClientWins, pEntity)
        End Sub








        Friend Function GetOriginalValues(pEntity As IEntityObjectIdentifier, pAllProperties As Boolean) As KeyValuePair(Of String, Object)()
            Dim values As New List(Of KeyValuePair(Of String, Object))()
            Dim stateEntry As ObjectStateEntry = GetEntityStateEntry(pEntity)
            Dim modifiedProperties As String() = If(Not pAllProperties, stateEntry.GetModifiedProperties().ToArray(), Nothing)
            Dim originalValues As DbDataRecord = stateEntry.OriginalValues

            For i As Integer = 0 To originalValues.FieldCount - 1
                Dim fieldName As String = originalValues.GetName(i)

                If Not modifiedProperties Is Nothing _
                  AndAlso Not modifiedProperties.Any(Function(pName) pName = fieldName) Then
                    Continue For
                End If

                values.Add(New KeyValuePair(Of String, Object)(originalValues.GetName(i), originalValues.GetValue(i)))
            Next

            Return values.ToArray()
        End Function






        Friend Sub PreparingNextAttempt()
            mDisposed = False
        End Sub





        Friend Sub CancelLastCommand()
        End Sub





        Private Sub Close()
            If Not mConnection Is Nothing Then
                Try
                    mConnection.Close()
                Catch ex As Exception
                    If mGate.CanUseLogging Then
                        Logger.Error("Error on closing connection.", ex)
                    End If
                End Try

                SafeDispose(mConnection)



                mGate.Configuration.ConfigurationUpdated()
            End If
        End Sub





        Private Sub SafeDispose(pObject As IDisposable)
            If Not pObject Is Nothing Then
                Try
                    pObject.Dispose()
                Catch ex As Exception
                    If mGate.CanUseLogging Then
                        Logger.Error("Dispose error " & pObject.ToString() & ".", ex)
                    End If
                End Try
            End If
        End Sub







        Private Function GetEntityStateTargeted(pEntity As IEntityObjectIdentifier, pCurrentState As EntityState) As EntityState
            Dim targetState As EntityState = pCurrentState
            Dim currentKey As EntityKey = GetEntityKey(pEntity)


            If targetState <> EntityState.Added _
               OrElse Not HasEntityKey(currentKey) Then

                If Not pEntity.HasValidEntityKey() Then
                    targetState = EntityState.Added
                End If
            End If


            If targetState = EntityState.Detached Then
                targetState = EntityState.Modified
            End If

            Return targetState
        End Function




        Private Sub FireManagePocoEntitiesTracking()
            If mToken.IsTracked Then

                TrackPocoEntities()
            Else

                TrackMainPocoEntity()
            End If
        End Sub




        Private Sub TrackPocoEntities()
            Dim mainEntity As IEntityObjectIdentifier = mGate.CurrentEntityObject

            For Each entry As DbEntityEntry In GetEntriesTracked().Where(Function(pEntity) pEntity.State <> EntityState.Unchanged OrElse pEntity.Entity Is mainEntity)
                TrackPocoEntity(entry, entry.Entity Is mainEntity)
            Next
        End Sub




        Private Sub TrackMainPocoEntity()
            Dim entry As DbEntityEntry = GetEntityEntry(mGate.CurrentEntityObject)
            TrackPocoEntity(entry, True)
        End Sub






        Private Sub TrackPocoEntity(pEntry As DbEntityEntry, pIsMainEntry As Boolean)
            Dim pocoEntity As Object = If(pEntry.State = EntityState.Deleted, pEntry.OriginalValues.ToObject(), pEntry.CurrentValues.ToObject())
            mTracking.MarkEntity(pocoEntity, pEntry.State, pIsMainEntry)
        End Sub





        Private Function GetEntriesTracked() As IEnumerable(Of DbEntityEntry)
            Return mModel.ChangeTracker.Entries()
        End Function




        Private Sub DetectChanges()
            If Not mModel.Configuration.AutoDetectChangesEnabled Then
                If Logger.IsDebugEnabled Then
                    Logger.DebugFormat("Detect changes.")
                End If

                mModel.ChangeTracker.DetectChanges()
            End If
        End Sub




        Private Sub DetectLocalMode()
            If Not mTracking.HasEntities() Then

                ManagePocoEntitiesTracking()
            End If
        End Sub






        Private Function GetEntityStateEntry(pEntity As IEntityObjectIdentifier) As ObjectStateEntry
            Dim result As ObjectStateEntry = Nothing

            If Not TryGetEntityStateEntry(pEntity, result) _
              OrElse result Is Nothing Then
                If Logger.IsDebugEnabled Then


                    Logger.DebugFormat("Object state entry not found for {0}.", pEntity)
                End If
            End If

            Return result
        End Function







        Private Function TryGetEntityStateEntry(pEntity As IEntityObjectIdentifier, ByRef pEntry As ObjectStateEntry) As Boolean
            Return pEntity IsNot Nothing _
              AndAlso mModel.GetObjectContext() _
                     .ObjectStateManager _
                     .TryGetObjectStateEntry(pEntity, pEntry)
        End Function







        Private Function GetEntityTracked(pEntity As IEntityObjectIdentifier, pMergeValue As Boolean) As IEntityObjectIdentifier
            Dim trackedEntity As IEntityObjectIdentifier = Nothing
            Dim currentEntry As DbEntityEntry = GetEntityEntryTracked(pEntity)

            If currentEntry IsNot Nothing Then
                If pMergeValue Then
                    currentEntry.CurrentValues.SetValues(pEntity)
                End If

                trackedEntity = DirectCast(currentEntry.Entity, IEntityObjectIdentifier)
            End If

            Return trackedEntity
        End Function






        Private Function GetEntityEntryTracked(pEntity As IEntityObjectIdentifier) As DbEntityEntry
            Dim sourceType As Type = pEntity.GetType()
            Return GetEntriesTracked() _
              .FirstOrDefault(Function(pEntry) pEntry.Entity.GetType() Is sourceType _
                                               AndAlso DirectCast(pEntry.Entity, IEntityObjectIdentifier).Identifier.Equals(pEntity.Identifier))
        End Function






        Private Function GetEntityEntry(pEntity As Object) As DbEntityEntry
            If pEntity Is Nothing Then
                Throw New EntityGateException("Invalid entity for entry.")
            End If

            Dim entityEntry As DbEntityEntry = mModel.Entry(pEntity)

            If entityEntry Is Nothing Then
                Throw New EntityGateException($"Invalid entity entry for '{pEntity}'.")
            End If

            Return entityEntry
        End Function






        Private Function GetEntityKey(pEntity As IEntityObjectIdentifier) As EntityKey
            Dim result As EntityKey = Nothing
            Dim ose As ObjectStateEntry = GetEntityStateEntry(pEntity)

            If ose IsNot Nothing Then
                result = ose.EntityKey
            End If

            Return result
        End Function







        Private Function HasEntityKey(pEntityKey As EntityKey) As Boolean
            Return pEntityKey IsNot Nothing _
              AndAlso mModel.GetObjectContext() _
                     .ObjectStateManager _
                     .TryGetObjectStateEntry(pEntityKey, Nothing)
        End Function





        Private Sub InitializeModel()
            If mModel Is Nothing Then
                Throw New EntityGateException("Invalid context.")
            End If

            If Logger.IsDebugEnabled Then
                Logger.DebugFormat("Initializing model...")
            End If

            InitializeModelLog()
            InitializeModelConfiguration()
            InitializeModelWorkspace()

            ApplyTrackingIfNeeded()
        End Sub




        Private Sub ApplyTrackingIfNeeded()
            If mTracking.HasEntities() _
              AndAlso mToken.IsTracked Then
                If Logger.IsDebugEnabled Then
                    Logger.DebugFormat("Apply entity tracking...")
                End If

                Try
                    mToken.IsTracked = False
                    mTracking.UnloadEmptyEntityCollection()
                    ApplyTracking()
                Finally
                    mToken.IsTracked = True
                End Try
            End If
        End Sub




        Private Sub ApplyTracking()
            For Each stateTracking As EntityStateTracking In mTracking.GetEntities()
                ManageEntity(stateTracking.EntityObject, Nothing, stateTracking.State)
            Next
        End Sub




        Private Sub InitializeModelLog()
            mModel.Database.Log = AddressOf TraceEntityLog
        End Sub





        Private Sub TraceEntityLog(pMessage As String)
            If Logger.IsDebugEnabled Then
                Logger.Debug(pMessage)
            End If

            If pMessage.IsNotNullOrEmpty(4) Then
                If pMessage.IsMatch("ERROR|FATAL|EXCEPTION|CRASH") Then
                    Logger.Error(pMessage)
                End If

                If pMessage.IsMatch("SELECT|UPDATE|DELETE|INSERT") Then
                    mGate.SqlStatement = pMessage
                End If
            End If
        End Sub




        Private Sub InitializeModelConfiguration()
            Dim context As ObjectContext = mModel.GetObjectContext()

            mModel.Configuration.ProxyCreationEnabled = True
            context.CommandTimeout = Nothing ' TODO A VERIFIER

            ChangeLazyLoading(mLazyLoading)

            SetConnection(context.Connection)
        End Sub




        Private Sub InitializeModelWorkspace()
            Try
                mModel.GetObjectContext().MetadataWorkspace.LoadFromAssembly(mModel.GetType().Assembly)
            Catch ex As Exception
                Logger.Fatal("Unable to load metadata workspace for '" & GetModelName() & "'.", New EntityGateException("Metadata error.", ex))
                Throw New EntityGateException("Metadata error.", ex)
            End Try
        End Sub





        Private Sub AddHandlers()
            If Not mModel Is Nothing Then
                Dim context As ObjectContext = mModel.GetObjectContext()
                AddHandler context.SavingChanges, AddressOf Model_SavingChanges
            End If
        End Sub





        Private Sub RemoveHandlers()
            If Not mModel Is Nothing Then
                Dim context As ObjectContext = mModel.GetObjectContext()
                RemoveHandler context.SavingChanges, AddressOf Model_SavingChanges
            End If
        End Sub






        Private Function MakeModel() As TContext
            Dim rslt As TContext

            If Logger.IsDebugEnabled Then
                Logger.DebugFormat("Making a new model...")
            End If

            If HasCurrentEntityType() Then
                rslt = NewContextByEntityType()
            Else
                rslt = NewContextByInstance(GetType(TContext))
            End If

            If rslt Is Nothing Then
                Throw New EntityGateException("Failed to create DbContext for Entity Framework.")
            End If

            Return rslt
        End Function





        Private Shared Function MakeToken() As EntityGateToken
            Return New EntityGateToken()
        End Function






        Private Function NewContextByEntityType() As TContext
            Dim context As TContext = Nothing
            Dim safeEntityType As Type = GetCurrentEntityType()


            For Each currentType As Type In safeEntityType.Assembly.GetTypes().Where(Function(pContextType) ContextHelper.IsValidContext(Of TContext)(safeEntityType, pContextType))
                Try

                    context = NewContextByInstance(currentType)

                    If ContextHelper.IsContextRelativeToEntityType(context, safeEntityType) Then
                        Exit For
                    End If
                Catch ex As Exception
                    Logger.WarnFormat("Skip error '{0}'.", ex.Message)
                End Try
            Next

            If context Is Nothing Then
                Logger.FatalFormat("Fail to create context with entity type '{0}'.", safeEntityType.Name)
            End If

            Return context
        End Function







        Private Function NewContextByInstance(pContextType As Type) As TContext
            Dim context As TContext = Nothing

            Try
                For Each currentConstructorInfo As ConstructorInfo In pContextType.GetConstructors()
                    context = NewContextByInstance(pContextType, currentConstructorInfo)

                    If context IsNot Nothing Then
                        Exit For
                    End If
                Next
            Catch ex As Exception
                Logger.Fatal("Unable to create context object of '" & pContextType.Name & "' for Entity Framework.", ex)
            End Try

            If context Is Nothing Then
                Logger.FatalFormat("Fail to create context with type '{0}'.", pContextType.Name)
            End If

            Return context
        End Function







        Private Function NewContextByInstance(pContextType As Type, pConstructorInfo As ConstructorInfo) As TContext
            Dim context As TContext = Nothing
            Dim parameterInfos As ParameterInfo() = pConstructorInfo.GetParameters()

            If parameterInfos.Length = 1 Then
                Dim currentParameterInfo As ParameterInfo = DirectCast(parameterInfos.GetValue(0), ParameterInfo)

                If currentParameterInfo.ParameterType Is GetType(String) Then
                    context = DirectCast(pConstructorInfo.Invoke(New Object() {CreateEntityConnectionString(pContextType)}), TContext)
                End If
            End If

            Return context
        End Function







        Private Function CreateEntityConnectionString(pContextType As Type) As String
            Dim sqlBuilder As New SqlConnectionStringBuilder()
            sqlBuilder.IntegratedSecurity = False
            sqlBuilder.PersistSecurityInfo = True
            sqlBuilder.MultipleActiveResultSets = False

            mGate.Configuration.Update(sqlBuilder)

            Dim entityBuilder As New EntityConnectionStringBuilder()
            entityBuilder.ProviderConnectionString = sqlBuilder.ToString()
            entityBuilder.Provider = "System.Data.SqlClient"
            entityBuilder.Metadata = ContextHelper.GetMetadata(pContextType.Name, pContextType.Assembly.GetManifestResourceNames())

            Return entityBuilder.ToString()
        End Function

        Private Sub Model_SavingChanges(sender As Object, e As EventArgs)
            If mToken Is Nothing _
              OrElse Not mToken.IsManaged Then

                Throw New TransactionCanceledException("You must save your data with EntityGate.")
            End If
        End Sub

    End Class

End Namespace
