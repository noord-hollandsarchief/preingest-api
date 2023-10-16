<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:x="http://www.openpreservationexchange.org/opex/v1.1"
xmlns:t="http://www.nationaalarchief.nl/ToPX/v2.3"
xmlns:m="https://www.nationaalarchief.nl/mdto"
exclude-result-prefixes="x t m" xmlns="http://www.openpreservationexchange.org/opex/v1.1">
	<xsl:output method="xml" version="1.0" encoding="UTF-8" indent="yes"/>
	<xsl:strip-space elements="*"/>
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>
	<!-- Since we're using generated GUID  for creating OPEX
	<xsl:template match="x:SourceID">
		<xsl:choose>
			<xsl:when test="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:aggregatie">
				<SourceID>
					<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:aggregatie/x:identificatiekenmerk/text()"/>
				</SourceID>
			</xsl:when>
			<xsl:when test="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO">
				<xsl:variable name="identificatieKenmerk">
					<xsl:for-each select="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:informatieobject/x:identificatie/x:identificatieKenmerk">
						<xsl:if test="position() > 1">
							<xsl:text>-</xsl:text>
						</xsl:if>
						<xsl:value-of select="."/>
					</xsl:for-each>
				</xsl:variable>
				<SourceID>
					<xsl:value-of select="$identificatieKenmerk"/>
				</SourceID>
			</xsl:when>
			<xsl:otherwise>
				<xsl:copy-of select="."/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
	-->
	<xsl:template match="x:Title">
		<xsl:choose>
			<xsl:when test="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:bestand">
				<Title>
					<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:bestand/x:naam/text()"/>
				</Title>
			</xsl:when>
			<xsl:when test="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:aggregatie">
				<Title>
					<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:aggregatie/x:naam/text()"/>
				</Title>
			</xsl:when>
			<xsl:when test="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:informatieobject">
				<Title>
					<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:informatieobject/x:naam/text()"/>
				</Title>
			</xsl:when>
			<xsl:when test="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:bestand">
				<Title>
					<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:bestand/x:naam/text()"/>
				</Title>
			</xsl:when>
			<xsl:otherwise>
				<xsl:copy-of select="."/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
	<xsl:template match="x:Fixity/@type">
		<xsl:if test="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:bestand">
			<xsl:attribute name="type">
				<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:bestand/x:formaat/x:fysiekeIntegriteit/x:algoritme"/>
			</xsl:attribute>
		</xsl:if>
		<xsl:if test="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:bestand">
			<xsl:attribute name="type">
				<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:bestand/x:checksum/x:checksumAlgoritme/x:begripLabel"/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	<xsl:template match="x:Fixity/@value">
		<xsl:if test="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:bestand">
			<xsl:attribute name="value">
				<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:ToPX/x:bestand/x:formaat/x:fysiekeIntegriteit/x:waarde"/>
			</xsl:attribute>
		</xsl:if>
		<xsl:if test="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:bestand">
			<xsl:attribute name="value">
				<xsl:value-of select="/x:OPEXMetadata/x:DescriptiveMetadata/x:MDTO/x:bestand/x:checksum/x:checksumWaarde"/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	<xsl:template match="*[ancestor-or-self::x:ToPX]">
		<xsl:element name="{local-name()}" namespace="http://www.nationaalarchief.nl/ToPX/v2.3">
			<xsl:apply-templates select="@* | node()"/>
		</xsl:element>
	</xsl:template>	
	<xsl:template match="*[ancestor-or-self::x:MDTO]">
		<xsl:element name="{local-name()}" namespace="https://www.nationaalarchief.nl/mdto">
			<xsl:apply-templates select="@* | node()"/>
		</xsl:element>
	</xsl:template>
</xsl:stylesheet>
