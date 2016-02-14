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
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace FAP //Functional active pages , Functional programming And Pages, Free API Production, FAP seems like a good name!
{
	/// <summary>
	/// Server.
	/// </summary>
	public class Server
	{
		

		/* Constants */
		//Have a maximum of this many connections possible, set at 21000 as of 1.0.9
		const int SERVERWARM = 21000;

		//When reading for headers, retry this amount of times before giving up and returning 444
		//Unused in favour of using the timeout variable from the socket, throws an exception caught and then responded to with 444
		//const int READRETRIES = 10000;

		//If the connection count falls below this level, quickly make a lot more, set at 7000 as of 1.0.9 as it's the highest expected RPS
		// const int SERVERCOOL = 700;

		//Socket backlog, when initialising the socket for listening
		const int SOCKETBACKLOG = 10000;

		//This causes issues if set to anything greater or lesser (excluding 1.0)
		const string HTTP = "HTTP/1.1 ";

		//current FAP version,
		const string VERSION = "1.1.1";

		//Timeout for both send and receive
		const int TIMEOUT = 32;

		//Nagle no delay thing, leave as "true" for more efficient chunking
		const bool NODELAY = true;

		//Default value for CacheMaxAge, an hour, used to determine session variable timeout
		const int CACHEMAXAGEDEFAULT = 3600000;

		//Read buffer, best left between 1024 to 8192
		const int READBUFFER = 8192;

		//The only socket
		Socket listener;

		//the main listener, will be duplicated in later versions, perhaps for added speed
		//Queue<TcpClient> incomming;
		//The queue
		Dictionary<string, Page> pagelist;

		//Where all the pages are kept
		//Dictionary<string, HashSet<int>> cache304;

		//List<IAsyncResult> listenerlist; //This has... no way of actually workin
		//bool ever = true;

		/// <summary>
		/// Returns whether the listening/timing loop is currently running
		/// </summary>
		/// <value><c>true</c> if this instance is running; otherwise, <c>false</c>.</value>
		public bool IsRunning {
			get { return listener != null && listener.IsBound; }
		}

		int port;

		/// <summary>
		/// Gets the application port, the setter was removed as it's no longer possible to reset the callbacks using the current connection method
		/// </summary>
		/// <value>The application port.</value>
		public int Port {
			get { return port; }
		}

		//Port to listen on
		/// <summary>
		/// Address to listen on
		/// </summary>
		/// <value>The address.</value>
		IPAddress address;

		/// <summary>
		/// Gets the ip address, the setter was removed as it's no longer possible to reset the callbacks using the current connection method
		/// </summary>
		/// <value>The ip address.</value>
		public IPAddress Address {
			get { return address; }
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
		/// Gets or sets the maximum age of a cached page, such that page instances will remain in memory for a maximum of this amount of time,
		/// provided that the page is not accessed by a client user.
		/// </summary>
		/// <value>The page cache's max age in milliseconds. Default is an hour (3600000 milliseconds)</value>
		public int CacheMaxAge {
			get;
			set;
		}


		/// <summary>
		/// Gets or sets the Maximum Transmission Unit, used for determining whether to use chunked transfer encoding or not. Note, if you're using this framework as intended (proxied through NGINX or Apache), your static webserver/proxy may chunk anyway.
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
			if (p != null) {
				Page.SetStatic(p);
				pagelist.Add(p.Path, p);
			}
		}

		/// <summary>
		/// Adds a list of pages
		/// </summary>
		/// <param name="inList">A list or array of pages through any container implementing IEnumerable.</param>
		public void AddPage(IEnumerable<Page> inList)
		{
			foreach (Page p in inList)
				if (p != null) {
					Page.SetStatic(p);
					pagelist.Add(p.Path, p);
				}
		}

		/// <summary>
		/// Removes a page, mostly
		/// </summary>
		/// <param name="p">Pages, single pages</param>
		public void RemovePage(Page p)
		{
			if (p != null && pagelist.ContainsKey(p.Path))
				pagelist.Remove(p.Path);
		}

		/// <summary>
		/// Removes a list of pages
		/// </summary>
		/// <param name="inList">A list or array of pages through any container implementing IEnumerable.</param>
		public void RemovePage(IEnumerable<Page> inList)
		{
			foreach (Page p in inList)
				if (p != null && pagelist.ContainsKey(p.Path))
					pagelist.Remove(p.Path);
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
			listener.Close();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FAP.Server"/> class.
		/// </summary>
		/// <param name="inList">An IEnumerable of pages, try "Your data structure".Values</param>
		/// <param name="IpAddress">IP address, likely the default: 127.0.0.1 (please use a static webserver in conjunction with FAP.net)</param>
		/// <param name="port">Port or application port for a complete socket, default is 1024</param>
		/// <param name="mtu">mtu or Maximum Transmission Unit is the maximum size of a tcp packet, used for determining whether to use chunked transfer encoding or not</param>
		public Server(IEnumerable<Page> inList = null, string IpAddress = "127.0.0.1", int port = 1024, int mtu = 65535)
		{
			pagelist = new Dictionary<string, Page>();
			address = IPAddress.Parse(IpAddress);
			this.port = port;
			QueryCharacter = '?';
			CacheMaxAge = CACHEMAXAGEDEFAULT; //About an hour 3600000
			MTU = mtu; // Essentially the current MTU max
			listener = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);//new TcpListener(Address, Port);
			//cache304 = new Dictionary<string, HashSet<int>>();
			listener.NoDelay = NODELAY;
			listener.Bind(new IPEndPoint(address, port));
			listener.Listen(SOCKETBACKLOG);
			listener.ReceiveTimeout = TIMEOUT;
			listener.SendTimeout = TIMEOUT;
			if (inList != null)
				foreach (Page p in inList)
					if (p != null) {
						Page.SetStatic(p);
						pagelist.Add(p.Path, p);
					}
			LoadListeners();
			//Task.Factory.StartNew(() => ResetListener()); //Calls LoadListeners
		}

		void LoadListeners()
		{

			//while (listenercount < SERVERWARM) //Calling the following code multiple times still provides a proven benefit through benchmarking
			//{
			for (int i = 0; i < SERVERWARM; i++) {
				var ev = new SocketAsyncEventArgs();
				ev.Completed += CallBack;
				listener.AcceptAsync(ev);
			}
			//listener.BeginAccept(ListenerCallback, listener);
			//}
		}

		void CallBack(object sender, SocketAsyncEventArgs eve)
		{/*
			try {*/
			Parse(eve.AcceptSocket);
			eve.AcceptSocket.Disconnect(false);
			var ev = new SocketAsyncEventArgs();
			ev.Completed += CallBack;
			listener.AcceptAsync(ev);
			eve.AcceptSocket.Close();
			//If you close, you can't reuse SAEA even on .NET. If you don't close, you'l get a too many open files exception on mono
			//If anyonee knows a solution, please contact me!
			/*
			} catch (Exception e) {
				Console.Error.WriteLine("07: Connection error, " + e.Message);
			}*/
		}


		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the <see cref="FAP.Server"/> is
		/// reclaimed by garbage collection.
		/// </summary>
		~Server()
		{
			Stop();
		}

		/*
        async void Listen()
        {
            if (listenercount > SERVERCOOL)
            {		//If there's less than SC listeners, it's a good idea to create more
                do
                {
                    await Task.Yield();				//There is no thread
                } while (listener.Poll(-1,SelectMode.SelectRead));		//Do not spawn whilst the server is being spammed, reserve the ticks for processing
            }
            try
            {
                listenercount++;
                listener.BeginAccept(ListenerCallback, listener);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("02: " + DateTime.UtcNow + " Listener error: " + e.Message);
                if (!listener.IsBound)
                {
                    Console.Error.Write(", will attempt to reset in 0.5 seconds");
                    Thread.Sleep(500);      //now working with VisualStudio, which does not allow await in a catch statement
                    if (!listener.IsBound)  //IF the listener is still not listening THEN do something about it
                        ResetListener(true);
                    listenercount--;
                }
            }
        }

        void ListenerCallback(IAsyncResult result)
        {
            try
            {
                using (var c = listener.EndAccept(result))
                {
                    listenercount--; 						//Do this as soon as posible in case more listeners need to be spawned
                    //Parse(c);								//This makes the assumption you're already in a unique thread
                    c.Disconnect(true);
                }
            }
            catch
            { //This does hide errors... but even Apache and NGINX reset their listeners, so should we and it's always going to throw errors
                //Console.Error.WriteLine("03: " + DateTime.UtcNow + " Callback error, probably just resetting but specifically: " + e.Message);
            }
            listener.BeginAccept(ListenerCallback, listener);
            //Listen();
        }
        */
		/*
		 static int ReadAByte(Socket socket)
		{
			byte[] onebyteinput = new byte[1];
			try {
				while (0 == socket.Receive(onebyteinput, 1, 0)) {
				} //Polling is probably the quickest option, against callbacks
			} catch {
				return -1;
			}
			return (int)onebyteinput[0];
		}
		*/


		async void Parse(Socket client)
		{
			//char input;
			char method1 = '\0';
			char method2 = '\0';
			string code = "404"; //404 fail safe
			string message = string.Empty;
			string ipaddress = null;
			string output = string.Empty;
			string querystring = string.Empty;
			string path = string.Empty;
			string contenttype = string.Empty;
			string headers = string.Empty;
			string useragent = string.Empty;
			string requestEtag = string.Empty;
			//long contentlength = long.MinValue;
			int currentHash = -1;
			int outputbytelength;
			int hashsum;
			bool isCacheable = false;
			bool isGzip = false;
			Encoding encoder = Encoding.UTF8; //Used to switch between UTF8 and BigEndianUnicode for a last ditch attempt at binary safety
			//bool isIE = false;
			//HashSet<int> clientCache = null;
			StringBuilder outputheaderbuilder = new StringBuilder();
			StringBuilder inputheaderbuilder = new StringBuilder();
			List<byte> utf8bytes = new List<byte>(); //Used for a whole number of times I read/write with utf8
			Page thispage;
			Page staticpage;
			//client.NoDelay = NODELAY; //This is actually pointless after the connection has been opened, set on the TcpListener.Server instead
			/*if (!client.Poll(1000, SelectMode.SelectRead))
				return;*/
			try {
				#region inputparser
				byte[] bytesreceived = new byte[READBUFFER]; //whilst assigning data is expensive, I changed this code with the assumption that system calls are more expensive. See my blog
				long bytestoread = 0;
				string header;
				int seek = 0;
				while (true) {
					while ((bytestoread = client.Available) <= 0) {
					} //The idea behind polling, instead of grabbing all data through async methods, is to process headers whilst data is still being received
					if (bytestoread > READBUFFER)
						bytestoread = READBUFFER;
					client.Receive(bytesreceived, (int)bytestoread, 0);
					//retryread = 0;
					for (seek = 0; seek < bytestoread; seek++) {
						if (bytesreceived[seek] == '\n') {
							header = Encoding.UTF8.GetString(utf8bytes.ToArray());//header = builder.ToString();
							switch (header[0]) {
								case 'H':
								case 'G':
								case 'P':
								case 'D':
									if (method1 == '\0') { //First line condition
										method1 = header[0];
										method2 = header[1];
										var spaceindex = header.LastIndexOf(' ');
										var pagefinderindex = header.IndexOf('/') + 1;
										var querycharacterindex = header.IndexOf(QueryCharacter, pagefinderindex) + 1;
										if (querycharacterindex > 1 && querycharacterindex <= spaceindex) { //incase the query character is 'H', 'T', 'P', '1', '2', '.', or '/'
											path = header.Substring(pagefinderindex, querycharacterindex - pagefinderindex - 1);
											querystring = header.Substring(querycharacterindex, spaceindex - querycharacterindex);
										} else { //For no query string queries, such as page requests for FAP.React or blank queries from NGINX (which will drop the querycharacter)
											querycharacterindex = header.IndexOf(' ', pagefinderindex) + 1; //query character becomes ' '
											path = header.Substring(pagefinderindex, querycharacterindex - pagefinderindex - 1);
										}
										header = header.Substring(spaceindex + 1, (header.Length - spaceindex) - 1); //Gets the HTTP version
									}
									break;/*
								case 'C': //Content-Length might not be possible
									if (header.StartsWith("Content-Length", StringComparison.Ordinal)) {
										contentlength = long.Parse(header.Substring(16));
									}
									break;*/
								case 'I':
									const string IFNONEMATCH = "If-None-Match";
									if (header.StartsWith(IFNONEMATCH, StringComparison.Ordinal)) {
										requestEtag = header.Substring(header.IndexOf("\"") + 1, 8);
									}
									break;
								case 'U':
									const string USERAGENT = "User-Agent";
									if (header.StartsWith(USERAGENT, StringComparison.Ordinal)) {
										useragent = header.Substring(12, header.Length - 12 - 1);
									}
									break;
								case 'X':
									const string XFORWARDEDFOR = "X-Forwarded-For";
									const string XREALIP = "X-Real-IP";
									if (header.StartsWith(XFORWARDEDFOR, StringComparison.Ordinal)) {
										var endcr = header.IndexOf('\r');
										var endco = header.IndexOf(',');
										if (endco > 0)
											ipaddress = header.Substring(17, endco - 17);
										else
											ipaddress = header.Substring(17, endcr - 17);
									} else if (header.StartsWith(XREALIP, StringComparison.Ordinal)) {
										var endcr = header.IndexOf('\r');
										var endco = header.IndexOf(',');
										if (endco > 0)
											ipaddress = header.Substring(11, endco - 11);
										else
											ipaddress = header.Substring(11, endcr - 11);
									}
									break;
							}
							inputheaderbuilder.Append(header); //No matter what, append the header
							inputheaderbuilder.Append('\n');
							utf8bytes.Clear();//builder.Clear();
							if (bytesreceived[seek + 1] == '\r') {
								goto HeadersDone; //It's just more efficient...
							}
						} else
							utf8bytes.Add(bytesreceived[seek]);//builder.Append((char)bytesreceived[seek]);
					}
				}
				HeadersDone:
				headers = inputheaderbuilder.ToString();
				if (ipaddress == null) { //Next best guess for the ip address
					ipaddress = (((IPEndPoint)client.RemoteEndPoint).Address.ToString());
				}
				if (bytestoread >= seek + 3) {
					byte[] messagestart = new byte[bytestoread - (seek + 3)];
					Array.Copy(bytesreceived, seek + 3, messagestart, 0, bytestoread - (seek + 3));
					utf8bytes.AddRange(messagestart);
				}
				while ((bytestoread = client.Available) > 0) { //By this time, all the data has been received so we can just expect to read all
					if (bytestoread > READBUFFER)
						bytestoread = READBUFFER;
					if (bytestoread != bytesreceived.Length)
						bytesreceived = new byte[bytestoread];
					client.Receive(bytesreceived, (int)bytestoread, 0);
					utf8bytes.AddRange(bytesreceived);
				}
				message = Encoding.UTF8.GetString(utf8bytes.ToArray());
				#endregion
				#region PageProcessor

				if (querystring.Length > MTU) {
					code = "414"; //If the query string turns out to be greater than the MTU, generate an URI too big error
					headers = string.Empty; //Necessary, as otherwise the framework will append the request headers on error
				} else if (headers.Length > MTU) {
					code = "431"; //If the query string turns out to be greater than the MTU, generate an URI too big error
					headers = string.Empty;
				} else if (!string.IsNullOrEmpty(path) && pagelist.TryGetValue(path, out staticpage)) {
					//thispage = (Page)Activator.CreateInstance(staticpage.GetType());
					hashsum = ipaddress.GetHashCode() + useragent.GetHashCode();
					if (!staticpage.PageCache.TryGetValue(hashsum, out thispage)) {
						thispage = (Page)Activator.CreateInstance(staticpage.GetType());
						thispage.Path = staticpage.Path; //Necessary for FAP.React
						//Functions, set at creation in case of being reassigned during runtime
						thispage.get = staticpage.get;
						thispage.put = staticpage.put;
						thispage.post = staticpage.post;
						thispage.delete = staticpage.delete;
						//These two cannot possibly change per each page instance
						thispage.UserIP = ipaddress;
						thispage.UserAgent = useragent;
						Task.Factory.StartNew(() => cacheAdd(hashsum, thispage, staticpage)); //Add it to the page cache in case the visitor returns
					}
					staticpage.lastpage = thispage; //Allows setting the header from a static class
					thispage.Headers = headers; //this is assured to always need an update
					//For functionals
					staticpage.Headers = headers;
					staticpage.UserIP = ipaddress;
					staticpage.UserAgent = useragent;

					switch (method1) {
						case 'H': //Head
						case 'G': //Get
							{		//"HEAD" is identical to "GET", except no content is generated, this is ensured later
								isCacheable = true;
								output = await Task.FromResult<string>(thispage.Get(querystring, message));

								currentHash = hashsum + querystring.GetHashCode() + output.GetHashCode();
								//Tested with a C# stopwatch, performing GetHashCode multiple times is indeed A LOT faster than concatenation, please ignore the integer overflows behind the curtain
								if (requestEtag == String.Format("{0:x8}", currentHash)) //String.Format is slightly faster than int.Parse
									code = "304";
							}
							break;
						case 'P':
							{
								if (method2 == 'U' || method2 == 'u')
									output = await Task.FromResult<string>(thispage.Put(querystring, message));
								else if (method2 == 'O' || method2 == 'o')
									output = await Task.FromResult<string>(thispage.Post(querystring, message));
								break;
							}
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
						headers = "\r\n" + thispage.Headers; //Ensures at least one new line and prevents a concatenation later on
						foreach (string s in headers.Split('\n')) { //This has about a 50-100ns bottleneck, solution: forget about headers
							if (s.ToLower().StartsWith(contenttypeheader)) {
								contenttype = s.Substring(contenttypeheader.Length);
								contenttype.Replace("\r", null);	//Remove the possible carriage return character
								contenttype.Replace("\n", null);	//Remove the possible new line character
								headers.Replace(s + '\n', null);	//Remove the entire content type line from the headers (or else there'll be double)
								int startencoding;
								if ((startencoding = contenttype.IndexOf('=')) > 0) {
									encoder = Encoding.GetEncoding(contenttype.Substring(startencoding + 1));
								}
								int endrealcontenttype = contenttype.IndexOf(';');
								if (endrealcontenttype > 0)
									contenttype = contenttype.Substring(0, endrealcontenttype);
							}
						}
					}
				} else
					headers = string.Empty;

				#endregion PageProcessor
				#region outputparse
				if (code != "304" && !string.IsNullOrEmpty(output)) { //If we haven't generated a 304 or a nothing response
					code = "200"; //Begin code as 200 for default success, but now include user HTTP codes
					if (output.Length >= 5 && //Using the string length, not UTF8 length here as we're doing string operations
					    Char.IsDigit(output[0]) && //If the first three characters are digits
					    Char.IsDigit(output[1]) &&
					    Char.IsDigit(output[2]) &&
					    output[3] == '\r' && //If these three digits end with a line breaker
					    output[4] == '\n') {
						code = output.Substring(0, 3); //Then we have the code
						output = (output.Length > 5) ? output.Remove(0, 5) : string.Empty; 	//And we can remove it from the output
					}

					if (contenttype == string.Empty) { //If the contenttype is undefined
						contenttype = "text/plain"; //Fail safe with text/plain
						if (output.Length >= 2) { //If we have the bytes to sniff for a content type
							var bytes = Encoding.ASCII.GetBytes(output.ToCharArray(0, 2)); //No longer using unicode, in fact ASCII does seem correct...
							//If length of the resultant output is greater MTU OR the first two bytes indicate some sort of GZIP/ZIP encoding
							isGzip = ((bytes[0] == (char)0x1f) && (bytes[1] == (char)0x8b || bytes[1] == (char)0x3f)); //Gzip is NOT a mime type
							switch (output[0]) { //mime/content type handling
								case (char)0:
									{
										if (output.Length > 3) { //Throughout this entire if block it's performing string manipuation, therefore output.Lenght is needed
											if (output[0] == (char)0 && output[1] == (char)0 && output[2] == (char)1) {
												contenttype = "image/x-icon";
												encoder = Encoding.BigEndianUnicode; 	//Seems to create binary compatibility
											}											//Headers are still encoded using UTF8
											if (output.Length > 9 && "ftyp" == output.Substring(4, 4)) {
												contenttype = "video/mp4";
												encoder = Encoding.BigEndianUnicode;
											}
										}
										break;
									}
								case '[':
									{
										if (output[output.Length - 1] == ']') //output.Length != length, length is for writing only
											contenttype = "application/json"; //As always, this framework promotes the use of JSON over CSV or XML or null terminated strings
										break;
									}
								case '{'://0x7b
									{
										if (output[output.Length - 1] == '}')
											contenttype = "application/json";
										break;
									}
								case '<'://0x3c
									{
										if (output[output.Length - 1] == '>') {
											if (output.Length > 3 && output[2] == 'x') { //<?xm
												contenttype = "text/xml";
											} else
												contenttype = "text/html";
										}
										break;
									}
								case '%'://0x25
									{
										if (output.Length > 4 && (output.Substring(1, 3) == "PDF")) {
											contenttype = "application/pdf";  //
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}

								case (char)0x42:
									{
										if (output[1] == (char)0x4D) {
											contenttype = "image/bmp";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0x47:
									{
										if (output[1] == (char)0x49) {
											contenttype = "image/gif";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0x49:
									{
										if (output[1] == (char)0x44) {
											contenttype = "audio/mpeg";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0x4d:
									{
										if (output[1] == (char)0x54) {
											contenttype = "audio/midi";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0x4f:
									{
										if (output[1] == (char)0x67) {
											contenttype = "audio/ogg";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0x66:
									{
										if (output[1] == (char)0xfc) {
											contenttype = "audio/flac";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0x89:
									{
										if (output[1] == (char)0x50) {
											contenttype = "image/png";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								case (char)0xff: //It's unlikely these work, as it's unlikely BigEndianUnicode is truly binary safe... but one can dream
									{
										if (output[1] == (char)0xd8) {
											contenttype = "image/jpeg";
											encoder = Encoding.BigEndianUnicode;
										} else if (output[1] == (char)0xfb) {
											contenttype = "audio/mpeg";
											encoder = Encoding.BigEndianUnicode;
										}
										break;
									}
								default:
									if (isGzip) {
										contenttype = "application/x-gzip"; //Send this type if Gziped
										encoder = Encoding.BigEndianUnicode;
									}
									break;
							}
						}
					}
					outputbytelength = encoder.GetByteCount(output);
				} else
					outputbytelength = 0;

				#endregion outputparse
				#region httpcodeparser
				switch (code[0]) {
					case '1':
						{
							#region 1xx
							switch (code[1]) {
								case '0':
									outputbytelength = 0;
									outputheaderbuilder.Append(HTTP + H.S100 + headers + "\r\n\r\n");
									break;

								case '1':
									outputbytelength = 0;
									outputheaderbuilder.Append(HTTP + H.S101 + headers + "\r\n\r\n");
									break;

								case '2':
									outputbytelength = 0;
									outputheaderbuilder.Append(HTTP + H.S102 + headers + "\r\n\r\n");
									break;

								default:
									outputbytelength = 0;
									outputheaderbuilder.Append(HTTP + H.S1xx + headers + "\r\n\r\n");
									break;
							}
							break;
							#endregion
						}
					case '2':
						{
							#region 2xx
							switch (code[2]) {
								case '0':
									outputheaderbuilder.Append(HTTP + H.S200);
									break;

								case '1':
									outputheaderbuilder.Append(HTTP + H.S201);
									break;

								case '2':
									outputheaderbuilder.Append(HTTP + H.S202);
									break;

								case '3':
									outputheaderbuilder.Append(HTTP + H.S203);
									break;

								case '4':
									outputbytelength = 0; //204 does not generate content
									outputheaderbuilder.Append(HTTP + H.S204);
									break;

								case '5':
									outputheaderbuilder.Append(HTTP + H.S205);
									break;

								case '6':
									outputheaderbuilder.Append(HTTP + H.S206);
									break;

								default:
									outputheaderbuilder.Append(HTTP + code);
									break;
							}
							outputheaderbuilder.Append(String.Format(
								"\r\nServer: FAP.NET {0} Codename: Meisscanne\r\n" +
								"Date: {1}\r\n" +
								"Connection: keep-alive\r\n{2}{3}" +
								//"Expires: {1}\r\n" +
								//"Cache-Control: private, max-age={4}, must-revalidate\r\n{5}{6}",
								"Cache-Control: private, max-age=0, must-revalidate\r\n" +
								"{4}",
								VERSION,
								DateTime.UtcNow.ToString("R"),
								(outputbytelength > 0 ? "Content-type: " + contenttype + "; charset=" + encoder.WebName + "\r\n" : string.Empty),
								(headers.Length > 0 ? headers.Substring(2) + "\r\n" : string.Empty),
								//CacheMaxAge + (isIE ? string.Empty : ", no-cache"),
								(isCacheable ? "Etag: \"" + String.Format("{0:x8}", currentHash) + "\"\r\n" : string.Empty)
							));
							if (isGzip) {
								outputheaderbuilder.Append("Content-Encoding: gzip\r\nTransfer-Encoding: Chunked\r\n\r\n");
							} else {
								if (outputbytelength < MTU) {
									outputheaderbuilder.Append("Content-Length: " + outputbytelength + "\r\n\r\n");
								} else
									outputheaderbuilder.Append("Transfer-Encoding: Chunked\r\n\r\n");
							}
							break;

							#endregion 2xx
						}
					case '3':
						{
							#region 3xx
							switch (code[2]) {
								case '0':
									outputheaderbuilder.Append(HTTP + H.R300 + headers + "\r\n\r\n");
									break;

								case '1':
									outputheaderbuilder.Append(HTTP + H.R301 + headers + "\r\n\r\n");
									break;

								case '2':
									outputheaderbuilder.Append(HTTP + H.R302 + headers + "\r\n\r\n");
									break;

								case '3':
									outputheaderbuilder.Append(HTTP + H.R303 + headers + "\r\n\r\n");
									break;

								case '4':
									outputbytelength = 0;
									outputheaderbuilder.Append(HTTP + H.R304 + String.Format("\r\n" +
									"Server: FAP.NET {0} Codename: Meisscanne\r\n" +
									"Date: {1}\r\n" +
									"Connection: keep-alive\r\n" +
										//"Expires: {1}\r\n" +
										//"Expires: -1\r\n" +
										//"Cache-control: private, max-age={2}, must-revalidate\r\n" +
									"Cache-control: private, max-age=0, must-revalidate\r\n" +
									"Etag: \"{2}\"\r\n\r\n",
										VERSION,
										DateTime.UtcNow.ToString("R"),
										//CacheMaxAge + (isIE ? string.Empty : ", no-cache"),
										String.Format("{0:x8}", currentHash)));
									break;

								case '5':
									outputheaderbuilder.Append(HTTP + H.R305 + headers + "\r\n\r\n");
									break;

								case '6':
									outputheaderbuilder.Append(HTTP + H.R306 + headers + "\r\n\r\n");
									break;

								case '7':
									outputheaderbuilder.Append(HTTP + H.R307 + headers + "\r\n\r\n");
									break;

								case '8':
									outputheaderbuilder.Append(HTTP + H.R308 + headers + "\r\n\r\n");
									break;

								default:
									outputheaderbuilder.Append(HTTP + code + headers + "\r\n\r\n");
									break;
							}
							break;

							#endregion 3xx
						}
					case '4':
						{
							#region 4xx
							switch (code[1]) {
								case '0':
									switch (code[2]) {
										case '0':
											outputheaderbuilder.Append(HTTP + H.E400 + headers + "\r\n\r\n");
											break;

										case '1':
											outputheaderbuilder.Append(HTTP + H.E401 + headers + "\r\n\r\n");
											break;

										case '2':
											outputheaderbuilder.Append(HTTP + H.E402 + headers + "\r\n\r\n");
											break;

										case '3':
											outputheaderbuilder.Append(HTTP + H.E403 + headers + "\r\n\r\n");
											break;

										case '4':
											outputheaderbuilder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
											break;

										case '5':
											outputheaderbuilder.Append(HTTP + H.E405 + headers + "\r\n\r\n");
											break;

										case '6':
											outputheaderbuilder.Append(HTTP + H.E406 + headers + "\r\n\r\n");
											break;

										case '7':
											outputheaderbuilder.Append(HTTP + H.E407 + headers + "\r\n\r\n");
											break;

										case '8':
											outputheaderbuilder.Append(HTTP + H.E408 + headers + "\r\n\r\n");
											break;

										case '9':
											outputheaderbuilder.Append(HTTP + H.E409 + headers + "\r\n\r\n");
											break;

										default:
											outputheaderbuilder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
											break;
									}
									break;

								case '1':
									switch (code[2]) {
										case '0':
											outputheaderbuilder.Append(HTTP + H.E410 + headers + "\r\n\r\n");
											break;

										case '1':
											outputheaderbuilder.Append(HTTP + H.E411 + headers + "\r\n\r\n");
											break;

										case '2':
											outputheaderbuilder.Append(HTTP + H.E412 + headers + "\r\n\r\n");
											break;

										case '3':
											outputheaderbuilder.Append(HTTP + H.E413 + headers + "\r\n\r\n");
											break;

										case '4':
											outputheaderbuilder.Append(HTTP + H.E414 + headers + "\r\n\r\n");
											break;

										case '5':
											outputheaderbuilder.Append(HTTP + H.E415 + headers + "\r\n\r\n");
											break;

										case '6':
											outputheaderbuilder.Append(HTTP + H.E416 + headers + "\r\n\r\n");
											break;

										case '7':
											outputheaderbuilder.Append(HTTP + H.E417 + headers + "\r\n\r\n");
											break;

										case '9':
											outputheaderbuilder.Append(HTTP + H.E419 + headers + "\r\n\r\n");
											break;

										default:
											outputheaderbuilder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
											break;
									}
									break;

								case '2':
									switch (code[2]) {
										case '0':
											outputheaderbuilder.Append(HTTP + H.E420 + headers + "\r\n\r\n"); //For illegal reasons
											break;

										case '1':
											outputheaderbuilder.Append(HTTP + H.E421 + headers + "\r\n\r\n");
											break;

										default:
											outputheaderbuilder.Append(HTTP + H.E42x + headers + "\r\n\r\n");
											break;
									}
									break;

								case '3':
									outputheaderbuilder.Append(HTTP + H.E431 + headers + "\r\n\r\n");
									break;

								case '4':
									outputheaderbuilder.Append(H.E444 + "\r\n\r\n"); //A proper 444 report is JUST 444, nothing else
									break;

								case '5':
									outputheaderbuilder.Append(HTTP + H.E451 + headers + "\r\n\r\n"); //For legal reasons
									break;

								case '9':
									switch (code[2]) {
										case '5':
											outputheaderbuilder.Append(HTTP + H.E495 + headers + "\r\n\r\n");
											break;

										case '6':
											outputheaderbuilder.Append(HTTP + H.E496 + headers + "\r\n\r\n");
											break;

										case '7':
											outputheaderbuilder.Append(HTTP + H.E497 + headers + "\r\n\r\n");
											break;

										case '9':
											outputheaderbuilder.Append(HTTP + H.E499 + headers + "\r\n\r\n");
											break;

										default:
											outputheaderbuilder.Append(HTTP + H.E49x + headers + "\r\n\r\n");
											break;
									}
									break;

								default:
									outputheaderbuilder.Append(HTTP + H.E404 + headers + "\r\n\r\n");
									break;
							}
							break;
							#endregion 4xx
						}
					case '5':
						{
							#region 5xx
							outputbytelength = 0;
							switch (code[1]) {
								case '0':
									switch (code[2]) {
										case '0':
											outputheaderbuilder.Append(HTTP + H.G500 + headers + "\r\n\r\n");
											break;

										case '1':
											outputheaderbuilder.Append(HTTP + H.G501 + headers + "\r\n\r\n");
											break;

										case '2':
											outputheaderbuilder.Append(HTTP + H.G502 + headers + "\r\n\r\n");
											break;

										case '3':
											outputheaderbuilder.Append(HTTP + H.G503 + headers + "\r\n\r\n");
											break;

										case '4':
											outputbytelength = 0;
											outputheaderbuilder.Append(HTTP + H.G504 + headers + "\r\n\r\n");
											break;

										case '5':
											outputheaderbuilder.Append(HTTP + H.G505 + headers + "\r\n\r\n");
											break;

										case '6':
											outputheaderbuilder.Append(HTTP + H.G506 + headers + "\r\n\r\n");
											break;

										case '7':
											outputheaderbuilder.Append(HTTP + H.G506 + headers + "\r\n\r\n");
											break;

										case '8':
											outputheaderbuilder.Append(HTTP + H.G506 + headers + "\r\n\r\n");
											break;

										case '9':
											outputheaderbuilder.Append(HTTP + H.G506 + headers + "\r\n\r\n");
											break;

										default:
											outputheaderbuilder.Append(HTTP + H.G502 + headers + "\r\n\r\n");
											break;
									}
									break;

								default:
									if (code == "510") {
										outputheaderbuilder.Append(HTTP + H.G511 + headers + "\r\n\r\n");
										break;
									}
									if (code == "511") {
										outputheaderbuilder.Append(HTTP + H.G511 + headers + "\r\n\r\n");
										break;
									}
									if (code == "520") {
										outputheaderbuilder.Append(HTTP + H.G520 + headers + "\r\n\r\n");
										break;
									}
									if (code.StartsWith("59", StringComparison.Ordinal)) {
										outputheaderbuilder.Append(HTTP + H.G59x + headers + "\r\n\r\n");
										break;
									}
									break;
							}
							break;
							#endregion 5xx
						}
					default:
						outputheaderbuilder.Append(HTTP + code + "\r\n\r\n");
						break;
				}
				#endregion httpcodeparser
				#region sending
				if (outputheaderbuilder.Length > 0) {
					if (method1 == 'H')
						outputbytelength = 0; //Right at the very, very end, to ensure the response is otherwise identical
					if (outputbytelength < MTU) {
						utf8bytes.Clear();
						utf8bytes.AddRange(Encoding.UTF8.GetBytes(outputheaderbuilder.ToString())); //Favour utf8bytes.Add functions over string concatenation and ternary operations 
						if (outputbytelength != 0) {// && method1 != 'H') { //don't check HEAD here, for the condition that outputbytelength > MTU and HEAD 
							utf8bytes.AddRange(encoder.GetBytes(output));
							utf8bytes.Add((byte)'\r');
							utf8bytes.Add((byte)'\n');
							if (isGzip) {
								utf8bytes.Add((byte)'0');
								utf8bytes.Add((byte)'\r');
								utf8bytes.Add((byte)'\n');
							}
						}
						//byte[] towrite = Encoding.UTF8.GetBytes(outputbuilder + (outputbytelength == 0 ? string.Empty : (output + (isGzip ? "\r\n0\r\n" : "\r\n"))));
						client.Send(utf8bytes.ToArray(), utf8bytes.Count, 0);//stream.Write(towrite, 0, towrite.Length);
					} else {
						int bytestowrite = 0;
						byte[] towrite = Encoding.UTF8.GetBytes(outputheaderbuilder.ToString());
						client.Send(towrite, towrite.Length, 0);
						var outputchararray = output.ToCharArray();
						utf8bytes.Clear();
						for (int i = 0; i < outputbytelength; i += MTU) {
							bytestowrite = (outputbytelength - i > MTU) ? MTU : outputbytelength - i;
							utf8bytes.AddRange(Encoding.UTF8.GetBytes(String.Format("{0:x}", bytestowrite) + "\r\n"));
							utf8bytes.AddRange(encoder.GetBytes(outputchararray, i, bytestowrite));
							utf8bytes.Add((byte)'\r');
							utf8bytes.Add((byte)'\n');
							client.Send(utf8bytes.ToArray(), utf8bytes.Count, 0);
							utf8bytes.Clear();
						}
						//stream.Write(Encoding.UTF8.GetBytes("0\r\n"), 0, 3); //Finish, magic bullet is that it doesn't matter what failed up there
						client.Send(Encoding.UTF8.GetBytes("0\r\n"), 3, 0);
					}
				} else {
					var error501 = Encoding.UTF8.GetBytes(HTTP + H.G501 + "\r\n\r\n");
					client.Send(error501, error501.Length, 0);
				}
				#endregion
				//stream.Close();
				//client.Close();
				//client.Disconnect(true); //Do this after the connection code instead, for reasons
			} catch (Exception e) {
				var error444 = Encoding.UTF8.GetBytes(H.E444); //An exceptional error
				client.Send(error444, error444.Length, 0);
				Console.Error.WriteLine("01: " + DateTime.UtcNow + " Connection exception from client:  " + ipaddress + " " + useragent + "\n\t" + e.Message);
				//ResetListener(true);
			}
		}


		async void cacheAdd(int cacheVars, Page toAdd, Page parentPage)
		{
			try {
				parentPage.PageCache.Add(cacheVars, toAdd);
				while (toAdd != null) {
					await Task.Delay(CacheMaxAge);
					if (toAdd.PageAge > CacheMaxAge) {
						parentPage.PageCache.Remove(cacheVars);
						toAdd = null;
						break;
					}
				}
			} catch (Exception e) {
				Console.Error.WriteLine("02: " + DateTime.UtcNow + " Async Exception " + e.Message);
				if (parentPage != null && parentPage.PageCache != null && parentPage.PageCache.ContainsKey(cacheVars))
					parentPage.PageCache.Remove(cacheVars); //Oh the paranoia
			}
		}

		/*
		 async void ResetListener(bool force = false)
		{
			try {
				listener.Close();	//Fixes an "address in use" error
				listener = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);//new TcpListener(Address, Port);
				//cache304 = new Dictionary<string, HashSet<int>>();
				listener.NoDelay = NODELAY;
				listener.Bind(new IPEndPoint(address, port));
				listener.Listen(SOCKETBACKLOG);
				//LoadListeners();
				if (!force) {
					await Task.Delay(TimeSpan.FromMinutes(5));
					Task.Factory.StartNew(() => ResetListener()); //I do not wish to await this statement
				}
			} catch (Exception e) {
				Console.Error.WriteLine("06: " + DateTime.UtcNow + " Reset Error: " + e.Message);
			}
		}
		*/

		static class H
		{
			//Just a bunch of consts, returning from a page function with ###/r/n(rest of your message here) can be used for return codes
			public const string S100 = "100 Continue";
			public const string S101 = "101 Switching Protocols";
			public const string S102 = "102 Processing";
			public const string S1xx = "1xx Informational";
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
			public const string G507 = "507 Insufficient Storage";
			public const string G508 = "508 Loop Detected";
			public const string G509 = "509 Bandwidth Limit Exceeded";
			public const string G510 = "510 Not Extended";
			public const string G511 = "511 Network Authentication Required";
			public const string G520 = "520 Unknown Error";
			public const string G59x = "59x Read/Connect Timeout Error";
		}
	}
}
