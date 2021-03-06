﻿//******************************************************************************************************
//  PhasorMeasurementUserControl.xaml.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  05/12/2011 - Magdiel D. Lorenzo
//       Generated original version of source code.
//  05/13/2011 - Mehulbhai P Thakkar
//       Added constructor overload to handle device specific data.
//  07/12/2011 - Stephen C. Wills
//       Migrated phasor-specific code from MeasurementUserControl to here.
//
//******************************************************************************************************

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GSF.PhasorProtocols.UI.ViewModels;

namespace GSF.PhasorProtocols.UI.UserControls
{
    /// <summary>
    /// Interaction logic for PhasorMeasurementUserControl.xaml
    /// </summary>
    public partial class PhasorMeasurementUserControl : UserControl
    {
        #region [ Members ]

        private bool m_committing;
        private readonly PhasorMeasurements m_dataContext;
        private DataGridColumn m_sortColumn;
        private string m_sortMemberPath;
        private ListSortDirection m_sortDirection;

        #endregion

        #region [ Constructor ]

        /// <summary>
        /// Creates an instance of <see cref="PhasorMeasurementUserControl"/> class.
        /// </summary>
        public PhasorMeasurementUserControl()
            : this(0)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="PhasorMeasurementUserControl"/> class.
        /// </summary>
        /// <param name="deviceID">ID of the device to filter measurements.</param>
        public PhasorMeasurementUserControl(int deviceID)
        {
            InitializeComponent();
            this.Unloaded += PhasorMeasurementUserControl_Unloaded;
            m_dataContext = new PhasorMeasurements(deviceID, 17);
            m_dataContext.PropertyChanged += ViewModel_PropertyChanged;
            m_dataContext.SearchCategories = AdvancedSearch.Categories;
            this.DataContext = m_dataContext;
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Handles loaded event.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Arguments of the event.</param>
        private void PhasorMeasurementUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            m_dataContext.LoadSettings();
            Application.Current.Exit += Application_Exit;
        }

        /// <summary>
        /// Handles unloaded event.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Arguments of the event.</param>
        private void PhasorMeasurementUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit -= Application_Exit;
            m_dataContext.ProcessPropertyChange();
            m_dataContext.SaveSettings();
        }

        /// <summary>
        /// Handles the application exit event.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Arguments of the event.</param>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            m_dataContext.ProcessPropertyChange();
            m_dataContext.SaveSettings();
        }

        /// <summary>
        /// Handles key down event.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Arguments of the event.</param>
        private void PhasorMeasurementUserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                m_dataContext.AdvancedFindIsOpen = true;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles key down event on the datagrid object.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Arguments of the event.</param>
        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DataGrid dataGrid = sender as DataGrid;

                if ((object)dataGrid != null && dataGrid.SelectedItems.Count > 0)
                {
                    if (MessageBox.Show("Are you sure you want to delete " + dataGrid.SelectedItems.Count + " selected item(s)?", "Delete Selected Items", MessageBoxButton.YesNo) == MessageBoxResult.No)
                        e.Handled = true;
                }
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!m_committing && e.EditAction == DataGridEditAction.Commit)
            {
                m_committing = true;
                DataGridList.CommitEdit(DataGridEditingUnit.Row, true);
                m_dataContext.ProcessPropertyChange();
                m_committing = false;
            }
        }

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (e.Column.SortMemberPath != m_sortMemberPath)
                m_sortDirection = ListSortDirection.Ascending;
            else if (m_sortDirection == ListSortDirection.Ascending)
                m_sortDirection = ListSortDirection.Descending;
            else
                m_sortDirection = ListSortDirection.Ascending;

            m_sortColumn = e.Column;
            m_sortMemberPath = e.Column.SortMemberPath;
            m_dataContext.SortData(m_sortMemberPath, m_sortDirection);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ItemsSource")
            {
                Dispatcher.BeginInvoke(new Action(SortDataGrid));
            }
            else if (e.PropertyName == "AdvancedFindIsOpen")
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TextBox searchTextBox = m_dataContext.AdvancedFindIsOpen
                        ? AdvancedSearch.FindName("SearchTextBox") as TextBox
                        : SearchTextBox;

                    if ((object)searchTextBox != null)
                    {
                        searchTextBox.Focus();
                        searchTextBox.SelectAll();
                    }
                }));
            }
        }

        private void SortDataGrid()
        {
            if ((object)m_sortColumn != null)
            {
                m_sortColumn.SortDirection = m_sortDirection;
                DataGridList.Items.SortDescriptions.Clear();
                DataGridList.Items.SortDescriptions.Add(new SortDescription(m_sortMemberPath, m_sortDirection));
                DataGridList.Items.Refresh();
            }
        }

        private void GridDetailView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (m_dataContext.IsNewRecord)
                DataGridList.SelectedIndex = -1;
        }

        #endregion
    }
}
