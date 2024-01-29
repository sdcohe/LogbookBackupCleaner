# HRDLogbookCleaner

HRDLogbookCleaner is a command line utility program that will remove backup files created by HRD Logbook. It allows you to remove backup files based on their age, the number of files, or a combination of both. It is based on a batch file created by Robin Moseley, but has had several enhancements. It is a work in progress as additional requirements are still being discovered.

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes. See deployment for notes on how to deploy the project on a live system.

### Prerequisites

Development on this software uses Visual Studio Community Edition. It was written using the C# programming language.

### Installing

You can create a branch and get the source code. This software depends on the following Nuget packages, which will need to be installed in your solution:

```
* [Microsoft System.Commandline](https://www.nuget.org/packages/System.CommandLine/) - Command line argument processor (currently a beta)
* [Serilog](https://www.nuget.org/packages/Serilog/) - Debug Logging
* [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console) - Console sink for Serilog
* [Costura.Fody](https://www.nuget.org/packages/Costura.Fody/) - Add in for embedding references as resources 
```

## Running the tests

Currently there are no automated tests for this software.

## Deployment

Add additional notes about how to deploy this on a live system

## Contributing

Please read [CONTRIBUTING.md]() for details on our code of conduct, and the process for submitting pull requests to us.

## Authors

* **Seth Cohen** - *Initial work*

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

* Based on initial work (batch file) created by Robin Moseley and posted to the Facebook HRD group
