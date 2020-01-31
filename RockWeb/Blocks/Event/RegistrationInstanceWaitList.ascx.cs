﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Newtonsoft.Json;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Event
{
    /// <summary>
    /// Block for editing an event registration instance.
    /// </summary>
    [DisplayName( "Registration Instance - Wait List" )]
    [Category( "Event" )]
    [Description( "Block for editing the wait list associated with an event registration instance." )]

    #region Block Attributes

    [LinkedPage( "Wait List Process Page", "The page for moving a person from the wait list to a full registrant.", true, "", "", 8 )]
    [LinkedPage( "Registration Page", "The page for editing registration and registrant information", true, "", "", 1 )]

    #endregion

    public partial class RegistrationInstanceWaitList : Rock.Web.UI.RockBlock
    {
        #region Keys

        /// <summary>
        /// Keys for block attributes
        /// </summary>
        private static class AttributeKey
        {
            /// <summary>
            /// The page for editing a registration instance.
            /// </summary>
            public const string LinkageDetailPage = "LinkagePage";
        }

        #endregion

        #region Fields

        private Dictionary<int, Location> _homeAddresses = new Dictionary<int, Location>();
        private Dictionary<int, PhoneNumber> _mobilePhoneNumbers = new Dictionary<int, PhoneNumber>();
        private Dictionary<int, PhoneNumber> _homePhoneNumbers = new Dictionary<int, PhoneNumber>();
        private List<int> _waitListOrder = null;
        private bool _isExporting = false;

        /// <summary>
        /// Gets or sets the available registration attributes where IsGridColumn = true
        /// </summary>
        /// <value>
        /// The available attributes.
        /// </value>
        public List<AttributeCache> AvailableRegistrationAttributesForGrid { get; set; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the registrant form fields that were configured as 'Show on Grid' for the registration template
        /// </summary>
        /// <value>
        /// The registrant fields.
        /// </value>
        public List<RegistrantFormField> RegistrantFields { get; set; }

        /// <summary>
        /// Gets or sets the person campus ids.
        /// </summary>
        /// <value>
        /// The person campus ids.
        /// </value>
        private Dictionary<int, List<int>> PersonCampusIds { get; set; }

        /// <summary>
        /// Gets or sets the group links.
        /// </summary>
        /// <value>
        /// The group links.
        /// </value>
        private Dictionary<int, string> GroupLinks { get; set; }

        /// <summary>
        /// Gets or sets the registration template identifier.
        /// </summary>
        /// <value>
        /// The registration template identifier.
        /// </value>
        protected int? RegistrationTemplateId { get; set; }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            AvailableRegistrationAttributesForGrid = ViewState["AvailableRegistrationAttributesForGrid"] as List<AttributeCache>;

            RegistrationTemplateId = ViewState["RegistrationTemplateId"] as int? ?? 0;

            // don't set the values if this is a postback from a grid 'ClearFilter'
            bool setValues = this.Request.Params["__EVENTTARGET"] == null || !this.Request.Params["__EVENTTARGET"].EndsWith( "_lbClearFilter" );
            SetUserPreferencePrefix( RegistrationTemplateId.Value );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            fWaitList.ApplyFilterClick += fWaitList_ApplyFilterClick;
            gWaitList.DataKeyNames = new string[] { "Id" };
            gWaitList.Actions.ShowAdd = true;
            gWaitList.Actions.AddClick += gWaitList_AddClick;
            gWaitList.RowDataBound += gWaitList_RowDataBound;
            gWaitList.GridRebind += gWaitList_GridRebind;

            // add button to the wait list action grid
            Button btnProcessWaitlist = new Button();
            btnProcessWaitlist.CssClass = "pull-left margin-l-none btn btn-sm btn-default";
            btnProcessWaitlist.Text = "Move From Wait List";
            btnProcessWaitlist.Click += btnProcessWaitlist_Click;
            gWaitList.Actions.AddCustomActionControl( btnProcessWaitlist );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            InitializeActiveRegistrationInstance();

            if ( !Page.IsPostBack )
            {
                ShowDetail();
            }
        }

        /// <summary>
        /// Saves any user control view-state changes that have occurred since the last page postback.
        /// </summary>
        /// <returns>
        /// Returns the user control's current view state. If there is no view state associated with the control, it returns null.
        /// </returns>
        protected override object SaveViewState()
        {
            ViewState["RegistrationTemplateId"] = RegistrationTemplateId;
            ViewState["AvailableRegistrationAttributesForGrid"] = AvailableRegistrationAttributesForGrid;

            return base.SaveViewState();
        }

        /// <summary>
        /// Gets the bread crumbs.
        /// </summary>
        /// <param name="pageReference">The page reference.</param>
        /// <returns></returns>
        public override List<BreadCrumb> GetBreadCrumbs( PageReference pageReference )
        {
            var breadCrumbs = new List<BreadCrumb>();

            int? registrationInstanceId = PageParameter( pageReference, "RegistrationInstanceId" ).AsIntegerOrNull();
            if ( registrationInstanceId.HasValue )
            {
                RegistrationInstance registrationInstance = GetRegistrationInstance( registrationInstanceId.Value );
                if ( registrationInstance != null )
                {
                    breadCrumbs.Add( new BreadCrumb( registrationInstance.ToString(), pageReference ) );
                    return breadCrumbs;
                }
            }

            breadCrumbs.Add( new BreadCrumb( "New Registration Instance", pageReference ) );
            return breadCrumbs;
        }

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the RowSelected event of the gWaitList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gWaitList_RowSelected( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var registrantService = new RegistrationRegistrantService( rockContext );
                var registrant = registrantService.Get( e.RowKeyId );
                if ( registrant != null )
                {
                    var qryParams = new Dictionary<string, string>();
                    qryParams.Add( "RegistrationId", registrant.RegistrationId.ToString() );
                    string url = LinkedPageUrl( "RegistrationPage", qryParams );
                    url += "#" + e.RowKeyValue;
                    Response.Redirect( url, false );
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnProcessWaitlist control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnProcessWaitlist_Click( object sender, EventArgs e )
        {
            // create entity set with selected individuals
            var keys = gWaitList.SelectedKeys.ToList();
            if ( keys.Any() )
            {
                var entitySet = new Rock.Model.EntitySet();
                entitySet.EntityTypeId = Rock.Web.Cache.EntityTypeCache.Get<Rock.Model.RegistrationRegistrant>().Id;
                entitySet.ExpireDateTime = RockDateTime.Now.AddMinutes( 20 );

                foreach ( var key in keys )
                {
                    try
                    {
                        var item = new Rock.Model.EntitySetItem();
                        item.EntityId = ( int ) key;
                        entitySet.Items.Add( item );
                    }
                    catch { }
                }

                if ( entitySet.Items.Any() )
                {
                    var rockContext = new RockContext();
                    var service = new Rock.Model.EntitySetService( rockContext );
                    service.Add( entitySet );
                    rockContext.SaveChanges();

                    // redirect to the waitlist page
                    Dictionary<string, string> queryParms = new Dictionary<string, string>();
                    queryParms.Add( "WaitListSetId", entitySet.Id.ToString() );
                    NavigateToLinkedPage( "WaitListProcessPage", queryParms );
                }
            }
        }

        /// <summary>
        /// Handles the AddClick event of the gWaitList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gWaitList_AddClick( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "RegistrationPage", "RegistrationId", 0, "RegistrationInstanceId", hfRegistrationInstanceId.ValueAsInt() );
        }

        /// <summary>
        /// Handles the ApplyFilterClick event of the fWaitList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fWaitList_ApplyFilterClick( object sender, EventArgs e )
        {
            fWaitList.SaveUserPreference( "WL-Date Range", "Date Range", drpWaitListDateRange.DelimitedValues );
            fWaitList.SaveUserPreference( "WL-First Name", "First Name", tbWaitListFirstName.Text );
            fWaitList.SaveUserPreference( "WL-Last Name", "Last Name", tbWaitListLastName.Text );

            if ( RegistrantFields != null )
            {
                foreach ( var field in RegistrantFields )
                {
                    if ( field.FieldSource == RegistrationFieldSource.PersonField && field.PersonFieldType.HasValue )
                    {
                        switch ( field.PersonFieldType.Value )
                        {
                            case RegistrationPersonFieldType.Campus:
                                var ddlCampus = phWaitListFormFieldFilters.FindControl( "ddlWaitlistCampus" ) as RockDropDownList;
                                if ( ddlCampus != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-Home Campus", "Home Campus", ddlCampus.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbEmailFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistEmailFilter" ) as RockTextBox;
                                if ( tbEmailFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-Email", "Email", tbEmailFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpBirthdateFilter = phWaitListFormFieldFilters.FindControl( "drpWaitlistBirthdateFilter" ) as DateRangePicker;
                                if ( drpBirthdateFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-Birthdate Range", "Birthdate Range", drpBirthdateFilter.DelimitedValues );
                                }

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbMiddleNameFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistMiddleNameFilter" ) as RockTextBox;
                                if ( tbMiddleNameFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-MiddleName", "MiddleName", tbMiddleNameFilter.Text );
                                }
                                break;
                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drpAnniversaryDateFilter = phWaitListFormFieldFilters.FindControl( "drpWaitlistAnniversaryDateFilter" ) as DateRangePicker;
                                if ( drpAnniversaryDateFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-AnniversaryDate Range", "AnniversaryDate Range", drpAnniversaryDateFilter.DelimitedValues );
                                }
                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpGradeFilter = phWaitListFormFieldFilters.FindControl( "gpWaitlistGradeFilter" ) as GradePicker;
                                if ( gpGradeFilter != null )
                                {
                                    int? gradeOffset = gpGradeFilter.SelectedValueAsInt( false );
                                    fWaitList.SaveUserPreference( "WL-Grade", "Grade", gradeOffset.HasValue ? gradeOffset.Value.ToString() : string.Empty );
                                }

                                break;

                            case RegistrationPersonFieldType.Gender:
                                var ddlGenderFilter = phWaitListFormFieldFilters.FindControl( "ddlWaitlistGenderFilter" ) as RockDropDownList;
                                if ( ddlGenderFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-Gender", "Gender", ddlGenderFilter.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpMaritalStatusFilter = phWaitListFormFieldFilters.FindControl( "dvpWaitlistMaritalStatusFilter" ) as DefinedValuePicker;
                                if ( dvpMaritalStatusFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-Marital Status", "Marital Status", dvpMaritalStatusFilter.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.MobilePhone:
                                var tbMobilePhoneFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistMobilePhoneFilter" ) as RockTextBox;
                                if ( tbMobilePhoneFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-Phone", "Cell Phone", tbMobilePhoneFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                var tbWaitlistHomePhoneFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistHomePhoneFilter" ) as RockTextBox;
                                if ( tbWaitlistHomePhoneFilter != null )
                                {
                                    fWaitList.SaveUserPreference( "WL-HomePhone", "Home Phone", tbWaitlistHomePhoneFilter.Text );
                                }

                                break;
                        }
                    }

                    if ( field.Attribute != null )
                    {
                        var attribute = field.Attribute;
                        var filterControl = phWaitListFormFieldFilters.FindControl( "filterWaitlist_" + attribute.Id.ToString() );
                        if ( filterControl != null )
                        {
                            try
                            {
                                var values = attribute.FieldType.Field.GetFilterValues( filterControl, field.Attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                                fWaitList.SaveUserPreference( "WL-" + attribute.Key, attribute.Name, attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter ).ToJson() );
                            }
                            catch { }
                        }
                    }
                }
            }

            BindWaitListGrid();
        }

        /// <summary>
        /// Handles the ClearFilterClick event of the fWaitList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fWaitList_ClearFilterClick( object sender, EventArgs e )
        {
            fWaitList.DeleteUserPreferences();

            foreach ( var control in phWaitListFormFieldFilters.ControlsOfTypeRecursive<Control>().Where( a => a.ID != null && a.ID.StartsWith( "filter" ) && a.ID.Contains( "_" ) ) )
            {
                var attributeId = control.ID.Split( '_' )[1].AsInteger();
                var attribute = AttributeCache.Get( attributeId );
                if ( attribute != null )
                {
                    attribute.FieldType.Field.SetFilterValues( control, attribute.QualifierValues, new List<string>() );
                }
            }

            if ( RegistrantFields != null )
            {
                foreach ( var field in RegistrantFields )
                {
                    if ( field.FieldSource == RegistrationFieldSource.PersonField && field.PersonFieldType.HasValue )
                    {
                        switch ( field.PersonFieldType.Value )
                        {
                            case RegistrationPersonFieldType.Campus:
                                var ddlCampus = phWaitListFormFieldFilters.FindControl( "ddlWaitlistCampus" ) as RockDropDownList;
                                if ( ddlCampus != null )
                                {
                                    ddlCampus.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbEmailFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistEmailFilter" ) as RockTextBox;
                                if ( tbEmailFilter != null )
                                {
                                    tbEmailFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpBirthdateFilter = phWaitListFormFieldFilters.FindControl( "drpWaitlistBirthdateFilter" ) as DateRangePicker;
                                if ( drpBirthdateFilter != null )
                                {
                                    drpBirthdateFilter.UpperValue = null;
                                    drpBirthdateFilter.LowerValue = null;
                                }

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbMiddleNameFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistMiddleNameFilter" ) as RockTextBox;
                                if ( tbMiddleNameFilter != null )
                                {
                                    tbMiddleNameFilter.Text = string.Empty;
                                }
                                break;
                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drpAnniversaryDateFilter = phWaitListFormFieldFilters.FindControl( "drpWaitlistAnniversaryDateFilter" ) as DateRangePicker;
                                if ( drpAnniversaryDateFilter != null )
                                {
                                    drpAnniversaryDateFilter.UpperValue = null;
                                    drpAnniversaryDateFilter.LowerValue = null;
                                }
                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpGradeFilter = phWaitListFormFieldFilters.FindControl( "gpWaitlistGradeFilter" ) as GradePicker;
                                if ( gpGradeFilter != null )
                                {
                                    gpGradeFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.Gender:
                                var ddlGenderFilter = phWaitListFormFieldFilters.FindControl( "ddlWaitlistGenderFilter" ) as RockDropDownList;
                                if ( ddlGenderFilter != null )
                                {
                                    ddlGenderFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpMaritalStatusFilter = phWaitListFormFieldFilters.FindControl( "dvpWaitlistMaritalStatusFilter" ) as DefinedValuePicker;
                                if ( dvpMaritalStatusFilter != null )
                                {
                                    dvpMaritalStatusFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.MobilePhone:
                                var tbMobilePhoneFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistMobilePhoneFilter" ) as RockTextBox;
                                if ( tbMobilePhoneFilter != null )
                                {
                                    tbMobilePhoneFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                var tbWaitlistHomePhoneFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistHomePhoneFilter" ) as RockTextBox;
                                if ( tbWaitlistHomePhoneFilter != null )
                                {
                                    tbWaitlistHomePhoneFilter.Text = string.Empty;
                                }

                                break;
                        }
                    }
                }
            }

            BindWaitListFilter( null );
        }

        /// <summary>
        /// fs the wait list_ display filter value.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        protected void fWaitList_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            if ( e.Key.StartsWith( "WL-" ) )
            {
                var key = e.Key.Remove( 0, 3 );

                if ( RegistrantFields != null )
                {
                    var attribute = RegistrantFields
                        .Where( a =>
                            a.Attribute != null &&
                            a.Attribute.Key == key )
                        .Select( a => a.Attribute )
                        .FirstOrDefault();

                    if ( attribute != null )
                    {
                        try
                        {
                            var values = JsonConvert.DeserializeObject<List<string>>( e.Value );
                            e.Value = attribute.FieldType.Field.FormatFilterValues( attribute.QualifierValues, values );
                            return;
                        }
                        catch { }
                    }
                }

                switch ( key )
                {
                    case "Date Range":
                    case "Birthdate Range":
                        // The value might either be from a SlidingDateRangePicker or a DateRangePicker, so try both
                        var storedValue = e.Value;
                        e.Value = SlidingDateRangePicker.FormatDelimitedValues( storedValue );
                        if ( e.Value.IsNullOrWhiteSpace() )
                        {
                            e.Value = DateRangePicker.FormatDelimitedValues( storedValue );
                        }

                        break;

                    case "Grade":
                        e.Value = Person.GradeFormattedFromGradeOffset( e.Value.AsIntegerOrNull() );
                        break;

                    case "First Name":
                    case "Last Name":
                    case "Email":
                    case "Cell Phone":
                    case "Home Phone":
                    case "Signed Document":
                        break;

                    case "Gender":
                        var gender = e.Value.ConvertToEnumOrNull<Gender>();
                        e.Value = gender.HasValue ? gender.ConvertToString() : string.Empty;
                        break;

                    case "Campus":
                        int? campusId = e.Value.AsIntegerOrNull();
                        if ( campusId.HasValue )
                        {
                            var campus = CampusCache.Get( campusId.Value );
                            e.Value = campus != null ? campus.Name : string.Empty;
                        }
                        else
                        {
                            e.Value = string.Empty;
                        }

                        break;

                    case "Marital Status":
                        int? dvId = e.Value.AsIntegerOrNull();
                        if ( dvId.HasValue )
                        {
                            var maritalStatus = DefinedValueCache.Get( dvId.Value );
                            e.Value = maritalStatus != null ? maritalStatus.Value : string.Empty;
                        }
                        else
                        {
                            e.Value = string.Empty;
                        }

                        break;

                    case "In Group":
                        e.Value = e.Value;
                        break;

                    default:
                        e.Value = string.Empty;
                        break;
                }
            }
            else
            {
                e.Value = string.Empty;
            }
        }

        /// <summary>
        /// Handles the GridRebind event of the gWaitList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridRebindEventArgs"/> instance containing the event data.</param>
        private void gWaitList_GridRebind( object sender, GridRebindEventArgs e )
        {
            string title;
            if ( _RegistrationInstance == null || _RegistrationInstance.Id == 0 )
            {
                title = ActionTitle.Add( RegistrationInstance.FriendlyTypeName ).FormatAsHtmlTitle();
            }
            else
            {
                title = _RegistrationInstance.Name;
            }


            gWaitList.ExportTitleName = title + " - Registration Wait List";
            gWaitList.ExportFilename = gWaitList.ExportFilename ?? title + " - Registration Wait List";
            BindWaitListGrid( e.IsExporting );
        }

        /// <summary>
        /// Handles the RowDataBound event of the gWaitList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        private void gWaitList_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            var registrant = e.Row.DataItem as RegistrationRegistrant;
            if ( registrant != null )
            {
                // Set the wait list individual name value
                var lWaitListIndividual = e.Row.FindControl( "lWaitListIndividual" ) as Literal;
                if ( lWaitListIndividual != null )
                {
                    if ( registrant.PersonAlias != null && registrant.PersonAlias.Person != null )
                    {
                        lWaitListIndividual.Text = registrant.PersonAlias.Person.FullNameReversed;
                    }
                    else
                    {
                        lWaitListIndividual.Text = string.Empty;
                    }
                }

                var lWaitListOrder = e.Row.FindControl( "lWaitListOrder" ) as Literal;
                if ( lWaitListOrder != null )
                {
                    lWaitListOrder.Text = ( _waitListOrder.IndexOf( registrant.Id ) + 1 ).ToString();
                }

                // Set the campus
                var lCampus = e.Row.FindControl( "lWaitlistCampus" ) as Literal;
                if ( lCampus != null && PersonCampusIds != null )
                {
                    if ( registrant.PersonAlias != null )
                    {
                        if ( PersonCampusIds.ContainsKey( registrant.PersonAlias.PersonId ) )
                        {
                            var campusIds = PersonCampusIds[registrant.PersonAlias.PersonId];
                            if ( campusIds.Any() )
                            {
                                var campusNames = new List<string>();
                                foreach ( int campusId in campusIds )
                                {
                                    var campus = CampusCache.Get( campusId );
                                    if ( campus != null )
                                    {
                                        campusNames.Add( campus.Name );
                                    }
                                }

                                lCampus.Text = campusNames.AsDelimited( "<br/>" );
                            }
                        }
                    }
                }

                var lAddress = e.Row.FindControl( "lWaitlistAddress" ) as Literal;
                if ( lAddress != null && _homeAddresses.Count() > 0 && _homeAddresses.ContainsKey( registrant.PersonId.Value ) )
                {
                    var location = _homeAddresses[registrant.PersonId.Value];
                    lAddress.Text = location != null && location.FormattedAddress.IsNotNullOrWhiteSpace() ? location.FormattedAddress : string.Empty;
                }

                var mobileField = e.Row.FindControl( "lWaitlistMobile" ) as Literal;
                if ( mobileField != null )
                {

                    var mobilePhoneNumber = _mobilePhoneNumbers[registrant.PersonId.Value];
                    if ( mobilePhoneNumber == null || mobilePhoneNumber.NumberFormatted.IsNullOrWhiteSpace() )
                    {
                        mobileField.Text = string.Empty;
                    }
                    else
                    {
                        mobileField.Text = mobilePhoneNumber.IsUnlisted ? "Unlisted" : mobilePhoneNumber.NumberFormatted;
                    }
                }

                var homePhoneField = e.Row.FindControl( "lWaitlistHomePhone" ) as Literal;
                if ( homePhoneField != null )
                {

                    var homePhoneNumber = _homePhoneNumbers[registrant.PersonId.Value];
                    if ( homePhoneNumber == null || homePhoneNumber.NumberFormatted.IsNullOrWhiteSpace() )
                    {
                        homePhoneField.Text = string.Empty;
                    }
                    else
                    {
                        homePhoneField.Text = homePhoneNumber.IsUnlisted ? "Unlisted" : homePhoneNumber.NumberFormatted;
                    }
                }
            }
        }

        #endregion

        #region Methods

        #region Main Form Methods

        private RegistrationInstance _RegistrationInstance = null;

        /// <summary>
        /// Load the active Registration Instance for the current page context.
        /// </summary>
        private void InitializeActiveRegistrationInstance()
        {
            _RegistrationInstance = null;

            int? registrationInstanceId = this.PageParameter( "RegistrationInstanceId" ).AsInteger();

            if ( registrationInstanceId != 0 )
            {
                _RegistrationInstance = GetRegistrationInstance( registrationInstanceId.Value );
            }

            hfRegistrationInstanceId.Value = registrationInstanceId.ToString();
        }

        /// <summary>
        /// Gets the registration instance.
        /// </summary>
        /// <param name="registrationInstanceId">The registration instance identifier.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private RegistrationInstance GetRegistrationInstance( int registrationInstanceId, RockContext rockContext = null )
        {
            string key = string.Format( "RegistrationInstance:{0}", registrationInstanceId );
            RegistrationInstance registrationInstance = RockPage.GetSharedItem( key ) as RegistrationInstance;
            if ( registrationInstance == null )
            {
                rockContext = rockContext ?? new RockContext();
                registrationInstance = new RegistrationInstanceService( rockContext )
                    .Queryable( "RegistrationTemplate,Account,RegistrationTemplate.Forms.Fields" )
                    .AsNoTracking()
                    .FirstOrDefault( i => i.Id == registrationInstanceId );
                RockPage.SaveSharedItem( key, registrationInstance );
            }

            return registrationInstance;
        }
        /*
                public void ShowDetail( int itemId )
                {
                    ShowDetail();
                }

                    */

        /// <summary>
        /// Shows the detail.
        /// </summary>
        private void ShowDetail()
        {
            int? registrationInstanceId = PageParameter( "RegistrationInstanceId" ).AsIntegerOrNull();
            int? parentTemplateId = PageParameter( "RegistrationTemplateId" ).AsIntegerOrNull();

            if ( !registrationInstanceId.HasValue )
            {
                pnlDetails.Visible = false;
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                RegistrationInstance registrationInstance = null;
                if ( registrationInstanceId.HasValue )
                {
                    registrationInstance = GetRegistrationInstance( registrationInstanceId.Value, rockContext );
                }

                if ( registrationInstance == null )
                {
                    registrationInstance = new RegistrationInstance();
                    registrationInstance.Id = 0;
                    registrationInstance.IsActive = true;
                    registrationInstance.RegistrationTemplateId = parentTemplateId ?? 0;
                }

                if ( registrationInstance.RegistrationTemplate == null && registrationInstance.RegistrationTemplateId > 0 )
                {
                    registrationInstance.RegistrationTemplate = new RegistrationTemplateService( rockContext )
                        .Get( registrationInstance.RegistrationTemplateId );
                }

                AvailableRegistrationAttributesForGrid = new List<AttributeCache>();

                int entityTypeId = new Registration().TypeId;
                foreach ( var attributeCache in new AttributeService( new RockContext() ).GetByEntityTypeQualifier( entityTypeId, "RegistrationTemplateId", registrationInstance.RegistrationTemplateId.ToString(), false )
                    .Where( a => a.IsGridColumn )
                    .OrderBy( a => a.Order )
                    .ThenBy( a => a.Name )
                    .ToAttributeCacheList() )
                {
                    AvailableRegistrationAttributesForGrid.Add( attributeCache );
                }

                pnlDetails.Visible = true;
                hfRegistrationInstanceId.Value = registrationInstance.Id.ToString();
                hfRegistrationTemplateId.Value = registrationInstance.RegistrationTemplateId.ToString();
                RegistrationTemplateId = registrationInstance.RegistrationTemplateId;

                LoadRegistrantFormFields( registrationInstance );
                SetUserPreferencePrefix( hfRegistrationTemplateId.ValueAsInt() );
                BindWaitListFilter( registrationInstance );
                BindWaitListGrid();
            }
        }

        /// <summary>
        /// Sets the user preference prefix.
        /// </summary>
        private void SetUserPreferencePrefix( int registrationTemplateId )
        {
            fWaitList.UserPreferenceKeyPrefix = string.Format( "{0}-", registrationTemplateId );
        }

        #endregion

        #region Wait List Tab

        /// <summary>
        /// Binds the wait list filter.
        /// </summary>
        /// <param name="instance">The instance.</param>
        private void BindWaitListFilter( RegistrationInstance instance )
        {
            drpWaitListDateRange.DelimitedValues = fWaitList.GetUserPreference( "WL-Date Range" );
            tbWaitListFirstName.Text = fWaitList.GetUserPreference( "WL-First Name" );
            tbWaitListLastName.Text = fWaitList.GetUserPreference( "WL-Last Name" );
        }

        /// <summary>
        /// Binds the wait list grid.
        /// </summary>
        /// <param name="isExporting">if set to <c>true</c> [is exporting].</param>
        private void BindWaitListGrid( bool isExporting = false )
        {
            _isExporting = isExporting;
            int? instanceId = hfRegistrationInstanceId.Value.AsIntegerOrNull();
            if ( instanceId.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    var registrationInstance = new RegistrationInstanceService( rockContext ).Get( instanceId.Value );

                    _waitListOrder = new RegistrationRegistrantService( rockContext ).Queryable().Where( r =>
                                            r.Registration.RegistrationInstanceId == instanceId.Value &&
                                            r.PersonAlias != null &&
                                            r.PersonAlias.Person != null &&
                                            r.OnWaitList )
                                        .OrderBy( r => r.CreatedDateTime )
                                        .Select( r => r.Id ).ToList();

                    // Start query for registrants
                    var registrationRegistrantService = new RegistrationRegistrantService( rockContext );
                    var qry = registrationRegistrantService
                    .Queryable( "PersonAlias.Person.PhoneNumbers.NumberTypeValue,Fees.RegistrationTemplateFee" ).AsNoTracking()
                    .Where( r =>
                        r.Registration.RegistrationInstanceId == instanceId.Value &&
                        r.PersonAlias != null &&
                        r.PersonAlias.Person != null &&
                        r.OnWaitList );

                    // Filter by daterange
                    if ( drpWaitListDateRange.LowerValue.HasValue )
                    {
                        qry = qry.Where( r =>
                            r.CreatedDateTime.HasValue &&
                            r.CreatedDateTime.Value >= drpWaitListDateRange.LowerValue.Value );
                    }

                    if ( drpWaitListDateRange.UpperValue.HasValue )
                    {
                        qry = qry.Where( r =>
                            r.CreatedDateTime.HasValue &&
                            r.CreatedDateTime.Value <= drpWaitListDateRange.UpperValue.Value );
                    }

                    // Filter by first name
                    if ( !string.IsNullOrWhiteSpace( tbWaitListFirstName.Text ) )
                    {
                        string rfname = tbWaitListFirstName.Text;
                        qry = qry.Where( r =>
                            r.PersonAlias.Person.NickName.StartsWith( rfname ) ||
                            r.PersonAlias.Person.FirstName.StartsWith( rfname ) );
                    }

                    // Filter by last name
                    if ( !string.IsNullOrWhiteSpace( tbWaitListLastName.Text ) )
                    {
                        string rlname = tbWaitListLastName.Text;
                        qry = qry.Where( r =>
                            r.PersonAlias.Person.LastName.StartsWith( rlname ) );
                    }

                    var personIds = qry.Select( r => r.PersonAlias.PersonId ).Distinct().ToList();
                    if ( isExporting || RegistrantFields != null && RegistrantFields.Any( f => f.PersonFieldType != null && f.PersonFieldType == RegistrationPersonFieldType.Address ) )
                    {
                        _homeAddresses = Person.GetHomeLocations( personIds );
                    }

                    SetPhoneDictionary( rockContext, personIds );

                    bool preloadCampusValues = false;
                    var registrantAttributes = new List<AttributeCache>();
                    var personAttributes = new List<AttributeCache>();
                    var groupMemberAttributes = new List<AttributeCache>();
                    var registrantAttributeIds = new List<int>();
                    var personAttributesIds = new List<int>();
                    var groupMemberAttributesIds = new List<int>();

                    if ( RegistrantFields != null )
                    {
                        // Filter by any selected
                        foreach ( var personFieldType in RegistrantFields
                            .Where( f =>
                                f.FieldSource == RegistrationFieldSource.PersonField &&
                                f.PersonFieldType.HasValue )
                            .Select( f => f.PersonFieldType.Value ) )
                        {
                            switch ( personFieldType )
                            {
                                case RegistrationPersonFieldType.Campus:
                                    preloadCampusValues = true;

                                    var ddlCampus = phWaitListFormFieldFilters.FindControl( "ddlWaitlistCampus" ) as RockDropDownList;
                                    if ( ddlCampus != null )
                                    {
                                        var campusId = ddlCampus.SelectedValue.AsIntegerOrNull();
                                        if ( campusId.HasValue )
                                        {
                                            var familyGroupTypeGuid = Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid();
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.Members.Any( m =>
                                                    m.Group.GroupType.Guid == familyGroupTypeGuid &&
                                                    m.Group.CampusId.HasValue &&
                                                    m.Group.CampusId.Value == campusId ) );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.Email:
                                    var tbEmailFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistEmailFilter" ) as RockTextBox;
                                    if ( tbEmailFilter != null && !string.IsNullOrWhiteSpace( tbEmailFilter.Text ) )
                                    {
                                        qry = qry.Where( r =>
                                            r.PersonAlias.Person.Email != null &&
                                            r.PersonAlias.Person.Email.Contains( tbEmailFilter.Text ) );
                                    }

                                    break;

                                case RegistrationPersonFieldType.Birthdate:
                                    var drpBirthdateFilter = phWaitListFormFieldFilters.FindControl( "drpWaitlistBirthdateFilter" ) as DateRangePicker;
                                    if ( drpBirthdateFilter != null )
                                    {
                                        if ( drpBirthdateFilter.LowerValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.BirthDate.HasValue &&
                                                r.PersonAlias.Person.BirthDate.Value >= drpBirthdateFilter.LowerValue.Value );
                                        }

                                        if ( drpBirthdateFilter.UpperValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.BirthDate.HasValue &&
                                                r.PersonAlias.Person.BirthDate.Value <= drpBirthdateFilter.UpperValue.Value );
                                        }
                                    }

                                    break;
                                case RegistrationPersonFieldType.MiddleName:
                                    var tbWaitlistMiddleNameFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistMiddleNameFilter" ) as RockTextBox;
                                    if ( tbWaitlistMiddleNameFilter != null && !string.IsNullOrWhiteSpace( tbWaitlistMiddleNameFilter.Text ) )
                                    {
                                        qry = qry.Where( r =>
                                            r.PersonAlias.Person.MiddleName != null &&
                                            r.PersonAlias.Person.MiddleName.Contains( tbWaitlistMiddleNameFilter.Text ) );
                                    }

                                    break;

                                case RegistrationPersonFieldType.AnniversaryDate:
                                    var drpWaitlistAnniversaryDateFilter = phWaitListFormFieldFilters.FindControl( "drpWaitlistAnniversaryDateFilter" ) as DateRangePicker;
                                    if ( drpWaitlistAnniversaryDateFilter != null )
                                    {
                                        if ( drpWaitlistAnniversaryDateFilter.LowerValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.AnniversaryDate.HasValue &&
                                                r.PersonAlias.Person.AnniversaryDate.Value >= drpWaitlistAnniversaryDateFilter.LowerValue.Value );
                                        }

                                        if ( drpWaitlistAnniversaryDateFilter.UpperValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.AnniversaryDate.HasValue &&
                                                r.PersonAlias.Person.AnniversaryDate.Value <= drpWaitlistAnniversaryDateFilter.UpperValue.Value );
                                        }
                                    }

                                    break;
                                case RegistrationPersonFieldType.Grade:
                                    var gpGradeFilter = phWaitListFormFieldFilters.FindControl( "gpWaitlistGradeFilter" ) as GradePicker;
                                    if ( gpGradeFilter != null )
                                    {
                                        int? graduationYear = Person.GraduationYearFromGradeOffset( gpGradeFilter.SelectedValueAsInt( false ) );
                                        if ( graduationYear.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.GraduationYear.HasValue &&
                                                r.PersonAlias.Person.GraduationYear == graduationYear.Value );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.Gender:
                                    var ddlGenderFilter = phWaitListFormFieldFilters.FindControl( "ddlWaitlistGenderFilter" ) as RockDropDownList;
                                    if ( ddlGenderFilter != null )
                                    {
                                        var gender = ddlGenderFilter.SelectedValue.ConvertToEnumOrNull<Gender>();
                                        if ( gender.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.Gender == gender );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.MaritalStatus:
                                    var dvpMaritalStatusFilter = phWaitListFormFieldFilters.FindControl( "dvpWaitlistMaritalStatusFilter" ) as DefinedValuePicker;
                                    if ( dvpMaritalStatusFilter != null )
                                    {
                                        var maritalStatusId = dvpMaritalStatusFilter.SelectedValue.AsIntegerOrNull();
                                        if ( maritalStatusId.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.MaritalStatusValueId.HasValue &&
                                                r.PersonAlias.Person.MaritalStatusValueId.Value == maritalStatusId.Value );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.MobilePhone:
                                    var tbMobilePhoneFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistMobilePhoneFilter" ) as RockTextBox;
                                    if ( tbMobilePhoneFilter != null && !string.IsNullOrWhiteSpace( tbMobilePhoneFilter.Text ) )
                                    {
                                        string numericPhone = tbMobilePhoneFilter.Text.AsNumeric();

                                        if ( !string.IsNullOrEmpty( numericPhone ) )
                                        {
                                            var phoneNumberPersonIdQry = new PhoneNumberService( rockContext )
                                                .Queryable()
                                                .Where( a => a.Number.Contains( numericPhone ) )
                                                .Select( a => a.PersonId );

                                            qry = qry.Where( r => phoneNumberPersonIdQry.Contains( r.PersonAlias.PersonId ) );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.HomePhone:
                                    var tbWaitlistHomePhoneFilter = phWaitListFormFieldFilters.FindControl( "tbWaitlistHomePhoneFilter" ) as RockTextBox;
                                    if ( tbWaitlistHomePhoneFilter != null && !string.IsNullOrWhiteSpace( tbWaitlistHomePhoneFilter.Text ) )
                                    {
                                        string numericPhone = tbWaitlistHomePhoneFilter.Text.AsNumeric();

                                        if ( !string.IsNullOrEmpty( numericPhone ) )
                                        {
                                            var phoneNumberPersonIdQry = new PhoneNumberService( rockContext )
                                                .Queryable()
                                                .Where( a => a.Number.Contains( numericPhone ) )
                                                .Select( a => a.PersonId );

                                            qry = qry.Where( r => phoneNumberPersonIdQry.Contains( r.PersonAlias.PersonId ) );
                                        }
                                    }

                                    break;
                            }
                        }

                        // Get all the registrant attributes selected to be on grid
                        registrantAttributes = RegistrantFields
                            .Where( f =>
                                f.Attribute != null &&
                                f.FieldSource == RegistrationFieldSource.RegistrantAttribute )
                            .Select( f => f.Attribute )
                            .ToList();
                        registrantAttributeIds = registrantAttributes.Select( a => a.Id ).Distinct().ToList();

                        // Filter query by any configured registrant attribute filters
                        if ( registrantAttributes != null && registrantAttributes.Any() )
                        {
                            foreach ( var attribute in registrantAttributes )
                            {
                                var filterControl = phWaitListFormFieldFilters.FindControl( "filterWaitlist_" + attribute.Id.ToString() );
                                qry = attribute.FieldType.Field.ApplyAttributeQueryFilter( qry, filterControl, attribute, registrationRegistrantService, Rock.Reporting.FilterMode.SimpleFilter );
                            }
                        }

                        // Get all the person attributes selected to be on grid
                        personAttributes = RegistrantFields
                            .Where( f =>
                                f.Attribute != null &&
                                f.FieldSource == RegistrationFieldSource.PersonAttribute )
                            .Select( f => f.Attribute )
                            .ToList();
                        personAttributesIds = personAttributes.Select( a => a.Id ).Distinct().ToList();

                        // Filter query by any configured person attribute filters
                        if ( personAttributes != null && personAttributes.Any() )
                        {
                            PersonService personService = new PersonService( rockContext );
                            var personQry = personService.Queryable().AsNoTracking();

                            foreach ( var attribute in personAttributes )
                            {
                                var filterControl = phWaitListFormFieldFilters.FindControl( "filterWaitlist_" + attribute.Id.ToString() );
                                personQry = attribute.FieldType.Field.ApplyAttributeQueryFilter( personQry, filterControl, attribute, personService, Rock.Reporting.FilterMode.SimpleFilter );
                            }

                            qry = qry.Where( r => personQry.Any( p => p.Id == r.PersonAlias.PersonId ) );
                        }

                        // Get all the group member attributes selected to be on grid
                        groupMemberAttributes = RegistrantFields
                            .Where( f =>
                                f.Attribute != null &&
                                f.FieldSource == RegistrationFieldSource.GroupMemberAttribute )
                            .Select( f => f.Attribute )
                            .ToList();
                        groupMemberAttributesIds = groupMemberAttributes.Select( a => a.Id ).Distinct().ToList();

                        // Filter query by any configured person attribute filters
                        if ( groupMemberAttributes != null && groupMemberAttributes.Any() )
                        {
                            var groupMemberService = new GroupMemberService( rockContext );
                            var groupMemberQry = groupMemberService.Queryable().AsNoTracking();

                            foreach ( var attribute in groupMemberAttributes )
                            {
                                var filterControl = phWaitListFormFieldFilters.FindControl( "filterWaitlist_" + attribute.Id.ToString() );
                                groupMemberQry = attribute.FieldType.Field.ApplyAttributeQueryFilter( groupMemberQry, filterControl, attribute, groupMemberService, Rock.Reporting.FilterMode.SimpleFilter );
                            }

                            qry = qry.Where( r => groupMemberQry.Any( g => g.Id == r.GroupMemberId ) );
                        }
                    }

                    // Sort the query
                    IOrderedQueryable<RegistrationRegistrant> orderedQry = null;
                    SortProperty sortProperty = gWaitList.SortProperty;
                    if ( sortProperty != null )
                    {
                        orderedQry = qry.Sort( sortProperty );
                    }
                    else
                    {
                        orderedQry = qry
                            .OrderBy( r => r.Id );
                    }

                    // increase the timeout just in case. A complex filter on the grid might slow things down
                    rockContext.Database.CommandTimeout = 180;

                    // Set the grids LinqDataSource which will run query and set results for current page
                    gWaitList.SetLinqDataSource<RegistrationRegistrant>( orderedQry );

                    if ( RegistrantFields != null )
                    {
                        // Get the query results for the current page
                        var currentPageRegistrants = gWaitList.DataSource as List<RegistrationRegistrant>;
                        if ( currentPageRegistrants != null )
                        {
                            // Get all the registrant ids in current page of query results
                            var registrantIds = currentPageRegistrants
                                .Select( r => r.Id )
                                .Distinct()
                                .ToList();

                            // Get all the person ids in current page of query results
                            var currentPagePersonIds = currentPageRegistrants
                                .Select( r => r.PersonAlias.PersonId )
                                .Distinct()
                                .ToList();

                            // Get all the group member ids and the group id in current page of query results
                            var groupMemberIds = new List<int>();
                            GroupLinks = new Dictionary<int, string>();
                            foreach ( var groupMember in currentPageRegistrants
                                .Where( m =>
                                    m.GroupMember != null &&
                                    m.GroupMember.Group != null )
                                .Select( m => m.GroupMember ) )
                            {
                                groupMemberIds.Add( groupMember.Id );
                                string linkedPageUrl = LinkedPageUrl( "GroupDetailPage", new Dictionary<string, string> { { "GroupId", groupMember.GroupId.ToString() } } );
                                GroupLinks.AddOrIgnore( groupMember.GroupId, isExporting ? groupMember.Group.Name : string.Format( "<a href='{0}'>{1}</a>", linkedPageUrl, groupMember.Group.Name ) );
                            }

                            // If the campus column was selected to be displayed on grid, preload all the people's
                            // campuses so that the databind does not need to query each row
                            if ( preloadCampusValues )
                            {
                                PersonCampusIds = new Dictionary<int, List<int>>();

                                Guid familyGroupTypeGuid = Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid();
                                foreach ( var personCampusList in new GroupMemberService( rockContext )
                                    .Queryable().AsNoTracking()
                                    .Where( m =>
                                        m.Group.GroupType.Guid == familyGroupTypeGuid &&
                                        currentPagePersonIds.Contains( m.PersonId ) )
                                    .GroupBy( m => m.PersonId )
                                    .Select( m => new
                                    {
                                        PersonId = m.Key,
                                        CampusIds = m
                                            .Where( g => g.Group.CampusId.HasValue )
                                            .Select( g => g.Group.CampusId.Value )
                                            .ToList()
                                    } ) )
                                {
                                    PersonCampusIds.Add( personCampusList.PersonId, personCampusList.CampusIds );
                                }
                            }

                            // If there are any attributes that were selected to be displayed, we're going
                            // to try and read all attribute values in one query and then put them into a
                            // custom grid ObjectList property so that the AttributeField columns don't need
                            // to do the LoadAttributes and querying of values for each row/column
                            if ( personAttributesIds.Any() || groupMemberAttributesIds.Any() || registrantAttributeIds.Any() )
                            {
                                // Query the attribute values for all rows and attributes
                                var attributeValues = new AttributeValueService( rockContext )
                                    .Queryable( "Attribute" ).AsNoTracking()
                                    .Where( v =>
                                        v.EntityId.HasValue &&
                                        (
                                            (
                                                personAttributesIds.Contains( v.AttributeId ) &&
                                                currentPagePersonIds.Contains( v.EntityId.Value )
                                            ) ||
                                            (
                                                groupMemberAttributesIds.Contains( v.AttributeId ) &&
                                                groupMemberIds.Contains( v.EntityId.Value )
                                            ) ||
                                            (
                                                registrantAttributeIds.Contains( v.AttributeId ) &&
                                                registrantIds.Contains( v.EntityId.Value )
                                            )
                                        ) ).ToList();

                                // Get the attributes to add to each row's object
                                var attributes = new Dictionary<string, AttributeCache>();
                                RegistrantFields
                                        .Where( f => f.Attribute != null )
                                        .Select( f => f.Attribute )
                                        .ToList()
                                    .ForEach( a => attributes
                                        .Add( a.Id.ToString() + a.Key, a ) );

                                // Initialize the grid's object list
                                gWaitList.ObjectList = new Dictionary<string, object>();

                                // Loop through each of the current page's registrants and build an attribute
                                // field object for storing attributes and the values for each of the registrants
                                foreach ( var registrant in currentPageRegistrants )
                                {
                                    // Create a row attribute object
                                    var attributeFieldObject = new AttributeFieldObject();

                                    // Add the attributes to the attribute object
                                    attributeFieldObject.Attributes = attributes;

                                    // Add any person attribute values to object
                                    attributeValues
                                        .Where( v =>
                                            personAttributesIds.Contains( v.AttributeId ) &&
                                            v.EntityId.Value == registrant.PersonAlias.PersonId )
                                        .ToList()
                                        .ForEach( v => attributeFieldObject.AttributeValues
                                            .Add( v.AttributeId.ToString() + v.Attribute.Key, new AttributeValueCache( v ) ) );

                                    // Add any group member attribute values to object
                                    if ( registrant.GroupMemberId.HasValue )
                                    {
                                        attributeValues
                                            .Where( v =>
                                                groupMemberAttributesIds.Contains( v.AttributeId ) &&
                                                v.EntityId.Value == registrant.GroupMemberId.Value )
                                            .ToList()
                                            .ForEach( v => attributeFieldObject.AttributeValues
                                                .Add( v.AttributeId.ToString() + v.Attribute.Key, new AttributeValueCache( v ) ) );
                                    }

                                    // Add any registrant attribute values to object
                                    attributeValues
                                        .Where( v =>
                                            registrantAttributeIds.Contains( v.AttributeId ) &&
                                            v.EntityId.Value == registrant.Id )
                                        .ToList()
                                        .ForEach( v => attributeFieldObject.AttributeValues
                                            .Add( v.AttributeId.ToString() + v.Attribute.Key, new AttributeValueCache( v ) ) );

                                    // Add row attribute object to grid's object list
                                    gWaitList.ObjectList.Add( registrant.Id.ToString(), attributeFieldObject );
                                }
                            }
                        }
                    }

                    gWaitList.DataBind();
                }
            }
        }

        #endregion

        #endregion

        private void SetPhoneDictionary( RockContext rockContext, List<int> personIds )
        {
            if ( RegistrantFields.Any( f => f.PersonFieldType != null && f.PersonFieldType == RegistrationPersonFieldType.MobilePhone ) )
            {
                var phoneNumberService = new PhoneNumberService( rockContext );
                foreach ( var personId in personIds )
                {
                    _mobilePhoneNumbers[personId] = phoneNumberService.GetNumberByPersonIdAndType( personId, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE );
                }
            }

            if ( RegistrantFields.Any( f => f.PersonFieldType != null && f.PersonFieldType == RegistrationPersonFieldType.HomePhone ) )
            {
                var phoneNumberService = new PhoneNumberService( rockContext );
                foreach ( var personId in personIds )
                {
                    _homePhoneNumbers[personId] = phoneNumberService.GetNumberByPersonIdAndType( personId, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME );
                }
            }
        }

        /// <summary>
        /// Gets all of the form fields that were configured as 'Show on Grid' for the registration template
        /// </summary>
        /// <param name="registrationInstance">The registration instance.</param>
        private void LoadRegistrantFormFields( RegistrationInstance registrationInstance )
        {
            RegistrantFields = new List<RegistrantFormField>();

            if ( registrationInstance != null )
            {
                foreach ( var form in registrationInstance.RegistrationTemplate.Forms )
                {
                    foreach ( var formField in form.Fields
                        .Where( f => f.IsGridField )
                        .OrderBy( f => f.Order ) )
                    {
                        if ( formField.FieldSource == RegistrationFieldSource.PersonField )
                        {
                            if ( formField.PersonFieldType != RegistrationPersonFieldType.FirstName &&
                                formField.PersonFieldType != RegistrationPersonFieldType.LastName )
                            {
                                RegistrantFields.Add(
                                    new RegistrantFormField
                                    {
                                        FieldSource = formField.FieldSource,
                                        PersonFieldType = formField.PersonFieldType
                                    } );
                            }
                        }
                        else
                        {
                            RegistrantFields.Add(
                                new RegistrantFormField
                                {
                                    FieldSource = formField.FieldSource,
                                    Attribute = AttributeCache.Get( formField.AttributeId.Value )
                                } );
                        }
                    }
                }
            }
        }

        #region Helper Classes

        /// <summary>
        /// Helper class for tracking registration form fields
        /// </summary>
        [Serializable]
        public class RegistrantFormField
        {
            /// <summary>
            /// Gets or sets the field source.
            /// </summary>
            /// <value>
            /// The field source.
            /// </value>
            public RegistrationFieldSource FieldSource { get; set; }

            /// <summary>
            /// Gets or sets the type of the person field.
            /// </summary>
            /// <value>
            /// The type of the person field.
            /// </value>
            public RegistrationPersonFieldType? PersonFieldType { get; set; }

            /// <summary>
            /// Gets or sets the attribute.
            /// </summary>
            /// <value>
            /// The attribute.
            /// </value>
            public AttributeCache Attribute { get; set; }
        }

        #endregion


    }

}