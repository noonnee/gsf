'*******************************************************************************************************
'  PhasorDefinitionCollection.vb - Phasor value definition collection class
'  Copyright � 2005 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: James R Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  02/18/2005 - James R Carroll
'       Initial version of source generated
'
'*******************************************************************************************************

' This class represents the common implementation collection of protocol independent definitions of phasor values.
<CLSCompliant(False)> _
Public Class PhasorDefinitionCollection

    Inherits ChannelDefinitionCollectionBase(Of IPhasorDefinition)

    Public Sub New(ByVal maximumCount As Integer)

        MyBase.New(maximumCount)

    End Sub

    Public Overrides ReadOnly Property InheritedType() As Type
        Get
            Return Me.GetType()
        End Get
    End Property

    Public Sub SetCoordinateFormat(ByVal value As CoordinateFormat)

        For Each definition As IPhasorDefinition In Me
            definition.CoordinateFormat = value
        Next

    End Sub

End Class

