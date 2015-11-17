# FAP.NET
Website: http://michaelfroelich.com

If you found this library framework useful, wish to demonstrate your gratitude or just want to brighten my day, feel free to donate from my website! Lastly, please contact me through Github or on my website or shoot me an email if there are any issues.

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

Q: I absolute must return a very certain HTTP return code! HOW?

A: For example, the following will return the code "201" along with the client headers after navigating to the page 127.0.0.1:1024/header?somethingherethatsnotblank:
```
      Page page = new Page("header");
			page.get = (a, b) => {
				return "201\r\n" + page.Headers;
			};
			pagelist.Add(page);
			Server server = new Server(pagelist);
```
  I've implemented a wide range of http return codes (please read the source code for the full list (at the very bottom of the Server.cs file)), although you may have to build your own headers. Because the use of which headers against which return codes can be highly contentious, I've decided not to force any headers except in the use of 20x codes, 404 and 304.

Q: Do headers work both ways? If so, how do I add a specific header?

A: Yes, I've implemented headers both way. Again, the best way to demonstrate this is through example code:
```
      Page page = new Page("cookiepage");
			page.get = (a, b) => {
			  if(!page.Headers.Contains("Cookie"))
			    page.Headers = "Set-Cookie: " + (UserIP + UserAgent + DateTime.Now).GetHashCode();
				return "200\r\n";
			};
			pagelist.Add(page);
			Server server = new Server(pagelist);
```
  Furthermore, if you wish to return multiple headers, you may, just remember to separate each of them by ending each new line of headers with \r\n. The last header should not end with \r\n. Personally, I disagree with the use of cookies and cannot recommend anyone use cookies ever, but if you really must then feel free to do so.
  
  Lastly, if you do not wish to add any new headers, please leave the header string as is.

Q: The cache isn't working with *my extremely obscure browser here*!

A: Yeah, caching can get a bit tricky (for this I am sorry), which is a good reason this library should remain GPL, this way if it's really an issue you may change the caching logic yourself or simply remove the cache altogether. Internet Explorer implements "no-cache" not as "please re-validate each time regardless of age" but instead it replicates the usage of "no-store", which is specifically not what the RFC standard specifies. That being said, I'm 99% sure Firefox and Chrome/Chromium both cache correctly.

Q: Why ```Thread.Sleep(-1)```?

A: As the server is threaded (mostly using ```Task.Factory.StartNew()```) and completely non-blocking, for this library to work you will need an infinite loop, or a timed loop, or a loop with a long-term condition, somewhere within some file. Otherwise your program will end immediately and absolutely no users will arrive and you won't have a full API webservice.

I found a number of options.

1. A ```Thread.Sleep(1)``` in a while loop and this resulted in the slowest test results but didn't spin the CPU.
2. A WaitHandle with ```waitHandle.WaitOne()```, this resulted in intermediary results but didn't spin the CPU.
3. Simply ```Thread.Sleep(-1)```, which means "sleep the maximum amount of time" and provides intermediary results.
4. A ```Thread.Yield()``` in a while loop and this resulted in the best test results which are displayed on my site.

Unfortunately ```Thread.Yield()``` spins the CPU... my thinking on how this function works is by asking whatever mechanism handles the threading and task callbacks to check, meaning if you had a while loop calling ```Thread.Yield()``` multiple times within a second you'll naturally get better performance. This really comes back to you, as the person likely using my code, and your priorities.

If performance is the priority, I recommend an infinite loop with ```Thread.Yield()```
If resource efficiency is the priority, I recommend simply ```Thread.Sleep(-1)```
