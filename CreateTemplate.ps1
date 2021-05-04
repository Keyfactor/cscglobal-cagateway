Function ImportWindowsCATemplates($CSVFile,$SecurityCSV)
{
    Import-Csv $CSVFile | ForEach-Object {

        $ConfigContext = ([ADSI]"LDAP://RootDSE").ConfigurationNamingContext 
        $ADSI = [ADSI]"LDAP://CN=Certificate Templates,CN=Public Key Services,CN=Services,$ConfigContext" 

        $NewTempl = $ADSI.Create("pKICertificateTemplate", "CN=$($_.Code)") 
        $NewTempl.put("distinguishedName","CN=$($_.Code),CN=Certificate Templates,CN=Public Key Services,CN=Services,$ConfigContext") 
        # and put other atributes that you need 

        $NewTempl.put("flags","131680")
        $NewTempl.put("displayName","$($_.ProductName)")
        $NewTempl.put("revision","100")
        $NewTempl.put("pKIDefaultKeySpec","1")
        $NewTempl.SetInfo()

        $NewTempl.put("pKIMaxIssuingDepth","0")
        $NewTempl.put("pKICriticalExtensions","2.5.29.15")
        $NewTempl.put("pKIExtendedKeyUsage","1.3.6.1.4.1.311.47.1.1, 1.3.6.1.5.5.7.3.2")
        $NewTempl.put("pKIDefaultCSPs","1,Microsoft RSA SChannel Cryptographic Provider")
        $NewTempl.put("msPKI-RA-Signature","0")
        $NewTempl.put("msPKI-Enrollment-Flag","32")
        $NewTempl.put("msPKI-Private-Key-Flag","48")
        $NewTempl.put("msPKI-Certificate-Name-Flag","134217728")
        $NewTempl.put("msPKI-Minimal-Key-Size","2048")
        $NewTempl.put("msPKI-Template-Schema-Version","2")
        $NewTempl.put("msPKI-Template-Minor-Revision","0")
        $NewTempl.put("msPKI-Cert-Template-OID","1.3.6.1.4.1.311.21.8.$(Get-Random -Minimum 10000000 -Maximum 90000000).$(Get-Random -Minimum 10000000 -Maximum 90000000).$(Get-Random -Minimum 1000000 -Maximum 9000000).$(Get-Random -Minimum 1000000 -Maximum 9000000).$(Get-Random -Minimum 1000000 -Maximum 9000000).119.$(Get-Random -Minimum 10000000 -Maximum 90000000).1716 293")
        $NewTempl.put("msPKI-Certificate-Application-Policy","1.3.6.1.5.5.7.3.1")

        $NewTempl.SetInfo()

        $WATempl = $ADSI.psbase.children | where {$_.displayName -match "Workstation Authentication"}

        #before
        $NewTempl.pKIKeyUsage = $WATempl.pKIKeyUsage
        $NewTempl.pKIExpirationPeriod = $WATempl.pKIExpirationPeriod
        $NewTempl.pKIOverlapPeriod = $WATempl.pKIOverlapPeriod
        $NewTempl.SetInfo()

        $NewTempl | select *

        $acl = $NewTempl.psbase.ObjectSecurity
        $acl | select -ExpandProperty Access

        Import-Csv $SecurityCSV | ForEach-Object {
            $AdObj = New-Object System.Security.Principal.NTAccount("$($_.UserName)")
            $identity = $AdObj.Translate([System.Security.Principal.SecurityIdentifier])
            $adRights = "$($_.Rights)"
            $type = "Allow"

            $ACE = New-Object System.DirectoryServices.ActiveDirectoryAccessRule($identity,$adRights,$type)
            $NewTempl.psbase.ObjectSecurity.SetAccessRule($ACE)
            $NewTempl.psbase.commitchanges()
        }
    }
}

function Get-Data([string]$username, [string]$password, [string]$url) {
 
  $credPair = "$($username):$($password)"
  $encodedCredentials = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($credPair))
  $headers = @{ Authorization = "Basic $encodedCredentials";'Accept' = 'application/json';'x-keyfactor-requested-with' = 'APIClient'}
  $responseData = Invoke-WebRequest -Uri $url -Method Get -Headers $headers -UseBasicParsing
 
  return $responseData
}

function Put-Data([string]$username, [string]$password, [string]$url,[string]$body) {
 
  $json = $body | ConvertTo-Json
  $credPair = "$($username):$($password)"
  $encodedCredentials = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($credPair))
  $headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
  $headers.Add("Authorization","Basic $encodedCredentials")
  $headers.Add("Accept","application/json")
  $headers.Add("Content-Type","application/json")
  $headers.Add("x-keyfactor-requested-with","APIClient")  
  $responseData = Invoke-WebRequest -Uri $url -Method Put -Headers $headers -ContentType "application/json" -Body ($json|ConvertFrom-Json)
 
  return $responseData
}

