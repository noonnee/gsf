'*******************************************************************************************************
'  ConfigurationFrameCollection.vb - Configuration frame collection class
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
'  01/14/2005 - James R Carroll
'       Initial version of source generated
'
'*******************************************************************************************************

' This class represents the protocol independent collection of the common implementation of a configuration frame that can be sent or received from a PMU.
<CLSCompliant(False)> _
Public Class ConfigurationFrameCollection

    Inherits ChannelFrameCollectionBase(Of IConfigurationFrame)

    Public Sub New(ByVal maximumCount As Integer)

        MyBase.New(maximumCount)

    End Sub

    Public Overrides ReadOnly Property InheritedType() As Type
        Get
            Return Me.GetType()
        End Get
    End Property

End Class
