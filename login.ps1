$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$session.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.82 Safari/537.36"
$session.Cookies.Add((New-Object System.Net.Cookie("usession", "EC4E950F879BB68B938171A547125F4E.default", "/", "all.uddataplus.dk")))
$session.Cookies.Add((New-Object System.Net.Cookie("tab", "direkte", "/", "all.uddataplus.dk")))
$session.Cookies.Add((New-Object System.Net.Cookie("instkey", "64233665", "/", "all.uddataplus.dk")))
$session.Cookies.Add((New-Object System.Net.Cookie("instnr", "621407", "/", "all.uddataplus.dk")))
$session.Cookies.Add((New-Object System.Net.Cookie("usession", "89C595DE9E8BB4188F3E834EE780DE68.default", "/", "all.uddataplus.dk")))
Invoke-WebRequest -UseBasicParsing -Uri "https://all.uddataplus.dk/login/doLogin" `
-Method "POST" `
-WebSession $session `
-Headers @{
"method"="POST"
  "authority"="all.uddataplus.dk"
  "scheme"="https"
  "path"="/login/doLogin"
  "cache-control"="max-age=0"
  "sec-ch-ua"="`"Google Chrome`";v=`"93`", `" Not;A Brand`";v=`"99`", `"Chromium`";v=`"93`""
  "sec-ch-ua-mobile"="?0"
  "sec-ch-ua-platform"="`"Windows`""
  "upgrade-insecure-requests"="1"
  "origin"="https://all.uddataplus.dk"
  "accept"="text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9"
  "sec-fetch-site"="same-origin"
  "sec-fetch-mode"="navigate"
  "sec-fetch-user"="?1"
  "sec-fetch-dest"="document"
  "referer"="https://all.uddataplus.dk/login/doLogin"
  "accept-encoding"="gzip, deflate, br"
  "accept-language"="da-DK,da;q=0.9,en-US;q=0.8,en;q=0.7"
} `
-ContentType "application/x-www-form-urlencoded" `
-Body "instnr=621407&instkey=64233665&user=sts&pass=Holstebro48Vemb&how=direkte&huskmig=on"

$cookies = $session.Cookies.GetCookies("https://all.uddataplus.dk/login/doLogin") 
foreach ($cookie in $cookies) { 
     # You can get cookie specifics, or just use $cookie 
     # This gets each cookie's name and value 
     Write-Host "$($cookie.name) = $($cookie.value)" 
     
     if ($cookie.name -like "JSESSIONIDSSO") {
        "$($cookie.name)=$($cookie.value)" | Out-File -FilePath C:\Uplus\jsession.txt
     }

}