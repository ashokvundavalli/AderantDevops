$services = Get-Service "Aderant*"
foreach ($service in $services) {
    Stop-Service $service -Force
    $wmiService = Get-WmiObject -Class Win32_Service -Filter ("Name='{0}'" -f $service.Name)
    $wmiService.Delete()
}
