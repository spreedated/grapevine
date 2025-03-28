# <img src="grapevine.png" width=25px> Grapevine

This project is based on [https://github.com/scottoffen/grapevine](https://github.com/scottoffen/grapevine) but has been heavily modified,
and has it's own versioning and release cycle.

Grapevine is a fast, unopinionated, embeddable, minimalist web framework for .NET. Grapevine is not intended to be a replacement for IIS or ASP.NET, but rather to function as an embedded REST/HTTP server in non-ASP.NET projects.

## Usage

Grapevine is easy to get started with.

Create a simple route. This is the code that you want to run when a request comes in using the specified HTTP verb and path. **Route methods must be [asynchronous](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)!**

```csharp
[RestResource]
public class MyResource
{
    [RestRoute("Get", "/api/test")]
    public async Task Test(IHttpContext context)
    {
        await context.Response.SendResponseAsync("Successfully hit the test route!");
    }
}
```

Next, create your first server using provided defaults (it's recommended to use the `RestServerBuilder` class to do this) and start it up!

```csharp
using (var server = RestServerBuilder.UseDefaults().Build())
{
    server.Start();

    Console.WriteLine("Press enter to stop the server");
    Console.ReadLine();
}
```

Open your preferred browser and go to `http://localhost:1234/api/test`. You should see the following output in your browser:

```
Successfully hit the test route!
```

> You'll see a lot of output in the console as well, because the defaults inject a console logger with the minimum level set to trace.