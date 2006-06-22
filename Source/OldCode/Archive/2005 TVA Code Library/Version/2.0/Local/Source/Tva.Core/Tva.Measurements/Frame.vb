'***********************************************************************
'  Frame.vb - Time synchronized measurement values
'  Copyright � 2004 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2003
'  Primary Developer: James R Carroll, System Analyst [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  ---------------------------------------------------------------------
'  11/12/2004 - James R Carroll
'       Initial version of source generated
'
'***********************************************************************

Namespace Measurements

    ' This is essentially a collection of measurements at a given timestamp
    Public Class Frame

        Implements IComparable

        Private m_parent As Concentrator
        Private m_timestamp As Date
        Private m_index As Integer
        Private m_measurements As Dictionary(Of Integer, Double)
        Private m_published As Boolean

        Public Sub New(ByVal parent As Concentrator, ByVal baseTime As Date, ByVal index As Integer)

            m_parent = parent
            m_index = index
            m_measurements = New Dictionary(Of Integer, Double)

            ' We precalculate a regular .NET timestamp with milliseconds sitting in the middle of the sample index
            m_timestamp = baseTime.AddMilliseconds((m_index + 0.5@) * m_parent.FrameRate)

            ' As new measurement values are created, we track absolute latest timestamp
            m_parent.LatestMeasurements.Ticks = m_timestamp.Ticks

            For Each id As Integer In m_parent.LatestMeasurements.MeasurementIDs
                m_measurements.Add(id, Double.NaN)
            Next

        End Sub

        Public ReadOnly Property This() As Frame
            Get
                Return Me
            End Get
        End Property

        Public ReadOnly Property Index() As Integer
            Get
                Return m_index
            End Get
        End Property

        Public ReadOnly Property Timestamp() As Date
            Get
                Return m_timestamp
            End Get
        End Property

        Public Property Published() As Boolean
            Get
                Return m_published
            End Get
            Set(ByVal Value As Boolean)
                m_published = Value
            End Set
        End Property

        Default Public Property Value(ByVal measurementID As Integer) As Double
            Get
                SyncLock m_measurements
                    Return m_measurements(measurementID)
                End SyncLock
            End Get
            Set(ByVal value As Double)
                SyncLock m_measurements
                    m_measurements(measurementID) = value
                End SyncLock

                ' Check to see if we need to update latest measurement value as well
                m_parent.LatestMeasurements.Measurement(measurementID)(m_timestamp) = value
            End Set
        End Property

        Public ReadOnly Property BestValue(ByVal measurementID As Integer) As Double
            Get
                Dim measurement As Double = Value(measurementID)

                If Double.IsNaN(measurement) Then
                    Return LatestValue(measurementID)
                Else
                    Return measurement
                End If
            End Get
        End Property

        Public ReadOnly Property LatestValue(ByVal measurementID As Integer) As Double
            Get
                Return m_parent.LatestMeasurements(measurementID)
            End Get
        End Property

        Public ReadOnly Property Average() As Double
            Get
                Dim measurement As Double
                Dim total As Double
                Dim count As Integer

                SyncLock m_measurements
                    For Each entry As KeyValuePair(Of Integer, Double) In m_measurements
                        measurement = Value(entry.Key)
                        If Not Double.IsNaN(measurement) Then
                            total += measurement
                            count += 1
                        End If
                    Next
                End SyncLock

                'm_totalReporting = count
                Return total / count
            End Get
        End Property

        Public ReadOnly Property BestAverage() As Double
            Get
                Dim measurement As Double
                Dim total As Double
                Dim count As Integer

                SyncLock m_measurements
                    For Each entry As KeyValuePair(Of Integer, Double) In m_measurements
                        measurement = BestValue(entry.Key)
                        If Not Double.IsNaN(measurement) Then
                            total += measurement
                            count += 1
                        End If
                    Next
                End SyncLock

                'm_totalReporting = count
                Return total / count
            End Get
        End Property

        Public ReadOnly Property Minimum() As Double
            Get
                Dim minValue As Double = Double.MaxValue
                Dim measurement As Double

                SyncLock m_measurements
                    For Each entry As KeyValuePair(Of Integer, Double) In m_measurements
                        measurement = Value(entry.Key)
                        If Not Double.IsNaN(measurement) Then
                            If measurement < minValue Then minValue = measurement
                        End If
                    Next
                End SyncLock

                Return minValue
            End Get
        End Property

        Public ReadOnly Property BestMinimum() As Double
            Get
                Dim minValue As Double = Double.MaxValue
                Dim measurement As Double

                SyncLock m_measurements
                    For Each entry As KeyValuePair(Of Integer, Double) In m_measurements
                        measurement = BestValue(entry.Key)
                        If Not Double.IsNaN(measurement) Then
                            If measurement < minValue Then minValue = measurement
                        End If
                    Next
                End SyncLock

                Return minValue
            End Get
        End Property

        Public ReadOnly Property Maximum() As Double
            Get
                Dim maxValue As Double = Double.MinValue
                Dim measurement As Double

                SyncLock m_measurements
                    For Each entry As KeyValuePair(Of Integer, Double) In m_measurements
                        measurement = Value(entry.Key)
                        If Not Double.IsNaN(measurement) Then
                            If measurement > maxValue Then maxValue = measurement
                        End If
                    Next
                End SyncLock

                Return maxValue
            End Get
        End Property

        Public ReadOnly Property BestMaximum() As Double
            Get
                Dim maxValue As Double = Double.MinValue
                Dim measurement As Double

                SyncLock m_measurements
                    For Each entry As KeyValuePair(Of Integer, Double) In m_measurements
                        measurement = BestValue(entry.Key)
                        If Not Double.IsNaN(measurement) Then
                            If measurement > maxValue Then maxValue = measurement
                        End If
                    Next
                End SyncLock

                Return maxValue
            End Get
        End Property

        Public ReadOnly Property TagAverage(ByVal tag As String) As Double
            Get
                Dim measurement As Double
                Dim total As Double
                Dim count As Integer

                For Each measurementID As Integer In m_parent.LatestMeasurements.TagMeasurementIDs(tag)
                    measurement = Value(measurementID)
                    If Not Double.IsNaN(measurement) Then
                        total += measurement
                        count += 1
                    End If
                Next

                'm_taggedTotalReporting(tag) = count
                Return total / count
            End Get
        End Property

        Public ReadOnly Property BestTagAverage(ByVal tag As String) As Double
            Get
                Dim measurement As Double
                Dim total As Double
                Dim count As Integer

                For Each measurementID As Integer In m_parent.LatestMeasurements.TagMeasurementIDs(tag)
                    measurement = BestValue(measurementID)
                    If Not Double.IsNaN(measurement) Then
                        total += measurement
                        count += 1
                    End If
                Next

                'm_taggedTotalReporting(tag) = count
                Return total / count
            End Get
        End Property

        ' We sort syncrhonized measurement packets by timetag and index
        Public Function CompareTo(ByVal obj As Object) As Integer Implements System.IComparable.CompareTo

            If TypeOf obj Is Frame Then
                Dim comparison As Integer = m_timestamp.CompareTo(DirectCast(obj, Frame).TimeStamp)

                If comparison = 0 Then
                    Return m_index.CompareTo(DirectCast(obj, Frame).Index)
                Else
                    Return comparison
                End If
            Else
                Throw New ArgumentException("Frame can only be compared with other IFrames...")
            End If

        End Function

    End Class

End Namespace
