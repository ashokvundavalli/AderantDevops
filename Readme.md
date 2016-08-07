# Expert Build Infrastructure  #
Welcome to the Expert build system. This repository contains the source code and documentation for the tools and workflows which compile, build, lint and test the Expert Suite code base.

## Getting Started aka Before You Do Anything ##
The tools provide a set of PowerShell extensions which enable you to retrieve dependencies for products, compile the projects and run the tests.

Assuming you already have Git and [PowerShell 5.0](https://www.microsoft.com/en-us/download/details.aspx?id=50395) installed.

* Start PowerShell or a Command Prompt
* Clone the repository 
```git clone http://tfs:8080/tfs/Aderant/ExpertSuite/_git/Build.Infrastructure C:\Source\Build.Infrastructure```
* Change directory 
```cd C:\Source\Build.Infrastructure```
* Run 
```profilesetup.cmd```

This will install and configure the developer tools. 
The next time you start PowerShell you will be asked a series of questions which will configure the tools for you.