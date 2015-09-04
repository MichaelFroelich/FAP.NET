# FAP.NET
'Functional API Pages' is yet another network library that provides C# quality reliability but with a fresh functional paradigm spin. Additionally, the Page class is fully extendable allowing polymorphism for the object-oriented design paradigm.

This library can be used as simply as:
```
var page = new Page("api");  // Create a page accessible through 127.0.0.1:1024/api?
page.get = (a,b) => "Hello"; // Return "Hello" upon successfully connecting to the page
var server = new Server();   // Create a new instance of the server
server.AddPage(page);        // Load your page onto the server
Thread.Sleep(-1);           // Sleep until a user arrives
```

# Q/A

Q: Why ```Thread.Sleep(-1)```?

A: As the server is threaded (mostly using ```Task.Factory.StartNew()```) and completely non-blocking, for this library to work you will need an infinite loop, or a timed loop, or a loop with a long-term condition, somewhere within some file. Otherwise your program will end immediately and absolutely no users will arrive and you won't have a full API webservice.

I found a number of options.
1. A ```Thread.Sleep(1)``` in a while loop and this resulted in the slowest test results but didn't spin the CPU.
2. A WaitHandle with ```waitHandle.WaitOne()```, this resulted in intermediary results but didn't spin the CPU.
3. Simply ```Thread.Sleep(-1)```, which means "sleep the maximum amount of time" and provides intermediary results.
4. A ```Thread.Yield()``` in a while loop and this resulted in the best test results which are displayed on my site.

Unfortunately ```Thread.Yield()``` spins the CPU... my thinking on how this function works is by asking whatever mechanism handles the threading and task callbacks to check, meaning if you had a while loop calling ```Thread.Yield()``` multiple times within a second, you'll naturally get better performance. This really comes back to you, as the person likely using my code, and your priorities.

If performance is the priority, I recommend an infinite loop with ```Thread.Yield()```
If resource efficiency is the priority, I recommend simply ```Thread.Sleep(-1)```
