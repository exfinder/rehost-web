<%@ Control Language="C#" %>
<%@ OutputCache Duration="60" VaryByParam="none" %>
<span id="cached-stamp"><%= Guid.NewGuid().ToString("N") %></span>
