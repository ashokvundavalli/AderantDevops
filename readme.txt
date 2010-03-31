Aderant Powershell Module setup
  > $/ExpertSuite/<Branch>/Modules/Build.Infrastructure/Src/Profile/Aderant
  > Aderant folder goes in:  C:\Windows\System32\WindowsPowerShell\v1.0\Modules
     if 64bit also add in :  C:\Windows\SysWOW64\WindowsPowerShell\v1.0\Modules 
  NOTE: this contans your defaults file for your development environment
  
Defaults File Setup 
  > Goto the defaults file, that has been copied into your Aderant folder above and update you defaults settins

Profile Setup
  > $/ExpertSuite/<Branch>/Modules/Build.Infrastructure/Src/Profile/WindowsPowerShell
  > WindowsPowershell folder goes in your Documents folder. e.g. C:\Users\jamie.bate\Documents

PSUnit Setup
  # You need to ensure that you have downloaded the module Thirdparty.PsUnit for the branch you are working on.
  # To run a test open the *.Tests.ps1 file in the ISE and hit Ctrl-Shift-X to execute tests.



