<xsl:stylesheet version="1.0" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:x="http://www.openpreservationexchange.org/opex/v1.1" exclude-result-prefixes="x">
	<xsl:output method="xml" version="1.0" encoding="UTF-8" indent="yes"/>
	<xsl:strip-space elements="*"/>
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>
	<xsl:template match="x:ToPX">
		<OPEXMetadata xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.openpreservationexchange.org/opex/v1.1">
			<Transfer>
				<SourceID/>
				<Manifest>
					<Files/>
				</Manifest>
			</Transfer>
			<Properties>
				<Title/>
				<Description/>
				<SecurityDescriptor>closed</SecurityDescriptor>
			</Properties>
			<DescriptiveMetadata>
					<xsl:copy-of select="."/>
			</DescriptiveMetadata>
		</OPEXMetadata>
	</xsl:template>
	<xsl:template match="x:MDTO">
		<OPEXMetadata xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.openpreservationexchange.org/opex/v1.1">
			<Transfer>
				<SourceID/>
				<Manifest>
					<Files/>
				</Manifest>
			</Transfer>
			<Properties>
				<Title/>
				<Description/>
				<SecurityDescriptor>closed</SecurityDescriptor>
			</Properties>
			<DescriptiveMetadata>
					<xsl:copy-of select="."/>
			</DescriptiveMetadata>
		</OPEXMetadata>
	</xsl:template>
</xsl:stylesheet>
