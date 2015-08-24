'*******************************************************************************************************
'  MultiProtocolFrameParser.vb - Protocol independent frame parser
'  Copyright � 2008 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2008
'  Primary Developer: J. Ritchie Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  03/16/2006 - J. Ritchie Carroll
'       Initial version of source generated
'  06/26/2006 - Pinal C. Patel
'       Changed out the socket code with TcpClient and UdpClient components from TVA.Communication
'  01/31/2007 - J. Ritchie Carroll
'       Added TCP "server" support to allow listening connections from devices that act as data
'       clients, e.g., FNET devices
'  05/23/2007 - Pinal C. Patel
'       Added member variable 'm_clientConnectionAttempts' to track the number of attempts made for 
'       connecting to the server since this information is no longer provided by the event raised by 
'       any of the Communication Client components
'  07/05/2007 - J. Ritchie Carroll
'       Wrapped all event raising for frame parsing in Try/Catch so that any exceptions thrown in
'       consumer event handlers won't have a negative effect on continuous data parsing - exceptions
'       in consumer event handlers are duly noted and raised through the DataStreamException event
'  09/28/2007 - J. Ritchie Carroll
'       Implemented new disconnect overload on communications client that allows timeout on socket
'       close to fix an issue related non-responsive threads that "lock-up" after sending connection
'       commands that attempt to close the socket for remotely connected devices
'  12/14/2007 - J. Ritchie Carroll
'       Implemented simulated timestamp injection for published frames to allow for real-time
'       data simulations from archived sample data
'
'*******************************************************************************************************

Imports System.IO
Imports System.Text
Imports System.Net
Imports System.Threading
Imports System.ComponentModel
Imports TVA.Collections
Imports TVA.DateTime.Common
Imports TVA.Communication
Imports TVA.Communication.Common
Imports TVA.IO.Common
Imports TVA.Text.Common
Imports PhasorProtocols

