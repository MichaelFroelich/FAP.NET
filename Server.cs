/*
				GNU GENERAL PUBLIC LICENSE
		                   Version 3, 29 June 2007

	 Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
	 Everyone is permitted to copy and distribute verbatim copies
	 of this license document, but changing it is not allowed.
	 
		Author: Michael J. Froelich
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO;

namespace FAP //Functional active pages , Functional programming And Pages, Free API Production, FAP seems like a good name!
{
	/// <summary>
	/// Server.
	/// </summary>
	public class Server
	{
		/* Constants */
		const int SERVERWARM = 1500;
		//Have a maximum of this many connections possible
		const int SERVERCOOL = 251;
		//If the connection count falls below this level, quickly make a lot more
		const string HTTP = "HTTP/1.1 ";
		//This causes issues if set to anything greater or lesser (excluding 1.0)
		const string VERSION = "1.7";
		//Current version
		const int TIMEOUT = 5;
		//Send and receive timeout
		const bool NODELAY = false;
		//Nagle algorithm thing
		/* State booleans */
		//bool needsreset = true;
		//Used to reset the listeners every 5 minutes (remember, Apache and NGINX do this too!)
		/* Other private variables */
		int listenercount = 0;
		//Used to count how many listeners are active
		TcpListener listener;
		//the main listener, will be duplicated in later versions, perhaps for added speed
		//Queue<TcpClient> incomming;
		//The queue
		Dictionary<string, Page> pagelist;
		//Where all the pages are kept
		Dictionary<string, HashSet<int>> cache304;

		//List<IAsyncResult> listenerlist; //This has... no way of actually workin
		//bool ever = true;

		/// <summary>
		/// Returns whether the listening/timing loop is currently running
		/// </summary>
		/// <value><c>true</c> if this instance is running; otherwise, <c>false</c>.</value>
		public bool IsRunning {
			get { return listener != null && listener.Server.IsBound; }
		}

		int port;

		/// <summary>
		/// Gets or sets the application port, I highly recommend you leave it at 1024
		/// </summary>
		/// <value>The application port.</value>
		public int Port {
			get { return port; }
			set {
				port = value;
				ResetListener(true);
			}
		}
		//Port to listen on
		/// <summary>
		/// Address to listen on
		/// </summary>
		/// <value>The address.</value>
		IPAddress address;

		/// <summary>
		/// Gets or sets the ip address.
		/// </summary>
		/// <value>The ip address.</value>
		public IPAddress Address {
			get { return address; }
			set {
				address = value;
				ResetListener(true);
			}
		}

		/// <summary>
		/// Changes the value of the query character, ie localhost/yourpage? where the query character here is '?'. Default is '?'.
		/// </summary>
		/// <value>The query character.</value>
		public char QueryCharacter {
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the cache's max age.
		/// </summary>
		/// <value>The cache's max age.</value>
		public int CacheMaxAge {
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the Maximum Transmission Unit, used for determining whether to user chunked transfer encoding or not. Note, if you're using this framework as intended (proxied through NGINX or Apache), your static webserver/proxy may chunk anyway. 
		/// </summary>
		/// <value>The Maximum Transmission Unit.</value>
		public int MTU {
			get;
			set;
		}

		/// <summary>
		/// Adds a page, mostly
		/// </summary>
		/// <param name="p">Pages, single pages</param>
		public void AddPage(Page p)
		{
			if (p != null)
				pagelist.Add(p.Path, p);
		}

		/// <summary>
		/// Adds a list of pages
		/// </summary>
		/// <param name="inList">A list or array of page through IEnumerable.</param>
		public void AddPage(IEnumerable<Page> inList)
		{
			foreach (Page p in inList)
				if (p != null)
					pagelist.Add(p.Path, p);
		}

		/// <summary>
		/// Clears all the pages, so you can reload a new list in.
		/// </summary>
		public void ClearPages()
		{
			pagelist.Clear();
		}

		/// <summary>
		/// Stops the server
		/// </summary>
		public void Stop()
		{
			//ever = false;
			listener.Server.Close();
			listener.Stop();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FAP.Server"/> class.
		/// </summary>
		/// <param name="inList">An IEnumerable of pages, try "Your data structure".Values</param>
		/// <param name="IpAddress">IP address, likely the default: 127.0.0.1 (please use a static webserver in conjunction with FAP.net)</param>
		/// <param name="port">Port or application port for a complete socket, default is 1024</param>
		/// <param name="mtu">mtu or Maximum Transmission Unit is the maximum size of a tcp packet, used for determining whether to user chunked transfer encoding or not</param>
		public Server(IEnumerable<Page> inList = null, string IpAddress = "127.0.0.1", int port = 1024, int mtu = 65535)
		{
			pagelist = new Dictionary<string, Page>();
			address = IPAddress.Parse(IpAddress);
			this.port = port;
			QueryCharacter = '?';
			CacheMaxAge = 31536000; //About an hour
			MTU = mtu; // Essentially the current MTU max
			listener = new TcpListener(Address, Port);
			cache304 = new Dictionary<string, HashSet<int>>();
			listener.Server.NoDelay = NODELAY;
			listener.Start();
			if (inList != null)
				foreach (Page p in inList)
					if (p != null)
						pagelist.Add(p.Path, p);
			while (listenercount < SERVERWARM) {
				Task.Factory.StartNew(Listen);
			}
			Task.Factory.StartNew(() => ResetListener());
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the <see cref="FAP.Server"/> is
		/// reclaimed by garbage collection.
		/// </summary>
		~Server()
		{
			listener.Server.Close();
			listener.Stop();
			Stop();
		}

		async void Listen()
		{
			try {
				listenercount++;
				if (listenercount > SERVERCOOL) {		//If there's less than SC listeners, it's a good idea to create more
					do {
						Thread.Yield(); 				//Yielding informs the system to do something else
						await Task.Delay(50); 			//This basically means "check back in 50ms," it doesn't actually delay/suspend anything
					} while (listener.Pending());		//Do not spawn whilst the server is being spammed, reserve the ticks for processing
				}
				listener.BeginAcceptTcpClient(ListenerCallback, listener); //second param may be listener or just null
			} catch (Exception e) {
				Console.Error.WriteLine("02: " + DateTime.UtcNow + " Listener error: " + e.Message);
				if (!listener.Server.IsBound) {
					Console.Error.Write(", will attempt to reset in 0.5 seconds");
					await Task.Delay(500);
					if (!listener.Server.IsBound) //IF the listener is still not listening THEN do something about it
						ResetListener(true);
					listenercount--;
				}
			}
		}

		void ListenerCallback(IAsyncResult result)
		{
			try {
				using (var c = listener.EndAcceptTcpClient(result)) {
					listenercount--; 						//Do this as soon as posible in case more listeners need to be spawned
					Parse(c);								//This makes the assumption you're already in a unique thread
				}
				Task.Factory.StartNew(Listen);
			} catch { //This does hide errors... but even Apache and NGINX reset their listeners, so should we and it's always going to throw errors
				//Console.Error.WriteLine("03: " + DateTime.UtcNow + " Callback error, probably just resetting but specifically: " + e.Message);
			}
		}

		async void Parse(TcpClient client)
		{
			char input;
			char method1;
			char method2;
			string code = "404"; //404 fail safe
			string message = string.Empty;
			string ipaddress = null;
			string output = null;
			string querystring = string.Empty;
			string path = string.Empty;
			string contenttype = string.Empty;
			string headers = string.Empty;
			string useragent = string.Empty;
			long contentlength = -1;
			int currentHash = -1;
			bool isIE = false;
			HashSet<int> clientCache = null;
			StringBuilder builder = new StringBuilder();
			StringBuilder headerbuilder = new StringBuilder(); 
			Page thispage;
			//client.NoDelay = NODELAY; //Usually best left as the default, uncomment and change NODELAY at the top of this file if required
			if (client == null || !client.Connected)
				return;
			try {
				//using (var reader = new StreamReader(stream))//using (var writer = new StreamWriter(stream)) 
				using (var stream = client.GetStream()) {//In the end, the reward for only using one stream is immeasurable				
					//if (/*stream.DataAvailable && */stream.CanRead && stream.CanWrite) {

					if (stream.CanRead) {
						#region inputparser
						method1 = (char)stream.ReadByte(); //G
						method2 = (char)stream.ReadByte(); //E , get it?
						do {
							input = (char)stream.ReadByte();
							if (input == ' ') {
								stream.ReadByte(); //Trims '/'
								break;
							}
						} while (!(input == '\uffff' || input == '\r')/*!char.IsControl(input) && input != '\uffff'*/); //FYI '\ufff' == -1
						do {
							input = (char)stream.ReadByte();
							if (input == QueryCharacter) {
								path = builder.ToString();
								builder.Clear();
							} else if (input == ' ')
								break;
							else {
								builder.Append(input);
							}
						} while (!(input == '\uffff' || input == '\r')/*!char.IsControl(input) && input != '\uffff'*/);//input != '\n' && input != '\r' && input != '\uffff' && input != '\0');
						querystring = builder.ToString();
						builder.Clear();
						while (input != '\uffff') { 
							input = (char)stream.ReadByte();
							headerbuilder.Append(input);
							if (input == '\n') { 				//Ensures we only check after a new line
								input = (char)stream.ReadByte();
								switch (input) {
									case '\r':
										stream.ReadByte(); //Trims the '\n'
										goto NoMoreHeaders; //Virtually the only way how to jump out of a switch within a control loop
										break;
									case 'X':
									case 'x': //Find x-IP style headers
										while (input != ':') {
											builder.Append(input);
											input = (char)stream.ReadByte();
										}
										const string x4rd4 = "X-Forwarded-For";
										const string xreal = "X-Real-IP";
										var ipstring = builder.ToString();
										if (ipstring == x4rd4 || //In the desperate attempt to ensure HTTP compatibility
										    ipstring == xreal) {
											headerbuilder.Append(builder);
											builder.Clear();
											headerbuilder.Append(":" + (char)stream.ReadByte()); //Trims the space
											input = (char)stream.ReadByte();
											while (input != '\r' && input != ',') { //gets the first ip address (ie ipadd1, ipadd2,
												builder.Append(input);
												input = (char)stream.ReadByte();
											}
											ipaddress = builder.ToString();
										}
										headerbuilder.Append(builder);
										builder.Clear();
										break;
									case 'U':
									case 'u': //Find the user agent
										while (input != ':') {
											builder.Append(input);
											input = (char)stream.ReadByte();
										}
										const string useragentheader = "User-Agent";
										var useragentstring = builder.ToString();
										if (useragentstring == useragentheader) {
											builder.Clear();
											input = (char)stream.ReadByte();
											while (input != '\r') { //Read until the new line
												builder.Append(input);
												input = (char)stream.ReadByte();
											}
											useragent = builder.ToString();
											headerbuilder.Append("User-Agent: " + useragent);
										}
										builder.Clear();
										break;
									default:
										headerbuilder.Append(input);
										break;
								}
							} 
						}

						NoMoreHeaders:
						headers = headerbuilder.ToString();
						if (ipaddress == null) { //Next best guess for the ip address
							ipaddress = (((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()); 
						}
						//int integerinput =;
						List<byte> utf8bytes = new List<byte>();
						while (stream.DataAvailable) { //For Mono/.NET compatibility this must be used instead of if -1/uffff
							utf8bytes.Add((byte)stream.ReadByte());	//Networkstreams have strange behaviour at the end of their streams
							//integerinput = stream.ReadByte();
						}
						message = Encoding.UTF8.GetString(utf8bytes.ToArray());
						#endregion
						#region PageProcessor
						Page staticpage; //Static data remains untouched
						if (querystring.Length > MTU) {
							code = "414"; //If the query string turns out to be greater than the MTU, generate an URI too big error
							headers = string.Empty;
						} else if (headers.Length > MTU) {
							code = "431"; //If the query string turns out to be greater than the MTU, generate an URI too big error
							headers = string.Empty;
						} else if (!string.IsNullOrEmpty(path) && pagelist.TryGetValue(path, out staticpage)) {
							thispage = (Page)Activator.CreateInstance(staticpage.GetType());
							//Functions
							thispage.get = staticpage.get;
							thispage.put = staticpage.put;
							thispage.post = staticpage.post;
							thispage.delete = staticpage.delete;
							//For polymorphics
							thispage.Headers = headers;
							thispage.UserIP = ipaddress;
							thispage.UserAgent = useragent;
							//For functionals
							staticpage.Headers = headers;
							staticpage.UserIP = ipaddress;
							staticpage.UserAgent = useragent;
							switch (method1) {
								case 'h':
								case 'H': //Head
								case 'g':
								case 'G': //Get
									{		//"HEAD" is identical to "GET", except no content is generated, this is ensured later
										isIE = useragent.Contains("IE"); //Because Internet Explorer honestly does not implement caches correctly, "no-cache" does NOT mean "do not cache"
										output = await Task.FromResult<string>(thispage.Get(querystring, message));
										currentHash = (querystring + ipaddress + useragent + output).GetHashCode(); 
										try {
											if (cache304.TryGetValue(ipaddress, out clientCache)) {
												if (clientCache.Contains(currentHash)) {
													code = "304";
												} else {
													clientCache.Add(currentHash);
													Task.Factory.StartNew(() => CleanCache(clientCache, ipaddress, currentHash));
												}
											} else {
												clientCache = new HashSet<int>(); //New user
												clientCache.Add(currentHash);
												cache304.Add(ipaddress, clientCache);
												Task.Factory.StartNew(() => CleanCache(clientCache, ipaddress, currentHash));
											}
											break;
										} catch { 
											Console.Error.WriteLine("06: " + DateTime.UtcNow + " caching error from " + ipaddress + " " + useragent + "\t\nAre you benchmarking or under attack?");   
											code = "503";
										}
									}
									break;
								case 'p':
								case 'P':
									{
										if (method2 == 'U' || method2 == 'u')
											output = await Task.FromResult<string>(thispage.Put(querystring, message));
										else if (method2 == 'O' || method2 == 'o')
											output = await Task.FromResult<string>(thispage.Post(querystring, message));
										break;
									}
								case 'd':
								case 'D':
									{
										output = await Task.FromResult<string>(thispage.Delete(querystring, message));
										break;
									}
								default:
									code = "501";
									break;
							}
							if (headers == thispage.Headers)
								headers = string.Empty;
							else {
								const string contenttypeheader = "content-type:";
								headers = "\r\n" + thispage.Headers; //Ensures at least one new line... yeah I know
								foreach (string s in headers.Split('\n')) { //This has about a 50-100ns bottleneck, solution: forget about headers
									if (s.ToLower().StartsWith(contenttypeheader)) {
										contenttype = s.Substring(contenttypeheader.Length);
										contenttype.Replace("\r", null);	//Remove the possible carriage return character
										headers.Replace(s + '\n', null);	//Remove the entire content type line from the headers (or else there'll be double)
									}
								}
							}
						} else
							headers = string.Empty;
						#endregion
						#region outputparse
						int length = (output == null ? 0 : Encoding.UTF8.GetByteCount(output)); //Ensures I don't null check an output that is null
						if (code != "304" && length > 0) { //If we haven't generated a 304 or a nothing response
							code = "200"; //Begin code as 200 for default success, but now include user HTTP codes
							if (length >= 5 && //If the output is long enough to be xxx\r\n
							    Char.IsDigit(output[0]) && //If the first three characters are digits
							    Char.IsDigit(output[1]) &&
							    Char.IsDigit(output[2]) &&
							    output[3] == '\r' && //If these three digits end with a line breaker
							    output[4] == '\n') {
								code = output.Substring(0, 3); //Then we have the code
								output = (length > 5) ? output.Substring(5, length - 5) : string.Empty; 	//And we can remove it from the output
								length = Encoding.UTF8.GetByteCount(output); //Update the length
							}
						}
						#endregion
						#region MIME
						byte[] bytes = new byte[2]; //primitives begin as 0 equivalent
						bool isGzip = false;//... or false 
						if (length >= 2 && contenttype == string.Empty) {
							bytes = Encoding.ASCII.GetBytes(output.ToCharArray(0, 2)); //Needs to be ASCII bytes, here ASCII info loss is preferable
							//If length of the resultant output is greater MTU OR the first two bytes indicate some sort of GZIP/ZIP encoding
							isGzip = ((bytes[0] == (char)0x1f) && (bytes[1] == (char)0x8b || bytes[1] == (char)0x3f)); //Gzip is NOT a mime type
							contenttype = "text/plain";
							switch (output[0]) { //mime/content type handling
								case (char)0:
									{
										if (length > 3) {
											if (output[0] == (char)0 && output[1] == (char)0 && output[2] == (char)1) {
												contenttype = "image/x-icon";
											}
											if (length > 9 && "ftyp" == output.Substring(4, 4)) {
												contenttype = "video/mp4";
											}
										}
										break;
									}
								case '[':
									{
										if (output[length - 1] == ']')
											contenttype = "application/json"; //As always, this framework promotes the use of JSON over CSV or XML or null terminated strings
										break;
									}
								case '{'://0x7b
									{
										if (output[length - 1] == '}')
											contenttype = "application/json";
										break;
									}
								case '<'://0x3c
									{
										if (output[length - 1] == '>') {
											if (length > 3 && output[2] == 'x') { //<?xm
												contenttype = "text/xml";
											} else
												contenttype = "text/html";
										}
										break;
									}
								case '%'://0x25
									{
										if (length > 4 && (output.Substring(1, 3) == "PDF")) {
											contenttype = "application/pdf";
										}
										break;
									}

								case (char)0x42:
									{
										if (output[1] == (char)0x4D) {
											contenttype = "image/bmp";
										}
										break;
									}
								case (char)0x47:
									{
										if (output[1] == (char)0x49) {
											contenttype = "image/gif";
										}
										break;
									}
								case (char)0x49:
									{
										if (output[1] == (char)0x44) {
											contenttype = "audio/mpeg";
										}
										break;
									}
								case (char)0x4d:
									{
										if (output[1] == (char)0x54) {
											contenttype = "audio/midi";
										}
										break;
									}
								case (char)0x4f:
									{
										if (output[1] == (char)0x67) {
											contenttype = "audio/ogg";
										}
										break;
									}
								case (char)0x66:
									{
										if (output[1] == (char)0xfc) {
											contenttype = "audio/flac";
										}
										break;
									}
								case (char)0x89:
									{
										if (output[1] == (char)0x50) {
											contenttype = "image/png";
										}
										break;
									}
								case (char)0xff:
									{
										if (output[1] == (char)0xd8) {
											contenttype = "image/jpeg";
										} else if (output[1] == (char)0xfb) {
											contenttype = "audio/mpeg";
										}
										break;
									}
								default:
									contenttype = "text/plain";
									break;
							}
						}
						#endregion
						#region httpcodeparser
						switch (code[0]) {
							case '1':
								length = 0;
								builder.Append(HTTP + H.S100 + headers + "\r\n\r\n");
								break;
							case '2':
								{
									#region 2xx
									switch (code[2]) {
										case '0':
											builder.Append(HTTP + H.S200);
											break;
										case '1':
											builder.Append(HTTP + H.S201);
											break;
										case '2':
											builder.Append(HTTP + H.S202);
											break;
										case '3':
											builder.Append(HTTP + H.S203);
											break;
										case '4':
											length = 0; //204 does not generate content
											builder.Append(HTTP + H.S204);
											break;
										case '5':
											builder.Append(HTTP + H.S205);
											break;
										case '6':
											builder.Append(HTTP + H.S206);
											break;
										default:
											{
												builder.Append(HTTP + code);
												break;
											}
									}
									builder.Append(/*HTTP + CODE*/"\r\n" +
									"Server: FAP.NET " + VERSION + " Codename: Meisscanne\r\n" +
									"Date: " + DateTime.UtcNow.ToString("R") + "\r\n" +
									"Connection: close\r\n" +
									(length > 0 ? "Content-type: " + contenttype + "; charset=utf-8\r\n" : string.Empty) + //http://www.w3.org/Protocols/rfc2616/rfc2616-sec7.html#sec7.2.1
									(headers.Length > 0 ? headers.Substring(2) + "\r\n" : string.Empty) +
									"Cache-Control: private, max-age=" + CacheMaxAge + (isIE ? string.Empty : ", no-cache") + ", must-revalidate\r\n" + //Cache control since 1.4
									(method1 == 'g' || method1 == 'G' ? "ETag: \"" + String.Format("{0:x}", currentHash) + "\"\r\n" : string.Empty) +
									(isGzip ? 
											"Content-Encoding: gzip\r\nTransfer-Encoding: Chunked\r\n\r\n" : //If it seems GZIP is being sent, start chunking
											(length < MTU ? 
												"Content-Length: " + length + "\r\n\r\n" : "Transfer-Encoding: Chunked\r\n\r\n"))
									);
									break;
									#endregion
								}
							case '3':
								{
									#region 3xx
									switch (code[2]) {
										case '0':
											builder.Append(HTTP + H.R300 + headers + "\r\n\r\n");
											break;
										case '1':
											builder.Append(HTTP + H.R301 + headers + "\r\n\r\n");
											break;
										case '2':
											builder.Append(HTTP + H.R302 + headers + "\r\n\r\n");
											break;
										case '3':
											builder.Append(HTTP + H.R303 + headers + "\r\n\r\n");
											break;
										case '4':
											length = 0;
											builder.Append(HTTP + H.R304 + "\r\n" +
											"Server: FAP.NET " + VERSION + " Codename: Meisscanne\r\n" +
											"Date: " + DateTime.UtcNow.ToString("R") + "\r\n" +
											"Connection: close\r\n" +
											"Cache-control: private, max-age=" + CacheMaxAge + (isIE ? string.Empty : ", no-cache") + ", must-revalidate\r\n" +
											"ETag: \"" + String.Format("{0:x}", currentHash) + "\"\r\n\r\n");
											break;
										case '5':
											builder.Append(HTTP + H.R305 + headers + "\r\n\r\n");
											break;
										case '6':
											builder.Append(HTTP + H.R306 + headers + "\r\n\r\n");
											break;
										case '7':
											builder.Append(HTTP + H.R307 + headers + "\r\n\r\n");
											break;
										case '8':
											builder.Append(HTTP + H.R308 + headers + "\r\n\r\n");
											break;
										default:
											builder.Append(HTTP + H.S200 + headers + "\r\n\r\n");
											break;
									}
									break;
									#endregion
								}
							case '4':
								{
									#region 4xx
									switch (code[1]) {
										case '0':
											switch (code[2]) {
												case '0':
													builder.Append(HTTP + H.E400 + headers + "\r\n\r\n");
													break;
												case '1':
													builder.Append(HTTP + H.E401 + headers + "\r\n\r\n");
													break;
												case '2':
													builder.Append(HTTP + H.E402 + headers + "\r\n\r\n");
													break;
												case '3':
													builder.Append(HTTP + H.E403 + headers + "\r\n\r\n");
													break;
												case '4':
													builder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
													break;
												case '5':
													builder.Append(HTTP + H.E405 + headers + "\r\n\r\n");
													break;
												case '6':
													builder.Append(HTTP + H.E406 + headers + "\r\n\r\n");
													break;
												case '7':
													builder.Append(HTTP + H.E407 + headers + "\r\n\r\n");
													break;
												case '8':
													builder.Append(HTTP + H.E408 + headers + "\r\n\r\n");
													break;
												case '9':
													builder.Append(HTTP + H.E409 + headers + "\r\n\r\n");
													break;
												default:
													builder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
													break;

											}
											break;
										case '1':
											switch (code[2]) {
												case '0':
													builder.Append(HTTP + H.E410 + headers + "\r\n\r\n");
													break;
												case '1':
													builder.Append(HTTP + H.E411 + headers + "\r\n\r\n");
													break;
												case '2':
													builder.Append(HTTP + H.E412 + headers + "\r\n\r\n");
													break;
												case '3':
													builder.Append(HTTP + H.E413 + headers + "\r\n\r\n");
													break;
												case '4':
													builder.Append(HTTP + H.E414 + headers + "\r\n\r\n");
													break;
												case '5':
													builder.Append(HTTP + H.E415 + headers + "\r\n\r\n");
													break;
												case '6':
													builder.Append(HTTP + H.E416 + headers + "\r\n\r\n");
													break;
												case '7':
													builder.Append(HTTP + H.E417 + headers + "\r\n\r\n");
													break;
												case '9':
													builder.Append(HTTP + H.E419 + headers + "\r\n\r\n");
													break;
												default:
													builder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
													break;
											}
											break;
										case '2':
											switch (code[2]) {
												case '0':
													builder.Append(HTTP + H.E420 + headers + "\r\n\r\n"); //For illegal reasons
													break;
												case '1':
													builder.Append(HTTP + H.E421 + headers + "\r\n\r\n");
													break;
												default:
													builder.Append(HTTP + H.E42x + headers + "\r\n\r\n");
													break;
											}
											break;
										case '3':
											builder.Append(HTTP + H.E431 + headers + "\r\n\r\n");
											break;
										case '4':
											builder.Append(H.E444 + "\r\n\r\n"); //A proper 444 report is JUST 444, nothing else
											break;
										case '5':
											builder.Append(HTTP + H.E451 + headers + "\r\n\r\n"); //For legal reasons
											break;
										case '9':
											switch (code[2]) {
												case '5':
													builder.Append(HTTP + H.E495 + headers + "\r\n\r\n");
													break;
												case '6':
													builder.Append(HTTP + H.E496 + headers + "\r\n\r\n");
													break;
												case '7':
													builder.Append(HTTP + H.E497 + headers + "\r\n\r\n");
													break;
												case '9':
													builder.Append(HTTP + H.E499 + headers + "\r\n\r\n");
													break;
												default:
													builder.Append(HTTP + H.E49x + headers + "\r\n\r\n");
													break;
											}
											break;
										default:
											builder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
											break;
										
									}
									break;
									#endregion
								}
							case '5':
								{
									#region 5xx
									length = 0;
									switch (code[1]) {
										case '0':
											switch (code[2]) {
												case '0':
													builder.Append(HTTP + H.G500 + headers + "\r\n\r\n");
													break;
												case '1':
													builder.Append(HTTP + H.G501 + headers + "\r\n\r\n");
													break;
												case '2':
													builder.Append(HTTP + H.G502 + headers + "\r\n\r\n");
													break;
												case '3':
													builder.Append(HTTP + H.G503 + headers + "\r\n\r\n");
													break;
												case '4':
													length = 0;
													builder.Append(HTTP + H.G504 + headers + "\r\n\r\n");
													break;
												case '5':
													builder.Append(HTTP + H.G505 + headers + "\r\n\r\n");
													break;
												case '6':
													builder.Append(HTTP + H.G506 + headers + "\r\n\r\n");
													break;
												default:
													builder.Append(HTTP + H.G502 + headers + "\r\n\r\n");
													break;
											}
											break;
										default:
											if (code == "511") {
												builder.Append(HTTP + H.G502 + headers + "\r\n\r\n");
												break;
											} 
											if (code == "520") {
												builder.Append(HTTP + H.G502 + headers + "\r\n\r\n");
												break;
											}
											break;
									}
									break;
									#endregion
								}
							default:
								builder.Append(HTTP + code + "\r\n\r\n");
								break;
						}
						#endregion
						if (builder.Length > 0) {
							if (method1 == 'h' || method1 == 'H')
								length = 0; //Right at the very, very end, to ensure the response is otherwise identical
							if (length < MTU) {
								var towrite = Encoding.UTF8.GetBytes(builder + (length == 0 ? string.Empty : (output + (isGzip ? "\r\n0\r\n" : "\r\n"))));
								stream.Write(towrite, 0, towrite.Length);
							} else {
								try {
									int bytestowrite = 0;
									stream.Write(Encoding.UTF8.GetBytes(builder.ToString()), 0, builder.Length); //First build the headers
									for (int i = 0; i < length; i += MTU) {
										bytestowrite = (length - i > MTU) ? MTU : length - i;
										var towrite = Encoding.UTF8.GetBytes(String.Format("{0:x}", bytestowrite) + "\r\n" + output.Substring(i, bytestowrite) + "\r\n");
										stream.Write(towrite, 0, towrite.Length);
									}
								} catch (Exception e) {
									Console.Error.WriteLine("04: " + e.Message);
								}
								stream.Write(Encoding.UTF8.GetBytes("0\r\n"), 0, 3); //Finish, magic bullet is that it doesn't matter what failed up there
							}
						} else {
							const string towrite = HTTP + H.G501 + "\r\n\r\n";
							stream.Write(Encoding.UTF8.GetBytes(towrite), 0, towrite.Length);
						}
					}
					stream.Flush(); 
					stream.Close();
					client.Close(); 
				}

			} catch (Exception e) { 
				Console.Error.WriteLine("05: " + DateTime.UtcNow + " That was a bad client  " + ipaddress + " " + useragent + e);   
				ResetListener(true);
			}
		}

		async void CleanCache(HashSet<int> clientcache, string ipheaders, int hash)
		{
			await Task.Delay(CacheMaxAge);
			if (clientcache != null && clientcache.Count > 0)
				clientcache.Remove(hash);
			if (clientcache.Count == 0)
				cache304.Remove(ipheaders);
		}

		async void ResetListener(bool force = false)
		{
			try {
				listener.Server.Close();	//Fixes an "address in use" error
				listener.Stop();
				listenercount = 0;
				listener = new TcpListener(Address, Port);
				listener.Server.NoDelay = NODELAY;
				listener.Start();
				while (listenercount < SERVERCOOL) {
					listenercount++;
					listener.BeginAcceptTcpClient(ListenerCallback, listener);
				}
				if (!force) {
					await Task.Delay(TimeSpan.FromMinutes(5));
					Task.Factory.StartNew(() => ResetListener()); //I do not wish to await this statement
				}
			} catch (Exception e) {
				Console.Error.WriteLine("06: " + DateTime.UtcNow + " Reset Error: " + e.Message);
			}
		}

		static class H
		{
			//Just a bunch of consts, returning from a page function with ###/r/n(rest of your message here) can be used for return codes
			public const string S100 = "100 Continue";
			public const string S200 = "200 Ok";
			public const string S201 = "201 Created";
			public const string S202 = "202 Accepted";
			public const string S203 = "203 Non-Authoritative Information";
			public const string S204 = "204 No Content";
			public const string S205 = "205 Reset Content";
			public const string S206 = "206 Partial Content";
			public const string R300 = "300 Multiple Choices";
			public const string R301 = "301 Moved Permanently";
			public const string R302 = "302 Found";
			public const string R303 = "303 See Other";
			public const string R304 = "304 Not modified";
			public const string R305 = "305 Use Proxy";
			public const string R306 = "306 Switch Proxy";
			public const string R307 = "307 Temporary Redirect";
			public const string R308 = "308 Permanent Redirect";
			public const string E400 = "400 Bad Request";
			public const string E401 = "401 Unauthorized";
			public const string E402 = "402 Payment Required";
			public const string E403 = "403 Forbidden";
			public const string E404 = "404 Not Found";
			public const string E405 = "405 Method Not Allowed";
			public const string E406 = "406 Not Acceptable";
			public const string E407 = "407 Proxy Authentication Required";
			public const string E408 = "408 Request Timeout";
			public const string E409 = "409 Conflict";
			public const string E410 = "410 Gone";
			public const string E411 = "411 Length Required";
			public const string E412 = "412 Precondition Failed";
			public const string E413 = "413 Payload Too Large";
			public const string E414 = "414 Request-URI Too Long";
			public const string E415 = "415 Unsupported Media Type";
			public const string E416 = "416 Requested Range Not Satisfiable";
			public const string E417 = "417 Expectation Failed";
			public const string E419 = "419 Authentication Timeout";
			public const string E420 = "420 It's Time";
			public const string E421 = "421 Misdirected Request";
			public const string E42x = "42x Strange error";
			public const string E431 = "431 Request Header Fields Too Large";
			public const string E444 = "444";
			public const string E451 = "451 Unavailable For Legal Reasons";
			public const string E495 = "495 Cert Error";
			public const string E496 = "496 No Cert";
			public const string E497 = "497 HTTP to HTTPS";
			public const string E499 = "499 Client Closed Request";
			public const string E49x = "49x Unhandled front-end server error";
			public const string G500 = "500 Internal Server Error";
			public const string G501 = "501 Not Implemented";
			public const string G502 = "502 Bad Gateway";
			public const string G503 = "503 Service Unavailable";
			public const string G504 = "504 Gateway Timeout";
			public const string G505 = "505 HTTP Version Not Supported";
			public const string G506 = "506 Variant Also Negotiates";
		}
	}
}
