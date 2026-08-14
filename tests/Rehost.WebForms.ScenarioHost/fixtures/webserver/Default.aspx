<%@ Page Language="C#" %><%
    Response.ContentType = "text/plain";
    Response.Write("path=" + Request.Path + "\n");
    Response.Write("rawurl=" + Request.RawUrl + "\n");
    Response.Write("filepath=" + Request.FilePath + "\n");
    Response.Write("apprelative=" + Request.AppRelativeCurrentExecutionFilePath + "\n");
%>
