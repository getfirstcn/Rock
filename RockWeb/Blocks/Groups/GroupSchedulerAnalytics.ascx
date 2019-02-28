﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="GroupSchedulerAnalytics.ascx.cs" Inherits="RockWeb.Blocks.Groups.GroupSchedulerAnalytics" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlView" runat="server" CssClass="panel panel-block panel-analytics">

            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-line-chart"></i>
                    Group Scheduler Analytics
                </h1>
            </div>

            <div class="panel-body">
                <div class="row">
                    <div class="col-lg-2 col-md-3 filter-options" role="tabpanel">
                        <Rock:NotificationBox ID="nbFilterNotification" runat="server" NotificationBoxType="Warning" visible="false"></Rock:NotificationBox>
                        <asp:HiddenField ID="hfTabs" runat="server" />
                        <label>Please select a Group, Person, or Data View</label>
                        <ul class="nav nav-pills" role="tablist" id="tablist">
                            <li><a href="#group" aria-controls="group" role="tab" data-toggle="tab" onclick='$("#<%= hfTabs.ClientID %>").attr( "value", "group");'>Group</a></li>
                            <li><a href="#person" aria-controls="person" role="tab" data-toggle="tab" onclick='$("#<%= hfTabs.ClientID %>").attr( "value", "person");'>Person</a></li>
                            <li><a href="#dataview" aria-controls="dataview" role="tab" data-toggle="tab" onclick='$("#<%= hfTabs.ClientID %>").attr( "value", "dataview");'>Dataview</a></li>
                        </ul>

                        <Rock:SlidingDateRangePicker ID="sdrpDateRange" runat="server" Label="Date Range" EnabledSlidingDateRangeTypes="Previous, Last, Current, DateRange" EnabledSlidingDateRangeUnits="Week, Month, Year" SlidingDateRangeMode="Current"/>
                        
                        <div class="tab-content" style="padding-bottom:20px">
                            <div role="tabpanel" class="tab-pane fade in active" id="group">
                                <Rock:GroupPicker ID="gpGroups" runat="server" AllowMultiSelect="false" Label="Select Groups" LimitToSchedulingEnabledGroups="true" OnSelectItem="gpGroups_SelectItem"  />
                                <Rock:RockCheckBoxList ID="cblLocations" runat="server" Label="Locations" RepeatColumns="1" RepeatDirection="Vertical" OnSelectedIndexChanged="cblLocations_SelectedIndexChanged" AutoPostBack="true" ></Rock:RockCheckBoxList>
                                <Rock:RockCheckBoxList ID="cblSchedules" runat="server" Label="Schedules" RepeatColumns="1" RepeatDirection="Vertical" ></Rock:RockCheckBoxList>
                            </div>
                            <div role="tabpanel" class="tab-pane fade" id="person">
                                <Rock:PersonPicker ID="ppPerson" runat="server" Label="Person" OnSelectPerson="ppPerson_SelectPerson" />
                            </div>
                            <div role="tabpanel" class="tab-pane fade" id="dataview">
                                <Rock:DataViewItemPicker ID="dvDataViews" runat="server" Label="Data View" OnSelectItem="dvDataViews_SelectItem" ></Rock:DataViewItemPicker>
                            </div>
                        </div>
                        
                        <asp:LinkButton ID="btnUpdate" runat="server" CssClass="btn btn-default btn-block" OnClick="btnUpdate_Click"><i class="fa fa-sync"></i>&nbsp;Refresh</asp:LinkButton>

                    </div>

                    <div class="col-lg-10 col-md-9 resource-area">
                        <div class="row">
                            <%-- Bar chart to show the data in the tabular --%>
                            <div class="chart-container col-md-9">
                                <Rock:NotificationBox ID="nbBarChartMessage" runat="server" NotificationBoxType="Info" Text="No Group Scheduler Data To Show" visible="true"/>
                                <canvas id="barChartCanvas" runat="server" style="height: 450px;" />
                            </div>


                            <%-- Doughnut chart to show the decline reasons--%>
                            <div class="chart-container col-md-3">
                                <canvas id="doughnutChartCanvas" runat="server"></canvas>
                            </div>


                        </div>
                        <div class="row">
                            <div class="col-md-9">
                            <%-- tabular data --%>
                            <Rock:Grid ID="gData" runat="server" AllowPaging="true" EmptyDataText="No Data Found" ShowActionsInHeader="false">
                                <Columns>
                                    <Rock:RockBoundField DataField="Name" HeaderText="Name"></Rock:RockBoundField>
                                    <Rock:RockBoundField DataField="Scheduled" HeaderText="Scheduled"></Rock:RockBoundField>
                                    <Rock:RockBoundField DataField="NoResponse" HeaderText="No Response"></Rock:RockBoundField>
                                    <Rock:RockBoundField DataField="Declines" HeaderText="Declines"></Rock:RockBoundField>
                                    <Rock:RockBoundField DataField="Attended" HeaderText="Attended"></Rock:RockBoundField>
                                    <Rock:RockBoundField DataField="CommitedNoShow" HeaderText="Commited No Show"></Rock:RockBoundField>
                                </Columns>
                            </Rock:Grid>
                                </div>
                        </div>

                    </div>
                </div>
            </div>
            <script type="text/javascript">
                $(document).ready(function () {
                    showTab();
                });

                function showTab() {
                    var tab = document.getElementById('<%= hfTabs.ClientID%>').value;
                    $('#tablist a[href="#' + tab + '"]').tab('show');
                }

                Sys.WebForms.PageRequestManager.getInstance().add_endRequest(showTab);
            </script>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>