function Get-API-Settings
{
    $RestAPIURLDefault="https://kftrain.keyfactor.lab/KeyfactorAPI/Templates?sq.returnLimit=1000"
    $RestAPIURL = Read-Host "`r`nEnter url for Rest API (Hit Enter To Accept Default) [$($RestAPIURLDefault)]"
    if($RestAPIURL -eq "")
    {
        $RestAPIURL=$RestAPIURLDefault
    }


    $RestAPIUserDefault="keyfactor.lab\Administrator"
    $RestAPIUser = Read-Host "`r`nEnter Rest API User (Hit Enter To Accept Default) [$($RestAPIUserDefault)]"
    if($RestAPIUser -eq "")
    {
        $RestAPIUser=$RestAPIUserDefault
    }

    $RestAPIPasswordDefault="Password1"
    $RestAPIPassword = Read-Host "`r`nEnter Rest API Password (Hit Enter To Accept Default) [$($RestAPIPasswordDefault)]"
    if($RestAPIPassword -eq "")
    {
        $RestAPIPassword=$RestAPIPasswordDefault
    }

      $value = "" | Select-Object -Property ApiURL,ApiUser,ApiPassword
      $value.ApiUrl = $RestAPIURL
      $value.ApiUser = $RestAPIUser
      $value.ApiPassword = $RestAPIPassword
      return $value
}

function Show-Keyfactor-Templates
{
    $ApiSettings=Get-API-Settings
    $templatesResponse=Get-Data $ApiSettings.ApiUser $ApiSettings.ApiPassword $ApiSettings.ApiUrl
    $templatesResponse| ConvertFrom-Json | Select-Object|Format-Table -Property Id,CommonName,TemplateName
}

Function Update-Keyfactor-Templates($CSVFile)
{
    $ApiSettings=Get-API-Settings
    $templatesResponse=Get-Data $ApiSettings.ApiUser $ApiSettings.ApiPassword $ApiSettings.ApiUrl|ConvertFrom-Json
    
    Import-Csv $CSVFile | ForEach-Object{
       $Template=$templatesResponse|where CommonName -eq $_.Code

       #Call Put Tempate for Keyfactor to update the template and enrollment fields
       $filebase = Join-Path $PSScriptRoot "\Templates\$($_.Code).json"

       $fileContent=Get-Content -Path $filebase 
       $keyfactorJson=$fileContent -replace "`"Id`": 999999","`"Id`":$($Template.Id)"
       $keyfactorJson=$keyfactorJson -replace "`"Oid`": `"Replace OID`"","`"Oid`":`"$($Template.Oid)`""
       $keyfactorJson=$keyfactorJson -replace "`"FriendlyName`": `"Replace Friendly Name`"","`"FriendlyName`":`"$($_.ProductName[0..46] -join `"`")`""
       $keyfactorJson=$keyfactorJson -replace "`"DisplayName`": `"Replace Display Name`"","`"DisplayName`":`"$($_.ProductName[0..46] -join `"`")`""

       $templateResponse=Put-Data $ApiSettings.ApiUser $ApiSettings.ApiPassword $ApiSettings.ApiUrl $keyfactorJson

       $templateResponse| ConvertFrom-Json | Select-Object|Format-Table -Property Id,CommonName,TemplateName

       Write-Host "The Above Template has been updated"

    }
}

Function Generate-Keyfactor-Config($CSVFile)
{
  $configOutput="{"
  $RowCount=Import-Csv $CSVFile | Measure-Object | Select-Object -expand count
  $Counter=0
  
  Import-Csv $CSVFile | ForEach-Object{
    $Counter++
    $configOutput=$configOutput + "`"$($_.Code)`": {`r`n`"ProductID`": `"$($_.Code)`",`r`n`"Parameters`": {}`r`n}"
    if($Counter -ne $RowCount)
    {
        $configOutput=$configOutput + ",`r`n"
    }
  }

  $configOutput=$configOutput + "}"

  $FileName=$CSVFile -replace ".csv",".txt"
  
  $configOutput|Out-File -FilePath $FileName

}


#Show all possible CSV files to import, skip the security template
Get-ChildItem -Filter *.csv -Recurse -Exclude CaTemplateUserSecurity.csv

$FileName = Read-Host "`r`nEnter the name of the CSV File to process"

#Show available operations
$table = @( @{OperationNumber=1;OperationName="Create Windows CA Templates"},
            @{OperationNumber=2;OperationName="Check Sync'd Templates in Keyfactor"},
            @{OperationNumber=3;OperationName="Update Keyfactor Templates"},
            @{OperationNumber=4;OperationName="Generate Keyfactor Config Template JSON"})

$(foreach ($ht in $table)
 {new-object PSObject -Property $ht}) | Format-Table -AutoSize

$filebase = Join-Path $PSScriptRoot $FileName
$CSVfilebase = Join-Path $PSScriptRoot "TemplateSecurity\CaTemplateUserSecurity.csv"

#Process Operation
$Operation = Read-Host "Enter the OperationNumber shown above"
Switch ($Operation)
{
    1 {ImportWindowsCATemplates $filebase $CSVfilebase}
    2 {Show-Keyfactor-Templates}
    3 {Update-Keyfactor-Templates $filebase}
    4 {Generate-Keyfactor-Config $filebase}
}



