<%@ Control Language="C#" %>
<%@ OutputCache Duration="15" VaryByParam="none" %>
<div class="panel clock">
    <strong>Cached control</strong>
    <span>rendered at <%= DateTime.Now.ToString("HH:mm:ss.fff") %></span>
    <small>replayed from the output cache for 15 seconds</small>
</div>
