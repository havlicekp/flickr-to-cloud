# Flickr To Cloud

Backup your Flickr albums and/or photo stream to a cloud storage. When available, photos can be also copied directly between Flickr and the cloud, without downloading them locally. Give the app like here or on Microsoft Store to add more clouds. Currently only OneDrive is available.

![](https://github.com/havlicekp/flickr-to-cloud/blob/master/images/mockup4.jpg)

<p align="center">
<a href="//www.microsoft.com/store/apps/9N95CQ7CN70P?cid=storebadge&ocid=badge"><img src="https://assets.windowsphone.com/85864462-9c82-451e-9355-a3d5f874397a/English_get-it-from-MS_InvariantCulture_Default.png" height="50" /></a>
  </p>

## Technical details
* Windows Store application (UWP project + .NET Standard 2.0 libraries)
* Utilizing MVVM pattern to separate GUI (XAML) code from business logic ([MVVMCross framework](https://www.mvvmcross.com/))
* Built using [async/await](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)  to keep GUI responsive. Also used for parallel processing
* Dependency injection for loose coupling
* [EF Core](https://github.com/aspnet/EntityFrameworkCore) used for session persistence (SQLite)
* Consuming REST API(s) with interactive OAuth/OAuth2 authorization
* Logging using [Serilog](https://serilog.net/)
* Unit tests using [xUnit](https://xunit.net/) and [Moq](https://github.com/moq/moq4) 
* Adapts to light/dark Windows mode 

