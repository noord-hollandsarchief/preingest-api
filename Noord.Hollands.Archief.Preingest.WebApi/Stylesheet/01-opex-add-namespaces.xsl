<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output indent="no"/>
    <xsl:strip-space elements="*"/>

    <xsl:template match="@*|text()|comment()|processing-instruction()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="/ToPX">
        <ToPX xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.openpreservationexchange.org/opex/v1.1">
            <xsl:apply-templates select="@*|node()"/>
        </ToPX>
    </xsl:template>

    <xsl:template match="*">
        <xsl:element name="{local-name()}" namespace="http://www.openpreservationexchange.org/opex/v1.1">
            <xsl:apply-templates select="@*|node()"/>
        </xsl:element>
    </xsl:template>

</xsl:stylesheet>