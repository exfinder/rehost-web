<%@ Page Language="C#" CodeFile="Default.aspx.cs" Inherits="DefaultPage" %>
<html>
<head><title>Page fixture</title></head>
<body>
<p id="stages"><%= Stages %></p>
<asp:Label ID="Value" runat="server" />
<asp:Repeater ID="Items" runat="server">
<ItemTemplate><li><%# Container.DataItem %></li></ItemTemplate>
</asp:Repeater>
<asp:HyperLink ID="Self" runat="server" NavigateUrl="~/Default.aspx" Text="self" />
<asp:Image ID="Badge" runat="server" ImageUrl="~/badge.png" AlternateText="badge" />
<asp:Panel ID="Notes" runat="server">
<p>This paragraph exists so the generated page carries one literal run of at least two hundred and fifty six characters, which is the threshold where .NET Framework moves markup into a Win32 string resource and reads it back through native module handles. This port reports no Win32 resource support, so the same markup stays an ordinary metadata string written through the normal response path, which transcodes rather than copying bytes — so the run also carries non-ASCII text (déjà vu, naïve café, Ünicode) to exercise that.</p>
</asp:Panel>
</body>
</html>
