Imports System.Data.SqlClient
Imports System.Reflection
Imports log4net
Imports MetallicBlueDev.EntityGate.Extensions
Imports MetallicBlueDev.EntityGate.Gate

Namespace Configuration





    <Serializable()>
    Public NotInheritable Class ClientConfiguration

        Private Shared ReadOnly Logger As ILog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType)

        Private ReadOnly mGate As IEntityGate

        Private mUpdated As Boolean = False
        Private mMaximumNumberOfAttempts As Integer = 0
        Private mAttemptDelay As Integer = 0
        Private mConnectionString As String = Nothing
        Private mTimeout As Integer = 0







        Public ReadOnly Property Updated As Boolean
            Get
                Return mUpdated
            End Get
        End Property







        Public Property MaximumNumberOfAttempts As Integer
            Get
                Return mMaximumNumberOfAttempts
            End Get
            Set(value As Integer)
                If value > 0 Then
                    mMaximumNumberOfAttempts = value
                    ConfigurationUpdated()
                End If
            End Set
        End Property







        Public Property AttemptDelay As Integer
            Get
                Return mAttemptDelay
            End Get
            Set(value As Integer)
                If value > 0 Then
                    mAttemptDelay = value
                    ConfigurationUpdated()
                End If
            End Set
        End Property







        Public Property ConnectionString As String
            Get
                Return mConnectionString
            End Get
            Set(value As String)
                If value.IsNotNullOrEmpty() Then
                    mConnectionString = value
                    ConfigurationUpdated()
                End If
            End Set
        End Property





        Public Property Timeout As Integer
            Get
                Return mTimeout
            End Get
            Set(value As Integer)
                If value > 3 Then
                    mTimeout = value
                    ConfigurationUpdated()
                End If
            End Set
        End Property





        Friend Sub New(gate As IEntityGate)
            mGate = gate

            Dim defaultConfig As DataSetConfiguration.EntityGateConfigRow = GlobalConfiguration.GetFirstConfig()

            If defaultConfig IsNot Nothing Then
                ChangeConnectionString(defaultConfig.ConnectionName)
            End If
        End Sub






        Public Sub ChangeConnectionString(pConnectionName As String)
            Dim currentConfig As DataSetConfiguration.EntityGateConfigRow = GlobalConfiguration.GetConfig(pConnectionName)

            If currentConfig IsNot Nothing Then
                MaximumNumberOfAttempts = currentConfig.MaximumNumberOfAttempts
                AttemptDelay = currentConfig.AttemptDelay
                ConnectionString = GlobalConfiguration.GetConnectionString(currentConfig.ConnectionName)
            ElseIf mGate.CanUseLogging Then
                Logger.WarnFormat("Unable to find connectionString '{0}'.", pConnectionName)
            End If
        End Sub





        Friend Sub Update(pConnection As IDbConnection)
            pConnection.ConnectionString = ConnectionString

            ConfigurationUpToDate()
        End Sub





        Friend Sub Update(pSqlBuilder As SqlConnectionStringBuilder)
            pSqlBuilder.ConnectTimeout = Timeout
            pSqlBuilder.ConnectionString = ConnectionString

            ConfigurationUpToDate()
        End Sub




        Friend Sub ConfigurationUpdated()
            mUpdated = True
        End Sub





        Private Sub ConfigurationUpToDate()
            mUpdated = False
        End Sub

    End Class

End Namespace
