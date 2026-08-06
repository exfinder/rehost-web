<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeBehind="WapDemo.aspx.cs" Inherits="Sample.WapDemo" %>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>WAP-style code-behind</h1>
<p class="lede">
    This page uses the Web Application Project model: <code>CodeBehind</code> +
    <code>Inherits</code>. Its code-behind and designer partials were compiled by the SDK into
    <code>bin/Rehost.WebForms.SampleApp.dll</code> — the runtime never saw the
    <code>.cs</code> files and resolved the base type from <code>bin</code>, exactly as IIS
    did for a deployed WAP.
</p>

<div class="panel">
    <p>
        <asp:TextBox ID="Name" runat="server" />
        <asp:Button ID="Greet" runat="server" Text="Greet" OnClick="Greet_Click" />
    </p>
    <p><asp:Label ID="Answer" runat="server" /></p>
</div>

</asp:Content>
