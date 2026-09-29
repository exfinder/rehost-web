<%@ Page Language="C#" %>
<p id="encoded"><%= HttpUtility.HtmlEncode("a<b & \"c\"") %></p>
