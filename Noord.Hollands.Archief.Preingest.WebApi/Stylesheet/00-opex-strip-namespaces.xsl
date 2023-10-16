<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:t="http://www.nationaalarchief.nl/ToPX/v2.3" exclude-result-prefixes="t ">
	<xsl:output method="xml" version="1.0" encoding="UTF-8" indent="no"/>
	<xsl:strip-space elements="*"/>
	
    <!-- template to copy elements -->
    <xsl:template match="*">
		<xsl:element name="{local-name()}">
		<xsl:apply-templates select="@* | node()"/>
        </xsl:element>
    </xsl:template>

    <!-- template to copy attributes -->
    <xsl:template match="@*">
		<xsl:if test="not(local-name()='noNamespaceSchemaLocation')">
        <xsl:attribute name="{local-name()}">
			<xsl:value-of select="."/>
        </xsl:attribute>
		</xsl:if>
    </xsl:template>

    <!-- template to copy the rest of the nodes -->
    <xsl:template match="comment() | text() | processing-instruction()">
        <xsl:copy/>
    </xsl:template>
	
</xsl:stylesheet>
