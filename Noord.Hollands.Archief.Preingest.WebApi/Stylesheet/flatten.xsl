<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:output omit-xml-declaration="yes" indent="yes"/>
	<xsl:param name="paramOutputType"></xsl:param>
	<xsl:strip-space elements="*"/>
	<xsl:template match="/*">
		<xsl:apply-templates mode="children">
			<xsl:with-param name="pName" select="name()"/>
		</xsl:apply-templates>
	</xsl:template>
	<xsl:template match="*[*]" mode="children">
		<xsl:param name="pName"/>
		<xsl:variable name="vNewName" select="concat($pName, '/', name())"/>
		<!--xsl:value-of select="concat('Upper level(s) : /', $vNewName, '/&#10;')"/-->
		<xsl:apply-templates mode="children">
			<xsl:with-param name="pName" select="$vNewName"/>
		</xsl:apply-templates>
	</xsl:template>
	<xsl:template match="*[not(*)]" mode="children">
		<xsl:param name="pName"/>
		<xsl:if test="$paramOutputType='nonempty' and string-length(text()) > 0">
			<xsl:value-of select="concat('/', $pName, '/', name(), ' | ', ., '&#10;')"/>
		</xsl:if>
		<xsl:if test="$paramOutputType='empty' and string-length(text()) = 0">
			<xsl:value-of select="concat('/', $pName, '/', name(), ' | ', 'NULL', '&#10;')"/>
		</xsl:if>
	</xsl:template>
</xsl:stylesheet>
