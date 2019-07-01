# Flickr To Cloud
Backup your Flickr albums and photo stream to a cloud storage. If possible, photos are copied directly between Flickr and the cloud, without downloading them locally.

<img src="https://github.com/havlicekp/flickr-to-cloud/blob/master/images/mockup.jpg" alt="alt text"  align="left" >
<br />
## Technical details
* Windows Store application (UWP project + .NET Standard 2.0 assemblies)
* Utilizing MVVM pattern to separate GUI (XAML) code from business logic ([MVVMCross framework](https://www.mvvmcross.com/))
* Built using [async/await](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)  to keep GUI responsive. Also used for parallel processing
* Dependency injection for loose coupling
* [EF Core](https://github.com/aspnet/EntityFrameworkCore) used for session persistence (SQLite)
* Consuming REST API(s) with interactive OAuth/OAuth2 authorization
* Logging using [Serilog](https://serilog.net/)
* Unit tests using [Moq](https://github.com/moq/moq4) 
* Adapts to light/dark Windows mode 
