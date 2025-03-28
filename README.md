# <img src="grapevine.png" width=25px> Grapevine

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

## Support

- Check out the project documentation https://scottoffen.github.io/grapevine.

- Want to see a working project in action? Clone this repository and take a look at the [Samples](https://github.com/scottoffen/grapevine/tree/main/src/Samples) project.

- Engage in our [community discussions](https://github.com/scottoffen/grapevine/discussions) for Q&A, ideas, and show and tell!

- Have a question you can't find an answer for in the documentation? For the fastest and best results, ask your questions on [StackOverflow](https://stackoverflow.com) using [#grapevine](https://stackoverflow.com/questions/tagged/grapevine?sort=newest). Make sure you include the version of Grapevine you are using, the platform you using it on, code samples and any specific error messages you are seeing.

- **Issues created to ask "how to" questions will be closed.**

## License

Grapevine 5 is licensed under the [MIT](https://choosealicense.com/licenses/mit/) license.

## Using Grapevine? We'd Love To Hear About It!

Few thing are as satisfying as hearing that your open source project is being used and appreciated by others. (Except for [a nice MLT – mutton, lettuce and tomato sandwich, where the mutton is nice and lean and the tomato is ripe. They're so perky, I love that.](https://youtu.be/d4ftmOI5NnI?t=93)) Jump over to the discussion boards and [share the love](https://github.com/scottoffen/grapevine/discussions/13)!
