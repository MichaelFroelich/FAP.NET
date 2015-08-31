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

# Q/A

Q: Why ```Thread.Sleep(1)``` in an infinite loop within the above example program?

A: As the server is threaded (mostly using ```Task.Factory.StartNew()```) and completely non-blocking, for this library to work you will need an infinite loop, or a timed loop, or a loop with a long-term condition, somewhere within some file. Otherwise your program will end immediately and absolutely no users will arrive and you won't have a full API webservice.

Alternatively, instead of ```Thread.Sleep()```, which seems to guarantee the system performs at least some sleeping time, you could use ```Thread.Yield()``` within an infinite loop. I've found that this can increase performance by about 100ns. Another option is to use a wait handle.