''' <summary>Protocol independent frame parser</summary>
<CLSCompliant(False)> _
Public Class MultiProtocolFrameParser

    Implements IFrameParser

#Region " Public Member Declarations "

    Public Event ReceivedConfigurationFrame(ByVal frame As IConfigurationFrame) Implements IFrameParser.ReceivedConfigurationFrame
    Public Event ReceivedDataFrame(ByVal frame As IDataFrame) Implements IFrameParser.ReceivedDataFrame
    Public Event ReceivedHeaderFrame(ByVal frame As IHeaderFrame) Implements IFrameParser.ReceivedHeaderFrame
    Public Event ReceivedCommandFrame(ByVal frame As ICommandFrame) Implements IFrameParser.ReceivedCommandFrame
    Public Event ReceivedUndeterminedFrame(ByVal frame As IChannelFrame) Implements IFrameParser.ReceivedUndeterminedFrame
    Public Event ReceivedFrameBufferImage(ByVal frameType As FundamentalFrameType, ByVal binaryImage As Byte(), ByVal offset As Integer, ByVal length As Integer) Implements IFrameParser.ReceivedFrameBufferImage
    Public Event SentCommandFrame(ByVal frame As ICommandFrame)
    Public Event ConfigurationChanged() Implements IFrameParser.ConfigurationChanged
    Public Event DataStreamException(ByVal ex As Exception) Implements IFrameParser.DataStreamException
    Public Event ConnectionException(ByVal ex As Exception, ByVal connectionAttempts As Integer)
    Public Event AttemptingConnection()
    Public Event Connected()
    Public Event Disconnected()
    Public Event ServerStarted()
    Public Event ServerStopped()

    Public Const DefaultBufferSize As Int32 = 262144    ' 256K
    Public Const DefaultFrameRate As Double = 1 / 30

#End Region

#Region " Private Member Declarations "

    ' Connection properties
    Private m_phasorProtocol As PhasorProtocol
    Private m_transportProtocol As TransportProtocol
    Private m_connectionString As String
    Private m_maximumConnectionAttempts As Integer
    Private m_deviceID As UInt16
    Private m_bufferSize As Int32

    ' We internalize protocol specific processing to simplfy end user consumption
    Private WithEvents m_frameParser As IFrameParser
    Private WithEvents m_communicationClient As ICommunicationClient
    Private WithEvents m_communicationServer As ICommunicationServer
    Private WithEvents m_rateCalcTimer As Timers.Timer

    Private m_configurationFrame As IConfigurationFrame
    Private m_dataStreamStartTime As Long
    Private m_executeParseOnSeparateThread As Boolean
    Private m_autoRepeatCapturedPlayback As Boolean
    Private m_injectSimulatedTimestamp As Boolean
    Private m_totalFramesReceived As Long
    Private m_frameRateTotal As Int32
    Private m_byteRateTotal As Int32
    Private m_totalBytesReceived As Long
    Private m_frameRate As Double
    Private m_byteRate As Double
    Private m_sourceName As String
    Private m_definedFrameRate As Double
    Private m_lastFrameReceivedTime As Long
    Private m_autoStartDataParsingSequence As Boolean
    Private m_initiatingDataStream As Boolean
    Private m_initialBytesReceived As Long
    Private m_deviceSupportsCommands As Boolean
    Private m_enabled As Boolean
    Private m_connectionParameters As IConnectionParameters
    Private m_clientConnectionAttempts As Integer
    Private m_disposed As Boolean

#End Region

#Region " Construction Functions "

    Public Sub New()

        m_connectionString = "server=127.0.0.1; port=4712"
        m_deviceID = 1
        m_bufferSize = DefaultBufferSize
        m_definedFrameRate = DefaultFrameRate
        m_rateCalcTimer = New Timers.Timer
        m_maximumConnectionAttempts = -1
        m_autoStartDataParsingSequence = True

        m_phasorProtocol = PhasorProtocol.IeeeC37_118V1
        m_transportProtocol = TransportProtocol.Tcp

        m_rateCalcTimer.Interval = 5000
        m_rateCalcTimer.AutoReset = True
        m_rateCalcTimer.Enabled = False

    End Sub

    Public Sub New(ByVal phasorProtocol As PhasorProtocol, ByVal transportProtocol As TransportProtocol)

        MyClass.New()
        m_phasorProtocol = phasorProtocol
        m_transportProtocol = transportProtocol

    End Sub

    Protected Overrides Sub Finalize()

        Dispose(True)

    End Sub

    Protected Overridable Sub Dispose(ByVal disposing As Boolean)

        If Not m_disposed Then
            If disposing Then
                [Stop]()
                If m_rateCalcTimer IsNot Nothing Then m_rateCalcTimer.Dispose()
                m_rateCalcTimer = Nothing
            End If
        End If

        m_disposed = True

    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose

        Dispose(True)
        GC.SuppressFinalize(Me)

    End Sub

#End Region

#Region " Public Methods Implementation "

    Public Property PhasorProtocol() As PhasorProtocol
        Get
            Return m_phasorProtocol
        End Get
        Set(ByVal value As PhasorProtocol)
            m_phasorProtocol = value
            m_deviceSupportsCommands = GetDerivedCommandSupport()

            ' Setup protocol specific connection parameters...
            Select Case value
                Case PhasorProtocols.PhasorProtocol.BpaPdcStream
                    m_connectionParameters = New BpaPdcStream.ConnectionParameters
                Case PhasorProtocols.PhasorProtocol.FNet
                    m_connectionParameters = New FNet.ConnectionParameters
                Case Else
                    m_connectionParameters = Nothing
            End Select
        End Set
    End Property

    Property TransportProtocol() As TransportProtocol
        Get
            Return m_transportProtocol
        End Get
        Set(ByVal value As TransportProtocol)
            ' UDP transport has special requirements on buffer size
            If value = Communication.TransportProtocol.Udp Then
                If m_bufferSize < UdpClient.MinimumUdpBufferSize Then m_bufferSize = UdpClient.MinimumUdpBufferSize
                If m_bufferSize > UdpClient.MaximumUdpDatagramSize Then m_bufferSize = UdpClient.MaximumUdpDatagramSize
            Else
                m_bufferSize = DefaultBufferSize
            End If

            m_transportProtocol = value
            m_deviceSupportsCommands = GetDerivedCommandSupport()
        End Set
    End Property

    Public Property ConnectionString() As String
        Get
            Return m_connectionString
        End Get
        Set(ByVal value As String)
            m_connectionString = value
            m_deviceSupportsCommands = GetDerivedCommandSupport()
        End Set
    End Property

    Public Property DeviceSupportsCommands() As Boolean
        Get
            Return m_deviceSupportsCommands
        End Get
        Set(ByVal value As Boolean)
            ' Consumers can choose to override command support if needed
            m_deviceSupportsCommands = value
        End Set
    End Property

    Public Property DeviceID() As UInt16
        Get
            Return m_deviceID
        End Get
        Set(ByVal value As UInt16)
            m_deviceID = value
        End Set
    End Property

    Public Property BufferSize() As Int32
        Get
            Return m_bufferSize
        End Get
        Set(ByVal value As Int32)
            m_bufferSize = value

            ' UDP has special restrictions on overall buffer size
            If m_transportProtocol = Communication.TransportProtocol.Udp Then
                If m_bufferSize < UdpClient.MinimumUdpBufferSize Then m_bufferSize = UdpClient.MinimumUdpBufferSize
                If m_bufferSize > UdpClient.MaximumUdpDatagramSize Then m_bufferSize = UdpClient.MaximumUdpDatagramSize
            End If
        End Set
    End Property

    Public Property DefinedFrameRate() As Double
        Get
            Return m_definedFrameRate
        End Get
        Set(ByVal value As Double)
            m_definedFrameRate = value
        End Set
    End Property

    Public Property MaximumConnectionAttempts() As Integer
        Get
            Return m_maximumConnectionAttempts
        End Get
        Set(ByVal value As Integer)
            m_maximumConnectionAttempts = value
        End Set
    End Property

    Public Property AutoStartDataParsingSequence() As Boolean
        Get
            Return m_autoStartDataParsingSequence
        End Get
        Set(ByVal value As Boolean)
            m_autoStartDataParsingSequence = value
        End Set
    End Property

    Public Property InjectSimulatedTimestamp() As Boolean
        Get
            Return m_injectSimulatedTimestamp
        End Get
        Set(ByVal value As Boolean)
            m_injectSimulatedTimestamp = value
        End Set
    End Property

    Public Property SourceName() As String
        Get
            Return m_sourceName
        End Get
        Set(ByVal value As String)
            m_sourceName = value
        End Set
    End Property

    Public ReadOnly Property ConnectionName() As String
        Get
            If m_sourceName Is Nothing Then
                Return m_deviceID & " (" & m_connectionString & ")"
            Else
                Return m_sourceName & ", ID " & m_deviceID & " (" & m_connectionString & ")"
            End If
        End Get
    End Property

    Public Sub Start() Implements IFrameParser.Start

        [Stop]()
        m_totalFramesReceived = 0
        m_frameRateTotal = 0
        m_byteRateTotal = 0
        m_totalBytesReceived = 0
        m_frameRate = 0.0#
        m_byteRate = 0.0#

        Try
            ' Instantiate protocol specific frame parser
            Select Case m_phasorProtocol
                Case PhasorProtocols.PhasorProtocol.IeeeC37_118V1
                    m_frameParser = New IeeeC37_118.FrameParser(IeeeC37_118.DraftRevision.Draft7)
                Case PhasorProtocols.PhasorProtocol.IeeeC37_118D6
                    m_frameParser = New IeeeC37_118.FrameParser(IeeeC37_118.DraftRevision.Draft6)
                Case PhasorProtocols.PhasorProtocol.Ieee1344
                    m_frameParser = New Ieee1344.FrameParser
                Case PhasorProtocols.PhasorProtocol.BpaPdcStream
                    m_frameParser = New BpaPdcStream.FrameParser
                Case PhasorProtocols.PhasorProtocol.FNet
                    m_frameParser = New FNet.FrameParser
            End Select

            m_frameParser.ConnectionParameters = m_connectionParameters
            m_frameParser.ExecuteParseOnSeparateThread = m_executeParseOnSeparateThread
            m_frameParser.Start()

            ' Instantiate selected transport layer
            Select Case m_transportProtocol
                Case TransportProtocol.Tcp
                    ' The TCP transport may be set up as a server or as a client, we distinguish
                    ' this simply by deriving the value of an added key/value pair in the
                    ' connection string called "IsListener"
                    With ParseKeyValuePairs(m_connectionString)
                        If .ContainsKey("islistener") Then
                            If ParseBoolean(.Item("islistener")) Then
                                m_communicationServer = New TcpServer
                            Else
                                m_communicationClient = New TcpClient
                            End If
                        Else
                            ' If the key doesn't exist, we assume it's a client connection
                            ' (this way old connections strings are still backwards compatible)
                            m_communicationClient = New TcpClient
                        End If
                    End With
                Case TransportProtocol.Udp
                    m_communicationClient = New UdpClient
                Case TransportProtocol.Serial
                    m_communicationClient = New SerialClient
                Case TransportProtocol.File
                    With New FileClient
                        ' For file based playback, we allow the option of auto-repeat
                        .AutoRepeat = m_autoRepeatCapturedPlayback
                        m_communicationClient = .This
                    End With
            End Select

            If m_communicationClient IsNot Nothing Then
                ' Attempting connection to device
                With m_communicationClient
                    .ReceiveRawDataFunction = AddressOf IFrameParserWrite
                    .ReceiveBufferSize = m_bufferSize
                    .ConnectionString = m_connectionString
                    .MaximumConnectionAttempts = m_maximumConnectionAttempts
                    .Handshake = False
                    .Connect()
                End With
                m_clientConnectionAttempts = 0
            ElseIf m_communicationServer IsNot Nothing Then
                ' Listening for device connection
                With m_communicationServer
                    .ReceiveRawDataFunction = AddressOf IFrameParserWrite
                    .ReceiveBufferSize = m_bufferSize
                    .ConfigurationString = m_connectionString
                    .MaximumClients = 1
                    .Handshake = False
                    .Start()
                End With
            Else
                Throw New InvalidOperationException("No communications layer was initialized, cannot start parser")
            End If

            m_rateCalcTimer.Enabled = True
            m_enabled = True
        Catch
            [Stop]()
            Throw
        End Try

    End Sub

    Public Sub [Stop]() Implements IFrameParser.Stop

        m_enabled = False
        m_rateCalcTimer.Enabled = False

        If m_communicationClient IsNot Nothing Then m_communicationClient.Dispose()
        If m_communicationServer IsNot Nothing Then m_communicationServer.Dispose()
        If m_frameParser IsNot Nothing Then m_frameParser.Dispose()

        m_lastFrameReceivedTime = 0
        m_configurationFrame = Nothing
        m_frameParser = Nothing
        m_communicationClient = Nothing
        m_communicationServer = Nothing

    End Sub

    Public Property ExecuteParseOnSeparateThread() As Boolean Implements IFrameParser.ExecuteParseOnSeparateThread
        Get
            Return m_executeParseOnSeparateThread
        End Get
        Set(ByVal value As Boolean)
            m_executeParseOnSeparateThread = value

            ' Since frame parsers support dynamic changes in this value, we'll pass this value along to the
            ' the frame parser if one has been established...
            If m_frameParser IsNot Nothing Then m_frameParser.ExecuteParseOnSeparateThread = value
        End Set
    End Property

    Public Property AutoRepeatCapturedPlayback() As Boolean
        Get
            Return m_autoRepeatCapturedPlayback
        End Get
        Set(ByVal value As Boolean)
            m_autoRepeatCapturedPlayback = value
        End Set
    End Property

    Public Property ConfigurationFrame() As IConfigurationFrame Implements IFrameParser.ConfigurationFrame
        Get
            Return m_configurationFrame
        End Get
        Set(ByVal value As IConfigurationFrame)
            m_configurationFrame = value

            ' Pass new config frame onto appropriate parser, casting into appropriate protocol if needed...
            If m_frameParser IsNot Nothing Then m_frameParser.ConfigurationFrame = value
        End Set
    End Property

    Public ReadOnly Property IsIEEEProtocol() As Boolean
        Get
            Return m_phasorProtocol = PhasorProtocols.PhasorProtocol.IeeeC37_118V1 OrElse _
                m_phasorProtocol = PhasorProtocols.PhasorProtocol.IeeeC37_118D6 OrElse _
                m_phasorProtocol = PhasorProtocols.PhasorProtocol.Ieee1344
        End Get
    End Property

    Public ReadOnly Property Enabled() As Boolean Implements IFrameParser.Enabled
        Get
            Return m_enabled
        End Get
    End Property

    Public ReadOnly Property QueuedBuffers() As Int32 Implements IFrameParser.QueuedBuffers
        Get
            If m_frameParser IsNot Nothing Then Return m_frameParser.QueuedBuffers
        End Get
    End Property

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public ReadOnly Property InternalFrameParser() As IFrameParser
        Get
            Return m_frameParser
        End Get
    End Property

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public ReadOnly Property InternalCommunicationClient() As ICommunicationClient
        Get
            Return m_communicationClient
        End Get
    End Property

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public ReadOnly Property InternalCommunicationServer() As ICommunicationServer
        Get
            Return m_communicationServer
        End Get
    End Property

    Public ReadOnly Property TotalFramesReceived() As Long
        Get
            Return m_totalFramesReceived
        End Get
    End Property

    Public ReadOnly Property TotalBytesReceived() As Long
        Get
            Return m_totalBytesReceived
        End Get
    End Property

    Public ReadOnly Property FrameRate() As Double
        Get
            Return m_frameRate
        End Get
    End Property

    Public ReadOnly Property ByteRate() As Double
        Get
            Return m_byteRate
        End Get
    End Property

    Public ReadOnly Property BitRate() As Double
        Get
            Return m_byteRate * 8
        End Get
    End Property

    Public ReadOnly Property KiloBitRate() As Double
        Get
            Return m_byteRate * 8 / 1024
        End Get
    End Property

    Public ReadOnly Property MegaBitRate() As Double
        Get
            Return m_byteRate * 8 / 1048576
        End Get
    End Property

    Public Sub SendDeviceCommand(ByVal command As DeviceCommand)

        If m_deviceSupportsCommands AndAlso (m_communicationClient IsNot Nothing OrElse m_communicationServer IsNot Nothing) Then
            Dim commandFrame As ICommandFrame

            ' Only the IEEE protocols support commands
            Select Case m_phasorProtocol
                Case PhasorProtocols.PhasorProtocol.IeeeC37_118V1, PhasorProtocols.PhasorProtocol.IeeeC37_118D6
                    commandFrame = New IeeeC37_118.CommandFrame(m_deviceID, command, 1)
                Case PhasorProtocols.PhasorProtocol.Ieee1344
                    commandFrame = New Ieee1344.CommandFrame(m_deviceID, command)
                Case Else
                    commandFrame = Nothing
            End Select

            If commandFrame IsNot Nothing Then
                If m_communicationClient IsNot Nothing Then
                    m_communicationClient.Send(commandFrame.BinaryImage)
                Else
                    m_communicationServer.Multicast(commandFrame.BinaryImage)
                End If

                RaiseEvent SentCommandFrame(commandFrame)
            End If
        End If

    End Sub

    Public ReadOnly Property Status() As String Implements IFrameParser.Status
        Get
            With New StringBuilder
                .Append("      Device Connection ID: ")
                .Append(m_deviceID)
                .AppendLine()
                .Append("         Connection string: ")
                .Append(m_connectionString)
                .AppendLine()
                .Append("           Phasor protocol: ")
                .Append(Common.GetFormattedProtocolName(PhasorProtocol))
                .AppendLine()
                .Append("               Buffer size: ")
                .Append(m_bufferSize)
                .AppendLine()
                .Append("     Total frames received: ")
                .Append(m_totalFramesReceived)
                .AppendLine()
                .Append("     Calculated frame rate: ")
                .Append(m_frameRate)
                .AppendLine()
                .Append("      Calculated byte rate: ")
                .Append(m_byteRate)
                .AppendLine()
                .Append("   Calculated MegaBit rate: ")
                .Append(MegaBitRate.ToString("0.0000") & " mbps")
                .AppendLine()

                If m_frameParser IsNot Nothing Then .Append(m_frameParser.Status)
                If m_communicationClient IsNot Nothing Then .Append(m_communicationClient.Status)
                If m_communicationServer IsNot Nothing Then .Append(m_communicationServer.Status)

                Return .ToString()
            End With
        End Get
    End Property

    Public Property ConnectionParameters() As IConnectionParameters Implements IFrameParser.ConnectionParameters
        Get
            Return m_connectionParameters
        End Get
        Set(ByVal value As IConnectionParameters)
            m_connectionParameters = value

            ' Pass new connection parameters along to derived frame parser if instantiated
            If m_frameParser IsNot Nothing Then m_frameParser.ConnectionParameters = value
        End Set
    End Property

#End Region

#Region " Private Methods Implementation "

    Private Sub m_rateCalcTimer_Elapsed(ByVal sender As Object, ByVal e As System.Timers.ElapsedEventArgs) Handles m_rateCalcTimer.Elapsed

        Dim time As Double = TicksToSeconds(Date.Now.Ticks - m_dataStreamStartTime)

        m_frameRate = m_frameRateTotal / time
        m_byteRate = m_byteRateTotal / time

        m_totalFramesReceived += m_frameRateTotal
        m_totalBytesReceived += m_byteRateTotal

        m_frameRateTotal = 0
        m_byteRateTotal = 0
        m_dataStreamStartTime = Date.Now.Ticks

    End Sub

    ' We access the "raw data function" of the ICommunicationClient and ICommunicationServer for a speed boost in communications processing...
    Private Sub IFrameParserWrite(ByVal buffer() As Byte, ByVal offset As Integer, ByVal count As Integer) Implements IFrameParser.Write

        ' Pass data from communications client into protocol specific frame parser
        m_frameParser.Write(buffer, offset, count)
        m_byteRateTotal += count
        If m_initiatingDataStream Then m_initialBytesReceived += count

    End Sub

    Private Sub ClientConnected()

        RaiseEvent Connected()

        ' Begin data parsing sequence to handle reception of configuration frame
        If m_deviceSupportsCommands AndAlso m_autoStartDataParsingSequence Then
            m_initialBytesReceived = 0
            m_initiatingDataStream = True

#If ThreadTracking Then
            With TVA.Threading.ManagedThreadPool.QueueUserWorkItem(AddressOf StartDataParsingSequence)
                .Name = "PhasorProtocols.MultiProtocolFrameParser.StartDataParsingSequence()"
            End With
#Else
            ThreadPool.UnsafeQueueUserWorkItem(AddressOf StartDataParsingSequence, Nothing)
#End If
        End If

    End Sub

    Private Sub StartDataParsingSequence(ByVal state As Object)

        Dim attempts As Integer

        ' Some devices will only send a config frame once data streaming has been disabled, so
        ' we use this code to disable real-time data and wait for data to stop streaming...
        Try
            ' Make sure data stream is disabled
            SendDeviceCommand(DeviceCommand.DisableRealTimeData)
            Thread.Sleep(300)

            ' Wait for real-time data stream to cease for up to two seconds
            Do While m_initialBytesReceived > 0
                m_initialBytesReceived = 0
                Thread.Sleep(100)

                attempts += 1
                If attempts >= 20 Then Exit Do
            Loop
        Finally
            m_initiatingDataStream = False
        End Try

        ' Request configuration frame once real-time data has been disabled
        SendDeviceCommand(DeviceCommand.SendConfigurationFrame2)

    End Sub

    Private Function GetDerivedCommandSupport() As Boolean

        ' Command support is based on phasor protocol, transport protocol and connection style
        If IsIEEEProtocol Then
            ' IEEE protocols using TCP or Serial connection support device commands
            If m_transportProtocol = Communication.TransportProtocol.Tcp OrElse _
                m_transportProtocol = Communication.TransportProtocol.Serial Then _
                    Return True

            ' IEEE protocols "can" use UDP connection to support devices commands, but only
            ' when remote device acts as a UDP listener (i.e., a "server" connection)
            If m_transportProtocol = Communication.TransportProtocol.Udp Then
                If Not String.IsNullOrEmpty(m_connectionString) Then
                    With ParseKeyValuePairs(m_connectionString)
                        Return .ContainsKey("server")
                    End With
                End If
            End If
        End If

        Return False

    End Function

    Private Sub MaintainCapturedFrameReplayTiming()

        If m_lastFrameReceivedTime > 0 Then
            ' To keep precise timing on "frames per second", we wait for defined frame rate interval
            Dim sleepTime As Double = m_definedFrameRate - TicksToSeconds(Date.Now.Ticks - m_lastFrameReceivedTime)
            If sleepTime > 0 Then Thread.Sleep(sleepTime * 900)
        End If

        m_lastFrameReceivedTime = Date.Now.Ticks

    End Sub

#Region " Communications Client Event Handlers "

    Private Sub m_communicationClient_Connected(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles m_communicationClient.Connected

        ClientConnected()

    End Sub

    Private Sub m_communicationClient_Connecting(ByVal sender As Object, ByVal e As System.EventArgs) Handles m_communicationClient.Connecting

        m_clientConnectionAttempts += 1
        RaiseEvent AttemptingConnection()

    End Sub

    Private Sub m_communicationClient_ConnectingException(ByVal sender As Object, ByVal e As GenericEventArgs(Of System.Exception)) Handles m_communicationClient.ConnectingException

        RaiseEvent ConnectionException(e.Argument, m_clientConnectionAttempts)

    End Sub

    Private Sub m_communicationClient_Disconnected(ByVal sender As Object, ByVal e As System.EventArgs) Handles m_communicationClient.Disconnected

        RaiseEvent Disconnected()
        m_communicationClient = Nothing

    End Sub

#End Region

#Region " Communications Server Event Handlers "

    Private Sub m_communicationServer_ClientConnected(ByVal sender As Object, ByVal e As GenericEventArgs(Of System.Guid)) Handles m_communicationServer.ClientConnected

        ClientConnected()

    End Sub

    Private Sub m_communicationServer_ClientDisconnected(ByVal sender As Object, ByVal e As GenericEventArgs(Of System.Guid)) Handles m_communicationServer.ClientDisconnected

        RaiseEvent Disconnected()

    End Sub

    Private Sub m_communicationServer_ServerStarted(ByVal sender As Object, ByVal e As System.EventArgs) Handles m_communicationServer.ServerStarted

        RaiseEvent ServerStarted()

    End Sub

    Private Sub m_communicationServer_ServerStopped(ByVal sender As Object, ByVal e As System.EventArgs) Handles m_communicationServer.ServerStopped

        RaiseEvent ServerStopped()

    End Sub

    Private Sub m_communicationServer_ServerStartupException(ByVal sender As Object, ByVal e As GenericEventArgs(Of System.Exception)) Handles m_communicationServer.ServerStartupException

        RaiseEvent ConnectionException(e.Argument, 1)

    End Sub

#End Region

#Region " Frame Parser Event Handlers "

    Private Sub m_frameParser_ReceivedCommandFrame(ByVal frame As ICommandFrame) Handles m_frameParser.ReceivedCommandFrame

        m_frameRateTotal += 1

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            If m_injectSimulatedTimestamp Then frame.Ticks = Date.UtcNow.Ticks
            RaiseEvent ReceivedCommandFrame(frame)
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ReceivedCommandFrame"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

        If m_transportProtocol = Communication.TransportProtocol.File Then MaintainCapturedFrameReplayTiming()

    End Sub

    Private Sub m_frameParser_ReceivedConfigurationFrame(ByVal frame As IConfigurationFrame) Handles m_frameParser.ReceivedConfigurationFrame

        ' We automatically request enabling of real-time data upon reception of config frame if requested
        If m_configurationFrame Is Nothing AndAlso m_deviceSupportsCommands AndAlso m_autoStartDataParsingSequence Then
            SendDeviceCommand(DeviceCommand.EnableRealTimeData)
        End If

        m_frameRateTotal += 1
        m_configurationFrame = frame

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            If m_injectSimulatedTimestamp Then frame.Ticks = Date.UtcNow.Ticks
            RaiseEvent ReceivedConfigurationFrame(frame)
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ReceivedConfigurationFrame"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

        If m_transportProtocol = Communication.TransportProtocol.File Then MaintainCapturedFrameReplayTiming()

    End Sub

    Private Sub m_frameParser_ReceivedDataFrame(ByVal frame As IDataFrame) Handles m_frameParser.ReceivedDataFrame

        m_frameRateTotal += 1

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            If m_injectSimulatedTimestamp Then frame.Ticks = Date.UtcNow.Ticks
            RaiseEvent ReceivedDataFrame(frame)
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ReceivedDataFrame"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

        If m_transportProtocol = Communication.TransportProtocol.File Then MaintainCapturedFrameReplayTiming()

    End Sub

    Private Sub m_frameParser_ReceivedHeaderFrame(ByVal frame As IHeaderFrame) Handles m_frameParser.ReceivedHeaderFrame

        m_frameRateTotal += 1

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            If m_injectSimulatedTimestamp Then frame.Ticks = Date.UtcNow.Ticks
            RaiseEvent ReceivedHeaderFrame(frame)
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ReceivedHeaderFrame"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

        If m_transportProtocol = Communication.TransportProtocol.File Then MaintainCapturedFrameReplayTiming()

    End Sub

    Private Sub m_frameParser_ReceivedUndeterminedFrame(ByVal frame As IChannelFrame) Handles m_frameParser.ReceivedUndeterminedFrame

        m_frameRateTotal += 1

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            If m_injectSimulatedTimestamp Then frame.Ticks = Date.UtcNow.Ticks
            RaiseEvent ReceivedUndeterminedFrame(frame)
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ReceivedUndeterminedFrame"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

        If m_transportProtocol = Communication.TransportProtocol.File Then MaintainCapturedFrameReplayTiming()

    End Sub

    Private Sub m_frameParser_ReceivedFrameBufferImage(ByVal frameType As FundamentalFrameType, ByVal binaryImage() As Byte, ByVal offset As Integer, ByVal length As Integer) Handles m_frameParser.ReceivedFrameBufferImage

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            RaiseEvent ReceivedFrameBufferImage(frameType, binaryImage, offset, length)
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ReceivedFrameBufferImage"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

    End Sub

    Private Sub m_frameParser_ConfigurationChanged() Handles m_frameParser.ConfigurationChanged

        ' We don't stop parsing for exceptions thrown in consumer event handlers
        Try
            RaiseEvent ConfigurationChanged()
        Catch ex As Exception
            RaiseEvent DataStreamException( _
                New Exception(String.Format( _
                "MultiProtocolFrameParser Consumer ""ConfigurationChanged"" Event Handler Exception: {0}", _
                ex.Message), ex))
        End Try

    End Sub

    Private Sub m_frameParser_DataStreamException(ByVal ex As System.Exception) Handles m_frameParser.DataStreamException

        RaiseEvent DataStreamException(ex)

    End Sub

#End Region

#Region " Old Socket Code "

    'Private m_socketThread As Thread
    'Private m_tcpSocket As Sockets.TcpClient
    'Private m_udpSocket As Socket
    'Private m_receptionPoint As EndPoint
    'Private m_clientStream As NetworkStream

    '' Validate minimal connection parameters required for TCP connection
    'If String.IsNullOrEmpty(m_hostIP) Then Throw New InvalidOperationException("Cannot start TCP stream listener without specifing a host IP")
    'If m_port = 0 Then Throw New InvalidOperationException("Cannot start TCP stream listener without specifing a port")

    '' Connect to PDC/PMU using TCP
    'm_tcpSocket = New Sockets.TcpClient
    'm_tcpSocket.ReceiveBufferSize = m_bufferSize
    'm_tcpSocket.Connect(m_hostIP, m_port)
    'm_clientStream = m_tcpSocket.GetStream()

    '' Start listening to TCP data stream
    'm_socketThread = New Thread(AddressOf ProcessTcpStream)
    'm_socketThread.Start()

    '' Validate minimal connection parameters required for UDP connection
    'If m_port = 0 Then Throw New InvalidOperationException("Cannot start UDP stream listener without specifing a valid port")

    '' Connect to PDC/PMU using UDP (just listening to incoming stream on specified port)
    'm_udpSocket = New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
    'm_receptionPoint = CType(New IPEndPoint(IPAddress.Any, m_port), System.Net.EndPoint)
    'm_udpSocket.ReceiveBufferSize = m_bufferSize
    'm_udpSocket.Bind(m_receptionPoint)

    '' Start listening to UDP data stream
    'm_socketThread = New Thread(AddressOf ProcessUdpStream)
    'm_socketThread.Start()

    'If m_socketThread IsNot Nothing Then m_socketThread.Abort()
    'm_socketThread = Nothing

    'If m_tcpSocket IsNot Nothing Then m_tcpSocket.Close()
    'm_tcpSocket = Nothing

    'If m_udpSocket IsNot Nothing Then m_udpSocket.Close()
    'm_udpSocket = Nothing

    'm_clientStream = Nothing
    'm_receptionPoint = Nothing

    'Private Sub ProcessUdpStream()

    'Dim buffer As Byte() = CreateArray(Of Byte)(m_bufferSize)
    'Dim received As Int32

    '' Enter the data read loop
    'Do While True
    '    Try
    '        ' Block thread until we've received some data...
    '        received = m_udpSocket.ReceiveFrom(buffer, m_receptionPoint)

    '        ' Provide received buffer to protocol specific frame parser
    '        If received > 0 Then Write(buffer, 0, received)
    '    Catch ex As ThreadAbortException
    '        ' If we received an abort exception, we'll egress gracefully
    '        Exit Do
    '    Catch ex As IOException
    '        ' This will get thrown if the thread is being aborted and we are sitting in a blocked stream read, so
    '        ' in this case we'll bow out gracefully as well...
    '        Exit Do
    '    Catch ex As Exception
    '        RaiseEvent DataStreamException(ex)
    '        Exit Do
    '    End Try
    'Loop

    'End Sub

    'Private Sub ProcessTcpStream()

    'Dim buffer As Byte() = CreateArray(Of Byte)(m_bufferSize)
    'Dim received, attempts As Integer

    '' Handle reception of configuration frame - in case of device that only responds to commands when not sending real-time data,
    '' such as the SEL 421, we disable real-time data stream first...
    'Try
    '    ' Make sure data stream is disabled
    '    SendPmuCommand(DeviceCommand.DisableRealTimeData)

    '    ' Wait for real-time data stream to cease
    '    Do While m_clientStream.DataAvailable
    '        ' Remove all existing data from stream
    '        Do While m_clientStream.DataAvailable
    '            received = m_clientStream.Read(buffer, 0, buffer.Length)
    '        Loop

    '        Thread.Sleep(100)

    '        attempts += 1
    '        If attempts >= 50 Then Exit Do
    '    Loop

    '    ' Request configuration frame 2 (we'll try a few times)
    '    attempts = 0
    '    m_configurationFrame = Nothing

    '    For x As Integer = 1 To 4
    '        SendPmuCommand(DeviceCommand.SendConfigurationFrame2)

    '        Do While m_configurationFrame Is Nothing
    '            ' So long as we are receiving data, we'll push it to the frame parser
    '            Do While m_clientStream.DataAvailable
    '                ' Block thread until we've read some data...
    '                received = m_clientStream.Read(buffer, 0, buffer.Length)

    '                ' Send received data to frame parser
    '                If received > 0 Then Write(buffer, 0, received)
    '            Loop

    '            ' Hang out for a little while so config frame can be parsed
    '            Thread.Sleep(100)

    '            attempts += 1
    '            If attempts >= 50 Then Exit Do
    '        Loop

    '        If m_configurationFrame IsNot Nothing Then Exit For
    '    Next

    '    ' Enable data stream
    '    SendPmuCommand(DeviceCommand.EnableRealTimeData)
    'Catch ex As ThreadAbortException
    '    ' If we received an abort exception, we'll egress gracefully
    '    Exit Sub
    'Catch ex As IOException
    '    ' This will get thrown if the thread is being aborted and we are sitting in a blocked stream read, so
    '    ' in this case we'll bow out gracefully as well...
    '    Exit Sub
    'Catch ex As Exception
    '    RaiseEvent DataStreamException(ex)
    '    Exit Sub
    'End Try

    '' Enter the data read loop
    'Do While True
    '    Try
    '        ' Block thread until we've received some data...
    '        received = m_clientStream.Read(buffer, 0, buffer.Length)

    '        ' Provide received buffer to protocol specific frame parser
    '        If received > 0 Then Write(buffer, 0, received)
    '    Catch ex As ThreadAbortException
    '        ' If we received an abort exception, we'll egress gracefully
    '        Exit Do
    '    Catch ex As IOException
    '        ' This will get thrown if the thread is being aborted and we are sitting in a blocked stream read, so
    '        ' in this case we'll bow out gracefully as well...
    '        Exit Do
    '    Catch ex As Exception
    '        RaiseEvent DataStreamException(ex)
    '        Exit Do
    '    End Try
    'Loop

    'End Sub

    'Private Sub m_communicationClient_ReceivedData(ByVal sender As Object, ByVal e As DataEventArgs) Handles m_communicationClient.ReceivedData

    '    With e
    '        ' Pass data from communications client into protocol specific frame parser
    '        m_frameParser.Write(.Data, 0, .Data.Length)
    '        m_byteRateTotal += .Data.Length
    '        If m_initiatingDataStream Then m_initialBytesReceived += .Data.Length
    '    End With

    'End Sub

#End Region

#End Region

End Class