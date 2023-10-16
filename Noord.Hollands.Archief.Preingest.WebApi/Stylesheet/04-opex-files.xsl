<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:x="http://www.openpreservationexchange.org/opex/v1.1" exclude-result-prefixes="x">
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
				<Fixities>
					<Fixity type="" value=""/>
				</Fixities>
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
				<Fixities>
					<Fixity type="" value=""/>
				</Fixities>
			</Transfer>
			<Properties>
				<Title/>
				<Description/>
			</Properties>
			<DescriptiveMetadata>
					<xsl:copy-of select="."/>
			</DescriptiveMetadata>
		</OPEXMetadata>
	</xsl:template>
	
</xsl:stylesheet>
