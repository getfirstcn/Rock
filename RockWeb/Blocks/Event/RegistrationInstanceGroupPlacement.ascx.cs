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
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Event
{
    /// <summary>
    /// Block for editing an event registration instance.
    /// </summary>
    [DisplayName( "Registration Instance - Group Placement" )]
    [Category( "Event" )]
    [Description( "Block for editing the group placements associated with an event registration instance." )]

    #region Block Attributes

    [LinkedPage( "Group Detail Page", "The page for viewing details about a group", true, "", "", 4 )]

    #endregion

    public partial class RegistrationInstanceGroupPlacement : Rock.Web.UI.RockBlock
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
        /// Gets or sets the signed person ids.
        /// </summary>
        /// <value>
        /// The signed person ids.
        /// </value>
        private List<int> Signers { get; set; }

        /// <summary>
        /// Gets or sets the group links.
        /// </summary>
        /// <value>
        /// The group links.
        /// </value>
        private Dictionary<int, string> GroupLinks { get; set; }

        /// <summary>
        /// Gets or sets the active tab.
        /// </summary>
        /// <value>
        /// The active tab.
        /// </value>
        //protected string ActiveTab { get; set; }

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

            //ActiveTab = ( ViewState["ActiveTab"] as string ) ?? string.Empty;
            //RegistrantFields = ViewState["RegistrantFields"] as List<RegistrantFormField>;
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

            fGroupPlacements.ApplyFilterClick += fGroupPlacements_ApplyFilterClick;
            gGroupPlacements.DataKeyNames = new string[] { "Id" };
            gGroupPlacements.Actions.ShowAdd = false;
            gGroupPlacements.RowDataBound += gRegistrants_RowDataBound; // Intentionally using same row data bound event as the gRegistrants grid
            gGroupPlacements.GridRebind += gGroupPlacements_GridRebind;

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

        #region Main Form Events

        /// <summary>
        /// Handles the RowDataBound event of the gRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        private void gRegistrants_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            var registrant = e.Row.DataItem as RegistrationRegistrant;
            if ( registrant != null )
            {
                // Set the registrant name value
                var lRegistrant = e.Row.FindControl( "lRegistrant" ) as Literal;
                if ( lRegistrant != null )
                {
                    if ( registrant.PersonAlias != null && registrant.PersonAlias.Person != null )
                    {
                        lRegistrant.Text = registrant.PersonAlias.Person.FullNameReversed +
                            ( Signers != null && !Signers.Contains( registrant.PersonAlias.PersonId ) ? " <i class='fa fa-pencil-square-o text-danger'></i>" : string.Empty );
                    }
                    else
                    {
                        lRegistrant.Text = string.Empty;
                    }
                }

                // Set the Group Name
                if ( registrant.GroupMember != null && GroupLinks.ContainsKey( registrant.GroupMember.GroupId ) )
                {
                    var lGroup = e.Row.FindControl( "lGroup" ) as Literal;
                    if ( lGroup != null )
                    {
                        lGroup.Text = GroupLinks[registrant.GroupMember.GroupId];
                    }
                }

                // Set the campus
                var lCampus = e.Row.FindControl( "lRegistrantsCampus" ) as Literal;

                // if it's null, try looking for the "lGroupPlacementsCampus" control since this RowDataBound event is shared between
                // two different grids.
                if ( lCampus == null )
                {
                    lCampus = e.Row.FindControl( "lGroupPlacementsCampus" ) as Literal;
                }

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

                // Set the Fees
                var lFees = e.Row.FindControl( "lFees" ) as Literal;
                if ( lFees != null )
                {
                    if ( registrant.Fees != null && registrant.Fees.Any() )
                    {
                        var feeDesc = new List<string>();
                        foreach ( var fee in registrant.Fees )
                        {
                            feeDesc.Add( string.Format(
                                "{0}{1} ({2})",
                                fee.Quantity > 1 ? fee.Quantity.ToString( "N0" ) + " " : string.Empty,
                                fee.Quantity > 1 ? fee.RegistrationTemplateFee.Name.Pluralize() : fee.RegistrationTemplateFee.Name,
                                fee.Cost.FormatAsCurrency() ) );
                        }

                        lFees.Text = feeDesc.AsDelimited( "<br/>" );
                    }
                }

                if ( _homeAddresses.Any() && _homeAddresses.ContainsKey( registrant.PersonId.Value ) )
                {
                    var location = _homeAddresses[registrant.PersonId.Value];
                    // break up addresses if exporting
                    if ( _isExporting )
                    {
                        var lStreet1 = e.Row.FindControl( "lStreet1" ) as Literal;
                        var lStreet2 = e.Row.FindControl( "lStreet2" ) as Literal;
                        var lCity = e.Row.FindControl( "lCity" ) as Literal;
                        var lState = e.Row.FindControl( "lState" ) as Literal;
                        var lPostalCode = e.Row.FindControl( "lPostalCode" ) as Literal;
                        var lCountry = e.Row.FindControl( "lCountry" ) as Literal;

                        if ( location != null )
                        {
                            lStreet1.Text = location.Street1;
                            lStreet2.Text = location.Street2;
                            lCity.Text = location.City;
                            lState.Text = location.State;
                            lPostalCode.Text = location.PostalCode;
                            lCountry.Text = location.Country;
                        }
                    }
                    else
                    {
                        var addressField = e.Row.FindControl( "lRegistrantsAddress" ) as Literal ?? e.Row.FindControl( "lGroupPlacementsAddress" ) as Literal;
                        if ( addressField != null )
                        {
                            addressField.Text = location != null && location.FormattedAddress.IsNotNullOrWhiteSpace() ? location.FormattedAddress : string.Empty;
                        }
                    }
                }

                if ( _mobilePhoneNumbers.Any() )
                {
                    var mobileNumber = _mobilePhoneNumbers[registrant.PersonId.Value];
                    var mobileField = e.Row.FindControl( "lRegistrantsMobile" ) as Literal ?? e.Row.FindControl( "lGroupPlacementsMobile" ) as Literal;
                    if ( mobileField != null )
                    {
                        if ( mobileNumber == null || mobileNumber.NumberFormatted.IsNullOrWhiteSpace() )
                        {
                            mobileField.Text = string.Empty;
                        }
                        else
                        {
                            mobileField.Text = mobileNumber.IsUnlisted ? "Unlisted" : mobileNumber.NumberFormatted;
                        }
                    }

                }

                if ( _homePhoneNumbers.Any() )
                {
                    var homePhoneNumber = _homePhoneNumbers[registrant.PersonId.Value];
                    var homePhoneField = e.Row.FindControl( "lRegistrantsHomePhone" ) as Literal ?? e.Row.FindControl( "lGroupPlacementsHomePhone" ) as Literal;
                    if ( homePhoneField != null )
                    {
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
        }
        #region Group Placement Tab Events

        /// <summary>
        /// Handles the GridRebind event of the gGroupPlacements control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gGroupPlacements_GridRebind( object sender, GridRebindEventArgs e )
        {
            if ( _RegistrationInstance == null )
            {
                return;
            }

            gGroupPlacements.ExportTitleName = _RegistrationInstance.Name + " - Registration Group Placements";
            gGroupPlacements.ExportFilename = gGroupPlacements.ExportFilename ?? _RegistrationInstance.Name + "RegistrationGroupPlacements";
            BindGroupPlacementGrid( e.IsExporting );
        }

        /// <summary>
        /// Handles the SelectItem event of the gpGroupPlacementParentGroup control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gpGroupPlacementParentGroup_SelectItem( object sender, EventArgs e )
        {
            int? parentGroupId = gpGroupPlacementParentGroup.SelectedValueAsInt();

            SetUserPreference(
                string.Format( "ParentGroup_{0}_{1}", BlockId, hfRegistrationInstanceId.Value ),
                parentGroupId.HasValue ? parentGroupId.Value.ToString() : string.Empty,
                true );

            var groupPickerField = gGroupPlacements.Columns.OfType<GroupPickerField>().FirstOrDefault();
            if ( groupPickerField != null )
            {
                groupPickerField.RootGroupId = parentGroupId;
            }

            BindGroupPlacementGrid();
        }

        /// <summary>
        /// Handles the Click event of the lbPlaceInGroup control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbPlaceInGroup_Click( object sender, EventArgs e )
        {
            var col = gGroupPlacements.Columns.OfType<GroupPickerField>().FirstOrDefault();
            if ( col != null )
            {
                var placements = new Dictionary<int, List<int>>();

                var colIndex = gGroupPlacements.GetColumnIndex( col ).ToString();
                foreach ( GridViewRow row in gGroupPlacements.Rows )
                {
                    GroupPicker gp = row.FindControl( "groupPicker_" + colIndex.ToString() ) as GroupPicker;
                    if ( gp != null )
                    {
                        int? groupId = gp.SelectedValueAsInt();
                        if ( groupId.HasValue )
                        {
                            int registrantId = ( int ) gGroupPlacements.DataKeys[row.RowIndex].Value;
                            placements.AddOrIgnore( groupId.Value, new List<int>() );
                            placements[groupId.Value].Add( registrantId );
                        }
                    }
                }

                using ( var rockContext = new RockContext() )
                {
                    try
                    {
                        rockContext.WrapTransaction( () =>
                        {
                            var groupMemberService = new GroupMemberService( rockContext );

                            // Get all the registrants that were selected
                            var registrantIds = placements.SelectMany( p => p.Value ).ToList();
                            var registrants = new RegistrationRegistrantService( rockContext )
                                .Queryable( "PersonAlias" ).AsNoTracking()
                                .Where( r => registrantIds.Contains( r.Id ) )
                                .ToList();

                            // Get any groups that were selected
                            var groupIds = placements.Keys.ToList();
                            foreach ( var group in new GroupService( rockContext )
                                .Queryable( "GroupType" ).AsNoTracking()
                                .Where( g => groupIds.Contains( g.Id ) ) )
                            {
                                foreach ( int registrantId in placements[group.Id] )
                                {
                                    int? roleId = group.GroupType.DefaultGroupRoleId;
                                    if ( !roleId.HasValue )
                                    {
                                        roleId = group.GroupType.Roles
                                            .OrderBy( r => r.Order )
                                            .Select( r => r.Id )
                                            .FirstOrDefault();
                                    }

                                    var registrant = registrants.FirstOrDefault( r => r.Id == registrantId );
                                    if ( registrant != null && roleId.HasValue && roleId.Value > 0 )
                                    {
                                        var groupMember = groupMemberService.Queryable().AsNoTracking()
                                            .FirstOrDefault( m =>
                                                m.PersonId == registrant.PersonAlias.PersonId &&
                                                m.GroupId == group.Id &&
                                                m.GroupRoleId == roleId.Value );
                                        if ( groupMember == null )
                                        {
                                            groupMember = new GroupMember();
                                            groupMember.PersonId = registrant.PersonAlias.PersonId;
                                            groupMember.GroupId = group.Id;
                                            groupMember.GroupRoleId = roleId.Value;
                                            groupMember.GroupMemberStatus = GroupMemberStatus.Active;

                                            if ( !groupMember.IsValidGroupMember( rockContext ) )
                                            {
                                                throw new Exception( string.Format(
                                                    "Placing '{0}' in the '{1}' group is not valid for the following reason: {2}",
                                                    registrant.Person.FullName,
                                                    group.Name,
                                                    groupMember.ValidationResults.Select( a => a.ErrorMessage ).ToList().AsDelimited( "<br />" ) ) );
                                            }

                                            groupMemberService.Add( groupMember );

                                            if ( cbSetGroupAttributes.Checked )
                                            {
                                                registrant.LoadAttributes( rockContext );
                                                groupMember.LoadAttributes( rockContext );
                                                foreach ( var attr in groupMember.Attributes.Where( m => registrant.Attributes.Keys.Contains( m.Key ) ) )
                                                {
                                                    groupMember.SetAttributeValue( attr.Key, registrant.GetAttributeValue( attr.Key ) );
                                                }
                                            }

                                            rockContext.SaveChanges();
                                            groupMember.SaveAttributeValues( rockContext );
                                        }
                                    }
                                }
                            }
                        } );

                        nbPlacementNotifiction.NotificationBoxType = NotificationBoxType.Success;
                        nbPlacementNotifiction.Text = "Registrants were successfully placed in the selected groups.";
                        nbPlacementNotifiction.Visible = true;
                    }
                    catch ( Exception ex )
                    {
                        nbPlacementNotifiction.NotificationBoxType = NotificationBoxType.Danger;
                        nbPlacementNotifiction.Text = ex.Message;
                        nbPlacementNotifiction.Visible = true;
                    }
                }
            }

            BindGroupPlacementGrid();
        }

        #endregion

        #endregion

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


                //hlType.Visible = registrationInstance.RegistrationTemplate != null;
                //hlType.Text = registrationInstance.RegistrationTemplate != null ? registrationInstance.RegistrationTemplate.Name : string.Empty;

                //lWizardTemplateName.Text = hlType.Text;

                pnlDetails.Visible = true;
                hfRegistrationInstanceId.Value = registrationInstance.Id.ToString();
                hfRegistrationTemplateId.Value = registrationInstance.RegistrationTemplateId.ToString();
                RegistrationTemplateId = registrationInstance.RegistrationTemplateId;

                // render UI based on Authorized
                bool readOnly = false;

                bool canEdit = UserCanEdit ||
                    registrationInstance.IsAuthorized( Authorization.EDIT, CurrentPerson ) ||
                    registrationInstance.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson );

                //nbEditModeMessage.Text = string.Empty;

                // User must have 'Edit' rights to block, or 'Edit' or 'Administrate' rights to instance
                if ( !canEdit )
                {
                    readOnly = true;
                    //nbEditModeMessage.Heading = "Information";
                    //nbEditModeMessage.Text = EditModeMessage.NotAuthorizedToEdit( RegistrationInstance.FriendlyTypeName );
                }


                LoadRegistrantFormFields( registrationInstance );
                SetUserPreferencePrefix( hfRegistrationTemplateId.ValueAsInt() );
                BindGroupPlacementsFilter( registrationInstance );
                BindGroupPlacementGrid();
            }
        }

        /// <summary>
        /// Sets the user preference prefix.
        /// </summary>
        private void SetUserPreferencePrefix( int registrationTemplateId )
        {
            fGroupPlacements.UserPreferenceKeyPrefix = string.Format( "{0}-", registrationTemplateId );
        }

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

        #region Group Placement Tab

        private DateRange GetDateRangeFromContext()
        {
            //var dateRange = SlidingDateRangePicker.CalculateDateRangeFromDelimitedValues( sdrpRegistrationDateRange.DelimitedValues );
            var dateRange = RockPage.GetSharedItem( "RegistrationDateRange" ) as DateRange;

            return dateRange ?? new DateRange();
        }

        /// <summary>
        /// Binds the group placement grid.
        /// </summary>
        /// <param name="isExporting">if set to <c>true</c> [is exporting].</param>
        private void BindGroupPlacementGrid( bool isExporting = false )
        {
            _isExporting = isExporting;
            int? parentGroupId = gpGroupPlacementParentGroup.SelectedValueAsInt();
            int? instanceId = hfRegistrationInstanceId.Value.AsIntegerOrNull();
            if ( instanceId.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    // Start query for registrants
                    var registrationRegistrantService = new RegistrationRegistrantService( rockContext );
                    var qry = registrationRegistrantService
                        .Queryable( "PersonAlias.Person.PhoneNumbers.NumberTypeValue,Fees.RegistrationTemplateFee,GroupMember.Group" ).AsNoTracking()
                        .Where( r =>
                            r.Registration.RegistrationInstanceId == instanceId.Value &&
                            r.PersonAlias != null &&
                            r.OnWaitList == false &&
                            r.PersonAlias.Person != null );

                    if ( parentGroupId.HasValue )
                    {
                        var validGroupIds = new GroupService( rockContext ).GetAllDescendentGroupIds( parentGroupId.Value, false );

                        var existingPeopleInGroups = new GroupMemberService( rockContext )
                            .Queryable().AsNoTracking()
                            .Where( m => validGroupIds.Contains( m.GroupId ) && m.Group.IsActive && m.GroupMemberStatus == GroupMemberStatus.Active )
                            .Select( m => m.PersonId )
                            .ToList();

                        qry = qry.Where( r => !existingPeopleInGroups.Contains( r.PersonAlias.PersonId ) );
                    }

                    // Filter by daterange
                    var dateRange = GetDateRangeFromContext();

                    if ( dateRange.Start.HasValue )
                    {
                        qry = qry.Where( r =>
                            r.CreatedDateTime.HasValue &&
                            r.CreatedDateTime.Value >= dateRange.Start.Value );
                    }

                    if ( dateRange.End.HasValue )
                    {
                        qry = qry.Where( r =>
                            r.CreatedDateTime.HasValue &&
                            r.CreatedDateTime.Value < dateRange.End.Value );
                    }

                    // Filter by first name
                    if ( !string.IsNullOrWhiteSpace( tbGroupPlacementsFirstName.Text ) )
                    {
                        string rfname = tbGroupPlacementsFirstName.Text;
                        qry = qry.Where( r =>
                            r.PersonAlias.Person.NickName.StartsWith( rfname ) ||
                            r.PersonAlias.Person.FirstName.StartsWith( rfname ) );
                    }

                    // Filter by last name
                    if ( !string.IsNullOrWhiteSpace( tbGroupPlacementsLastName.Text ) )
                    {
                        string rlname = tbGroupPlacementsLastName.Text;
                        qry = qry.Where( r =>
                            r.PersonAlias.Person.LastName.StartsWith( rlname ) );
                    }

                    var personIds = qry.Select( r => r.PersonAlias.PersonId ).Distinct().ToList();
                    if ( isExporting || RegistrantFields != null && RegistrantFields.Any( f => f.PersonFieldType == RegistrationPersonFieldType.Address ) )
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

                                    var ddlCampus = phGroupPlacementsFormFieldFilters.FindControl( "ddlGroupPlacementsCampus" ) as RockDropDownList;
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
                                    var tbEmailFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsEmailFilter" ) as RockTextBox;
                                    if ( tbEmailFilter != null && !string.IsNullOrWhiteSpace( tbEmailFilter.Text ) )
                                    {
                                        qry = qry.Where( r =>
                                            r.PersonAlias.Person.Email != null &&
                                            r.PersonAlias.Person.Email.Contains( tbEmailFilter.Text ) );
                                    }

                                    break;

                                case RegistrationPersonFieldType.Birthdate:
                                    var drpBirthdateFilter = phGroupPlacementsFormFieldFilters.FindControl( "drpGroupPlacementsBirthdateFilter" ) as DateRangePicker;
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
                                    var tbGroupPlacementsMiddleNameFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsMiddleNameFilter" ) as RockTextBox;
                                    if ( tbGroupPlacementsMiddleNameFilter != null && !string.IsNullOrWhiteSpace( tbGroupPlacementsMiddleNameFilter.Text ) )
                                    {
                                        qry = qry.Where( r =>
                                            r.PersonAlias.Person.MiddleName != null &&
                                            r.PersonAlias.Person.MiddleName.Contains( tbGroupPlacementsMiddleNameFilter.Text ) );
                                    }

                                    break;

                                case RegistrationPersonFieldType.AnniversaryDate:
                                    var drpGroupPlacementsAnniversaryDateFilter = phGroupPlacementsFormFieldFilters.FindControl( "drpGroupPlacementsAnniversaryDateFilter" ) as DateRangePicker;
                                    if ( drpGroupPlacementsAnniversaryDateFilter != null )
                                    {
                                        if ( drpGroupPlacementsAnniversaryDateFilter.LowerValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.AnniversaryDate.HasValue &&
                                                r.PersonAlias.Person.AnniversaryDate.Value >= drpGroupPlacementsAnniversaryDateFilter.LowerValue.Value );
                                        }

                                        if ( drpGroupPlacementsAnniversaryDateFilter.UpperValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.AnniversaryDate.HasValue &&
                                                r.PersonAlias.Person.AnniversaryDate.Value <= drpGroupPlacementsAnniversaryDateFilter.UpperValue.Value );
                                        }
                                    }

                                    break;
                                case RegistrationPersonFieldType.Grade:
                                    var gpGradeFilter = phGroupPlacementsFormFieldFilters.FindControl( "gpGroupPlacementsGradeFilter" ) as GradePicker;
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
                                    var ddlGenderFilter = phGroupPlacementsFormFieldFilters.FindControl( "ddlGroupPlacementsGenderFilter" ) as RockDropDownList;
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
                                    var dvpMaritalStatusFilter = phGroupPlacementsFormFieldFilters.FindControl( "dvpGroupPlacementsMaritalStatusFilter" ) as DefinedValuePicker;
                                    if ( dvpMaritalStatusFilter != null )
                                    {
                                        var maritalStatusId = dvpMaritalStatusFilter.SelectedValueAsId();
                                        if ( maritalStatusId.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.MaritalStatusValueId.HasValue &&
                                                r.PersonAlias.Person.MaritalStatusValueId.Value == maritalStatusId.Value );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.MobilePhone:
                                    var tbMobilePhoneFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsMobilePhoneFilter" ) as RockTextBox;
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
                                    var tbGroupPlacementsHomePhoneFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsHomePhoneFilter" ) as RockTextBox;
                                    if ( tbGroupPlacementsHomePhoneFilter != null && !string.IsNullOrWhiteSpace( tbGroupPlacementsHomePhoneFilter.Text ) )
                                    {
                                        string numericPhone = tbGroupPlacementsHomePhoneFilter.Text.AsNumeric();

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
                                var filterControl = phGroupPlacementsFormFieldFilters.FindControl( "filterGroupPlacements_" + attribute.Id.ToString() );
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
                                var filterControl = phGroupPlacementsFormFieldFilters.FindControl( "filterGroupPlacements_" + attribute.Id.ToString() );
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
                                var filterControl = phGroupPlacementsFormFieldFilters.FindControl( "filterGroupPlacements_" + attribute.Id.ToString() );
                                groupMemberQry = attribute.FieldType.Field.ApplyAttributeQueryFilter( groupMemberQry, filterControl, attribute, groupMemberService, Rock.Reporting.FilterMode.SimpleFilter );
                            }

                            qry = qry.Where( r => groupMemberQry.Any( g => g.Id == r.GroupMemberId ) );
                        }
                    }

                    // Sort the query
                    IOrderedQueryable<RegistrationRegistrant> orderedQry = null;
                    SortProperty sortProperty = gGroupPlacements.SortProperty;
                    if ( sortProperty != null )
                    {
                        orderedQry = qry.Sort( sortProperty );
                    }
                    else
                    {
                        orderedQry = qry
                            .OrderBy( r => r.PersonAlias.Person.LastName )
                            .ThenBy( r => r.PersonAlias.Person.NickName );
                    }

                    // Set the grids LinqDataSource which will run query and set results for current page
                    gGroupPlacements.SetLinqDataSource<RegistrationRegistrant>( orderedQry );

                    if ( RegistrantFields != null )
                    {
                        // Get the query results for the current page
                        var currentPageRegistrants = gGroupPlacements.DataSource as List<RegistrationRegistrant>;
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
                                gGroupPlacements.ObjectList = new Dictionary<string, object>();

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
                                    gGroupPlacements.ObjectList.Add( registrant.Id.ToString(), attributeFieldObject );
                                }
                            }
                        }
                    }

                    gGroupPlacements.DataBind();
                }
            }
        }

        /// <summary>
        /// Handles the ApplyFilterClick event of the fGroupPlacements control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fGroupPlacements_ApplyFilterClick( object sender, EventArgs e )
        {
            fGroupPlacements.SaveUserPreference( "GroupPlacements-Date Range", "Date Range", sdrpGroupPlacementsDateRange.DelimitedValues );
            fGroupPlacements.SaveUserPreference( "GroupPlacements-First Name", "First Name", tbGroupPlacementsFirstName.Text );
            fGroupPlacements.SaveUserPreference( "GroupPlacements-Last Name", "Last Name", tbGroupPlacementsLastName.Text );
            fGroupPlacements.SaveUserPreference( "GroupPlacements-In Group", "In Group", ddlGroupPlacementsInGroup.SelectedValue );
            fGroupPlacements.SaveUserPreference( "GroupPlacements-Signed Document", "Signed Document", ddlGroupPlacementsSignedDocument.SelectedValue );

            if ( RegistrantFields != null )
            {
                foreach ( var field in RegistrantFields )
                {
                    if ( field.FieldSource == RegistrationFieldSource.PersonField && field.PersonFieldType.HasValue )
                    {
                        switch ( field.PersonFieldType.Value )
                        {
                            case RegistrationPersonFieldType.Campus:
                                var ddlCampus = phGroupPlacementsFormFieldFilters.FindControl( "ddlGroupPlacementsCampus" ) as RockDropDownList;
                                if ( ddlCampus != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Home Campus", "Home Campus", ddlCampus.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbEmailFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsEmailFilter" ) as RockTextBox;
                                if ( tbEmailFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Email", "Email", tbEmailFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpBirthdateFilter = phGroupPlacementsFormFieldFilters.FindControl( "drpGroupPlacementsBirthdateFilter" ) as DateRangePicker;
                                if ( drpBirthdateFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Birthdate Range", "Birthdate Range", drpBirthdateFilter.DelimitedValues );
                                }

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbGroupPlacementsMiddleNameFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsMiddleNameFilter" ) as RockTextBox;
                                if ( tbGroupPlacementsMiddleNameFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-MiddleName", "MiddleName", tbGroupPlacementsMiddleNameFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drpGroupPlacementsAnniversaryDateFilter = phGroupPlacementsFormFieldFilters.FindControl( "drpGroupPlacementsAnniversaryDateFilter" ) as DateRangePicker;
                                if ( drpGroupPlacementsAnniversaryDateFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-AnniversaryDate Range", "AnniversaryDate Range", drpGroupPlacementsAnniversaryDateFilter.DelimitedValues );
                                }

                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpGradeFilter = phGroupPlacementsFormFieldFilters.FindControl( "gpGroupPlacementsGradeFilter" ) as GradePicker;
                                if ( gpGradeFilter != null )
                                {
                                    int? gradeOffset = gpGradeFilter.SelectedValueAsInt( false );
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Grade", "Grade", gradeOffset.HasValue ? gradeOffset.Value.ToString() : string.Empty );
                                }

                                break;

                            case RegistrationPersonFieldType.Gender:
                                var ddlGenderFilter = phGroupPlacementsFormFieldFilters.FindControl( "ddlGroupPlacementsGenderFilter" ) as RockDropDownList;
                                if ( ddlGenderFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Gender", "Gender", ddlGenderFilter.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpMaritalStatusFilter = phGroupPlacementsFormFieldFilters.FindControl( "dvpGroupPlacementsMaritalStatusFilter" ) as DefinedValuePicker;
                                if ( dvpMaritalStatusFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Marital Status", "Marital Status", dvpMaritalStatusFilter.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.MobilePhone:
                                var tbMobilePhoneFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsMobilePhoneFilter" ) as RockTextBox;
                                if ( tbMobilePhoneFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-Phone", "Cell Phone", tbMobilePhoneFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                var tbGroupPlacementsHomePhoneFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsHomePhoneFilter" ) as RockTextBox;
                                if ( tbGroupPlacementsHomePhoneFilter != null )
                                {
                                    fGroupPlacements.SaveUserPreference( "GroupPlacements-HomePhone", "Home Phone", tbGroupPlacementsHomePhoneFilter.Text );
                                }

                                break;
                        }
                    }

                    if ( field.Attribute != null )
                    {
                        var attribute = field.Attribute;
                        var filterControl = phGroupPlacementsFormFieldFilters.FindControl( "filterGroupPlacements_" + attribute.Id.ToString() );
                        if ( filterControl != null )
                        {
                            try
                            {
                                var values = attribute.FieldType.Field.GetFilterValues( filterControl, field.Attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                                fGroupPlacements.SaveUserPreference( "GroupPlacements-" + attribute.Key, attribute.Name, attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter ).ToJson() );
                            }
                            catch { }
                        }
                    }
                }
            }

            BindGroupPlacementGrid();
        }

        /// <summary>
        /// Handles the ClearFilterClick event of the fGroupPlacements control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fGroupPlacements_ClearFilterClick( object sender, EventArgs e )
        {
            fGroupPlacements.DeleteUserPreferences();

            foreach ( var control in phGroupPlacementsFormFieldFilters.ControlsOfTypeRecursive<Control>().Where( a => a.ID != null && a.ID.StartsWith( "filter" ) && a.ID.Contains( "_" ) ) )
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
                                var ddlCampus = phGroupPlacementsFormFieldFilters.FindControl( "ddlGroupPlacementsCampus" ) as RockDropDownList;
                                if ( ddlCampus != null )
                                {
                                    ddlCampus.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbEmailFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsEmailFilter" ) as RockTextBox;
                                if ( tbEmailFilter != null )
                                {
                                    tbEmailFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpBirthdateFilter = phGroupPlacementsFormFieldFilters.FindControl( "drpGroupPlacementsBirthdateFilter" ) as DateRangePicker;
                                if ( drpBirthdateFilter != null )
                                {
                                    drpBirthdateFilter.LowerValue = null;
                                    drpBirthdateFilter.UpperValue = null;
                                }

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbGroupPlacementsMiddleNameFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsMiddleNameFilter" ) as RockTextBox;
                                if ( tbGroupPlacementsMiddleNameFilter != null )
                                {
                                    tbGroupPlacementsMiddleNameFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drpGroupPlacementsAnniversaryDateFilter = phGroupPlacementsFormFieldFilters.FindControl( "drpGroupPlacementsAnniversaryDateFilter" ) as DateRangePicker;
                                if ( drpGroupPlacementsAnniversaryDateFilter != null )
                                {
                                    drpGroupPlacementsAnniversaryDateFilter.LowerValue = null;
                                    drpGroupPlacementsAnniversaryDateFilter.UpperValue = null;
                                }

                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpGradeFilter = phGroupPlacementsFormFieldFilters.FindControl( "gpGroupPlacementsGradeFilter" ) as GradePicker;
                                if ( gpGradeFilter != null )
                                {
                                    gpGradeFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.Gender:
                                var ddlGenderFilter = phGroupPlacementsFormFieldFilters.FindControl( "ddlGroupPlacementsGenderFilter" ) as RockDropDownList;
                                if ( ddlGenderFilter != null )
                                {
                                    ddlGenderFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpMaritalStatusFilter = phGroupPlacementsFormFieldFilters.FindControl( "dvpGroupPlacementsMaritalStatusFilter" ) as DefinedValuePicker;
                                if ( dvpMaritalStatusFilter != null )
                                {
                                    dvpMaritalStatusFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.MobilePhone:
                                var tbMobilePhoneFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsMobilePhoneFilter" ) as RockTextBox;
                                if ( tbMobilePhoneFilter != null )
                                {
                                    tbMobilePhoneFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                var tbGroupPlacementsHomePhoneFilter = phGroupPlacementsFormFieldFilters.FindControl( "tbGroupPlacementsHomePhoneFilter" ) as RockTextBox;
                                if ( tbGroupPlacementsHomePhoneFilter != null )
                                {
                                    tbGroupPlacementsHomePhoneFilter.Text = string.Empty;
                                }

                                break;
                        }
                    }
                }
            }

            BindGroupPlacementsFilter( null );
        }

        /// <summary>
        /// fs the group placements display filter value.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        protected void fGroupPlacements_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            if ( e.Key.StartsWith( "GroupPlacements-" ) )
            {
                var key = e.Key.Remove( 0, "GroupPlacements-".Length );

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
                    case "HomePhone":
                    case "Phone":
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
        /// Binds the group placements filter.
        /// </summary>
        /// <param name="instance">The instance.</param>
        private void BindGroupPlacementsFilter( RegistrationInstance instance )
        {
            sdrpGroupPlacementsDateRange.DelimitedValues = fGroupPlacements.GetUserPreference( "GroupPlacements-Date Range" );
            tbGroupPlacementsFirstName.Text = fGroupPlacements.GetUserPreference( "GroupPlacements-First Name" );
            tbGroupPlacementsLastName.Text = fGroupPlacements.GetUserPreference( "GroupPlacements-Last Name" );
            ddlGroupPlacementsInGroup.SetValue( fGroupPlacements.GetUserPreference( "GroupPlacements-In Group" ) );

            ddlGroupPlacementsSignedDocument.SetValue( fGroupPlacements.GetUserPreference( "GroupPlacements-Signed Document" ) );
            ddlGroupPlacementsSignedDocument.Visible = instance != null && instance.RegistrationTemplate != null && instance.RegistrationTemplate.RequiredSignatureDocumentTemplateId.HasValue;
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

        #endregion

        #endregion


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