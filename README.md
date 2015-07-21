# FAP.NET
'Functional API Pages' is yet another network library that provides C# quality reliability but with a fresh functional paradigm spin. Additionally, the Page class is fully extendable allowing polymorphism for the object-oriented design paradigm.

This library can be used as simply as:
```
var page = new Page("api");  // Create a page accessible through 127.0.0.1:1024/api?
page.get = (a,b) => "Hello"; // Return "Hello" upon successfully connecting to the page
var server = new Server();   // Create a new instance of the server
server.AddPage(page);        // Load your page onto the server
while(1)
	Thread.Sleep(1);           // Sleep until a user arrives :)
```
