'*******************************************************************************************************
'  IDataCell.vb - Data cell interface
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
'  04/16/2005 - J. Ritchie Carroll
'       Initial version of source generated
'
'*******************************************************************************************************

Imports TVA.Measurements

''' <summary>This interface represents the protocol independent representation of a set of phasor related data values.</summary>
<CLSCompliant(False)> _
Public Interface IDataCell

    Inherits IChannelCell, IMeasurement

    Shadows ReadOnly Property Parent() As IDataFrame

    Shadows ReadOnly Property This() As IDataCell

    Property ConfigurationCell() As IConfigurationCell

    ReadOnly Property StationName() As String

    ReadOnly Property IDLabel() As String

    Property StatusFlags() As Int16

    Property CommonStatusFlags() As Int32

    ReadOnly Property AllValuesAssigned() As Boolean

    ReadOnly Property PhasorValues() As PhasorValueCollection

    Property FrequencyValue() As IFrequencyValue

    ReadOnly Property AnalogValues() As AnalogValueCollection

    ReadOnly Property DigitalValues() As DigitalValueCollection

    ' These properties correspond to the CommonStatusFlags enumeration
    ' allowing all protocols to implement a common set of status flags

    Property DataIsValid() As Boolean

    Property SynchronizationIsValid() As Boolean

    Property DataSortingType() As DataSortingType

    Property PmuError() As Boolean

End Interface