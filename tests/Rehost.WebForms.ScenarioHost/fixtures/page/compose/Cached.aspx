<%@ Page Language="C#" MasterPageFile="~/compose/Site.master" Title="Fragment cache probe" %>
<%@ Register Src="~/compose/CachedWidget.ascx" TagPrefix="uc" TagName="Cached" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <p id="page-stamp"><%= Guid.NewGuid().ToString("N") %></p>
    <uc:Cached ID="TheCached" runat="server" />
</asp:Content>
