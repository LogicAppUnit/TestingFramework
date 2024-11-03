<xsl:stylesheet xmlns:tns="http://schemas.logicappunit.net/CampaignRequest" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:ns0="http://schemas.logicappunit.net/CustomerCampaign/v1" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:math="http://www.w3.org/2005/xpath-functions/math" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:dm="http://azure.workflow.datamapper" xmlns:ef="http://azure.workflow.datamapper.extensions" exclude-result-prefixes="xsl xs math dm ef ns0" version="3.0" expand-text="yes">
  <xsl:output indent="yes" media-type="text/xml" method="xml" />
  <xsl:template match="/">
    <xsl:apply-templates select="." mode="azure.workflow.datamapper" />
  </xsl:template>
  <xsl:template match="/" mode="azure.workflow.datamapper">
    <tns:campaignRequest>
      <xsl:attribute name="numberOfCampaigns">{count(/ns0:CustomerCampaigns/ns0:Campaign)}</xsl:attribute>
      <xsl:for-each select="/ns0:CustomerCampaigns/ns0:Campaign">
        <tns:campaign>
          <tns:campaignDetails>
            <tns:id>{ns0:CampaignId}</tns:id>
            <tns:name>{substring(ns0:CampaignName, 0, 20)}</tns:name>
          </tns:campaignDetails>
          <tns:customer>
            <tns:id>{ns0:CustomerId}</tns:id>
            <tns:forename>{substring(ns0:FirstName, 0, 40)}</tns:forename>
            <tns:surname>{substring(ns0:LastName, 0, 40)}</tns:surname>
            <tns:email>{ns0:Email}</tns:email>
            <tns:age>{ns0:Age}</tns:age>
          </tns:customer>
          <tns:premisesid>{ns0:SiteCode}</tns:premisesid>
        </tns:campaign>
      </xsl:for-each>
    </tns:campaignRequest>
  </xsl:template>
</xsl:stylesheet>