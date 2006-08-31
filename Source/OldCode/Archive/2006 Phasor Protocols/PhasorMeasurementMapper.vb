'*******************************************************************************************************
'  PhasorMeasurementMapper.vb - Basic phasor measurement mapper
'  Copyright � 2006 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: J. Ritchie Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  05/08/2006 - J. Ritchie Carroll
'       Initial version of source generated
'
'*******************************************************************************************************

Imports System.Text
Imports Tva.DateTime
Imports Tva.Phasors
Imports Tva.Communication
Imports Tva.Measurements

<CLSCompliant(False)> _
Public Class PhasorMeasurementMapper

    Public Event NewParsedMeasurements(ByVal measurements As Dictionary(Of MeasurementKey, IMeasurement))
    Public Event ParsingStatus(ByVal message As String)

    Private WithEvents m_dataStreamMonitor As Timers.Timer
    Private WithEvents m_frameParser As MultiProtocolFrameParser
    Private m_archiverCode As String
    Private m_source As String
    Private m_pmuIDs As PmuInfoCollection
    Private m_measurementIDs As Dictionary(Of String, Measurement)
    Private m_measurementFrames As List(Of IFrame)
    Private m_lastReportTime As Long
    Private m_bytesReceived As Long
    Private m_totalBytesReceived As Long
    Private m_errorCount As Integer
    Private m_errorTime As Long
    Private m_receivedConfigFrame As Boolean
    Private m_timezone As Win32TimeZone
    Private m_timeAdjustmentTicks As Long

    Public Sub New( _
        ByVal frameParser As MultiProtocolFrameParser, _
        ByVal archiverCode As String, _
        ByVal source As String, _
        ByVal pmuIDs As PmuInfoCollection, _
        ByVal measurementIDs As Dictionary(Of String, Measurement))

        m_frameParser = frameParser
        m_archiverCode = archiverCode
        m_source = source
        m_pmuIDs = pmuIDs
        m_measurementIDs = measurementIDs
        m_measurementFrames = New List(Of IFrame)

        m_dataStreamMonitor = New Timers.Timer

        With m_dataStreamMonitor
            .AutoReset = True
            .Interval = 10000
            .Enabled = False
        End With

    End Sub

    Public Sub Connect()

        ' Make sure we are disconnected before attempting a connection
        Disconnect()

        ' Start the connection cycle
        m_totalBytesReceived = 0
        m_frameParser.Start()

    End Sub

    Public Sub Disconnect()

        If m_frameParser IsNot Nothing Then m_frameParser.[Stop]()

        ' Stop data stream monitor, if running
        If m_dataStreamMonitor IsNot Nothing Then m_dataStreamMonitor.Enabled = False

        m_receivedConfigFrame = False
        m_errorCount = 0
        m_errorTime = 0

    End Sub

    Public Function GetQueuedFrames() As IFrame()

        Dim frames As IFrame()

        SyncLock m_measurementFrames
            frames = m_measurementFrames.ToArray()
            m_measurementFrames.Clear()
        End SyncLock

        Return frames

    End Function

    Public ReadOnly Property This() As PhasorMeasurementMapper
        Get
            Return Me
        End Get
    End Property

    Public ReadOnly Property Status() As String
        Get
            With New StringBuilder
                .Append("Phasor Data Parsing Connection for ")
                .Append(Name)
                .Append(Environment.NewLine)
                .Append(m_frameParser.Status)

                Return .ToString()
            End With
        End Get
    End Property

    Public ReadOnly Property LastReportTime() As Long
        Get
            Return m_lastReportTime
        End Get
    End Property

    Public ReadOnly Property TotalBytesReceived() As Long
        Get
            Return m_totalBytesReceived
        End Get
    End Property

    Public ReadOnly Property Name() As String
        Get
            With New StringBuilder
                .Append(m_source)

                If m_pmuIDs.Count > 1 OrElse String.Compare(m_source, m_pmuIDs(0).Tag) <> 0 Then
                    .Append(" [")
                    For x As Integer = 0 To m_pmuIDs.Count - 1
                        If x > 0 Then .Append(", ")
                        .Append(m_pmuIDs(x).Tag)
                    Next
                    .Append("]")
                End If

                Return .ToString()
            End With
        End Get
    End Property

    Public Property TimeZone() As Win32TimeZone
        Get
            Return m_timezone
        End Get
        Set(ByVal value As Win32TimeZone)
            m_timezone = value
        End Set
    End Property

    Public Property TimeAdjustmentTicks() As Long
        Get
            Return m_timeAdjustmentTicks
        End Get
        Set(ByVal value As Long)
            m_timeAdjustmentTicks = value
        End Set
    End Property

    Public ReadOnly Property PmuIDs() As PmuInfoCollection
        Get
            Return m_pmuIDs
        End Get
    End Property

    ''' <summary>This key function is the glue that binds a phasor measurement value to a historian measurement ID</summary>
    Protected Overridable Sub MapDataFrameMeasurements(ByVal frame As Tva.Phasors.IDataFrame)

        ' Map data frame measurement instances to their associated point ID's
        With frame
            Dim pmuID As PmuInfo = Nothing
            Dim x, y As Integer
            Dim ticks As Long

            ' Adjust time to UTC based on source PDC/PMU time zone, if provided (typically when not UTC already)
            If m_timezone IsNot Nothing Then .Ticks = m_timezone.ToUniversalTime(.Timestamp).Ticks

            ' We also allow "fine tuning" of time for fickle GPS clocks...
            If m_timeAdjustmentTicks > 0 Then .Ticks += m_timeAdjustmentTicks

            ' Get ticks of this frame
            ticks = .Ticks

            ' Loop through each parsed PMU data cell
            For x = 0 To .Cells.Count - 1
                With .Cells(x)
                    ' Lookup PMU information by its ID code
                    If m_pmuIDs.TryGetValue(.IDCode, pmuID) Then
                        ' Track lastest reporting time
                        If ticks > pmuID.LastReportTime Then pmuID.LastReportTime = ticks
                        If ticks > m_lastReportTime Then m_lastReportTime = ticks

                        ' Map status flags (SF) from PMU data cell itself
                        MapMeasurementIDToValue(frame, pmuID.Tag & "-SF", .This)

                        ' Map phasor angles (PAn) and magnitudes (PMn)
                        With .PhasorValues
                            For y = 0 To .Count - 1
                                With .Item(y)
                                    ' Map angle
                                    MapMeasurementIDToValue(frame, pmuID.Tag & "-PA" & (y + 1), .Measurements(CompositePhasorValue.Angle))

                                    ' Map magnitude
                                    MapMeasurementIDToValue(frame, pmuID.Tag & "-PM" & (y + 1), .Measurements(CompositePhasorValue.Magnitude))
                                End With
                            Next
                        End With

                        ' Map frequency (FQ) and delta-frequency (DF)
                        With .FrequencyValue
                            ' Map frequency
                            MapMeasurementIDToValue(frame, pmuID.Tag & "-FQ", .Measurements(CompositeFrequencyValue.Frequency))

                            ' Map df/dt
                            MapMeasurementIDToValue(frame, pmuID.Tag & "-DF", .Measurements(CompositeFrequencyValue.DfDt))
                        End With

                        ' Map digital values (DVn)
                        With .DigitalValues
                            For y = 0 To .Count - 1
                                ' Map digital value
                                MapMeasurementIDToValue(frame, pmuID.Tag & "-DV" & y, .Item(y).Measurements(0))
                            Next
                        End With
                    Else
                        ' TODO: Encountered a new PMU - decide best way to report this...
                        ' Don't report it 30 times a second :)
                    End If
                End With
            Next
        End With

        ' Provide real-time measurements where needed
        RaiseEvent NewParsedMeasurements(frame.Measurements)

        ' Queue up frame for polled retrieval into DatAWare...
        SyncLock m_measurementFrames
            m_measurementFrames.Add(frame)
        End SyncLock

    End Sub

    Private Sub MapMeasurementIDToValue(ByVal frame As IDataFrame, ByVal synonym As String, ByVal measurementValue As IMeasurement)

        Dim measurementID As Measurement = Nothing

        ' Lookup synonym value in measurement ID list
        If m_measurementIDs.TryGetValue(synonym, measurementID) Then
            ' Assign ID and other relavent attributes to the measurement value
            With measurementValue
                .ID = measurementID.ID
                .Source = m_archiverCode
                .Adder = measurementID.Adder
                .Multiplier = measurementID.Multiplier
            End With

            ' Add the updated measurement value to frame measurement list which is keyed on ID
            frame.Measurements.Add(measurementValue.Key, measurementValue)
        End If

    End Sub

    Private Sub UpdateStatus(ByVal message As String)

        RaiseEvent ParsingStatus(message)

    End Sub

    Private Sub m_dataStreamMonitor_Elapsed(ByVal sender As Object, ByVal e As System.Timers.ElapsedEventArgs) Handles m_dataStreamMonitor.Elapsed

        ' If we've received no data in the last little timespan, we restart connect cycle...
        If m_bytesReceived = 0 Then
            UpdateStatus(Environment.NewLine & "No data on " & m_source & " received in " & m_dataStreamMonitor.Interval / 1000 & " seconds, restarting connect cycle..." & Environment.NewLine)
            Connect()
            m_dataStreamMonitor.Enabled = False
        End If

        m_bytesReceived = 0

    End Sub

    Private Sub m_frameParser_AttemptingConnection() Handles m_frameParser.AttemptingConnection

        With m_frameParser
            UpdateStatus("Attempting " & [Enum].GetName(GetType(PhasorProtocol), m_frameParser.PhasorProtocol).ToUpper() & " " & [Enum].GetName(GetType(TransportProtocol), .TransportProtocol).ToUpper() & " based connection to " & m_source)
        End With

    End Sub

    Private Sub m_frameParser_Connected() Handles m_frameParser.Connected

        ' Enable data stream monitor for non-UDP connections
        m_dataStreamMonitor.Enabled = (m_frameParser.TransportProtocol <> TransportProtocol.Udp)

        UpdateStatus("Connection to " & m_source & " established.")

    End Sub

    Private Sub m_frameParser_ConnectionException(ByVal ex As System.Exception) Handles m_frameParser.ConnectionException

        UpdateStatus(m_source & " connection to """ & m_frameParser.ConnectionName & """ failed: " & ex.Message)
        Connect()

    End Sub

    Private Sub m_frameParser_DataStreamException(ByVal ex As System.Exception) Handles m_frameParser.DataStreamException

        UpdateStatus("Data stream exception: " & ex.Message)

        ' We monitor for exceptions that occur in quick succession
        If Date.Now.Ticks - m_errorTime > 100000000L Then
            m_errorTime = Date.Now.Ticks
            m_errorCount = 1
        End If

        m_errorCount += 1

        ' When we get 10 or more exceptions within a ten second timespan, we will then restart connection cycle...
        If m_errorCount >= 10 Then
            UpdateStatus("Client connection terminated due to excessive exceptions.")
            Connect()
        End If

    End Sub

    Private Sub m_frameParser_Disconnected() Handles m_frameParser.Disconnected

        UpdateStatus("Disconnected from " & m_source & ".")

    End Sub

    Private Sub m_frameParser_ReceivedConfigurationFrame(ByVal frame As Tva.Phasors.IConfigurationFrame) Handles m_frameParser.ReceivedConfigurationFrame

        UpdateStatus("Received " & m_source & " configuration frame at " & Date.Now)
        m_receivedConfigFrame = True

    End Sub

    Private Sub m_frameParser_ReceivedDataFrame(ByVal frame As Tva.Phasors.IDataFrame) Handles m_frameParser.ReceivedDataFrame

        MapDataFrameMeasurements(frame)

    End Sub

    Private Sub m_frameParser_ReceivedFrameBufferImage(ByVal frameType As FundamentalFrameType, ByVal binaryImage() As Byte, ByVal offset As Integer, ByVal length As Integer) Handles m_frameParser.ReceivedFrameBufferImage

        m_bytesReceived += length
        m_totalBytesReceived += length

    End Sub

End Class
