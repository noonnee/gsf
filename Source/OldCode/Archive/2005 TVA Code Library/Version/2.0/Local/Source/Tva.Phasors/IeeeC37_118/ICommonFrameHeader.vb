'*******************************************************************************************************
'  ICommonFrameHeader.vb - IEEE C37.118 Common frame header interface
'  Copyright � 2005 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: J. Ritchie Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  01/14/2005 - J. Ritchie Carroll
'       Initial version of source generated
'
'*******************************************************************************************************

Namespace IeeeC37_118

    <CLSCompliant(False)> _
    Public Interface ICommonFrameHeader

        Inherits IChannelFrame

        'Property RevisionNumber() As RevisionNumber

        Shadows Property FrameType() As FrameType

        Property Version() As Byte

        Property FrameLength() As Int16

        Property InternalTimeQualityFlags() As Int32

        ReadOnly Property TimeBase() As Int32

        ReadOnly Property SecondOfCentury() As UInt32

        ReadOnly Property FractionOfSecond() As Int32

        Property TimeQualityFlags() As TimeQualityFlags

        Property TimeQualityIndicatorCode() As TimeQualityIndicatorCode

    End Interface

End Namespace
