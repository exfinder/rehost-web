<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" encoding="utf-8" indent="yes" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="configuration/runtime|configuration/system.codedom" />

  <xsl:template match="configuration/system.web/pages/controls/add[@assembly='Microsoft.AspNet.Web.Optimization.WebForms']/@assembly">
    <xsl:attribute name="assembly">Rehost.WebForms.Optimization.WebForms</xsl:attribute>
  </xsl:template>
</xsl:stylesheet>
